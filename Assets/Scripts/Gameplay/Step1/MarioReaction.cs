using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S223（总方案阶段 B）：马里奥"中招反应"表 —— 纯逻辑，沙盒可测，无 Unity 场景依赖。
/// 三段式（动画 take：准备—停顿—爆发）：
///   ① 愣住 freeze：身体僵住、微微压扁（Bergson：好笑 = 活人身上出现机械的僵硬）；
///   ② 动作 act：每种坑一种固定动作（H6：同一种坑永远同一种反应，玩家能预期 → GDC《Comedy Through Patterns》先建立模式）；
///   ③ 恢复 recover：平滑回到原样。
/// 只是画面：不改晕眩时长、不改速度/碰撞/AI（H9 时限不变，H4 不读捣蛋者）。sim 检查：愣住+动作 ≤ 这种坑本来就晕的秒数。
/// 连锁时（上一个反应还没演完又中招）：新反应跳过"愣住"直接演动作 → 连锁不会越连越拖沓（Reddit"顿帧太多 = 拖沓"）。
/// 数据：Assets/Resources/MarioReactions.json（文本文件，可直接改数字/台词）；读不到就用 Default（两者 sim 逐项对照）。
/// </summary>
public static class MarioReaction
{
    public enum Pose { Flail, Tumble, Slip, Spin, Rattle, Dangle, Surprise, Look, Stomp }

    public struct Beat
    {
        public string kind;
        public float freeze, act, recover;
        public Pose pose;
        public string zh, en;
        public float Total => freeze + act + recover;
        /// <summary>他"站着不动演戏"的部分（愣住 + 动作）。</summary>
        public float Held => freeze + act;
    }

    /// <summary>某一时刻的画面偏移（全部作用在马里奥的"外观"上，碰撞体不动）。</summary>
    public struct Frame
    {
        public float sx, sy, rotDeg, dx, dy;
        public int phase; // 0 愣住 1 动作 2 恢复 3 结束
        public static Frame Identity => new Frame { sx = 1f, sy = 1f, phase = 3 };
    }

    public const float FreezeSquash = 0.12f;

    /// <summary>默认表（和 MarioReactions.json 一致）。kind 与连招事件名相同（Step1Combo.Register 的 kind）。</summary>
    public static readonly Beat[] Default =
    {
        B("hurt",   0.20f, 0.70f, 0.30f, Pose.Flail,    "哎哟！",     "OUCH!"),
        B("trip",   0.08f, 0.30f, 0.15f, Pose.Tumble,   "绊倒了！",   "WHOA!"),
        B("slip",   0.10f, 0.35f, 0.25f, Pose.Slip,     "滑——！",     "SLIP!"),
        B("launch", 0.10f, 0.45f, 0.20f, Pose.Spin,     "飞了——！",   "WHEEE!"),
        B("cage",   0.25f, 1.80f, 0.40f, Pose.Rattle,   "放我出去！", "LET ME OUT!"),
        B("snare",  0.20f, 2.00f, 0.40f, Pose.Dangle,   "倒挂了！",   "HEY!"),
        B("drop",   0.00f, 0.40f, 0.20f, Pose.Surprise, "掉下去了！", "UH-OH!"),
        B("pit",    0.00f, 0.50f, 0.20f, Pose.Look,     "坑？！",     "A PIT?!"),
        B("stop",   0.00f, 0.60f, 0.20f, Pose.Stomp,    "过不去！",   "BLOCKED!"),
    };

    private static Beat B(string k, float f, float a, float r, Pose p, string zh, string en)
        => new Beat { kind = k, freeze = f, act = a, recover = r, pose = p, zh = zh, en = en };

    /// <summary>不会让他站住的坑（掉坑/掉层/被挡）：整段反应最多几秒（边走边演）。</summary>
    public const float MaxUnstunnedTotal = 0.8f;

    public static bool TryGet(IList<Beat> table, string kind, out Beat beat)
    {
        if (table != null)
            for (int i = 0; i < table.Count; i++)
                if (table[i].kind == kind) { beat = table[i]; return true; }
        beat = default;
        return false;
    }

    /// <summary>新反应从第几秒开始演：连锁中（上一个还没演完）跳过愣住。</summary>
    public static float StartTime(Beat b, bool chaining) => chaining ? b.freeze : 0f;

    /// <summary>t 秒时的画面。facing = +1 朝右 / -1 朝左（往前摔、往后仰都按朝向）。</summary>
    public static Frame Sample(Beat b, float t, float facing)
    {
        facing = facing < 0f ? -1f : 1f;
        if (t < 0f) t = 0f;
        if (t >= b.Total) return Frame.Identity;
        if (t < b.freeze)
        {
            float e = Mathf.Clamp01(t / 0.05f);
            return new Frame { sx = 1f + FreezeSquash * e, sy = 1f - FreezeSquash * e, phase = 0 };
        }
        if (t < b.Held || b.recover <= 0f)
            return Act(b, Mathf.Min(t, b.Held) - b.freeze, facing);
        // 恢复：从"动作最后一帧"平滑回到原样（角度先规整到 ±180，转了一圈的不会倒着转回去）
        var end = Act(b, b.act, facing);
        end.rotDeg = Wrap(end.rotDeg);
        float k = Step1Feel.SmoothStep01((t - b.Held) / b.recover);
        return new Frame
        {
            sx = Mathf.Lerp(end.sx, 1f, k), sy = Mathf.Lerp(end.sy, 1f, k), rotDeg = Mathf.Lerp(end.rotDeg, 0f, k),
            dx = Mathf.Lerp(end.dx, 0f, k), dy = Mathf.Lerp(end.dy, 0f, k), phase = 2
        };
    }

    private static Frame Act(Beat b, float local, float facing)
    {
        float a = Mathf.Max(0.0001f, b.act);
        float u = Mathf.Clamp01(local / a);
        // 愣住时的压扁在动作前 20% 里慢慢松开（不跳变）；没有愣住段的直接从原样开始
        float squash = b.freeze > 0f ? FreezeSquash * (1f - Step1Feel.SmoothStep01(u * 5f)) : 0f;
        var f = new Frame { sx = 1f + squash, sy = 1f - squash, phase = 1 };
        float s = (float)Math.Sin(Math.PI * u);
        switch (b.pose)
        {
            case Pose.Flail: // 被烫到：原地蹦一下 + 手忙脚乱地晃
                f.dy = 0.35f * s;
                f.sy += 0.12f * s; f.sx -= 0.08f * s;
                f.rotDeg = 15f * (float)Math.Sin(u * 6f * Math.PI) * (1f - u);
                break;
            case Pose.Tumble: // 绊倒：往前栽
                f.rotDeg = -35f * facing * Step1Feel.SmoothStep01(u * 2.5f);
                f.dy = -0.1f * Step1Feel.SmoothStep01(u * 2.5f);
                break;
            case Pose.Slip: // 滑倒：往后仰、腿被拉长
                f.rotDeg = 30f * facing * s;
                f.sx += 0.15f * s; f.sy -= 0.1f * s;
                break;
            case Pose.Spin: // 被弹飞：空中转一圈
                f.rotDeg = -360f * facing * Step1Feel.SmoothStep01(u);
                break;
            case Pose.Rattle: // 被关：摇笼子
                f.dx = 0.08f * (float)Math.Sin(u * a * 2f * Math.PI * 7f) * (1f - 0.4f * u);
                break;
            case Pose.Dangle: // 被吊：荡来荡去，越荡越小
                f.rotDeg = 25f * (float)Math.Sin(u * a * 2f * Math.PI * 1.2f) * (1f - 0.5f * u);
                break;
            case Pose.Surprise: // 脚下一空：吓得拉长
                f.sy += 0.3f * s; f.sx -= 0.15f * s;
                break;
            case Pose.Look: // 掉进坑：左右张望
                f.rotDeg = 12f * (float)Math.Sin(u * 4f * Math.PI);
                break;
            case Pose.Stomp: // 被挡：气得跺脚
                float st = Math.Abs((float)Math.Sin(u * 3f * Math.PI));
                f.dy = 0.12f * st; f.sy -= 0.1f * (1f - st) * s;
                break;
        }
        return f;
    }

    public static float Wrap(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        if (deg <= -180f) deg += 360f;
        return deg;
    }

    /// <summary>头顶台词（两行，中文在上）。</summary>
    public static string Line(Beat b) => b.zh + "\n" + b.en;

    // ── 数据文件 ─────────────────────────────────────────
    /// <summary>读 MarioReactions.json。格式：{"reactions":[{"kind":"hurt","freeze":0.2,"act":0.7,"recover":0.3,"pose":"Flail","zh":"哎哟！","en":"OUCH!"}, …]}。
    /// 读不到 / 写错 → 返回 Default，error 写原因（不会让游戏坏掉）。缺的种类用 Default 补上。</summary>
    public static Beat[] Parse(string json, out string error)
    {
        error = "";
        var root = MiniJson.Parse(json, out string je) as Dictionary<string, object>;
        if (root == null || !root.TryGetValue("reactions", out object listObj) || !(listObj is List<object> list))
        {
            error = string.IsNullOrEmpty(je) ? "没有 reactions 列表" : je;
            return (Beat[])Default.Clone();
        }
        var result = new List<Beat>();
        foreach (var item in list)
        {
            if (!(item is Dictionary<string, object> d)) continue;
            string kind = Str(d, "kind");
            if (string.IsNullOrEmpty(kind)) { error = "有一项没写 kind"; continue; }
            TryGet(Default, kind, out Beat def);
            var b = def;
            b.kind = kind;
            b.freeze = Mathf.Clamp(Num(d, "freeze", def.freeze), 0f, 1f);
            b.act = Mathf.Clamp(Num(d, "act", def.act), 0.05f, 4f);
            b.recover = Mathf.Clamp(Num(d, "recover", def.recover), 0f, 1f);
            string pose = Str(d, "pose");
            if (!string.IsNullOrEmpty(pose))
            {
                if (Enum.TryParse(pose, out Pose p)) b.pose = p;
                else error = $"{kind}：不认识的动作 {pose}";
            }
            string zh = Str(d, "zh"), en = Str(d, "en");
            if (!string.IsNullOrEmpty(zh)) b.zh = zh;
            if (!string.IsNullOrEmpty(en)) b.en = en;
            bool dup = false;
            for (int i = 0; i < result.Count; i++) if (result[i].kind == kind) { result[i] = b; dup = true; }
            if (!dup) result.Add(b);
        }
        foreach (var def in Default)
            if (!TryGet(result, def.kind, out _)) result.Add(def);
        return result.ToArray();
    }

    private static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out object v) && v is string s ? s : "";
    private static float Num(Dictionary<string, object> d, string k, float fallback) => d.TryGetValue(k, out object v) && v is double x ? (float)x : fallback;

    /// <summary>把表写回 JSON（sim 用它对照数据文件；也方便以后网页设计台读写）。</summary>
    public static string ToJson(IList<Beat> table)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n  \"version\": 1,\n  \"reactions\": [\n");
        for (int i = 0; i < table.Count; i++)
        {
            var b = table[i];
            sb.Append("    {\"kind\": \"").Append(b.kind).Append("\", \"freeze\": ").Append(F(b.freeze))
              .Append(", \"act\": ").Append(F(b.act)).Append(", \"recover\": ").Append(F(b.recover))
              .Append(", \"pose\": \"").Append(b.pose).Append("\", \"zh\": \"").Append(b.zh).Append("\", \"en\": \"").Append(b.en).Append("\"}")
              .Append(i < table.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    private static string F(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
