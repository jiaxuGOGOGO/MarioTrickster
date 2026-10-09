using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S198：可炸毁的普通方块。用户要求"炸弹对除了机关陷阱和特殊地形之外的都能破坏"。
/// 规则（Step1PrankRoomBuilder.MarkDestructibles 按格子挂上）：
///   - 可炸：房间内部的地面 #、墙 W（**最外圈围墙与最底层地面除外**，否则会炸出地图，H9）、箱子 c、草丛 b、装饰 d、裂墙 %、单向台面 -；
///   - 不可炸：所有机关/陷阱（Controllable）、特殊地形（毒池/黏胶/通风管/捷径门/出口/宝物/出生点）。
/// 生成器把一行连续的 # 合并成一个长碰撞体，所以这里把"被炸的那几格"从长条里**切掉**：
///   关掉原碰撞体，按剩余格子重建若干段碰撞体与外观（纯运行时，回合重置时复原）。
/// 可达性：炸只会**多开路**；最外圈/底层不可炸 → 不会掉出地图。死局检查照旧按"不炸"算（最坏情况）。
/// </summary>
public class Destructible : MonoBehaviour
{
    [SerializeField] private int startX, y, width = 1;
    [SerializeField] private bool[] locked = new bool[0]; // 每格：true = 不可炸（外圈/底层）
    private bool[] gone;
    private readonly List<GameObject> pieces = new List<GameObject>();
    private BoxCollider2D original;
    private Transform visual;
    private static readonly List<Destructible> all = new List<Destructible>();
    public static IReadOnlyList<Destructible> All => all;
    public int StartX => startX; public int Y => y; public int Width => width;

    [SerializeField] private bool[] reinforcedCells = new bool[0]; // S202：策略模拟判定的承重格（炸不掉，画铆钉）
    public void Configure(int sx, int row, int w, bool[] lockedCells, bool[] reinforced = null)
    {
        startX = sx; y = row; width = Mathf.Max(1, w);
        locked = lockedCells != null && lockedCells.Length == width ? lockedCells : new bool[width];
        reinforcedCells = reinforced != null && reinforced.Length == width ? reinforced : new bool[width];
        for (int i = 0; i < width; i++) if (reinforcedCells[i]) locked[i] = true;
    }

    /// <summary>S202：加固格画"铆钉"（H6：看得见哪里炸不掉）。</summary>
    private void Start()
    {
        if (reinforcedCells == null) return;
        for (int i = 0; i < reinforcedCells.Length && i < width; i++)
        {
            if (!reinforcedCells[i]) continue;
            var go = new GameObject("Rivet");
            go.transform.SetParent(transform.parent, false);
            go.transform.position = new Vector3(startX + i, y + 0.28f, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Step1Sprites.Square; r.color = new Color(0.85f, 0.85f, 0.9f, 0.9f); r.sortingOrder = 5;
            go.transform.localScale = new Vector3(0.28f, 0.14f, 1f);
        }
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    private void Awake()
    {
        original = GetComponent<BoxCollider2D>();
        visual = transform.Find("Visual");
        gone = new bool[width];
    }

    /// <summary>纯逻辑：炸掉 [a,b] 格后剩下的连续段（用于重建碰撞体）。</summary>
    public static List<(int from, int to)> Segments(bool[] removed)
    {
        var segs = new List<(int, int)>();
        int s = -1;
        for (int i = 0; i <= removed.Length; i++)
        {
            bool solid = i < removed.Length && !removed[i];
            if (solid && s < 0) s = i;
            if (!solid && s >= 0) { segs.Add((s, i - 1)); s = -1; }
        }
        return segs;
    }

    /// <summary>纯逻辑：哪些格子在爆炸圆内且可炸（返回局部下标）。</summary>
    public static List<int> CellsInBlast(int sx, int row, int w, bool[] lockedCells, Vector2 center, float radius)
    {
        var hit = new List<int>();
        for (int i = 0; i < w; i++)
        {
            if (lockedCells != null && i < lockedCells.Length && lockedCells[i]) continue;
            if ((new Vector2(sx + i, row) - center).sqrMagnitude <= radius * radius) hit.Add(i);
        }
        return hit;
    }

    /// <summary>炸：返回炸掉了几格。</summary>
    public int Blast(Vector2 center, float radius)
    {
        if (gone == null || gone.Length != width) gone = new bool[width];
        int n = 0;
        foreach (int i in CellsInBlast(startX, y, width, locked, center, radius))
            if (!gone[i]) { gone[i] = true; n++; }
        if (n > 0) Rebuild();
        return n;
    }

    private void Rebuild()
    {
        foreach (var p in pieces) if (p != null) Destroy(p);
        pieces.Clear();
        if (original != null) original.enabled = false;
        if (visual != null) visual.gameObject.SetActive(false);
        var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        foreach (var (a, b) in Segments(gone))
        {
            int w = b - a + 1;
            var go = new GameObject($"{name}_part{a}");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform.parent, false);
            go.transform.position = new Vector3(startX + a + (w - 1) * 0.5f, y, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(w, original != null ? original.size.y : 1f);
            col.offset = original != null ? new Vector2(0f, original.offset.y) : Vector2.zero;
            if (original != null) col.sharedMaterial = original.sharedMaterial;
            if (sr != null)
            {
                var v = new GameObject("Visual");
                v.transform.SetParent(go.transform, false);
                var r = v.AddComponent<SpriteRenderer>();
                r.sprite = sr.sprite; r.color = sr.color; r.sortingOrder = sr.sortingOrder; r.drawMode = sr.drawMode;
                if (sr.drawMode == SpriteDrawMode.Tiled) { r.size = new Vector2(w * (sr.size.x / Mathf.Max(1, width)), sr.size.y); v.transform.localScale = visual.localScale; } // S243：像素地形是平铺的 → 切块也平铺（不拉伸）
                else v.transform.localScale = new Vector3(w * (visual.localScale.x / Mathf.Max(1, width)), visual.localScale.y, 1f);
            }
            pieces.Add(go);
        }
    }

    public void Restore()
    {
        foreach (var p in pieces) if (p != null) Destroy(p);
        pieces.Clear();
        gone = new bool[width];
        if (original != null) original.enabled = true;
        if (visual != null) visual.gameObject.SetActive(true);
    }

    /// <summary>回合重置：全部复原（Step1RoomReset 调用）。</summary>
    public static void RestoreAll() { foreach (var d in all.ToArray()) if (d != null) d.Restore(); }
}
