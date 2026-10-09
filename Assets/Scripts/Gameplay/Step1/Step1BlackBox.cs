using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// S242：试玩黑匣子（纯逻辑，沙盒 sim 也编译）。用户："如何打包上传的反馈包括任何我在 unity 测试可能出现的问题 比如卡住
/// 并且保证大小可控 最精确的反馈给出最精确的修复"。
/// 做法参考：
///   · Sentry breadcrumbs —— 出事前的最近 N 条操作（默认 100 条）https://docs.sentry.io/product/issues/issue-details/breadcrumbs/
///   · PlayReport —— 一键报告 = 最近几秒的回放 + 日志 + 系统信息，出异常自动报 https://discussions.unity.com/t/released-playreport-one-key-playtest-reports-with-gif-replay-logs-and-discord/1736266
///   · BetaHub —— 滚动日志、只留最近的、有大小上限 https://betahub.io/blog/general/2024/09/25/feedback-form-unity.html
///   · Bugnet —— 截图先缩小再存；在报告界面画出来之前截 https://bugnet.io/blog/automated-screenshot-capture-for-game-bug-reports
///   · vav-labs —— bug 报告要证据（当时的状态），不要口述 https://vav-labs.com/blog/unity-bug-reports-evidence-not-folklore/
/// 本文件只有数据结构和判断：环形缓冲、面包屑、自动检测（卡住 / 按住不动 / 顿卡 / 坏数字 / 时间停住 / 狂按）、限频、
/// 房间文字快照、按字节预算挑文件、自动总结。采样与写文件在 Step1BlackBoxRecorder。纯记录，不碰玩法（H4）。
/// </summary>
public static class Step1BlackBox
{
    /// <summary>一帧采样（4–5 次/秒）。只存数字和短字符串，25 秒约 120 条 ≈ 15KB 文字。</summary>
    public struct Sample
    {
        public float t;          // Time.unscaledTime
        public Vector2 mario, marioVel, you, youVel;
        public string marioState; // 马里奥心智状态（Running / Chasing …）
        public string youFlags;   // 伪 = 伪装 地 = 遁地 丝 = 蛛丝 炮 = 坐炮 缩 = 缩小
        public float timeScale, fps;
        public string keys;       // 这一刻按住的方向 / 技能键
    }

    /// <summary>固定容量的环形缓冲：满了就丢最旧的。</summary>
    public sealed class Ring<T>
    {
        private readonly T[] buf; private int start, count;
        public Ring(int capacity) { buf = new T[Mathf.Max(1, capacity)]; }
        public int Count => count;
        public int Capacity => buf.Length;
        public void Add(T v) { if (count < buf.Length) { buf[(start + count) % buf.Length] = v; count++; } else { buf[start] = v; start = (start + 1) % buf.Length; } }
        public T this[int i] => buf[(start + i) % buf.Length];
        public List<T> ToList() { var l = new List<T>(count); for (int i = 0; i < count; i++) l.Add(this[i]); return l; }
        public void Clear() { start = 0; count = 0; }
    }

    public const int BreadcrumbCap = 150;
    public static int SampleCap(float seconds, float hz) => Mathf.Clamp(Mathf.CeilToInt(seconds * hz), 10, 600);

    // ── 自动检测（每一项都是"可能是 bug"的客观信号，限频后自动记一条反馈） ─────────────────
    public enum Kind { Manual, Stuck, HeldNoMove, Hitch, BadNumber, TimeFrozen, Mash, Error, Rescue }

    public static string KindZh(Kind k)
    {
        switch (k)
        {
            case Kind.Manual: return "你按 F8";
            case Kind.Stuck: return "马里奥卡住";
            case Kind.HeldNoMove: return "按住方向键你却不动";
            case Kind.Hitch: return "画面顿卡";
            case Kind.BadNumber: return "位置坏了（NaN / 出房间）";
            case Kind.TimeFrozen: return "时间停住太久";
            case Kind.Mash: return "同一个键狂按（多半是按了没反应）";
            case Kind.Error: return "红字错误";
            case Kind.Rescue: return "卡住救援";
            default: return k.ToString();
        }
    }

    /// <summary>严重度（总结里排前三用）：坏数字 / 错误 &gt; 卡住 &gt; 按了没反应 &gt; 顿卡。</summary>
    public static int Severity(Kind k)
    {
        switch (k)
        {
            case Kind.BadNumber: case Kind.Error: return 5;
            case Kind.Stuck: case Kind.TimeFrozen: case Kind.Rescue: return 4;
            case Kind.HeldNoMove: case Kind.Mash: return 3;
            case Kind.Hitch: return 2;
            default: return 1;
        }
    }

    /// <summary>某段时间内的位移（首尾样本之间）。窗口内样本不足 = -1。</summary>
    public static float Travel(IList<Vector2> pos)
    {
        if (pos == null || pos.Count < 2) return -1f;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in pos) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        return Mathf.Max(maxX - minX, maxY - minY);
    }

    /// <summary>马里奥卡住：在跑（不是被晕 / 等开局 / 时间停）但 seconds 秒内活动范围小于 0.35 格。</summary>
    public static bool Stuck(IList<Vector2> marioWindow, bool shouldMove, float span, float needSeconds) =>
        shouldMove && span >= needSeconds && Travel(marioWindow) >= 0f && Travel(marioWindow) < 0.35f;

    /// <summary>你按住 ← / → 超过 seconds 秒，位置几乎不变（被墙夹住 / 输入没收到 / 被某个状态锁住）。</summary>
    public static bool HeldNoMove(float heldSeconds, float needSeconds, IList<Vector2> youWindow, bool allowedToMove) =>
        allowedToMove && heldSeconds >= needSeconds && Travel(youWindow) >= 0f && Travel(youWindow) < 0.2f;

    /// <summary>一帧比 threshold 秒还长（编辑器里 0.25–0.3 秒以上人眼会觉得"卡了一下"）。</summary>
    public static bool Hitch(float unscaledFrameSeconds, float threshold) => threshold > 0f && unscaledFrameSeconds >= threshold;

    /// <summary>位置是 NaN / 无穷，或者离房间超过 margin 格。roomW/H ≤ 0 = 不知道房间，只查 NaN。</summary>
    public static bool BadNumber(Vector2 p, int roomW, int roomH, float margin = 3f)
    {
        if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) return true;
        if (roomW <= 0 || roomH <= 0) return false;
        return p.x < -margin || p.y < -margin || p.x > roomW - 1 + margin || p.y > roomH - 1 + margin;
    }

    /// <summary>timeScale 是 0 而且没有暂停菜单 / 问卷 / 帮助页挡着，持续超过 seconds 秒。</summary>
    public static bool TimeFrozen(float frozenSeconds, bool legitPause, float needSeconds) => !legitPause && frozenSeconds >= needSeconds;

    /// <summary>同一个键 window 秒内按了 ≥ presses 次（"按了没反应 → 狂按"，类似网页分析里的 rage click）。</summary>
    public static bool Mash(IList<float> pressTimes, float now, float window, int presses)
    {
        if (pressTimes == null) return false;
        int n = 0; foreach (var t in pressTimes) if (now - t <= window) n++;
        return n >= presses;
    }

    /// <summary>限频：同一类每 cooldown 秒最多一次、一局最多 cap 次（防止一个 bug 刷几百张截图把包撑爆）。</summary>
    public sealed class Throttle
    {
        private readonly Dictionary<Kind, float> last = new Dictionary<Kind, float>();
        private int used; private readonly float cooldown; private readonly int cap;
        public Throttle(float cooldownSeconds, int maxPerSession) { cooldown = cooldownSeconds; cap = maxPerSession; }
        public int Used => used;
        public bool Allow(Kind k, float now)
        {
            if (k == Kind.Manual) return true; // 你自己按的永远记
            if (used >= cap) return false;
            if (last.TryGetValue(k, out var t) && now - t < cooldown) return false;
            last[k] = now; used++; return true;
        }
    }

    // ── 房间快照：ASCII 房间上叠 M（马里奥）和 T（你）——AI 一眼看到"在哪卡住" ─────────────
    public static string[] Snapshot(IList<string> rows, Vector2 mario, Vector2 you)
    {
        if (rows == null || rows.Count == 0) return new string[0];
        var o = rows.Select(r => r.ToCharArray()).ToArray();
        void Put(Vector2 p, char c)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.y)) return;
            int x = Mathf.RoundToInt(p.x), row = o.Length - 1 - Mathf.RoundToInt(p.y);
            if (row >= 0 && row < o.Length && x >= 0 && x < o[row].Length) o[row][x] = c;
        }
        Put(you, 'T'); Put(mario, 'M');
        return o.Select(a => new string(a)).ToArray();
    }

    // ── 截图缩放：最宽 maxW 像素（JPG 质量 72 时 960 宽 ≈ 80–150KB；原来全分辨率 PNG 1–4MB） ──────
    public static (int w, int h) Fit(int w, int h, int maxW)
    {
        if (w <= 0 || h <= 0) return (0, 0);
        if (maxW <= 0 || w <= maxW) return (w, h);
        return (maxW, Mathf.Max(1, Mathf.RoundToInt(h * (maxW / (float)w))));
    }

    // ── 按字节预算挑文件：必带的先放，然后新的优先，超了就丢旧截图 ─────────────────────────
    public struct FileItem { public string name; public long bytes; public int priority; public long order; } // priority 越小越重要；order 越大越新

    public static (List<FileItem> keep, List<FileItem> dropped) Budget(IEnumerable<FileItem> files, long budgetBytes)
    {
        var keep = new List<FileItem>(); var drop = new List<FileItem>(); long used = 0;
        foreach (var f in files.OrderBy(f => f.priority).ThenByDescending(f => f.order))
        {
            if (f.priority == 0 || used + f.bytes <= budgetBytes) { keep.Add(f); used += f.bytes; }
            else drop.Add(f);
        }
        return (keep, drop);
    }

    /// <summary>日志尾巴：只留错误 / 异常 / 警告行和它下面 2 行堆栈，最多 maxBytes 字节（从最新往回取）。</summary>
    public static string TailErrors(IList<string> lines, int maxBytes)
    {
        if (lines == null || lines.Count == 0) return "";
        var keep = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i] ?? "";
            if (l.Contains("Exception") || l.Contains("Error") || l.Contains("error CS") || l.StartsWith("Assert") || l.Contains("NullReference"))
            {
                keep.Add(l);
                for (int k = 1; k <= 2 && i + k < lines.Count; k++) keep.Add("    " + lines[i + k]);
                keep.Add("");
            }
        }
        var outp = new List<string>(); int bytes = 0;
        for (int i = keep.Count - 1; i >= 0; i--)
        {
            int b = Encoding.UTF8.GetByteCount(keep[i]) + 1;
            if (bytes + b > maxBytes) break;
            outp.Insert(0, keep[i]); bytes += b;
        }
        return string.Join("\n", outp);
    }

    // ── 一次记录（F8 或自动）= 一份 md：总述 + 面包屑 + 最近几秒的采样表 + 房间快照 ─────────────
    public struct Report { public int n; public Kind kind; public string when, scene, note, context; }

    public static string Markdown(Report r, IList<Sample> samples, IList<string> crumbs, IList<string> roomRows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 记录 {r.n:000} · {KindZh(r.kind)} · {r.when}");
        sb.AppendLine($"- 场景：{r.scene}");
        sb.AppendLine($"- 情况：{r.context}");
        if (!string.IsNullOrEmpty(r.note)) sb.AppendLine($"- 说明：{r.note}");
        sb.AppendLine();
        if (roomRows != null && roomRows.Count > 0 && samples != null && samples.Count > 0)
        {
            var last = samples[samples.Count - 1];
            sb.AppendLine("## 房间快照（M = 马里奥，T = 你）");
            sb.AppendLine("```");
            foreach (var l in Snapshot(roomRows, last.mario, last.you)) sb.AppendLine(l);
            sb.AppendLine("```");
        }
        sb.AppendLine("## 出事前发生了什么（最近的在最下面）");
        if (crumbs == null || crumbs.Count == 0) sb.AppendLine("（没有）");
        else foreach (var c in crumbs) sb.AppendLine("- " + c);
        sb.AppendLine();
        sb.AppendLine("## 最近几秒（每行 = 一次采样）");
        sb.AppendLine("| 秒 | 马里奥 | 速度 | 状态 | 你 | 速度 | 你在 | 按键 | timeScale | fps |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        if (samples != null && samples.Count > 0)
        {
            float t0 = samples[samples.Count - 1].t;
            foreach (var s in samples)
                sb.AppendLine($"| {s.t - t0:0.0} | {s.mario.x:0.0},{s.mario.y:0.0} | {s.marioVel.x:0.0},{s.marioVel.y:0.0} | {s.marioState} | {s.you.x:0.0},{s.you.y:0.0} | {s.youVel.x:0.0},{s.youVel.y:0.0} | {s.youFlags} | {s.keys} | {s.timeScale:0.##} | {s.fps:0} |");
        }
        return sb.ToString();
    }

    /// <summary>反馈包首页的自动总结：每类几次 + 最严重的 3 条（AI 先看这个）。</summary>
    public static string Summary(IList<(Kind kind, string when, string file, string line)> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 自动总结");
        if (events == null || events.Count == 0) { sb.AppendLine("这次没有自动记下任何问题（只有你按的 F8，或什么都没记）。"); return sb.ToString(); }
        foreach (var g in events.GroupBy(e => e.kind).OrderByDescending(g => Severity(g.Key)).ThenByDescending(g => g.Count()))
            sb.AppendLine($"- {KindZh(g.Key)}：{g.Count()} 次");
        sb.AppendLine();
        sb.AppendLine("### 最该先看的 3 条");
        foreach (var e in events.OrderByDescending(e => Severity(e.kind)).ThenByDescending(e => e.when).Take(3))
            sb.AppendLine($"- [{KindZh(e.kind)}] {e.when} → {e.file}：{e.line}");
        return sb.ToString();
    }
}
