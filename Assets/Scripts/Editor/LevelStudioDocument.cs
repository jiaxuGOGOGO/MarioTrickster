using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bounded, lossless grid editing over the existing ASCII format. No scene mutations.
/// World Y=0 is the bottom row; adding headroom must never move the floor.
/// </summary>
public sealed class LevelStudioDocument
{
    public const int MaxWidth = 128;
    public const int MaxHeight = 48;
    private readonly char[][] rows;
    private readonly string[] metadata;
    public int Width => rows[0].Length;
    public int Height => rows.Length;
    public string Grid => string.Join("\n", rows.Select(r => new string(r)));
    public string Text => metadata.Length == 0 ? Grid : Grid + "\n" + string.Join("\n", metadata);

    private LevelStudioDocument(char[][] rows, string[] metadata)
    {
        this.rows = rows;
        this.metadata = metadata;
    }

    public static bool TryParse(string text, out LevelStudioDocument document, out string error)
    {
        document = null;
        error = "";
        if (string.IsNullOrWhiteSpace(text)) { error = "先选一个起步房间，或导入关卡。"; return false; }
        if (text.Length > 32768) { error = "模板过大，请拆成较短的房间。"; return false; }
        var grid = new List<string>();
        var notes = new List<string>();
        foreach (string raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            string start = line.TrimStart();
            if (IsMetadata(start)) { notes.Add(line); continue; }
            grid.Add(line.Replace(' ', '.'));
        }
        if (grid.Count == 0 || grid.Count > MaxHeight || grid.Any(r => r.Length > MaxWidth))
        { error = $"画布需要 1–{MaxWidth} 列、1–{MaxHeight} 行；超大模板可用高级工具。"; return false; }
        int width = grid.Max(r => r.Length);
        if (width == 0) { error = "画布为空。"; return false; }
        var registry = AsciiElementRegistry.GetDefault();
        for (int y = 0; y < grid.Count; y++)
            for (int x = 0; x < grid[y].Length; x++)
                if (grid[y][x] != '.' && registry.GetEntry(grid[y][x]) == null)
                { error = $"第 {y + 1} 行第 {x + 1} 列的 '{grid[y][x]}' 不在元素库中；原文未修改。"; return false; }
        document = new LevelStudioDocument(grid.Select(r => r.PadRight(width, '.').ToCharArray()).ToArray(), notes.ToArray());
        return true;
    }

    public static bool IsMetadata(string line)
    {
        return line.StartsWith("# Override_", StringComparison.Ordinal) ||
            new[] { "MainRoute", "ShadowRoute", "TrapRoles", "Budget", "TestGoal" }
                .Any(key => line.StartsWith("# " + key + ":", StringComparison.Ordinal));
    }

    public char Cell(int x, int y) => rows[Height - 1 - y][x];

    public void Paint(int x, int y, char value)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        if (value != '.' && AsciiElementRegistry.GetDefault().GetEntry(value) == null)
            throw new ArgumentException("Unknown palette element", nameof(value));
        // Spawns and the goal are moves, not copies: one clear objective per room.
        if (value == 'M' || value == 'T' || value == 'G')
            foreach (char[] row in rows)
                for (int i = 0; i < row.Length; i++) if (row[i] == value) row[i] = '.';
        rows[Height - 1 - y][x] = value;
    }

    public LevelStudioDocument Resize(int width, int height)
    {
        if (width < 1 || width > MaxWidth || height < 1 || height > MaxHeight)
            throw new ArgumentOutOfRangeException(nameof(width));
        var next = Enumerable.Range(0, height).Select(_ => new string('.', width).ToCharArray()).ToArray();
        for (int y = 0; y < Math.Min(height, Height); y++)
            for (int x = 0; x < Math.Min(width, Width); x++) next[height - 1 - y][x] = Cell(x, y);
        return new LevelStudioDocument(next, metadata);
    }

    public string PlayReadiness()
    {
        foreach (char c in new[] { 'M', 'T', 'G' })
        {
            int count = rows.Sum(row => row.Count(value => value == c));
            if (count != 1) return $"需要且只能有一个 {c}（闯关者 / 捣蛋者 / 终点）。当前有 {count} 个。";
        }
        return "";
    }

    public string DesignNudge()
    {
        var registry = AsciiElementRegistry.GetDefault();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Cell(x, y) == 'M' || Cell(x, y) == 'T')
                {
                    if (y == 0 || !registry.IsSolid(Cell(x, y - 1)))
                        return "出生点脚下缺少实心支撑：先让双方安全站稳，再增加挑战。";
                    for (int dx = -2; dx <= 2; dx++)
                        if (x + dx >= 0 && x + dx < Width && registry.IsHazard(Cell(x + dx, y)))
                            return "出生附近有危险物：留一点观察空间，让失败来自选择而不是偷袭。";
                }
        return "一次只改一个挑战：先教会 → 再变化 → 留喘息。自动检查不能证明关卡一定好玩或可通关。";
    }

    public static readonly string[] StarterNames = { "初次跳跃", "读懂陷阱", "双路博弈" };
    public static readonly string[] StarterGoals = {
        "先在平地熟悉移动，跨过一个小台阶，再试着越过两格缺口。",
        "观察单个地刺后起跳。思考：危险前是否留出了反应时间？",
        "上层捷径与下层绕路：让捣蛋者选择时机，让闯关者保有反制路线。"
    };

    public static string Starter(int index)
    {
        int width = 32;
        var grid = Enumerable.Range(0, 9).Select(_ => new string('.', width).ToCharArray()).ToArray();
        for (int x = 0; x < width; x++) grid[8][x] = '#';
        grid[7][2] = 'M'; grid[7][5] = 'T'; grid[7][29] = 'G';
        if (index == 0)
        { grid[7][12] = '#'; grid[8][20] = '.'; grid[8][21] = '.'; }
        else if (index == 1)
        { grid[7][14] = '^'; grid[7][23] = '^'; }
        else
        {
            for (int x = 10; x <= 24; x++) grid[5][x] = '-';
            grid[6][8] = '-'; grid[6][26] = '-';
            grid[7][12] = 'B'; grid[7][18] = 'X'; grid[7][23] = 'B';
        }
        return string.Join("\n", grid.Select(r => new string(r)));
    }
}
