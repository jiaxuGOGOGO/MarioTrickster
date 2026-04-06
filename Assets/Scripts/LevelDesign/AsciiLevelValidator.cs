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
///
/// 使用方式：
///   在 TestConsoleWindow 的 Build 按钮中调用 ValidateTemplate()，
///   生成前先验证，有错误时弹出警告但不阻止生成（设计师可能故意设计极限关卡）。
///
/// 所有阈值引用 PhysicsMetrics 常量，确保与物理系统同步。
///
/// Session 32: 关卡度量转译系统
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
    /// 验证 ASCII 模板的物理可行性
    /// </summary>
    /// <param name="template">多行 ASCII 字符串</param>
    /// <returns>验证结果</returns>
    public static ValidationResult ValidateTemplate(string template)
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

        // ── 检查 3: 水平间隙扫描 ──
        // 扫描每一行的地面层，查找连续空白间隙
        for (int y = 0; y < height; y++)
        {
            int gapStart = -1;
            bool inGap = false;

            for (int x = 0; x < maxWidth; x++)
            {
                bool solid = IsSolid(grid, x, y, maxWidth, height);

                if (!solid && !inGap)
                {
                    // 间隙开始
                    // 但只有当左边有实体时才算间隙（排除关卡边缘）
                    if (x > 0 && IsSolid(grid, x - 1, y, maxWidth, height))
                    {
                        inGap = true;
                        gapStart = x;
                    }
                }
                else if (solid && inGap)
                {
                    // 间隙结束
                    int gapWidth = x - gapStart;
                    CheckGap(gapWidth, gapStart, y, result);
                    inGap = false;
                }
            }
        }

        // ── 检查 4: 垂直高台扫描 ──
        // 扫描每列，查找需要跳跃才能到达的高台
        for (int x = 1; x < maxWidth - 1; x++)
        {
            for (int y = 1; y < height; y++)
            {
                // 找到一个实体块，检查它下方是否有足够的空间构成高台
                if (IsSolid(grid, x, y, maxWidth, height) &&
                    !IsSolid(grid, x, y + 1, maxWidth, height) && // 上方是空气（可站立的表面）
                    y + 1 < height)
                {
                    // 向下查找最近的地面
                    int dropHeight = 0;
                    for (int checkY = y - 1; checkY >= 0; checkY--)
                    {
                        if (IsSolid(grid, x, checkY, maxWidth, height))
                            break;
                        dropHeight++;
                    }

                    if (dropHeight > PhysicsMetrics.ASCII_MAX_HEIGHT &&
                        dropHeight <= PhysicsMetrics.ASCII_EXTREME_HEIGHT)
                    {
                        // 检查左右是否有更低的平台可以跳上来
                        bool hasSteppingStone = false;
                        for (int checkX = Mathf.Max(0, x - PhysicsMetrics.ASCII_MAX_GAP);
                             checkX <= Mathf.Min(maxWidth - 1, x + PhysicsMetrics.ASCII_MAX_GAP);
                             checkX++)
                        {
                            if (checkX == x) continue;
                            for (int checkY = y - dropHeight + 1; checkY < y; checkY++)
                            {
                                if (IsSolid(grid, checkX, checkY, maxWidth, height))
                                {
                                    hasSteppingStone = true;
                                    break;
                                }
                            }
                            if (hasSteppingStone) break;
                        }

                        if (!hasSteppingStone)
                        {
                            result.warnings.Add(
                                $"High platform at ({x},{y}): {dropHeight} blocks high " +
                                $"(max safe = {PhysicsMetrics.ASCII_MAX_HEIGHT}). " +
                                $"Requires precise jumping.");
                        }
                    }
                    else if (dropHeight > PhysicsMetrics.ASCII_EXTREME_HEIGHT)
                    {
                        result.errors.Add(
                            $"UNREACHABLE platform at ({x},{y}): {dropHeight} blocks high " +
                            $"(max jump = {PhysicsMetrics.MAX_JUMP_HEIGHT:F1} blocks). " +
                            $"This is a physical dead end!");
                    }
                }
            }
        }

        // ── 检查 5: S35 出生点安全距离 ──
        // 业界参考: Celeste 每个 checkpoint 周围无危险物，给玩家安全起步空间
        CheckSpawnSafety(grid, maxWidth, height, marioX, marioY, result);

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
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int cx = marioX + dx;
                int cy = marioY + dy;
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
