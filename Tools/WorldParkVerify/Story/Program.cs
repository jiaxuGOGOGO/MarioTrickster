using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// S251 剧情带入模拟器：不调用 Unity，也不是真人试玩。
// 每个“玩家人格”只能看到运行器会显示给玩家的公开信息（规则、阶段文字、已观察线索、博弈 pending 与记录），
// 用固定策略做决定；探索用真实移动/碰撞/计时走过去；博弈用同一内核逐轮推进；每个检查点都存档→恢复对比。
// 产物：.work/worldpark-verify/story/ 下每个人格一份中文旅程记录 + 汇总（单会场矩阵与体验检验）。
class Persona { public string id = "", name = "", bio = "", explore = "", beauty = "", mark = "", split = "", chase = "ignore"; public List<string> route = new List<string>(); }
class VenueRun
{
    public string venue = "", venueName = "", kind = "", result = "", decisive = "";
    public bool survived, informedFair = true, decoyNearest, caught, hasChasers;
    public int rounds, startLives, minLives = int.MaxValue, nearDeath, othersOut, rulesAdded, rulesBit, betrayals, liesExposed, cluesFound, cluesTotal, exitsTried;
    // S252 追逐：被看见次数、草晃起疑次数、鬼跟丢你的次数
    public int seen, rustles, escapes;
    public double elapsed, timeout;
    public List<string> md = new List<string>();
}
class Journey { public Persona p; public List<string> md = new List<string>(); public List<VenueRun> venues = new List<VenueRun>(); public string end = ""; }

static class Program
{
    // 真人阅读速度假设：探索计时在读线索时不会暂停（运行器只在 Esc 暂停/出口确认时停表），按每秒 10 个字估算。
    const double ReadCharsPerSecond = 10;
    static int checkpoints, checkpointFailures;
    static readonly List<string> failures = new List<string>();
    // S252 博弈桌：每轮用 WorldParkTableModel 排一次桌，核对“玩家在桌上看见的”与规则记录一致。
    static int tableRounds;
    static readonly List<string> tableIssues = new List<string>();
    // S252 剧情字幕卡：每张剧情/揭示/结局卡用 WorldParkSceneModel 排一次，核对内容完整、反转卡与线索揭示一致、门与连接规则一致。
    static int sceneCount;
    static readonly List<string> sceneIssues = new List<string>();
    static readonly Dictionary<string, int> twistSeen = new Dictionary<string, int>();

    static int Main(string[] args)
    {
        string repo = FindRepo();
        string specPath = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(repo, "Tools", "WorldParkVerify", "Story", "Borderland.expect.json");
        using var spec = JsonDocument.Parse(File.ReadAllText(specPath));
        var root = spec.RootElement;
        string packPath = Path.Combine(repo, root.GetProperty("pack").GetString());
        var pack = WorldParkPack.Parse(File.ReadAllText(packPath), out string error);
        if (pack == null) { Console.WriteLine("世界包无法解析：" + error); return 1; }
        var errors = WorldParkPack.Validate(pack).Where(i => i.severity == "error").ToList();
        if (errors.Count > 0) { Console.WriteLine("世界包有结构错误：" + errors[0].code + " " + errors[0].text); return 1; }
        var personas = root.GetProperty("personas").EnumerateArray().Select(e => new Persona
        {
            id = e.GetProperty("id").GetString(), name = e.GetProperty("name").GetString(), bio = e.GetProperty("bio").GetString(),
            explore = e.GetProperty("explore").GetString(), beauty = e.GetProperty("beauty").GetString(), mark = e.GetProperty("mark").GetString(), split = e.GetProperty("split").GetString(),
            chase = e.TryGetProperty("chase", out var ch) ? ch.GetString() : "ignore",
            route = e.GetProperty("route").EnumerateArray().Select(x => x.GetString()).ToList()
        }).ToList();

        string outDir = Path.Combine(repo, ".work", "worldpark-verify", "story");
        Directory.CreateDirectory(outDir);
        if (Environment.GetEnvironmentVariable("STORY_TRACE") == "1") { traceDir = Path.Combine(outDir, "trace"); Directory.CreateDirectory(traceDir); }
        // A. 全程旅程：带剧情的沉浸记录
        var journeys = personas.Select(p => PlayJourney(pack, p)).ToList();
        foreach (var j in journeys) File.WriteAllText(Path.Combine(outDir, "旅程_" + j.p.id + ".md"), string.Join("\n", j.md) + "\n", new UTF8Encoding(false));
        // B. 单会场矩阵：每个人格单独打每个会场，衡量公平/威胁/张力
        var matrix = new Dictionary<string, Dictionary<string, VenueRun>>();
        foreach (var p in personas)
        {
            matrix[p.id] = new Dictionary<string, VenueRun>();
            foreach (var v in pack.venues) matrix[p.id][v.id] = TrialVenue(pack, v.id, p);
        }

        var checks = new List<(bool ok, string text)>();
        foreach (var e in root.GetProperty("expect").EnumerateArray())
        {
            string pid = e.GetProperty("persona").GetString(), why = e.TryGetProperty("why", out var w) ? w.GetString() : "";
            if (e.TryGetProperty("venue", out var ve))
            {
                string vid = ve.GetString(), want = e.GetProperty("result").GetString();
                string got = matrix.TryGetValue(pid, out var row) && row.TryGetValue(vid, out var run) ? run.result : "（无此人格/会场）";
                checks.Add((got == want, "预期 " + Name(personas, pid) + " 单独挑战 " + VenueName(pack, vid) + " → " + want + "，实际 " + got + (why == "" ? "" : "（" + why + "）")));
            }
            else
            {
                string want = e.GetProperty("journeyEnd").GetString();
                var j = journeys.FirstOrDefault(x => x.p.id == pid);
                string got = j == null ? "（无此人格）" : j.end;
                checks.Add((got == want, "预期 " + Name(personas, pid) + " 全程旅程结束于 " + want + "，实际 " + got + (why == "" ? "" : "（" + why + "）")));
            }
        }
        foreach (var v in pack.venues)
        {
            var runs = personas.Select(p => matrix[p.id][v.id]).ToList();
            bool canDie = v.nodes.Any(n => n.kind == "ending" && n.result == "dead");
            int alive = runs.Count(r => r.survived), dead = runs.Count - alive;
            if (canDie)
            {
                checks.Add((alive > 0, "公平：" + v.name + " 至少有一种只用公开信息的策略能活下来（" + alive + "/" + runs.Count + " 存活）"));
                checks.Add((dead > 0, "威胁：" + v.name + " 至少会淘汰一种策略（" + dead + "/" + runs.Count + " 出局）"));
            }
            foreach (var r in runs.Where(r => r.survived && r.kind == "game"))
                if (r.nearDeath + r.betrayals + r.rulesAdded + r.othersOut == 0) checks.Add((false, "张力：" + v.name + " 中 " + r.decisive + " 通关过程没有任何濒死/背叛/规则追加/他人出局"));
            foreach (var r in runs.Where(r => !r.informedFair)) checks.Add((false, "公平：" + v.name + " 存在“公开记录推理”也答错的回合（信息不足）"));
            if (v.nodes.Any(n => n.kind == "game" && n.game != null && n.game.mode == "beauty" && n.game.escalate == 1))
                checks.Add((runs.Any(r => r.rulesBit > 0), "规则追加：" + v.name + " 的新规则至少在一次挑战中真正改变了局面（生效 " + runs.Sum(r => r.rulesBit) + " 次）"));
            foreach (var n in v.nodes.Where(n => n.kind == "explore" && n.timeout > 0))
            {
                var ok = runs.Where(r => r.survived).ToList();
                double minUse = ok.Count == 0 ? 0 : ok.Min(r => r.elapsed / r.timeout);
                checks.Add((ok.Count > 0 && minUse >= 0.4, "限时压迫：" + v.name + " 的存活者至少用掉 40% 时间（最少 " + (minUse * 100).ToString("0") + "%，含每秒 " + ReadCharsPerSecond + " 字的阅读耗时）"));
            }
            if (v.nodes.Any(n => n.kind == "explore" && n.points.Any(q => q.kind == "chaser")))
            {
                int caught = runs.Count(r => r.caught);
                checks.Add((caught > 0, "鬼的威胁：" + v.name + " 至少有一种策略被鬼抓住（" + caught + "/" + runs.Count + " 被抓）"));
                var close = runs.Where(r => r.survived && r.seen + r.rustles > 0).Select(r => r.decisive).ToList();
                checks.Add((close.Count > 0, "追逐张力：" + v.name + " 至少一名存活者被鬼看见或起疑过，仍然活着离开（" + (close.Count == 0 ? "没有人" : string.Join("、", close)) + "；全部挑战里鬼跟丢玩家 " + runs.Sum(r => r.escapes) + " 次）"));
            }
        }
        int minEndings = root.TryGetProperty("minDistinctJourneyEnds", out var me) ? me.GetInt32() : 1;
        int distinct = journeys.Select(j => j.end).Distinct().Count();
        checks.Add((distinct >= minEndings, "结局多样：" + distinct + " 种不同的旅程终点（要求 ≥ " + minEndings + "）"));
        checks.Add((journeys.Any(j => j.venues.Count > 0 && j.venues.Last().venue == pack.venues.Last().id), "至少一种人格能走完全程到达最终会场"));
        checks.Add((checkpointFailures == 0 && checkpoints > 0, "检查点：" + checkpoints + " 次存档→恢复全部一致（含博弈动作重放）"));
        checks.Add((tableIssues.Count == 0 && tableRounds > 0, "博弈桌：" + tableRounds + " 轮桌面画面（对你说的话、逐个亮牌、扣命、出局、结算字、新规则）与规则记录一致" + (tableIssues.Count > 0 ? "；共 " + tableIssues.Count + " 处不一致，首个：" + tableIssues[0] : "")));
        checks.Add((sceneIssues.Count == 0 && sceneCount > 0, "字幕卡：" + sceneCount + " 张剧情/揭示/结局卡内容完整（标题、正文逐字出完、反转前后对照、线索本、结局章、门与连接规则一致）" + (sceneIssues.Count > 0 ? "；共 " + sceneIssues.Count + " 处问题，首个：" + sceneIssues[0] : "")));
        foreach (var v in pack.venues.Where(v => v.nodes.Any(n => n.kind == "reveal" && v.clues.Any(c => c.revealAt == n.id))))
        {
            int seen = twistSeen.TryGetValue(v.id, out int k) ? k : 0;
            checks.Add((seen > 0, "反转：" + v.name + " 的揭示至少让一种人格翻开过“当时以为 → 其实”的线索卡（全部模拟里共翻开 " + seen + " 张）"));
        }
        foreach (string f in failures) checks.Add((false, "模拟异常：" + f));

        var sb = new StringBuilder();
        sb.AppendLine("# 剧情带入模拟报告：" + pack.name);
        sb.AppendLine();
        sb.AppendLine("> 由 `Tools/WorldParkVerify/Story` 生成：人格策略 + 真实规则内核 + 真实移动计时，不是 Unity Play，也不是真人试玩。");
        sb.AppendLine();
        sb.AppendLine("## 一、全程旅程");
        sb.AppendLine();
        sb.AppendLine("| 人格 | 策略 | 经过的会场 | 旅程终点 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var j in journeys)
            sb.AppendLine("| " + j.p.name + " | " + Strategy(j.p) + " | " + string.Join(" → ", j.venues.Select(r => Short(r.venueName) + "(" + Label(r.result) + ")")) + " | " + j.end + " |");
        sb.AppendLine();
        sb.AppendLine("## 二、单会场矩阵（每个人格单独挑战每个会场）");
        sb.AppendLine();
        sb.AppendLine("| 会场 | " + string.Join(" | ", personas.Select(p => p.name)) + " | 存活率 |");
        sb.AppendLine("|---|" + string.Join("|", personas.Select(p => "---")) + "|---|");
        foreach (var v in pack.venues)
        {
            var runs = personas.Select(p => matrix[p.id][v.id]).ToList();
            sb.AppendLine("| " + v.name + " | " + string.Join(" | ", runs.Select(Cell)) + " | " + runs.Count(r => r.survived) + "/" + runs.Count + " |");
        }
        sb.AppendLine();
        sb.AppendLine("单元格说明：结果 · 关键数据（博弈：轮数/最低命/濒死/背叛/规则追加/他人出局；探索：用时/线索/尝试出口）。");
        sb.AppendLine();
        sb.AppendLine("## 三、体验检验");
        sb.AppendLine();
        foreach (var c in checks) sb.AppendLine("- " + (c.ok ? "PASS" : "FAIL") + "　" + c.text);
        sb.AppendLine();
        sb.AppendLine("## 四、各人格旅程记录");
        sb.AppendLine();
        foreach (var j in journeys) sb.AppendLine("- [" + j.p.name + "](旅程_" + j.p.id + ".md)");
        File.WriteAllText(Path.Combine(outDir, "模拟报告.md"), sb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDir, "summary.json"), JsonSerializer.Serialize(new
        {
            pack = pack.id, journeys = journeys.Select(j => new { persona = j.p.id, end = j.end, venues = j.venues.Select(r => new { r.venue, r.result }) }),
            matrix = personas.Select(p => new { persona = p.id, venues = pack.venues.Select(v => new { venue = v.id, matrix[p.id][v.id].result, matrix[p.id][v.id].rounds, matrix[p.id][v.id].nearDeath, matrix[p.id][v.id].betrayals, matrix[p.id][v.id].rulesAdded }) }),
            checks = checks.Select(c => new { c.ok, c.text }), checkpoints
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));

        int failed = checks.Count(c => !c.ok);
        foreach (var c in checks) Console.WriteLine((c.ok ? "PASS " : "FAIL ") + c.text);
        Console.WriteLine("剧情模拟：" + journeys.Count + " 个人格全程 + " + personas.Count * pack.venues.Count + " 场单会场挑战；检验 " + (checks.Count - failed) + "/" + checks.Count + " 通过；报告 " + Path.Combine(outDir, "模拟报告.md"));
        if (failed == 0) Console.WriteLine("story simulation PASS");
        return failed == 0 ? 0 : 1;
    }

    // ———————————————————— 全程旅程 ————————————————————
    static Journey PlayJourney(WorldParkPack.Pack pack, Persona p)
    {
        var j = new Journey { p = p };
        j.md.Add("# " + p.name + " 的旅程：" + pack.name);
        j.md.Add("");
        j.md.Add("> 人格：" + p.bio + "　策略：" + Strategy(p));
        j.md.Add("");
        j.md.Add(pack.premise);
        var s = WorldParkPack.Start(pack, out string error);
        if (s == null) { failures.Add(p.name + " 无法开始：" + error); j.end = "无法开始"; return j; }
        string lastVenue = "";
        VenueRun current = null;
        for (int guard = 0; guard < 400; guard++)
        {
            var v = s.Venue; var n = s.Current;
            if (v.id != lastVenue)
            {
                lastVenue = v.id;
                current = new VenueRun { venue = v.id, venueName = v.name };
                j.venues.Add(current);
                j.md.Add("");
                j.md.Add("## " + v.name);
                j.md.Add("");
                j.md.Add("*" + v.setting + "　难度 " + v.difficulty + "*");
                j.md.Add("");
                var cue = WorldParkStageCues.Entrance(v);
                j.md.Add("> 【亮灯】" + cue.card + "　" + cue.title + "　" + cue.sub);
                j.md.Add("");
                j.md.Add("**公开规则**：" + v.rules);
            }
            if (n.kind == "story" || n.kind == "reveal" || n.kind == "ending") Checkpoint(pack, s, p);
            if (n.kind == "story" || n.kind == "reveal")
            {
                j.md.Add("");
                var sc = SceneCheck(s);
                j.md.Add("**" + n.name + "**　" + n.text);
                foreach (var tw in sc.twists) j.md.Add("- 【反转】「" + tw.name + "」当时以为：" + tw.before + " → 其实：" + tw.after);
                if (sc.missed.Count > 0) j.md.Add("- 【错过】" + string.Join("、", sc.missed) + "（这次没发现，揭示时只告诉你错过了）");
                if (!s.Continue(out error)) { failures.Add(p.name + " 无法继续：" + error); break; }
            }
            else if (n.kind == "explore") { var run = Explore(s, p, current); j.md.AddRange(run.md); }
            else if (n.kind == "game") { var run = Game(s, p, current); j.md.AddRange(run.md); }
            else if (n.kind == "ending")
            {
                current.result = n.result; current.survived = n.result != "dead";
                j.md.Add("");
                var esc = SceneCheck(s);
                var endCue = WorldParkStageCues.Ending(v, n);
                j.md.Add("> 【" + endCue.title + "】" + endCue.sub);
                j.md.Add("");
                j.md.Add("**" + n.name + "**　" + n.text);
                var shut = esc.doors.Where(d => !d.open).Select(d => d.card + " " + d.name + "（" + d.reason + "）").ToList();
                if (shut.Count > 0) { j.md.Add(""); j.md.Add("> 关着的门：" + string.Join("、", shut)); }
                var options = pack.links.Where(l => l.from == v.id && s.CanTravel(l.to)).Select(l => l.to).Distinct().ToList();
                if (options.Count == 0) { j.end = v.name + " / " + Label(n.result); j.md.Add(""); j.md.Add("**旅程终点**：" + j.end); break; }
                string pick = p.route.FirstOrDefault(options.Contains) ?? options[0];
                j.md.Add("");
                j.md.Add("> 可前往：" + string.Join("、", options.Select(o => VenueName(pack, o))) + "。" + p.name + "选择了 **" + VenueName(pack, pick) + "**。");
                if (!s.Travel(pick, out error)) { failures.Add(p.name + " 无法前往：" + error); break; }
            }
            else { failures.Add(p.name + " 遇到无法模拟的阶段：" + n.kind); break; }
        }
        if (j.end == "") { j.end = "模拟中断"; failures.Add(p.name + " 的旅程没有正常结束"); }
        return j;
    }

    static VenueRun TrialVenue(WorldParkPack.Pack pack, string venueId, Persona p)
    {
        var copy = WorldParkPack.Parse(WorldParkPack.ToJson(pack), out string error);
        copy.entryVenue = venueId;
        var run = new VenueRun { venue = venueId, venueName = VenueName(pack, venueId) };
        var s = WorldParkPack.Start(copy, out error);
        if (s == null) { failures.Add("单会场 " + venueId + " 无法开始：" + error); run.result = "无法开始"; return run; }
        for (int guard = 0; guard < 200; guard++)
        {
            var n = s.Current;
            if (n.kind == "story" || n.kind == "reveal") { SceneCheck(s); if (!s.Continue(out error)) { failures.Add(error); break; } }
            else if (n.kind == "explore") Explore(s, p, run);
            else if (n.kind == "game") Game(s, p, run);
            else if (n.kind == "ending") { SceneCheck(s); run.result = n.result; run.survived = n.result != "dead"; break; }
            else { failures.Add("单会场遇到未知阶段 " + n.kind); break; }
        }
        run.decisive = p.name;
        return run;
    }

    /// <summary>按 Unity 字幕卡的排版规则排一次当前阶段，核对玩家在卡上看到的东西完整且与会话状态一致。</summary>
    static WorldParkSceneModel.Scene SceneCheck(WorldParkPack.Session s)
    {
        sceneCount++;
        var v = s.Venue; var n = s.Current;
        var sc = WorldParkSceneModel.Build(s, null);
        string at = v.name + " / " + (n.name ?? n.id) + "：";
        if (sc.title == "" || sc.card == "") sceneIssues.Add(at + "缺标题或扑克牌");
        if (sc.text.Trim() == "") sceneIssues.Add(at + "正文为空");
        if (sc.kind == "ending")
        {
            if (sc.stamp == "") sceneIssues.Add(at + "结局没有盖章");
            var can = s.pack.links.Where(l => l.from == v.id && s.CanTravel(l.to)).Select(l => l.to).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var open = sc.doors.Where(d => d.open).Select(d => d.venue).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!can.SequenceEqual(open)) sceneIssues.Add(at + "开着的门（" + string.Join(",", open) + "）与连接规则（" + string.Join(",", can) + "）不一致");
            if (sc.doors.Any(d => !d.open && d.reason == "")) sceneIssues.Add(at + "关着的门没有说明原因");
            if (sc.action != "") sceneIssues.Add(at + "结局不应有“继续”主按钮");
        }
        else if (sc.action == "") sceneIssues.Add(at + "没有主按钮");
        if (sc.kind == "reveal")
        {
            int want = v.clues.Count(c => c.revealAt == n.id && s.revealed.Contains(v.id + "/" + c.id));
            if (sc.twists.Count != want) sceneIssues.Add(at + "反转卡 " + sc.twists.Count + " 张，实际揭示 " + want + " 条");
            foreach (var tw in sc.twists)
                if (tw.before.Trim() == "" || tw.after.Trim() == "" || tw.before == tw.after) sceneIssues.Add(at + "反转卡「" + tw.name + "」没有前后对照");
            twistSeen[v.id] = (twistSeen.TryGetValue(v.id, out int k) ? k : 0) + sc.twists.Count;
        }
        int found = v.clues.Count(c => s.found.Contains(v.id + "/" + c.id));
        if (sc.notes.Count != found) sceneIssues.Add(at + "线索本 " + sc.notes.Count + " 条，实际已发现 " + found + " 条");
        if (sc.journey.Count != s.pack.venues.Count) sceneIssues.Add(at + "旅程进度没有列出全部会场");
        double ready = WorldParkSceneModel.ReadyAt(sc);
        if (WorldParkSceneModel.Visible(sc.text, ready + 0.01) != sc.text.Length) sceneIssues.Add(at + "出字结束时正文没有完整显示");
        if (ready > 30) sceneIssues.Add(at + "字幕卡要等 " + ready.ToString("0") + " 秒才能操作（太长，应拆段）");
        return sc;
    }

    // ———————————————————— 探索：真实移动、碰撞、计时 ————————————————————
    static VenueRun Explore(WorldParkPack.Session s, Persona p, VenueRun run)
    {
        var node = s.Current; var venue = s.Venue;
        run.kind = "explore"; run.timeout = node.timeout;
        run.md.Add("");
        run.md.Add("**" + node.name + "**　" + node.text);
        var r = WorldParkExplore.Create(s, out string error);
        if (r == null) { failures.Add("探索无法创建：" + error); return run; }
        var clues = node.points.Where(q => q.kind == "clue").ToList();
        var exits = node.points.Where(q => q.kind == "exit").ToList();
        run.cluesTotal = clues.Count;
        run.hasChasers = r.Chasers.Count > 0;
        var log = new ChaseLog(r, run, p);
        var fromSpawn = Distances(r);
        var nearest = exits.OrderBy(q => Dist(fromSpawn, q)).First();
        run.decoyNearest = nearest.outcome == "lose";
        string thought = p.explore == "rush" ? "规则太长了，先冲最近的门再说" : p.explore == "intuitive" ? "最显眼的门多半是陷阱，我去离我最远的那扇" : "先把能看的都看一遍，再决定走哪扇门";
        if (run.hasChasers)
            thought = (p.explore == "careful" ? "先看公告板和能找到的东西" : "鬼走得没我快，直接冲阵地") + "；" +
                (p.chase == "evade" ? "盯着鬼的视野走，被看见就拐弯或钻草，它面朝这边就停下" : p.chase == "flee" ? "被追就往离它远的地方跑" : "鬼？不用管它");
        run.md.Add("");
        run.md.Add("（" + p.name + "心里想：" + thought + "）");
        if (p.explore == "careful")
        {
            var todo = new List<WorldParkPack.ExplorePoint>(clues);
            while (todo.Count > 0 && !r.Ended)
            {
                var d = Distances(r);
                var next = todo.OrderBy(q => Dist(d, q)).First(); todo.Remove(next);
                if (!Go(r, p, next.x, next.y, log)) break;
                log.Flush();
                if (r.Interact(next.id, out string msg))
                {
                    var c = venue.clues.First(x => x.id == next.clue);
                    run.cluesFound++;
                    run.md.Add("- " + Clock(r) + " 调查「" + next.label + "」：" + c.hint);
                    Read(r, p, c.hint.Length / ReadCharsPerSecond, log);
                }
            }
        }
        IEnumerable<WorldParkPack.ExplorePoint> order;
        if (p.explore == "careful")
        {
            var d = Distances(r);
            order = exits.OrderBy(q => q.requires.Count > 0 && q.requires.All(c => s.found.Contains(venue.id + "/" + c)) ? 0 : 1).ThenBy(q => Dist(d, q));
        }
        else if (p.explore == "intuitive") order = exits.OrderByDescending(q => Dist(fromSpawn, q));
        else order = exits.OrderBy(q => Dist(fromSpawn, q));
        foreach (var e in order.ToList())
        {
            if (r.Ended) break;
            if (!Go(r, p, e.x, e.y, log)) break;
            log.Flush();
            run.exitsTried++;
            bool ok = r.Interact(e.id, out string msg);
            run.md.Add("- " + Clock(r) + " 走到「" + e.label + "」：" + (run.hasChasers && ok ? "碰到了！" : msg));
            if (ok) break;
        }
        while (!r.Ended && node.timeout > 0) r.Tick(1.0, 0, 0);
        log.Flush();
        if (!r.Ended) { failures.Add("探索在没有计时的会场里找不到可用出口：" + node.id); return run; }
        if (!r.Won && r.CaughtBy == "" && r.Remaining <= 0 && node.timeout > 0) run.md.Add("- 时间到。还没来得及做出选择。");
        run.elapsed = r.Elapsed;
        run.caught = r.CaughtBy != "";
        run.seen = r.Events.Count(e => e.type == "seen");
        run.rustles = r.Events.Count(e => e.type == "rustle");
        run.escapes = r.Events.Count(e => e.type == "lost");
        var cue = WorldParkStageCues.Activity(venue, r.Won, r.LastMessage);
        run.md.Add("- " + (cue.kind == "laser" ? "【激光】红光落下：" + cue.title + (cue.sub == "" ? "" : "（" + cue.sub + "）") : "【通过】" + cue.card + " " + cue.title));
        if (!s.ResolveExplore(r.Won, out error)) failures.Add("探索结果无法提交：" + error);
        run.md.Add("- 结果：" + (r.Won ? "活着离开" : run.caught ? "被「" + r.CaughtBy + "」抓住" : "失败") + "（用时 " + r.Elapsed.ToString("0.0") + " 秒" + (node.timeout > 0 ? " / 限时 " + node.timeout + " 秒" : "") + "，线索 " + run.cluesFound + "/" + run.cluesTotal
            + (run.hasChasers ? "，被看见 " + run.seen + " 次，草晃起疑 " + run.rustles + " 次，甩掉 " + run.escapes + " 次" : "") + "）");
        if (traceDir != null && run.hasChasers)
            File.WriteAllLines(Path.Combine(traceDir, "trace_" + p.id + "_" + venue.id + "_" + (++traceCount) + ".txt"), log.trace, new UTF8Encoding(false));
        return run;
    }

    static string Clock(WorldParkExplore.Run r) => "[" + r.Elapsed.ToString("0.0") + "s" + (r.node.timeout > 0 ? "，剩 " + Math.Max(0, r.Remaining).ToString("0") + " 秒" : "") + "]";
    static string traceDir; static int traceCount;

    // ———————————————————— S252 追逐：三种应对策略 ————————————————————
    // ignore：无视鬼，照最短路走（Walk）。flee：只会跑——被追或鬼靠近时往离它最远的相邻格跑，否则走最短路。
    // evade：边躲边找——只用玩家看得见的信息（8 格内、无遮挡的鬼及其朝向；红色扇形在画面上是可见的），
    // 不走进鬼此刻的视野，离记得的鬼远一点；被盯上就去最近的“它看不见、且离它够远”的格子；前路被视野挡住就停下等它转身。
    // 所有移动都按格心到格心真实 Tick，鬼的行为完全由探索内核决定。
    sealed class ChaseLog
    {
        readonly WorldParkExplore.Run r; readonly VenueRun run; readonly Persona p;
        int events, notes; bool countdown; string lastMode = "";
        public readonly List<string> trace = new List<string>(); double lastTrace = -1;
        // 记忆：每个鬼最后一次看见的位置与时间；累计看了多久（看够 1 秒就记住它的巡逻路线：鬼沿固定路线循环走，看一眼方向就能猜到）。
        public readonly Dictionary<WorldParkExplore.Chaser, (double x, double y, double t)> mem = new Dictionary<WorldParkExplore.Chaser, (double x, double y, double t)>();
        public readonly Dictionary<WorldParkExplore.Chaser, double> watched = new Dictionary<WorldParkExplore.Chaser, double>();
        public readonly Dictionary<WorldParkExplore.Chaser, HashSet<(int, int)>> rings = new Dictionary<WorldParkExplore.Chaser, HashSet<(int, int)>>();
        double lastSeenTick = -1;
        public void Observe(List<WorldParkExplore.Chaser> visible)
        {
            double dt = lastSeenTick < 0 ? 0 : Math.Min(0.3, r.Elapsed - lastSeenTick);
            lastSeenTick = r.Elapsed;
            foreach (var c in visible)
            {
                mem[c] = (c.x, c.y, r.Elapsed);
                watched[c] = (watched.TryGetValue(c, out var w) ? w : 0) + dt;
                if (watched[c] >= 1.0 && !rings.ContainsKey(c))
                {
                    rings[c] = PatrolRing(r.map, c);
                    Note("learn" + c.Name, "看了一会儿，记住了「" + c.Name + "」的巡逻路线");
                }
            }
        }
        public ChaseLog(WorldParkExplore.Run run0, VenueRun venueRun, Persona persona) { r = run0; run = venueRun; p = persona; }
        public void Trace(string what)
        {
            if (traceDir == null || r.Elapsed - lastTrace < 0.2) return;
            lastTrace = r.Elapsed;
            trace.Add("t=" + r.Elapsed.ToString("0.0") + " P(" + r.x.ToString("0.0") + "," + r.y.ToString("0.0") + ") " + what + " | " +
                string.Join(" | ", r.Chasers.Select(c => c.Name + "(" + c.x.ToString("0.0") + "," + c.y.ToString("0.0") + ") " + c.mode + (c.mode == "search" ? "→(" + c.lastX.ToString("0.00") + "," + c.lastY.ToString("0.00") + ")" : "") + " f(" + c.fx.ToString("0.0") + "," + c.fy.ToString("0.0") + ")" + (r.PlayerSees(c) ? " 可见" : ""))));
        }
        public void Flush()
        {
            var items = new List<(double t, string text)>();
            for (; events < r.Events.Count; events++)
            {
                var e = r.Events[events];
                string sound = WorldParkStageCues.ChaseSound(e);
                items.Add((e.time, (sound == "alarm" ? "【警报】" : sound == "rustle" ? "【沙沙】" : "") + WorldParkStageCues.ChaseLine(e)));
            }
            if (!countdown && r.node.timeout > 0 && r.Remaining <= WorldParkStageCues.CountdownFrom)
            {
                countdown = true;
                items.Add((Math.Max(0, r.node.timeout - WorldParkStageCues.CountdownFrom), "【倒计时】最后 " + WorldParkStageCues.CountdownFrom + " 秒：每秒一声滴，剩 " + WorldParkStageCues.CountdownUrgent + " 秒时数字变红"));
            }
            foreach (var it in items.OrderBy(x => x.t)) run.md.Add("- [" + it.t.ToString("0.0") + "s] " + it.text);
        }
        public void Note(string mode, string text)
        {
            if (mode == lastMode || (mode == "go" && lastMode == "")) { if (mode == "go") lastMode = mode; return; }
            lastMode = mode;
            if (notes >= 18) return;
            notes++;
            Flush();
            run.md.Add("- [" + r.Elapsed.ToString("0.0") + "s] （" + p.name + "：" + text + "）");
        }
    }

    static bool Go(WorldParkExplore.Run r, Persona p, int gx, int gy, ChaseLog log)
    {
        if (r.Chasers.Count == 0 || p.chase == "ignore") return Walk(r, gx, gy);
        for (int guard = 0; guard < 6000; guard++)
        {
            if (r.Ended) return false;
            int cx = (int)Math.Floor(r.x), cy = (int)Math.Floor(r.y);
            if (cx == gx && cy == gy) return true;
            var visible = r.Chasers.Where(r.PlayerSees).ToList();
            if (p.chase == "evade") log.Observe(visible);
            OverworldMap.Cell? step = p.chase == "flee" ? FleeStep(r, visible, gx, gy, log) : EvadeStep(r, visible, log, gx, gy);
            log.Trace("→" + (step == null ? "等" : step.Value.x + "," + step.Value.y) + " 目标" + gx + "," + gy);
            if (step == null) { r.Tick(0.1, 0, 0); continue; }
            if (!StepTo(r, step.Value)) return false;
        }
        failures.Add("追逐策略没有在限定步数内走到 " + gx + "," + gy);
        return false;
    }

    /// <summary>鬼的巡逻圈：出生点→巡逻点1→巡逻点2→…→回到巡逻点1，按地形代价寻路（和内核里鬼巡逻用的同一个 Path）。</summary>
    static HashSet<(int, int)> PatrolRing(OverworldMap.Map map, WorldParkExplore.Chaser c)
    {
        var ring = new HashSet<(int, int)>();
        var pts = new List<OverworldMap.Cell> { new OverworldMap.Cell(c.point.x, c.point.y) };
        foreach (var s in c.point.route) { var a = s.Split(','); pts.Add(new OverworldMap.Cell(int.Parse(a[0]), int.Parse(a[1]))); }
        if (pts.Count > 2) pts.Add(pts[1]);
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var path = OverworldMap.Path(map, pts[i], pts[i + 1]);
            if (path != null) foreach (var q in path) ring.Add((q.x, q.y));
        }
        return ring;
    }

    static void Read(WorldParkExplore.Run r, Persona p, double seconds, ChaseLog log)
    {
        while (seconds > 0 && !r.Ended)
        {
            if (r.Chasers.Count > 0 && p.chase != "ignore")
            {
                int cx = (int)Math.Floor(r.x), cy = (int)Math.Floor(r.y);
                var near = r.Chasers.Where(r.PlayerSees).FirstOrDefault(c => c.mode == "chase" || (p.chase == "evade" && (Watched(r.map, c, cx, cy, false) || D(c.x, c.y, r.x, r.y) < 4)));
                if (near != null) { log.Note("cut", "读到一半，「" + near.Name + "」过来了——先走"); return; }
            }
            double step = Math.Min(0.1, seconds); r.Tick(step, 0, 0); seconds -= step;
        }
    }

    static double D(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    static readonly int[] SX = { 1, 0, -1, 0 }, SY = { 0, 1, 0, -1 };

    /// <summary>鬼此刻会不会看见站在（或走过）这一格的人：2 格内一律算危险；草里静止不算；否则按视野放宽一点（多 1 格、多 15°）留余量。</summary>
    static bool Watched(OverworldMap.Map map, WorldParkExplore.Chaser c, int x, int y, bool moving)
    {
        double tx = x + 0.5, ty = y + 0.5, dx = tx - c.x, dy = ty - c.y, d = Math.Sqrt(dx * dx + dy * dy);
        bool still = !moving && OverworldCatalog.Hides(map.At(x, y));
        if (d <= (still ? 1.8 : 2.0)) return true;
        if (still) return false;
        if (d > c.Sight + 1.0) return false;
        if ((dx * c.fx + dy * c.fy) / d < Math.Cos(75 * Math.PI / 180)) return false;
        return OverworldMap.LineOfSight(map, c.x, c.y, tx, ty);
    }

    static bool StepTo(WorldParkExplore.Run r, OverworldMap.Cell c)
    {
        int budget = 400;
        while (Math.Abs(r.x - c.x - 0.5) > 0.025 || Math.Abs(r.y - c.y - 0.5) > 0.025)
        {
            if (r.Ended) return false;
            if (--budget == 0) { failures.Add("真实碰撞卡住 " + c.x + "," + c.y); return false; }
            double h = c.x + 0.5 - r.x, v = c.y + 0.5 - r.y, dist = Math.Sqrt(h * h + v * v);
            r.Tick(Math.Min(0.025, dist / 4), h / dist, v / dist);
        }
        return !r.Ended;
    }

    /// <summary>格子 Dijkstra：blocked 的格子不能进（起点除外），每进一格花 1 + cost。返回每格的花费与前驱。</summary>
    static (double[,] dist, (int, int)[,] prev) Dijkstra(OverworldMap.Map map, int sx, int sy, Func<int, int, bool> blocked, Func<int, int, double> cost, double limit)
    {
        var dist = new double[map.W, map.H]; var prev = new (int, int)[map.W, map.H];
        for (int x = 0; x < map.W; x++) for (int y = 0; y < map.H; y++) { dist[x, y] = double.MaxValue; prev[x, y] = (-1, -1); }
        dist[sx, sy] = 0;
        var open = new SortedSet<(double d, int x, int y)> { (0, sx, sy) };
        while (open.Count > 0)
        {
            var cur = open.Min; open.Remove(cur);
            if (cur.d > dist[cur.x, cur.y] || cur.d > limit) continue;
            for (int k = 0; k < 4; k++)
            {
                int nx = cur.x + SX[k], ny = cur.y + SY[k];
                if (!OverworldMap.Walkable(map, nx, ny) || (blocked != null && blocked(nx, ny))) continue;
                double nd = cur.d + 1 + (cost == null ? 0 : cost(nx, ny));
                if (nd < dist[nx, ny]) { dist[nx, ny] = nd; prev[nx, ny] = (cur.x, cur.y); open.Add((nd, nx, ny)); }
            }
        }
        return (dist, prev);
    }

    static OverworldMap.Cell? FirstStep((int, int)[,] prev, int sx, int sy, int tx, int ty)
    {
        var at = (tx, ty);
        if (prev[tx, ty].Item1 < 0) return null;
        while (prev[at.Item1, at.Item2] != (sx, sy)) { at = prev[at.Item1, at.Item2]; if (at.Item1 < 0) return null; }
        return new OverworldMap.Cell(at.Item1, at.Item2);
    }

    static OverworldMap.Cell? FleeStep(WorldParkExplore.Run r, List<WorldParkExplore.Chaser> visible, int gx, int gy, ChaseLog log)
    {
        int cx = (int)Math.Floor(r.x), cy = (int)Math.Floor(r.y);
        var threat = visible.Where(c => c.mode == "chase" || D(c.x, c.y, r.x, r.y) < 3).OrderBy(c => D(c.x, c.y, r.x, r.y)).FirstOrDefault();
        if (threat == null)
        {
            log.Note("go", "看不到鬼了，接着往目标走");
            var (dist, prev) = Dijkstra(r.map, cx, cy, null, null, double.MaxValue);
            return FirstStep(prev, cx, cy, gx, gy);
        }
        log.Note("flee", "「" + threat.Name + "」追过来了！往离它最远的方向跑");
        OverworldMap.Cell? best = null; double bestD = D(cx + 0.5, cy + 0.5, threat.x, threat.y);
        for (int k = 0; k < 4; k++)
        {
            int nx = cx + SX[k], ny = cy + SY[k];
            if (!OverworldMap.Walkable(r.map, nx, ny)) continue;
            double d = D(nx + 0.5, ny + 0.5, threat.x, threat.y);
            if (d > bestD + 1e-9) { bestD = d; best = new OverworldMap.Cell(nx, ny); }
        }
        if (best == null) log.Note("corner", "无路可退了");
        return best;
    }

    static OverworldMap.Cell? EvadeStep(WorldParkExplore.Run r, List<WorldParkExplore.Chaser> visible, ChaseLog log, int gx, int gy)
    {
        var map = r.map;
        int cx = (int)Math.Floor(r.x), cy = (int)Math.Floor(r.y);
        // 记得的鬼（此刻看得见，或 6 秒内见过）：它附近的格子更“贵”，让路线离它远一点；记住了巡逻路线的鬼，巡逻圈附近也更“贵”。
        var soft = new double[map.W, map.H];
        foreach (var kv in log.mem)
        {
            if (r.Elapsed - kv.Value.t > 6) continue;
            var (near, _) = Dijkstra(map, (int)Math.Floor(kv.Value.x), (int)Math.Floor(kv.Value.y), null, null, 7);
            for (int x = 0; x < map.W; x++) for (int y = 0; y < map.H; y++) if (near[x, y] <= 7) soft[x, y] += (8 - near[x, y]) * 1.5;
        }
        foreach (var ring in log.rings.Values)
            for (int x = 0; x < map.W; x++) for (int y = 0; y < map.H; y++)
            {
                int best = int.MaxValue;
                foreach (var q in ring) best = Math.Min(best, Math.Max(Math.Abs(q.Item1 - x), Math.Abs(q.Item2 - y)));
                if (best <= 2) soft[x, y] += best == 0 ? 6 : best == 1 ? 4 : 2;
            }
        var hunter = visible.FirstOrDefault(c => c.mode == "chase");
        var watcher = visible.FirstOrDefault(c => Watched(map, c, cx, cy, false));
        // 阵地/线索就在隔壁、而且鬼没贴着它：冲过去。
        if (Math.Abs(cx - gx) + Math.Abs(cy - gy) == 1 && !visible.Any(c => D(gx + 0.5, gy + 0.5, c.x, c.y) < 1.3)) return new OverworldMap.Cell(gx, gy);
        if (hunter != null || watcher != null)
        {
            var who = hunter ?? watcher;
            var (dist, prev) = Dijkstra(map, cx, cy, (x, y) => visible.Any(c => D(x + 0.5, y + 0.5, c.x, c.y) < 1.6), (x, y) => soft[x, y] + (OverworldCatalog.Hides(map.At(x, y)) && visible.Any(c => Watched(map, c, x, y, true)) ? 4 : 0), 60);
            OverworldMap.Cell? best = null; double score = double.MaxValue;
            for (int x = 0; x < map.W; x++) for (int y = 0; y < map.H; y++)
            {
                if (dist[x, y] == double.MaxValue || (x == cx && y == cy)) continue;
                bool grass = OverworldCatalog.Hides(map.At(x, y));
                bool safe = visible.All(c =>
                {
                    double d = D(x + 0.5, y + 0.5, c.x, c.y);
                    return d >= 3.5 && (grass || !OverworldMap.LineOfSight(map, c.x, c.y, x + 0.5, y + 0.5) || d > c.Sight + 1.5);
                });
                if (!safe) continue;
                double s = dist[x, y] + 0.15 * (Math.Abs(x - gx) + Math.Abs(y - gy));
                if (s < score) { score = s; best = new OverworldMap.Cell(x, y); }
            }
            if (best == null) { log.Note("stuck", "「" + who.Name + "」就在旁边，没有地方躲"); return null; }
            log.Note(hunter != null ? "hide" : "dodge", hunter != null ? "被「" + who.Name + "」盯上了，拐过墙角、钻进草里甩开它" : "「" + who.Name + "」的视野扫过来了，先退到它看不见的地方");
            return FirstStep(prev, cx, cy, best.Value.x, best.Value.y);
        }
        var plan = Dijkstra(map, cx, cy, (x, y) => (x != gx || y != gy || true) && visible.Any(c => Watched(map, c, x, y, true)), (x, y) => soft[x, y], double.MaxValue);
        var next = FirstStep(plan.prev, cx, cy, gx, gy);
        if (next != null) { log.Note("go", visible.Count > 0 ? "「" + visible[0].Name + "」没朝这边看，绕开它的视野走" : "附近没有鬼，走"); return next; }
        var block = visible.FirstOrDefault();
        log.Note("wait", OverworldCatalog.Hides(map.At(cx, cy)) ? "躲在高草里一动不动，等「" + (block == null ? "鬼" : block.Name) + "」走过去" : "前面在「" + (block == null ? "鬼" : block.Name) + "」的视野里，停下来等它转身");
        return null;
    }
    static Dictionary<(int, int), int> Distances(WorldParkExplore.Run r)
    {
        var from = ((int)Math.Floor(r.x), (int)Math.Floor(r.y));
        var dist = new Dictionary<(int, int), int> { [from] = 0 };
        var q = new Queue<(int, int)>(); q.Enqueue(from);
        int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                var n = (c.Item1 + dx[i], c.Item2 + dy[i]);
                if (!dist.ContainsKey(n) && OverworldMap.Walkable(r.map, n.Item1, n.Item2)) { dist[n] = dist[c] + 1; q.Enqueue(n); }
            }
        }
        return dist;
    }
    static int Dist(Dictionary<(int, int), int> d, WorldParkPack.ExplorePoint q) => d.TryGetValue((q.x, q.y), out int v) ? v : int.MaxValue / 2;

    /// <summary>沿最短路逐格真实 Tick；超时（Ended）就停下并返回 false。</summary>
    static bool Walk(WorldParkExplore.Run r, int x, int y)
    {
        var from = new OverworldMap.Cell((int)Math.Floor(r.x), (int)Math.Floor(r.y)); var to = new OverworldMap.Cell(x, y);
        var queue = new Queue<OverworldMap.Cell>(); var prev = new Dictionary<OverworldMap.Cell, OverworldMap.Cell>(); queue.Enqueue(from); prev[from] = from;
        int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
        while (queue.Count > 0 && !prev.ContainsKey(to)) { var c = queue.Dequeue(); for (int i = 0; i < 4; i++) { var n = new OverworldMap.Cell(c.x + dx[i], c.y + dy[i]); if (OverworldMap.Walkable(r.map, n.x, n.y) && !prev.ContainsKey(n)) { prev[n] = c; queue.Enqueue(n); } } }
        if (!prev.ContainsKey(to)) { failures.Add("现场点不可达：" + x + "," + y); return false; }
        var path = new List<OverworldMap.Cell>(); for (var c = to; !c.Equals(from); c = prev[c]) path.Add(c); path.Reverse();
        path.Add(to);
        foreach (var c in path)
        {
            int budget = 400;
            while (Math.Abs(r.x - c.x - 0.5) > 0.025 || Math.Abs(r.y - c.y - 0.5) > 0.025)
            {
                if (r.Ended) return false;
                if (--budget == 0) { failures.Add("真实碰撞卡住 " + c.x + "," + c.y); return false; }
                double h = c.x + 0.5 - r.x, v = c.y + 0.5 - r.y, dist = Math.Sqrt(h * h + v * v);
                r.Tick(Math.Min(0.025, dist / 4), h / dist, v / dist);
            }
        }
        return !r.Ended;
    }

    // ———————————————————— 博弈：只看公开信息逐轮决定 ————————————————————
    static VenueRun Game(WorldParkPack.Session s, Persona p, VenueRun run)
    {
        var node = s.Current; var def = node.game;
        run.kind = "game"; run.startLives = def.lives;
        run.md.Add("");
        run.md.Add("**" + node.name + "**　" + node.text);
        var choices = new List<string>(); var thoughts = new List<string>();
        var tableLines = new List<string>();
        BorderlandGame.State st = null;
        for (int i = 0; i < 25; i++)
        {
            st = BorderlandGame.Run(def, choices);
            if (st.error != "") { failures.Add("博弈出错：" + st.error); return run; }
            if (st.finished) break;
            TableChoose(def, st, node.name);
            string why; string pick = Decide(def, st, p, out why);
            var after = BorderlandGame.Run(def, new List<string>(choices) { pick });
            if (after.error == "") tableLines.Add(TableReveal(def, st, after, node.name));
            choices.Add(pick); thoughts.Add(why + " → 选「" + pick + "」");
        }
        if (st == null || !st.finished) { failures.Add("博弈没有在 25 轮内结束"); return run; }
        if (!s.ResolveGame(choices, out string error)) { failures.Add("博弈结果无法提交：" + error); return run; }
        int round = 0, tableShown = 0;
        Action<int> flush = upTo => { while (tableShown < upTo && tableShown < tableLines.Count) run.md.Add("- " + tableLines[tableShown++]); };
        foreach (string line in st.transcript)
        {
            var m = Regex.Match(line, "^—— 第(\\d+)轮 ——$");
            if (m.Success)
            {
                round = int.Parse(m.Groups[1].Value);
                flush(round - 1);
                run.md.Add(""); run.md.Add("**第 " + round + " 轮**");
                continue;
            }
            if (line.StartsWith("【通关】") || line.StartsWith("【失败】")) flush(round);
            if (def.mode == "mark" && round > 0 && line.StartsWith("你宣告") && round - 1 < thoughts.Count) run.md.Add("- （" + p.name + "心里想：" + thoughts[round - 1] + "）");
            run.md.Add("- " + line);
            if (round > 0 && IsLastPending(def, line) && round - 1 < thoughts.Count) run.md.Add("- （" + p.name + "心里想：" + thoughts[round - 1] + "）");
        }
        flush(tableLines.Count);
        // 统计
        run.rounds = st.round;
        foreach (var rec in st.records)
        {
            run.minLives = Math.Min(run.minLives, rec.livesAfter);
            if (rec.livesAfter == 1 && def.lives > 1 && rec.livesBefore > 1) run.nearDeath++;
            run.othersOut += rec.eliminated.Count(x => x != "你");
        }
        run.rulesAdded = st.transcript.Count(l => l.StartsWith("【新规则】"));
        run.rulesBit = st.transcript.Count(l => l.StartsWith("重复数字无效：") || l.StartsWith("有人正好命中") || l.StartsWith("0 对 100"));
        run.liesExposed = st.transcript.Count(l => l.StartsWith("公开记录："));
        if (def.mode == "mark")
        {
            var truths = st.transcript.Where(l => l.StartsWith("你宣告")).Select(l => Regex.Match(l, "真实是(.)").Groups[1].Value).ToList();
            for (int i = 0; i < st.records.Count && i < truths.Count; i++) if (st.records[i].informedAnswer != truths[i]) run.informedFair = false;
        }
        if (def.mode == "split")
        {
            string promise = "";
            foreach (string l in st.transcript)
            {
                if (l.StartsWith("本轮对手：")) promise = l;
                var m = Regex.Match(l, "^你选(.)，(.+)选(.)$");
                if (m.Success && m.Groups[3].Value == "偷" && promise.Contains("选分")) run.betrayals++;
            }
        }
        run.decisive = p.name;
        return run;
    }

    static bool IsLastPending(BorderlandGame.Def def, string line)
    {
        if (def.mode == "beauty") return line.StartsWith("追加规则：");
        if (def.mode == "split") return line.StartsWith("你：") && line.Contains("目标：");
        return false; // mark：在最后一条证词后插入，见下方专门处理
    }

    /// <summary>出手前的桌面：你坐正下方、座位不重叠、别人对你说的话挂在说话人座位上、分偷的对手被聚光。</summary>
    static void TableChoose(BorderlandGame.Def def, BorderlandGame.State st, string where)
    {
        var v = WorldParkTableModel.Build(def, null, st, -1);
        string at = where + " 第" + (st.round + 1) + "轮出手前：";
        if (v.phase != "choose" || !v.inputOpen) tableIssues.Add(at + "桌面没有处于出手阶段（" + v.phase + "）");
        if (v.seats.Count != def.seats.Count) tableIssues.Add(at + "座位数与参与者不一致");
        var me = v.seats.FirstOrDefault(x => x.me);
        if (me == null || v.seats.Any(x => !x.me && x.y >= me.y)) tableIssues.Add(at + "你没有坐在桌子正下方");
        for (int a = 0; a < v.seats.Count; a++)
            for (int b = a + 1; b < v.seats.Count; b++)
                if (Math.Sqrt(Math.Pow(v.seats[a].x - v.seats[b].x, 2) + Math.Pow(v.seats[a].y - v.seats[b].y, 2)) < 0.1) tableIssues.Add(at + v.seats[a].name + " 与 " + v.seats[b].name + " 座位挤在一起");
        if (def.mode == "mark" && v.seats.Any(x => x.alive && !x.me && x.bubble == "")) tableIssues.Add(at + "有存活者对你说的标记没有挂到他的座位上");
        if (def.mode == "split" && (v.seats.Count(x => x.spotlight) != 1 || v.seats.First(x => x.spotlight).bubble == "")) tableIssues.Add(at + "本轮对手没有被聚光，或没有显示对方说的话");
        if (def.mode == "split" && v.seats.Count(x => x.active) != 2) tableIssues.Add(at + "分偷出手前应只有你和本轮对手在桌上");
        if (v.prompt == "") tableIssues.Add(at + "没有出手提示");
    }

    /// <summary>出手后的桌面：先全部扣着、再逐个翻开，结算时每张牌、扣命、出局、新规则都与规则记录一致；演完回到下一轮或结束。</summary>
    static string TableReveal(BorderlandGame.Def def, BorderlandGame.State before, BorderlandGame.State after, string where)
    {
        tableRounds++;
        var rec = after.records[after.records.Count - 1];
        string at = where + " 第" + rec.round + "轮亮牌：";
        double len = WorldParkTableModel.RevealLength(rec, after.player);
        var early = WorldParkTableModel.Build(def, before, after, WorldParkTableModel.FlipStart - 0.01);
        if (early.phase != "reveal" || early.flipped != 0) tableIssues.Add(at + "第一张牌该翻开之前就已经亮牌");
        if (early.inputOpen) tableIssues.Add(at + "亮牌过程中仍可出手");
        if (early.seats.Any(x => x.lost > 0 || x.eliminatedNow)) tableIssues.Add(at + "牌还没翻就显示了扣命/出局");
        var res = WorldParkTableModel.Build(def, before, after, len - 0.01);
        if (res.phase != "result") tableIssues.Add(at + "结算阶段错位（" + res.phase + "）");
        for (int i = 0; i < res.seats.Count; i++)
        {
            var x = res.seats[i];
            string want = i < rec.picks.Length ? rec.picks[i] : "";
            if (x.pick != want) tableIssues.Add(at + x.name + " 桌上显示「" + x.pick + "」，记录是「" + want + "」");
            if (x.active && !x.revealed) tableIssues.Add(at + x.name + " 的牌没有翻开");
            int lost = Math.Max(0, Math.Max(0, before.lives[i]) - Math.Max(0, after.lives[i]));
            if (x.lost != lost) tableIssues.Add(at + x.name + " 桌上显示失去 " + x.lost + " 命，实际 " + lost);
            if (x.eliminatedNow != (before.alive[i] && !after.alive[i])) tableIssues.Add(at + x.name + " 的出局标记与规则不一致");
            if (def.mode == "mark" && x.active && x.truth != rec.marks[i]) tableIssues.Add(at + x.name + " 的真实标记显示错误");
        }
        if (res.flipped != res.seats.Count(x => x.active && !x.me)) tableIssues.Add(at + "翻开的牌数与参加者不一致");
        if (res.centerBig == "" || res.centerSmall == "") tableIssues.Add(at + "没有结算大字");
        if (def.mode == "split" && res.seats.Count(x => x.active) != 2) tableIssues.Add(at + "分偷应只亮出你和本轮对手两张牌");
        string newRule = after.transcript.Skip(before.transcript.Count).FirstOrDefault(l => l.StartsWith("【新规则】")) ?? "";
        if (res.newRule != newRule) tableIssues.Add(at + "新规则宣布与记录不一致");
        var next = WorldParkTableModel.Build(def, before, after, len + 0.01);
        if (after.finished ? next.phase != "done" || next.finishLine == "" : next.phase != "choose") tableIssues.Add(at + "亮牌结束后没有回到下一轮/结束画面（" + next.phase + "）");
        var parts = res.seats.Where(x => x.active).Select(x => x.name + " " + x.pick
            + (x.truth != "" ? "（真实" + x.truth + (x.wrong ? "，错" : "") + "）" : "")
            + (x.winner ? "★" : "") + (x.lost > 0 ? " −" + x.lost + "命" : "") + (x.gain > 0 ? " +" + x.gain + "分" : "") + (x.eliminatedNow ? " 出局" : ""));
        return "【桌面】" + string.Join(" · ", parts) + " → " + res.centerBig + "（" + res.centerSmall + "）" + (res.newRule != "" ? "　" + res.newRule : "");
    }

    static string Decide(BorderlandGame.Def def, BorderlandGame.State st, Persona p, out string why)
    {
        if (def.mode == "beauty") return Beauty(def, st, p.beauty, out why);
        if (def.mode == "mark") return Mark(st, p.mark, out why);
        return Split(st, p.split, out why);
    }

    static string Beauty(BorderlandGame.Def def, BorderlandGame.State st, string style, out string why)
    {
        int prev = -1; var lastPicks = new List<int>();
        foreach (string l in st.transcript)
        {
            var m = Regex.Match(l, "^平均×\\d+% ≈ (\\d+)$"); if (m.Success) prev = int.Parse(m.Groups[1].Value);
            if (l.Contains("选") && Regex.IsMatch(l, "选\\d+")) lastPicks = Regex.Matches(l, "(?<=选)\\d+").Select(x => int.Parse(x.Value)).ToList();
        }
        bool dup = st.pending.Any(l => l.Contains("同数字无效"));
        bool duel = Regex.Matches(st.pending[0], "命\\)").Count == 2;
        bool zero = duel && st.pending.Any(l => l.Contains("选100者获胜"));
        var otherPicks = Regex.Matches(Regex.Replace(st.transcript.LastOrDefault(l => Regex.IsMatch(l, "选\\d+")) ?? "", "^你选\\d+，?", ""), "(?<=选)\\d+").Select(x => int.Parse(x.Value)).ToList();
        int R = def.ratio;
        if (style == "naive") { why = "平均大概是一半吧"; return "50"; }
        if (style == "reasoner") { why = "大家会写 50 左右，打八折"; return (50 * R / 100).ToString(); }
        if (style == "adaptive") { why = prev < 0 ? "第一轮先保守一点" : "跟着上一轮的目标 " + prev + " 走"; return (prev < 0 ? 35 : prev).ToString(); }
        int guess = prev < 0 ? 50 * R * R / 10000 - 1 : prev * R / 100;
        why = prev < 0 ? "别人会想到打八折，我再往下推一层" : "目标会继续往下走：上一轮 " + prev + "，这一轮再打八折";
        if (zero && (otherPicks.Count == 0 || otherPicks.Min() <= 10)) { why = "只剩两个人，更小的数一定赢，对手会冲向 0；新规则下写 100 的人赢"; return "100"; }
        if (duel) { why = "只剩两个人：平均打八折后，更小的数一定更接近目标，写 0"; return "0"; }
        if (dup && lastPicks.Contains(guess)) { guess = Math.Max(0, guess - 1); why += "；规则改了，避开别人可能写的数"; }
        return guess.ToString();
    }

    static string Mark(BorderlandGame.State st, string style, out string why)
    {
        var claims = st.pending.Select(l => Regex.Match(l, "^(.+)说你的标记是(.)$")).Where(m => m.Success).Select(m => (who: m.Groups[1].Value, suit: m.Groups[2].Value)).ToList();
        var liars = new HashSet<string>(st.transcript.Select(l => Regex.Match(l, "^公开记录：(.+?)对")).Where(m => m.Success).Select(m => m.Groups[1].Value));
        if (style == "first") { why = claims[0].who + "第一个开口，看起来很真诚"; return claims[0].suit; }
        var pool = style == "informed" ? claims.Where(c => !liars.Contains(c.who)).ToList() : claims;
        if (pool.Count == 0) pool = claims;
        var best = pool.GroupBy(c => c.suit).OrderByDescending(g => g.Count()).ThenBy(g => pool.FindIndex(c => c.suit == g.Key)).First();
        why = style == "informed" ? (liars.Count > 0 ? "公开记录里撒过谎的" + string.Join("、", liars) + "不算，剩下的人多数说" + best.Key : "还没人被拆穿，听多数") : "听多数人的";
        return best.Key;
    }

    static string Split(BorderlandGame.State st, string style, out string why)
    {
        string opp = Regex.Match(st.pending.First(l => l.StartsWith("本轮对手：")), "^本轮对手：(.+?)　").Groups[1].Value;
        bool last = st.pending.Any(l => l.Contains("（最后一轮）"));
        string lastMove = ""; bool stoleFromMe = false;
        foreach (string l in st.transcript)
        {
            var m = Regex.Match(l, "^你选(.)，(.+)选(.)$");
            if (m.Success && m.Groups[2].Value == opp) { lastMove = m.Groups[3].Value; if (lastMove == "偷" && m.Groups[1].Value == "分") stoleFromMe = true; }
        }
        if (style == "trust") { why = opp + "说会选分，那我也选分"; return "分"; }
        if (style == "greedy") { why = "偷一次就是 2 分"; return "偷"; }
        if (style == "deep")
        {
            if (last) { why = "最后一轮没有明天，谁都可能背叛"; return "偷"; }
            if (stoleFromMe) { why = opp + "偷过我，承诺不值钱"; return "偷"; }
        }
        if (lastMove == "") { why = "第一次见" + opp + "，先给一次信任"; return "分"; }
        why = opp + "上次选了" + lastMove + "，我照着还给它"; return lastMove;
    }

    // ———————————————————— 检查点 ————————————————————
    static void Checkpoint(WorldParkPack.Pack pack, WorldParkPack.Session s, Persona p)
    {
        checkpoints++;
        string saved = WorldParkProgress.Save(s, out string error);
        var back = saved == null ? null : WorldParkProgress.Restore(pack, saved, out error);
        bool same = back != null && back.venueId == s.venueId && back.nodeId == s.nodeId && back.found.SetEquals(s.found) && back.revealed.SetEquals(s.revealed)
            && back.activityResults.Count == s.activityResults.Count && back.results.Count == s.results.Count && back.results.All(kv => s.results.TryGetValue(kv.Key, out var v) && v == kv.Value);
        if (!same) { checkpointFailures++; failures.Add(p.name + " 在 " + s.venueId + "/" + s.nodeId + " 存档恢复不一致：" + error); }
    }

    // ———————————————————— 文字工具 ————————————————————
    static string Cell(VenueRun r)
    {
        string head = Label(r.result);
        if (r.kind == "game") return head + " · " + r.rounds + "轮/最低" + Math.Max(0, r.minLives == int.MaxValue ? 0 : r.minLives) + "命" + (r.nearDeath > 0 ? "/濒死" + r.nearDeath : "") + (r.betrayals > 0 ? "/背叛" + r.betrayals : "") + (r.rulesAdded > 0 ? "/新规" + r.rulesAdded : "") + (r.rulesBit > 0 ? "/规则生效" + r.rulesBit : "") + (r.othersOut > 0 ? "/他人出局" + r.othersOut : "");
        if (r.kind == "explore") return head + " · " + r.elapsed.ToString("0") + "s/线索" + r.cluesFound + "/" + r.cluesTotal + "/试" + r.exitsTried + "门" + (r.hasChasers ? "/被看见" + r.seen + (r.caught ? "/被抓" : "") : "");
        return head;
    }
    static string Label(string result) => result == "dead" ? "出局" : result == "clear" ? "通关" : result == "leave" ? "离开" : result == "stay" ? "留下" : result == "" ? "未结束" : result;
    static string Short(string name) => name.Length > 8 ? name.Substring(0, 8) : name;
    static string Strategy(Persona p) => "探索" + Map(p.explore) + "·猜心" + Map(p.beauty) + "·标记" + Map(p.mark) + "·分偷" + Map(p.split);
    static string Map(string k) => k switch { "rush" => "冲最近", "intuitive" => "走最远", "careful" => "先调查", "naive" => "直觉50", "reasoner" => "推一层", "deep" => "深推", "adaptive" => "跟目标", "first" => "信第一个", "majority" => "听多数", "informed" => "查记录", "trust" => "全信", "greedy" => "全偷", "mirror" => "以牙还牙", _ => k };
    static string Name(List<Persona> ps, string id) => ps.FirstOrDefault(x => x.id == id)?.name ?? id;
    static string VenueName(WorldParkPack.Pack pack, string id) => WorldParkPack.GetVenue(pack, id)?.name ?? id;
    static string FindRepo()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null && !(Directory.Exists(Path.Combine(d.FullName, "Assets")) && Directory.Exists(Path.Combine(d.FullName, "Tools")))) d = d.Parent;
        if (d == null) throw new Exception("请在仓库根目录运行");
        return d.FullName;
    }
}
