using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// ASCII 关卡物理验证器 — 在生成前/后自动检查关卡的物理可行性
///
/// 功能：
///   1. 扫描 ASCII 模板中的所有间隙（连续的 '.' 字符），检查是否超过安全上限
///   2. 扫描所有高台（需要跳跃才能到达的平台），检查是否超过跳跃极限
///   3. 验证 Mario 出生点 (M) 是否存在且可站立
///   4. 验证终点 (G) 是否存在且可到达
///   5. 输出详细的验证报告（警告/错误），帮助设计师快速定位问题
///   6. 支持片段模式 (isSnippet)：片段不要求 M/G，适用于局部挑战片段
///
/// 使用方式：
///   在 TestConsoleWindow 的 Build 按钮中调用 ValidateTemplate()，
///   生成前先验证，有错误时弹出警告但不阻止生成（设计师可能故意设计极限关卡）。
///
/// 所有阈值引用 PhysicsMetrics 常量，确保与物理系统同步。
///
/// Session 32: 关卡度量转译系统
/// Session 43: 修复间隙检测误报、增加片段模式、改进高台可达性检测
/// </summary>
public static class AsciiLevelValidator
{
    /// <summary>验证结果</summary>
    public class ValidationResult
    {
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> info = new List<string>();

        public bool HasErrors => errors.Count > 0;
        public bool HasWarnings => warnings.Count > 0;
        public bool IsClean => !HasErrors && !HasWarnings;

        /// <summary>生成可读的验证报告</summary>
        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("═══ ASCII Level Validation Report ═══");

            if (IsClean)
            {
                sb.AppendLine("✅ All checks passed! Level is physically valid.");
            }
            else
            {
                if (HasErrors)
                {
                    sb.AppendLine($"\n❌ ERRORS ({errors.Count}):");
                    foreach (string e in errors)
                        sb.AppendLine($"  • {e}");
                }
                if (HasWarnings)
                {
                    sb.AppendLine($"\n⚠️ WARNINGS ({warnings.Count}):");
                    foreach (string w in warnings)
                        sb.AppendLine($"  • {w}");
                }
            }

            if (info.Count > 0)
            {
                sb.AppendLine($"\nℹ️ INFO ({info.Count}):");
                foreach (string i in info)
                    sb.AppendLine($"  • {i}");
            }

            sb.AppendLine("\n═══ End of Report ═══");
            return sb.ToString();
        }
    }

    // 地面字符集（玩家可以站在上面的字符）
    private static readonly HashSet<char> solidChars = new HashSet<char>
    {
        '#', // 地面
        '=', // 平台
        'W', // 墙壁
        'B', // 弹跳平台
        'C', // 崩塌平台
        '-', // 单向平台
        'F', // 伪装墙
    };

    // 空气字符集（玩家可以通过的字符）
    private static readonly HashSet<char> airChars = new HashSet<char>
    {
        '.', // 空白
        'M', // Mario出生点
        'G', // 终点
        '^', // 地刺（空气中）
        '~', // 火焰
        'P', // 摆锤
        'E', // 弹跳怪
        'e', // 简单敌人
        'o', // 收集物
        'H', // 隐藏通道
        '>', // 移动平台
    };

    /// <summary>
    /// 验证 ASCII 模板的物理可行性（完整关卡模式）
    /// </summary>
    /// <param name="template">多行 ASCII 字符串</param>
    /// <returns>验证结果</returns>
    public static ValidationResult ValidateTemplate(string template)
    {
        return ValidateTemplate(template, false);
    }

    /// <summary>
    /// 验证 ASCII 模板的物理可行性
    /// </summary>
    /// <param name="template">多行 ASCII 字符串</param>
    /// <param name="isSnippet">是否为片段模式（片段不要求 M/G）</param>
    /// <returns>验证结果</returns>
    // [AI防坑警告] isSnippet 参数控制验证严格度。
    // 片段(snippet)是局部挑战片段，不含 M/G 出生点和终点，
    // 验证器不应对片段报 "No Mario spawn" 或 "No goal zone" 错误。
    // 但物理可行性检查（间隙、高台）对片段同样适用。
    public static ValidationResult ValidateTemplate(string template, bool isSnippet)
    {
        ValidationResult result = new ValidationResult();

        if (string.IsNullOrEmpty(template))
        {
            result.errors.Add("Template is empty!");
            return result;
        }

        string[] lines = template.Split('\n');
        int height = lines.Length;

        // 清理行尾
        for (int i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd('\r');

        int maxWidth = 0;
        foreach (string line in lines)
            if (line.Length > maxWidth) maxWidth = line.Length;

        // 构建字符网格（坐标系与 AsciiLevelGenerator 一致）
        char[,] grid = new char[maxWidth, height];
        for (int x = 0; x < maxWidth; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = '.';

        bool hasMario = false;
        bool hasGoal = false;
        int marioX = -1, marioY = -1;
        int goalX = -1, goalY = -1;

        for (int row = 0; row < height; row++)
        {
            int worldY = height - 1 - row;
            for (int col = 0; col < lines[row].Length; col++)
            {
                char c = lines[row][col];
                grid[col, worldY] = c;

                if (c == 'M')
                {
                    hasMario = true;
                    marioX = col;
                    marioY = worldY;
                }
                if (c == 'G')
                {
                    hasGoal = true;
                    goalX = col;
                    goalY = worldY;
                }
            }
        }

        // ── 检查 1: Mario 出生点 ──
        // S43: 片段模式下跳过 M/G 检查（片段是局部挑战，不含出生点和终点）
        if (!isSnippet)
        {
            if (!hasMario)
            {
                result.errors.Add("No Mario spawn point (M) found!");
            }
            else
            {
                // 检查 Mario 脚下是否有地面
                if (marioY > 0 && !IsSolid(grid, marioX, marioY - 1, maxWidth, height))
                {
                    result.warnings.Add($"Mario spawn (M) at ({marioX},{marioY}) has no ground below! Mario will fall.");
                }
                result.info.Add($"Mario spawn at ({marioX},{marioY})");
            }

            // ── 检查 2: 终点 ──
            if (!hasGoal)
            {
                result.warnings.Add("No goal zone (G) found. Level has no ending.");
            }
            else
            {
                result.info.Add($"Goal at ({goalX},{goalY})");
            }
        }
        else
        {
            // 片段模式下仍然记录 M/G 信息（如果有的话）
            if (hasMario) result.info.Add($"Mario spawn at ({marioX},{marioY})");
            if (hasGoal) result.info.Add($"Goal at ({goalX},{goalY})");
            result.info.Add("Snippet mode: M/G checks skipped.");
        }

        // ── 检查 3: 水平间隙扫描（改进版）──
        // S43 改进: 只检测"关键地面层"的间隙，而非逐行扫描所有行。
        // 关键地面层定义：该行有实体块，且实体块上方可站立（不被实体覆盖），
        // 且间隙上方在跳跃高度范围内没有替代通路（桥梁/平台）。
        // 这避免了对非地面行（如纯装饰行、高空行）的间隙误报。
        CheckGroundGaps(grid, maxWidth, height, result);

        // ── 检查 4: 垂直高台扫描（改进版）──
        // S43 改进: 检查周围水平范围内是否有可跳达的平台，
        // 而非只看正下方的垂直距离。
        CheckHighPlatforms(grid, maxWidth, height, result);

        // ── 检查 5: S35 出生点安全距离 ──
        // 业界参考: Celeste 每个 checkpoint 周围无危险物，给玩家安全起步空间
        if (!isSnippet)
        {
            CheckSpawnSafety(grid, maxWidth, height, marioX, marioY, result);
        }

        // ── 检查 6: S35 弹跳平台上方安全净空 ──
        // 业界参考: SMW 弹跳垫上方始终留出完整弹跳弧线空间
        CheckBounceClearance(grid, maxWidth, height, result);

        // ── 检查 7: S35 陷阱着陆缓冲 ──
        // 业界参考: Dylan Wolf 程序化生成中陷阱必须指向至少一个空tile
        CheckHazardLandingBuffer(grid, maxWidth, height, result);

        // ── 检查 8: 关卡尺寸信息 ──
        result.info.Add($"Level size: {maxWidth} x {height} (W x H)");
        result.info.Add($"Physics limits: max jump height = {PhysicsMetrics.MAX_JUMP_HEIGHT:F1}, " +
                       $"max jump distance = {PhysicsMetrics.MAX_JUMP_DISTANCE:F1}, " +
                       $"safe gap = {PhysicsMetrics.ASCII_MAX_GAP}, " +
                       $"safe height = {PhysicsMetrics.ASCII_MAX_HEIGHT}");

        return result;
    }

    // ═══════════════════════════════════════════════════
    // S43 改进: 间隙检测 — 只检测关键地面层
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 检测关键地面层的水平间隙。
    /// 
    /// 改进逻辑（S43）：
    /// 1. 只在"可行走表面"上检测间隙（实体块上方为空气的行）
    /// 2. 对检测到的间隙，检查上方是否有替代通路（桥梁/平台），
    ///    如果间隙上方在跳跃高度范围内有连续的实体桥梁横跨整个间隙，
    ///    则该间隙不是真正的死路（玩家可以走上面的桥）
    /// 3. 避免对纯空气行、装饰行的间隙误报
    /// </summary>
    private static void CheckGroundGaps(char[,] grid, int width, int height, ValidationResult result)
    {
        // 收集所有"可行走表面行"：该行至少有一个实体块，且该实体块上方为空气
        for (int y = 0; y < height; y++)
        {
            bool hasWalkableSurface = false;
            for (int x = 0; x < width; x++)
            {
                if (IsSolid(grid, x, y, width, height) &&
                    !IsSolid(grid, x, y + 1, width, height))
                {
                    hasWalkableSurface = true;
                    break;
                }
            }
            if (!hasWalkableSurface) continue;

            // 在这一行扫描间隙
            int gapStart = -1;
            bool inGap = false;

            for (int x = 0; x < width; x++)
            {
                bool solid = IsSolid(grid, x, y, width, height);

                if (!solid && !inGap)
                {
                    // 间隙开始：只有当左边有实体时才算间隙（排除关卡边缘）
                    if (x > 0 && IsSolid(grid, x - 1, y, width, height))
                    {
                        inGap = true;
                        gapStart = x;
                    }
                }
                else if (solid && inGap)
                {
                    // 间隙结束
                    int gapWidth = x - gapStart;
                    // S43: 检查间隙上方是否有替代通路
                    if (!HasBridgeAbove(grid, gapStart, x - 1, y, width, height))
                    {
                        CheckGap(gapWidth, gapStart, y, result);
                    }
                    inGap = false;
                }
            }
        }
    }

    /// <summary>
    /// 检查间隙上方是否有替代通路（桥梁/平台）。
    /// 如果在间隙正上方的跳跃高度范围内，存在连续的实体块横跨整个间隙，
    /// 则玩家可以走上面的桥，该间隙不是死路。
    /// </summary>
    private static bool HasBridgeAbove(char[,] grid, int gapStartX, int gapEndX, int gapY,
        int width, int height)
    {
        // 在间隙上方 1 到 MAX_JUMP_HEIGHT 范围内查找桥梁
        int maxCheckHeight = Mathf.CeilToInt(PhysicsMetrics.MAX_JUMP_HEIGHT);
        for (int dy = 1; dy <= maxCheckHeight; dy++)
        {
            int checkY = gapY + dy;
            if (checkY >= height) break;

            // 检查这一行是否有连续实体横跨整个间隙
            bool fullBridge = true;
            for (int x = gapStartX; x <= gapEndX; x++)
            {
                if (!IsSolid(grid, x, checkY, width, height))
                {
                    fullBridge = false;
                    break;
                }
            }
            if (fullBridge) return true;

            // S43: 也检查是否有足够密集的平台序列可以跳跃通过
            // （不要求完全连续，但间隙内的子间隙不超过安全跳跃距离）
            bool hasSteppingPath = CheckSteppingPath(grid, gapStartX, gapEndX, checkY, width, height);
            if (hasSteppingPath) return true;
        }
        return false;
    }

    /// <summary>
    /// 检查指定行在间隙范围内是否有足够密集的平台序列可以跳跃通过。
    /// 即：间隙范围内的实体块之间的子间隙都不超过安全跳跃距离。
    /// </summary>
    private static bool CheckSteppingPath(char[,] grid, int startX, int endX, int y,
        int width, int height)
    {
        // 从间隙左侧开始，检查是否能通过平台序列到达右侧
        // 需要左侧入口和右侧出口都有实体可站立
        bool hasLeftEntry = IsSolid(grid, startX - 1, y, width, height) ||
                           IsSolid(grid, startX, y, width, height);
        bool hasRightExit = IsSolid(grid, endX + 1, y, width, height) ||
                           IsSolid(grid, endX, y, width, height);
        if (!hasLeftEntry || !hasRightExit) return false;

        // 检查间隙范围内的子间隙
        int subGapWidth = 0;
        for (int x = startX; x <= endX; x++)
        {
            if (IsSolid(grid, x, y, width, height))
            {
                subGapWidth = 0;
            }
            else
            {
                subGapWidth++;
                if (subGapWidth > PhysicsMetrics.ASCII_MAX_GAP)
                    return false;
            }
        }
        return true;
    }

    // ═══════════════════════════════════════════════════
    // S43 改进: 高台检测 — 考虑水平可达性
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 检测需要跳跃才能到达的高台。
    /// 
    /// 改进逻辑（S43）：
    /// 1. 找到可站立表面（实体块上方为空气）
    /// 2. 计算该表面到正下方最近地面的垂直距离
    /// 3. 如果垂直距离超过安全高度，检查水平范围内是否有可跳达的中间平台
    /// 4. 水平搜索范围 = MAX_JUMP_DISTANCE（而非仅 ASCII_MAX_GAP）
    /// </summary>
    private static void CheckHighPlatforms(char[,] grid, int width, int height, ValidationResult result)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 1; y < height; y++)
            {
                // 找到一个实体块，检查它是否是可站立表面
                if (!IsSolid(grid, x, y, width, height)) continue;
                if (y + 1 < height && IsSolid(grid, x, y + 1, width, height)) continue; // 上方被覆盖，不是表面

                // 向下查找最近的地面
                int dropHeight = 0;
                for (int checkY = y - 1; checkY >= 0; checkY--)
                {
                    if (IsSolid(grid, x, checkY, width, height))
                        break;
                    dropHeight++;
                }

                // 跳过底层地面（dropHeight=0 说明紧贴下方实体）
                if (dropHeight <= PhysicsMetrics.ASCII_MAX_HEIGHT) continue;

                // S43: 在水平范围内搜索可跳达的平台
                // 搜索范围 = MAX_JUMP_DISTANCE（含 coyote 时间）
                int searchRange = Mathf.CeilToInt(PhysicsMetrics.MAX_GAP_WITH_COYOTE);
                bool reachable = false;

                for (int dx = -searchRange; dx <= searchRange; dx++)
                {
                    if (dx == 0) continue;
                    int checkX = x + dx;
                    if (checkX < 0 || checkX >= width) continue;

                    // 在目标平台的跳跃高度范围内搜索可站立的平台
                    // 玩家可以从较低的平台跳上来
                    for (int checkY = Mathf.Max(0, y - Mathf.CeilToInt(PhysicsMetrics.MAX_JUMP_HEIGHT));
                         checkY <= y; checkY++)
                    {
                        if (IsSolid(grid, checkX, checkY, width, height) &&
                            (checkY + 1 >= height || !IsSolid(grid, checkX, checkY + 1, width, height)))
                        {
                            // 找到一个可站立的平台，检查是否物理可达
                            int heightDiff = y - checkY; // 需要跳的高度
                            int horizDist = Mathf.Abs(dx);  // 水平距离

                            if (PhysicsMetrics.IsGapTraversable(horizDist, heightDiff))
                            {
                                reachable = true;
                                break;
                            }
                        }
                    }

                    // 也检查从较高平台跳下来的情况
                    if (!reachable)
                    {
                        for (int checkY = y; checkY <= Mathf.Min(height - 1, y + Mathf.CeilToInt(PhysicsMetrics.MAX_JUMP_HEIGHT));
                             checkY++)
                        {
                            if (IsSolid(grid, checkX, checkY, width, height) &&
                                (checkY + 1 >= height || !IsSolid(grid, checkX, checkY + 1, width, height)))
                            {
                                // 从高处跳下来，高度差为负（向下跳更容易）
                                int heightDiff = y - checkY; // 负值 = 向下跳
                                int horizDist = Mathf.Abs(dx);

                                if (PhysicsMetrics.IsGapTraversable(horizDist, heightDiff))
                                {
                                    reachable = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (reachable) break;
                }

                if (!reachable)
                {
                    if (dropHeight > PhysicsMetrics.ASCII_EXTREME_HEIGHT)
                    {
                        result.errors.Add(
                            $"UNREACHABLE platform at ({x},{y}): {dropHeight} blocks above nearest ground, " +
                            $"no reachable platform within jump range " +
                            $"(max jump = {PhysicsMetrics.MAX_JUMP_HEIGHT:F1}). " +
                            $"This is a physical dead end!");
                    }
                    else if (dropHeight > PhysicsMetrics.ASCII_MAX_HEIGHT)
                    {
                        result.warnings.Add(
                            $"High platform at ({x},{y}): {dropHeight} blocks above nearest ground " +
                            $"(max safe = {PhysicsMetrics.ASCII_MAX_HEIGHT}). " +
                            $"Requires precise jumping or stepping stones.");
                    }
                }
            }
        }
    }

    /// <summary>检查间隙宽度是否安全</summary>
    private static void CheckGap(int gapWidth, int gapStartX, int y, ValidationResult result)
    {
        if (gapWidth > PhysicsMetrics.ASCII_MAX_GAP &&
            gapWidth <= Mathf.FloorToInt(PhysicsMetrics.MAX_JUMP_DISTANCE))
        {
            result.warnings.Add(
                $"Tight gap at row Y={y}, X=[{gapStartX}..{gapStartX + gapWidth - 1}]: " +
                $"{gapWidth} blocks wide (safe limit = {PhysicsMetrics.ASCII_MAX_GAP}). " +
                $"Requires precise timing.");
        }
        else if (gapWidth > Mathf.FloorToInt(PhysicsMetrics.MAX_GAP_WITH_COYOTE))
        {
            result.errors.Add(
                $"IMPASSABLE gap at row Y={y}, X=[{gapStartX}..{gapStartX + gapWidth - 1}]: " +
                $"{gapWidth} blocks wide (max with coyote = {PhysicsMetrics.MAX_GAP_WITH_COYOTE:F1}). " +
                $"This is a physical dead end!");
        }
    }

    /// <summary>检查指定位置是否为实体</summary>
    private static bool IsSolid(char[,] grid, int x, int y, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return solidChars.Contains(grid[x, y]);
    }

    // ═══════════════════════════════════════════════════
    // S35 新增安全验证方法
    // 业界参考:
    //   - Celeste (Maddy Thorson): checkpoint 周围无危险物
    //   - Super Mario World (Reverse Design): 弹跳垫上方留完整弧线空间
    //   - Dylan Wolf: 陷阱必须指向至少一个空tile
    //   - sgalban/platformer-gen-2D: 每个节奏组之间有 rest area
    // ═══════════════════════════════════════════════════

    // 危险字符集（对玩家造成伤害的元素）
    private static readonly HashSet<char> hazardChars = new HashSet<char>
    {
        '^', // 地刺
        '~', // 火焰
        'P', // 摆锤
    };

    /// <summary>检查指定位置是否为危险物</summary>
    private static bool IsHazard(char[,] grid, int x, int y, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return hazardChars.Contains(grid[x, y]);
    }

    /// <summary>
    /// S35 检查 5: 出生点安全性
    /// 验证 M/T 之间的间距以及出生点周围是否有危险物。
    /// </summary>
    private static void CheckSpawnSafety(
        char[,] grid, int width, int height,
        int marioX, int marioY, ValidationResult result)
    {
        if (marioX < 0) return; // 无 Mario 出生点，已在检查 1 报错

        // 查找 Trickster 出生点
        int tricksterX = -1, tricksterY = -1;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == 'T')
                {
                    tricksterX = x;
                    tricksterY = y;
                    break;
                }
            }
            if (tricksterX >= 0) break;
        }

        // 检查 M/T 间距
        if (tricksterX >= 0)
        {
            int dx = Mathf.Abs(marioX - tricksterX);
            int dy = Mathf.Abs(marioY - tricksterY);
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist < PhysicsMetrics.SPAWN_SAFE_RADIUS)
            {
                result.warnings.Add(
                    $"M/T spawn too close: M({marioX},{marioY}) T({tricksterX},{tricksterY}) " +
                    $"distance={dist:F1} (min={PhysicsMetrics.SPAWN_SAFE_RADIUS}). " +
                    $"Players may overlap on start. Separate by at least {PhysicsMetrics.SPAWN_SAFE_RADIUS} blocks.");
            }
        }

        // 检查 Mario 出生点周围安全半径内是否有危险物
        int r = PhysicsMetrics.SPAWN_SAFE_RADIUS;
        for (int ddx = -r; ddx <= r; ddx++)
        {
            for (int ddy = -r; ddy <= r; ddy++)
            {
                int cx = marioX + ddx;
                int cy = marioY + ddy;
                if (IsHazard(grid, cx, cy, width, height))
                {
                    result.warnings.Add(
                        $"Hazard '{grid[cx, cy]}' at ({cx},{cy}) is within spawn safe radius " +
                        $"({r} blocks) of Mario spawn ({marioX},{marioY}). " +
                        $"Player may take damage immediately on start.");
                }
            }
        }
    }

    /// <summary>
    /// S35 检查 6: 弹跳平台上方安全净空
    /// 扫描所有 B 字符，检查上方 BOUNCE_CLEARANCE 格内是否有危险物或实体阻挡。
    /// </summary>
    private static void CheckBounceClearance(
        char[,] grid, int width, int height, ValidationResult result)
    {
        int clearance = PhysicsMetrics.BOUNCE_CLEARANCE;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != 'B') continue;

                // 检查 B 上方 clearance 格
                for (int dy = 1; dy <= clearance; dy++)
                {
                    int checkY = y + dy;
                    if (checkY >= height) break;

                    char above = grid[x, checkY];

                    if (hazardChars.Contains(above))
                    {
                        result.warnings.Add(
                            $"Hazard '{above}' at ({x},{checkY}) is only {dy} block(s) above " +
                            $"BouncyPlatform at ({x},{y}). Min clearance = {clearance}. " +
                            $"Player will be launched into hazard with no escape.");
                    }
                    else if (solidChars.Contains(above))
                    {
                        result.warnings.Add(
                            $"Solid block at ({x},{checkY}) is only {dy} block(s) above " +
                            $"BouncyPlatform at ({x},{y}). Min clearance = {clearance}. " +
                            $"Bounce trajectory will be cut short.");
                        break; // 实体阻挡后上方不用再查
                    }
                }
            }
        }
    }

    /// <summary>
    /// S35 检查 7: 陷阱着陆缓冲
    /// 扫描所有陷阱字符(^~)，检查其左右是否紧贴平台边缘而无缓冲空间。
    /// 当陷阱直接相邻平台边缘时，玩家着陆后无法避开伤害。
    /// </summary>
    private static void CheckHazardLandingBuffer(
        char[,] grid, int width, int height, ValidationResult result)
    {
        int buffer = PhysicsMetrics.HAZARD_LANDING_BUFFER;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                char c = grid[x, y];
                // 只检查地刺和火焰（地面层陷阱），摆锤是空中陷阱不适用此规则
                if (c != '^' && c != '~') continue;

                // 检查左侧是否紧贴平台边缘
                bool leftPlatformEdge = false;
                for (int bx = 1; bx <= buffer; bx++)
                {
                    int checkX = x - bx;
                    if (checkX < 0) break;
                    // 如果缓冲区内是实体且上方可站立，说明是平台边缘
                    if (IsSolid(grid, checkX, y, width, height) &&
                        !IsSolid(grid, checkX, y + 1, width, height))
                    {
                        leftPlatformEdge = true;
                        break;
                    }
                }

                // 检查右侧是否紧贴平台边缘
                bool rightPlatformEdge = false;
                for (int bx = 1; bx <= buffer; bx++)
                {
                    int checkX = x + bx;
                    if (checkX >= width) break;
                    if (IsSolid(grid, checkX, y, width, height) &&
                        !IsSolid(grid, checkX, y + 1, width, height))
                    {
                        rightPlatformEdge = true;
                        break;
                    }
                }

                // 如果陷阱两侧都紧贴平台边缘，玩家无安全着陆区
                if (leftPlatformEdge && rightPlatformEdge)
                {
                    result.warnings.Add(
                        $"Hazard '{c}' at ({x},{y}) is sandwiched between platforms " +
                        $"with no landing buffer (min={buffer}). " +
                        $"Player has no safe landing zone when jumping across.");
                }
            }
        }
    }
}
