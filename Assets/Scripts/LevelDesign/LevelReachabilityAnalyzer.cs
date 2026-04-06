using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// L2 级 BFS 可达性验证器 — 纯网格图搜索 (Session 47)
///
/// 核心功能:
///   在 ASCII 模板的二维网格上执行 BFS 广度优先搜索，验证从起点 'M' 到终点 'G'
///   是否存在一条物理上可行的路径。不依赖运行时物理引擎，纯数学推演。
///
/// 状态转移规则:
///   - 角色站在 (x, y) 表示: grid[y][x] 下方 (y-1) 有 IsSolid 支撑，或 y==0（地面）
///   - 从当前位置可以跳跃到 (x+dx, y+dy)，其中:
///     dx 范围: [-MAX_GAP_CELLS, +MAX_GAP_CELLS]（含 Coyote Time）
///     dy 范围: [-自由落体高度, +MAX_JUMP_CELLS]
///   - 落点合法条件: 落点 (nx, ny) 在网格内，且 ny==0 或 grid[ny-1] 有 Solid 支撑
///   - 踩中 JumpBoost > 0 的元素（弹跳平台 B、弹跳怪 E）时，纵向跳跃上限临时增加
///
/// Auto-Prompting 闭环:
///   若 BFS 无法到达 'G'，找出离 'G' 最近的已探索合法坐标，
///   自动生成纠错话术并复制到系统剪贴板，供用户直接粘贴给 AI 修正关卡。
///
/// [AI防坑警告] 本验证器是纯静态网格分析，不依赖 Unity 物理引擎。
/// 所有跳跃能力参数来自 PhysicsMetrics 常量，修改物理参数后本验证器自动同步。
/// </summary>
public static class LevelReachabilityAnalyzer
{
    // ═══════════════════════════════════════════════════
    // 跳跃能力参数（从 PhysicsMetrics 派生的网格单位值）
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 这些值必须与 PhysicsMetrics 保持一致。
    // 使用 Mathf.FloorToInt 是因为角色必须完整站在一个格子上才算"到达"。
    // Coyote Time 的额外距离已包含在 MAX_GAP_WITH_COYOTE 中。

    /// <summary>最大水平跳跃距离（网格数，含 Coyote Time）</summary>
    private static int MaxHorizontalCells
    {
        get { return Mathf.FloorToInt(PhysicsMetrics.MAX_GAP_WITH_COYOTE); }
    }

    /// <summary>最大垂直跳跃高度（网格数）</summary>
    private static int MaxVerticalCells
    {
        get { return Mathf.FloorToInt(PhysicsMetrics.MAX_JUMP_HEIGHT); }
    }

    /// <summary>自由落体最大安全高度（网格数）— 平台跳跃中向下跳没有高度限制，但搜索需要边界</summary>
    private const int MAX_FALL_CELLS = 30;

    /// <summary>JumpBoost 额外跳跃高度（网格数）— 踩弹跳怪/弹跳平台的额外高度</summary>
    private static int BounceExtraCells
    {
        get { return Mathf.FloorToInt(PhysicsMetrics.MAX_JUMP_HEIGHT); }
    }

    // ═══════════════════════════════════════════════════
    // BFS 状态定义
    // ═══════════════════════════════════════════════════

    private struct BfsState
    {
        public int x;
        public int y;
        public bool hasBounce; // 是否站在 JumpBoost > 0 的元素上

        public BfsState(int x, int y, bool hasBounce)
        {
            this.x = x;
            this.y = y;
            this.hasBounce = hasBounce;
        }
    }

    // ═══════════════════════════════════════════════════
    // 分析结果
    // ═══════════════════════════════════════════════════

    public class ReachabilityResult
    {
        public bool IsReachable;
        public int StartX, StartY;
        public int GoalX, GoalY;
        public int ClosestReachedX, ClosestReachedY;
        public float ClosestDistance;
        public int ExploredCount;
        public string ErrorPrompt; // Auto-Prompting 纠错话术（仅不可达时生成）

        public string GetReport()
        {
            if (IsReachable)
            {
                return $"[L2 BFS] ✅ 路径可达: M({StartX},{StartY}) → G({GoalX},{GoalY})，" +
                       $"探索了 {ExploredCount} 个合法站位。";
            }
            return $"[L2 BFS] ❌ 物理死路: M({StartX},{StartY}) → G({GoalX},{GoalY}) 不可达。\n" +
                   $"  角色最远抵达 ({ClosestReachedX},{ClosestReachedY})，" +
                   $"距终点 {ClosestDistance:F1} 格。\n" +
                   $"  共探索 {ExploredCount} 个合法站位。";
        }
    }

    // ═══════════════════════════════════════════════════
    // 核心 BFS 分析方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 分析 ASCII 模板的可达性。
    /// </summary>
    /// <param name="template">ASCII 模板字符串（多行，第一行=最高层）</param>
    /// <param name="isSnippet">是否为片段模式（片段不要求 M/G，直接返回可达）</param>
    /// <returns>可达性分析结果</returns>
    public static ReachabilityResult Analyze(string template, bool isSnippet = false)
    {
        var result = new ReachabilityResult();

        if (string.IsNullOrEmpty(template))
        {
            result.IsReachable = true; // 空模板不报错
            return result;
        }

        // ── 解析网格 ──
        string[] lines = template.Split('\n');
        int height = lines.Length;
        int width = 0;
        foreach (var line in lines)
        {
            int lineLen = line.TrimEnd('\r').Length;
            if (lineLen > width) width = lineLen;
        }

        if (width == 0 || height == 0)
        {
            result.IsReachable = true;
            return result;
        }

        // 构建网格（与 Generator 坐标系一致：row 0 = 最高层 = worldY = height-1）
        char[,] grid = new char[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = '.'; // 默认空气

        int startX = -1, startY = -1;
        int goalX = -1, goalY = -1;

        for (int row = 0; row < lines.Length; row++)
        {
            string line = lines[row].TrimEnd('\r');
            int worldY = height - 1 - row;

            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];
                grid[col, worldY] = c;

                if (c == 'M')
                {
                    startX = col;
                    startY = worldY;
                }
                else if (c == 'G')
                {
                    goalX = col;
                    goalY = worldY;
                }
            }
        }

        // 片段模式：不要求 M/G，直接返回可达
        if (isSnippet)
        {
            result.IsReachable = true;
            return result;
        }

        // 缺少起点或终点
        if (startX < 0 || goalX < 0)
        {
            result.IsReachable = true; // 交给 L1 Validator 报错
            return result;
        }

        result.StartX = startX;
        result.StartY = startY;
        result.GoalX = goalX;
        result.GoalY = goalY;

        // ── 获取 Registry 元数据 ──
        var registry = AsciiElementRegistry.GetDefault();
        HashSet<char> solidChars = registry.GetSolidChars();

        // ── BFS 搜索 ──
        // visited[x, y, hasBounce] — hasBounce 作为状态维度，因为弹跳能力影响可达范围
        HashSet<long> visited = new HashSet<long>();
        Queue<BfsState> queue = new Queue<BfsState>();

        // 起点入队：M 所在位置
        bool startBounce = HasJumpBoost(grid, startX, startY, registry);
        EnqueueIfNew(queue, visited, startX, startY, startBounce, width, height);

        // 也检查起点正下方是否能站（M 可能悬空，需要下落到支撑面）
        // 向下搜索第一个支撑面
        for (int fallY = startY; fallY >= 0; fallY--)
        {
            if (CanStandAt(grid, startX, fallY, solidChars, width, height))
            {
                bool fb = HasJumpBoost(grid, startX, fallY, registry);
                EnqueueIfNew(queue, visited, startX, fallY, fb, width, height);
                break;
            }
        }

        float closestDist = float.MaxValue;
        int closestX = startX, closestY = startY;
        int exploredCount = 0;

        while (queue.Count > 0)
        {
            BfsState current = queue.Dequeue();
            exploredCount++;

            // 到达终点？
            if (current.x == goalX && current.y == goalY)
            {
                result.IsReachable = true;
                result.ExploredCount = exploredCount;
                return result;
            }

            // 终点可能在空中（G 下方有平台），检查是否能"经过"终点列并落到终点行
            // 更新最近距离
            float dist = Mathf.Sqrt((current.x - goalX) * (current.x - goalX) +
                                     (current.y - goalY) * (current.y - goalY));
            if (dist < closestDist)
            {
                closestDist = dist;
                closestX = current.x;
                closestY = current.y;
            }

            // 计算当前位置的跳跃能力
            int jumpUp = MaxVerticalCells;
            int jumpHoriz = MaxHorizontalCells;
            if (current.hasBounce)
            {
                jumpUp += BounceExtraCells;
            }

            // ── 枚举所有可能的跳跃落点 ──
            // 水平范围: [-jumpHoriz, +jumpHoriz]
            // 垂直范围: [-MAX_FALL_CELLS, +jumpUp]
            // 优化：不需要枚举所有 (dx, dy) 组合，只需要枚举合法落点

            for (int nx = Mathf.Max(0, current.x - jumpHoriz);
                 nx <= Mathf.Min(width - 1, current.x + jumpHoriz);
                 nx++)
            {
                // 对每个水平位置，向下搜索第一个可站立的位置
                int dx = Mathf.Abs(nx - current.x);

                // 向上跳：从当前高度到 current.y + jumpUp
                for (int ny = current.y; ny <= Mathf.Min(height - 1, current.y + jumpUp); ny++)
                {
                    int dy = ny - current.y;

                    // 物理可行性检查：跳跃抛物线约束
                    // 向上跳时，水平距离和垂直高度有耦合关系
                    if (dy > 0 && !IsJumpPhysicallyFeasible(dx, dy, jumpUp, jumpHoriz))
                        continue;

                    if (CanStandAt(grid, nx, ny, solidChars, width, height))
                    {
                        // 检查路径上没有被实体方块挡住头部
                        if (!IsPathBlocked(grid, current.x, current.y, nx, ny, solidChars, width, height))
                        {
                            bool nb = HasJumpBoost(grid, nx, ny, registry);
                            EnqueueIfNew(queue, visited, nx, ny, nb, width, height);
                        }
                    }
                }

                // 向下跳/平跳后下落：从当前高度向下搜索第一个支撑面
                int fallLimit = Mathf.Max(0, current.y - MAX_FALL_CELLS);
                for (int ny = current.y - 1; ny >= fallLimit; ny--)
                {
                    if (CanStandAt(grid, nx, ny, solidChars, width, height))
                    {
                        // 向下跳不需要抛物线约束（重力自然下落）
                        bool nb = HasJumpBoost(grid, nx, ny, registry);
                        EnqueueIfNew(queue, visited, nx, ny, nb, width, height);
                        break; // 只取第一个支撑面（不能穿过实体）
                    }
                    // 如果遇到实体方块挡住下落路径，停止
                    if (ny > 0 && IsSolidAt(grid, nx, ny, solidChars, width, height))
                        break;
                }
            }

            // ── 特殊：行走（左右相邻格，不需要跳跃）──
            int[] walkDx = { -1, 1 };
            foreach (int wdx in walkDx)
            {
                int wx = current.x + wdx;
                if (wx >= 0 && wx < width)
                {
                    // 同层行走
                    if (CanStandAt(grid, wx, current.y, solidChars, width, height) &&
                        !IsSolidAt(grid, wx, current.y, solidChars, width, height))
                    {
                        bool wb = HasJumpBoost(grid, wx, current.y, registry);
                        EnqueueIfNew(queue, visited, wx, current.y, wb, width, height);
                    }

                    // 走下台阶（走到边缘掉落一格）
                    if (current.y > 0 &&
                        !IsSolidAt(grid, wx, current.y, solidChars, width, height) &&
                        CanStandAt(grid, wx, current.y - 1, solidChars, width, height))
                    {
                        bool wb = HasJumpBoost(grid, wx, current.y - 1, registry);
                        EnqueueIfNew(queue, visited, wx, current.y - 1, wb, width, height);
                    }
                }
            }
        }

        // ── BFS 耗尽，未到达终点 ──
        result.IsReachable = false;
        result.ClosestReachedX = closestX;
        result.ClosestReachedY = closestY;
        result.ClosestDistance = closestDist;
        result.ExploredCount = exploredCount;

        // Auto-Prompting: 生成纠错话术
        result.ErrorPrompt = GenerateErrorPrompt(closestX, closestY, goalX, goalY);

        return result;
    }

    // ═══════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════

    /// <summary>判断 (x, y) 是否可以站立：y==0（地面底部）或 (x, y-1) 是 Solid</summary>
    private static bool CanStandAt(char[,] grid, int x, int y,
        HashSet<char> solidChars, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;

        // 自身位置不能是实体（角色不能站在实体内部）
        if (solidChars.Contains(grid[x, y])) return false;

        // y==0 时视为地面底部（如果下方没有格子，说明是最底层）
        if (y == 0) return true;

        // 下方有实体支撑
        if (y - 1 >= 0 && solidChars.Contains(grid[x, y - 1])) return true;

        return false;
    }

    /// <summary>判断 (x, y) 位置是否是实体方块</summary>
    private static bool IsSolidAt(char[,] grid, int x, int y,
        HashSet<char> solidChars, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return solidChars.Contains(grid[x, y]);
    }

    /// <summary>判断 (x, y) 位置是否有 JumpBoost > 0 的元素（弹跳平台/弹跳怪）</summary>
    private static bool HasJumpBoost(char[,] grid, int x, int y,
        AsciiElementRegistry registry)
    {
        // 检查脚下的元素（y-1）是否有 JumpBoost
        if (y <= 0) return false;
        int belowY = y - 1;
        if (belowY < 0 || belowY >= grid.GetLength(1)) return false;
        if (x < 0 || x >= grid.GetLength(0)) return false;

        char belowChar = grid[x, belowY];
        var entry = registry.GetEntry(belowChar);
        if (entry != null && entry.jumpBoost > 0f) return true;

        // 也检查当前位置（弹跳怪 E 是非实体，角色可能与之重叠）
        char currentChar = grid[x, y];
        var currentEntry = registry.GetEntry(currentChar);
        if (currentEntry != null && currentEntry.jumpBoost > 0f) return true;

        return false;
    }

    /// <summary>
    /// 检查跳跃是否物理可行（抛物线约束）。
    /// 向上跳时，水平距离和垂直高度存在耦合：跳得越高，水平距离越短。
    /// </summary>
    private static bool IsJumpPhysicallyFeasible(int absDx, int dy, int maxJumpUp, int maxJumpHoriz)
    {
        if (dy <= 0) return true; // 向下或平跳，只受水平距离限制

        // 简化的抛物线约束：
        // 高度占比越大，可用水平距离越短
        // 与 PhysicsMetrics.IsGapTraversable 的逻辑一致
        float heightRatio = (float)dy / maxJumpUp;
        if (heightRatio > 1f) return false; // 超过跳跃高度极限

        float availableHoriz = maxJumpHoriz * (1f - heightRatio * 0.5f);
        return absDx <= availableHoriz;
    }

    /// <summary>
    /// 检查从 (fromX, fromY) 到 (toX, toY) 的跳跃路径是否被实体方块挡住。
    /// 简化检查：只检查头顶是否有实体（向上跳时）。
    /// </summary>
    private static bool IsPathBlocked(char[,] grid, int fromX, int fromY,
        int toX, int toY, HashSet<char> solidChars, int width, int height)
    {
        if (toY <= fromY) return false; // 向下或平跳不检查

        // 向上跳时，检查起点正上方到目标高度之间是否有实体
        int maxCheckY = Mathf.Min(toY, height - 1);
        for (int checkY = fromY + 1; checkY <= maxCheckY; checkY++)
        {
            if (IsSolidAt(grid, fromX, checkY, solidChars, width, height))
                return true; // 头顶被挡
        }

        return false;
    }

    /// <summary>BFS 状态编码（用于 visited 集合去重）</summary>
    private static long EncodeState(int x, int y, bool hasBounce)
    {
        // x: 0~999, y: 0~999, hasBounce: 0/1
        return (long)x * 10000L + (long)y * 10L + (hasBounce ? 1L : 0L);
    }

    /// <summary>入队辅助：检查是否已访问，未访问则入队</summary>
    private static void EnqueueIfNew(Queue<BfsState> queue, HashSet<long> visited,
        int x, int y, bool hasBounce, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        long key = EncodeState(x, y, hasBounce);
        if (visited.Contains(key)) return;

        visited.Add(key);
        queue.Enqueue(new BfsState(x, y, hasBounce));
    }

    // ═══════════════════════════════════════════════════
    // Auto-Prompting 纠错话术生成
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 生成纠错话术并复制到系统剪贴板。
    /// 话术包含断层位置和修复建议，用户可直接粘贴给 AI 修正关卡。
    /// </summary>
    private static string GenerateErrorPrompt(int closestX, int closestY, int goalX, int goalY)
    {
        float distance = Mathf.Sqrt((closestX - goalX) * (closestX - goalX) +
                                     (closestY - goalY) * (closestY - goalY));

        string prompt =
            $"[系统反馈] 你的关卡存在物理死路。" +
            $"角色最远抵达 ({closestX},{closestY})，距离终点 ({goalX},{goalY}) " +
            $"路径发生断层（距离 {distance:F1} 格），超过了跳跃极限" +
            $"（水平极限 {PhysicsMetrics.MAX_GAP_WITH_COYOTE:F1} 格，" +
            $"垂直极限 {PhysicsMetrics.MAX_JUMP_HEIGHT:F1} 格）。" +
            $"请在 ({closestX},{closestY}) 附近增加平台或弹跳跳板（B），" +
            $"确保路径连通，输出修正后的完整 ASCII 模板。";

        return prompt;
    }

    /// <summary>
    /// 将纠错话术复制到系统剪贴板并在 Console 输出。
    /// 仅在 Editor 环境下执行剪贴板操作。
    /// </summary>
    public static void CopyErrorToClipboard(ReachabilityResult result)
    {
        if (result == null || result.IsReachable || string.IsNullOrEmpty(result.ErrorPrompt))
            return;

#if UNITY_EDITOR
        GUIUtility.systemCopyBuffer = result.ErrorPrompt;
        Debug.Log("[L2 BFS] 📋 纠错话术已复制到剪贴板，可直接粘贴给 AI 修正关卡。");
#endif
        Debug.LogError($"[L2 BFS] {result.GetReport()}");
        Debug.Log($"[L2 BFS] 纠错话术:\n{result.ErrorPrompt}");
    }
}
