using System.Collections.Generic;

/// <summary>
/// S224（总方案阶段 C · SpyParty"事后告诉你他为什么怀疑"）：记下"他差点发现你"的时刻。纯逻辑，沙盒可测。
/// 一段 = 他从平静变成起疑（头顶 ?）到重新平静；中间被抓 = 这段算"被抓"，不算差点。
/// 只读马里奥自己的起疑表（Meter）和小镇已经算好的"他是怎么注意到你的"（seenWhy），不新增任何感知（H4）。
/// 宪法体验目标："差一点就被他发现了"——以前只能靠你自己记，现在结算时列出来。
/// </summary>
public sealed class NearMissLog
{
    public struct Moment
    {
        public string clock;   // 小镇 = "09:12"；房间 = "41s"
        public string why;     // OverworldMap.SeenWhy 的名字；房间里为空
        public float peak;     // 最高起疑进度 0–1（1 = 认出你）
        public bool caught;
    }

    private readonly List<Moment> done = new List<Moment>();
    private bool open;
    private Moment cur;

    public IReadOnlyList<Moment> All => done;
    public int NearMisses { get { int n = 0; foreach (var m in done) if (!m.caught) n++; return n; } }

    public void Clear() { done.Clear(); open = false; cur = default; }

    /// <summary>每帧喂一次。suspicious = 头顶有标记（Level != Calm）。</summary>
    public void Feed(bool suspicious, float normalized, string clock, string why)
    {
        if (suspicious)
        {
            if (!open) { open = true; cur = new Moment { clock = clock ?? "", why = why ?? "", peak = normalized }; }
            else
            {
                if (normalized > cur.peak) cur.peak = normalized;
                if (string.IsNullOrEmpty(cur.why) && !string.IsNullOrEmpty(why)) cur.why = why;
            }
        }
        else if (open) { open = false; done.Add(cur); }
    }

    /// <summary>被抓：正在进行的那段算被抓（没有进行中的就单独记一条）。</summary>
    public void Caught(string clock, string why)
    {
        if (open) { cur.caught = true; cur.peak = 1f; if (!string.IsNullOrEmpty(why)) cur.why = why; done.Add(cur); open = false; }
        else done.Add(new Moment { clock = clock ?? "", why = why ?? "", peak = 1f, caught = true });
    }

    /// <summary>最接近被发现的 n 次（没被抓的；按起疑最高排，一样高按时间先后）。</summary>
    public List<Moment> Closest(int n)
    {
        var l = new List<(Moment m, int i)>();
        for (int i = 0; i < done.Count; i++) if (!done[i].caught) l.Add((done[i], i));
        l.Sort((a, b) => b.m.peak != a.m.peak ? b.m.peak.CompareTo(a.m.peak) : a.i.CompareTo(b.i));
        var r = new List<Moment>();
        for (int i = 0; i < l.Count && i < n; i++) r.Add(l[i].m);
        return r;
    }
}
