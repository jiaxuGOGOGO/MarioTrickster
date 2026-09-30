using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// S210：星露谷视角小镇（大地图）。守住：样板能玩且无人捣乱时一天走得完（H10）、视线规则（高草/遮挡/夜晚/路灯，H4）、
/// 起疑先 '?' 后 '!'（H2）、追/找都有时限（H9）、门口三种结果、关卡包导入导出、房间场景接回小镇的接线。需在 Unity Test Runner (EditMode) 实跑。
/// </summary>
public class OverworldTests
{
    static MarioMindTuningSO Tuning() => ScriptableObject.CreateInstance<MarioMindTuningSO>();
    static string Read(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative)).Replace("\r\n", "\n");
    static OverworldMap.Map Sample() => OverworldPack.Parse(OverworldPack.SampleText)[0];

    [Test]
    public void SampleTown_IsPlayable_AndDayCompletes()
    {
        var m = Sample();
        var rep = OverworldMap.Check(m, OverworldMap.Rules.Default);
        Assert.IsTrue(rep.Playable, string.Join("\n", rep.issues));
        Assert.AreEqual(4, rep.schedule.stops.Count);
        var day = OverworldWalker.SimulateDay(m, OverworldMap.Rules.Default);
        Assert.IsTrue(day.ok, day.summary);
        Assert.Less(day.homeMinute, OverworldMap.DayEnd);
        for (int k = 0; k < rep.schedule.stops.Count; k++) Assert.Greater(OverworldMap.AmbushLead(rep.schedule, k, 4), 5, "每扇门你都来得及埋伏");
    }

    [Test]
    public void TextAndJson_RoundTrip()
    {
        var m = Sample();
        Assert.AreEqual(OverworldMap.ToText(m), OverworldMap.ToText(OverworldMap.Parse(OverworldMap.ToText(m))));
        var pk = OverworldPack.Parse("{\"levels\":[],\"overworlds\":[" + OverworldMap.ToJson(m) + "]}");
        Assert.AreEqual(1, pk.Count);
        Assert.AreEqual(OverworldMap.ToText(m), OverworldMap.ToText(pk[0]));
        Assert.IsNull(LevelPack.Parse("{\"levels\":[" + OverworldMap.ToJson(m) + "]}", c => true, out _), "横版关卡包不把小镇当房间");
    }

    [Test]
    public void Check_CatchesBrokenTowns()
    {
        var m = OverworldMap.Parse("# Overworld: x\n" + string.Join("\n", OverworldMap.NewMap(20, 12)));
        Assert.IsFalse(OverworldMap.Check(m, OverworldMap.Rules.Default).Playable, "没有家/出生点/门");
        var b = Sample();
        foreach (int y in new[] { 14, 19 }) { int r = b.H - 1 - y; b.rows[r] = b.rows[r].Substring(0, 21) + "ww" + b.rows[r].Substring(23); }
        Assert.IsFalse(OverworldMap.Check(b, OverworldMap.Rules.Default).Playable, "拆了桥走不到右半边");
    }

    [Test]
    public void Sight_TallGrassWallsNightLamps()
    {
        var m = OverworldMap.Parse("# Overworld: s\n" + string.Join("\n", new[]
        {
            "tttttttttttttttttttt",
            "t..................t",
            "t..................t",
            "t........\"\"\"......t",
            "t..................t",
            "t....W.............t",
            "t..................t",
            "t..................t",
            "t..................t",
            "t..................t",
            "t..................t",
            "tttttttttttttttttttt",
        }));
        var lamps = new System.Collections.Generic.List<OverworldMap.Cell>();
        var r = new OverworldMap.SightRules { range = 7, nightRange = 3.5, halfAngleDeg = 55, nearRadius = 1.2, grassRadius = 1.5, lampRadius = 3 };
        Assert.IsTrue(OverworldMap.CanSee(m, lamps, 2.5, 8.5, 1, 0, 7.5, 8.5, r), "空地、正前方 5 格：看得见");
        Assert.IsFalse(OverworldMap.CanSee(m, lamps, 2.5, 8.5, -1, 0, 7.5, 8.5, r), "背对：看不见");
        Assert.IsFalse(OverworldMap.CanSee(m, lamps, 2.5, 6.5, 1, 0, 8.5, 6.5, r), "房子挡住：看不见");
        Assert.IsFalse(OverworldMap.CanSee(m, lamps, 5.5, 8.5, 1, 0, 9.5, 8.5, r), "高草里、4 格：看不见");
        Assert.IsTrue(OverworldMap.SeesRustle(m, lamps, 5.5, 8.5, 1, 0, 9.5, 8.5, r), "但看得见草在晃");
        r.night = true;
        Assert.IsFalse(OverworldMap.CanSee(m, lamps, 2.5, 8.5, 1, 0, 8.5, 8.5, r), "晚上 6 格：看不见");
        lamps.Add(new OverworldMap.Cell(9, 9));
        Assert.IsTrue(OverworldMap.CanSee(m, lamps, 2.5, 8.5, 1, 0, 8.5, 8.5, r), "站在路灯下：看得见");
    }

    [Test]
    public void Mind_OmenBeforeAlert_AndChaseEnds()
    {
        var t = Tuning(); var mind = new OverworldMind(t);
        var p = new OverworldPercept { marioPos = Vector2.zero, seesFigure = true, figurePos = new Vector2(4, 0), scheduleTarget = new Vector2(10, 0) };
        bool sawCurious = false; OverworldMarioState s = OverworldMarioState.Walking;
        for (int i = 0; i < 60 && s != OverworldMarioState.Chasing; i++) { s = mind.Tick(0.05f, p).state; if (s == OverworldMarioState.Curious) sawCurious = true; }
        Assert.IsTrue(sawCurious, "H2：先 '?'"); Assert.AreEqual(OverworldMarioState.Chasing, s);
        p.seesFigure = false;
        for (int i = 0; i < 600; i++) s = mind.Tick(0.05f, p).state;
        Assert.AreEqual(OverworldMarioState.Walking, s, "H9：追不到就回到日程");
    }

    [Test]
    public void Mind_DisguiseStillIsNotSuspicious_AndSlipStuns()
    {
        var t = Tuning(); var mind = new OverworldMind(t);
        var p = new OverworldPercept { seesFigure = true, figurePos = new Vector2(3, 0), figureLooksLikeProp = true, figureMoving = false, scheduleTarget = new Vector2(10, 0) };
        for (int i = 0; i < 100; i++) Assert.AreEqual(OverworldMarioState.Walking, mind.Tick(0.05f, p).state);
        p.slipped = true; var o = mind.Tick(0.05f, p);
        Assert.AreEqual(OverworldMarioState.Dizzy, o.state); Assert.IsNull(o.target);
    }

    [Test]
    public void DoorOutcomes()
    {
        Assert.AreEqual(OverworldMind.DoorOutcome.Ambush, OverworldMind.AtDoor(true, 0, 6));
        Assert.AreEqual(OverworldMind.DoorOutcome.Late, OverworldMind.AtDoor(false, 3, 6));
        Assert.AreEqual(OverworldMind.DoorOutcome.Missed, OverworldMind.AtDoor(false, 7, 6));
        Assert.AreEqual(0f, OverworldMind.CarriedSuspicion(OverworldMarioState.Walking, 80, 35));
        Assert.AreEqual(35f, OverworldMind.CarriedSuspicion(OverworldMarioState.Chasing, 80, 35), "带进房间最多 '?'，不跳过预兆（H2）");
        OverworldSession.NewDay("x", "Town");
        OverworldSession.RecordRoom(1, true); OverworldSession.RecordRoom(2, false); OverworldSession.RecordMissed(3);
        Assert.AreEqual(1, OverworldSession.Count(OverworldSession.DoorResult.Defended));
        Assert.IsFalse(OverworldSession.DayWon(3)); Assert.IsTrue(OverworldSession.DayWon(2));
    }

    [Test]
    public void Tuning_HasOverworldDefaults()
    {
        var t = Tuning();
        Assert.Less(t.overworldMarioSpeed, t.overworldTricksterSpeed, "你比他快，才能抄近路埋伏");
        Assert.Less(t.overworldChaseSpeed, t.overworldTricksterSpeed, "他追你时你还跑得掉");
        Assert.LessOrEqual(t.overworldSlipStunSeconds, t.maxStunSeconds);
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 16);
    }

    [Test]
    public void H4_OverworldMindOnlyReadsPercept()
    {
        string src = Read("Scripts/Overworld/OverworldMind.cs");
        foreach (var bad in new[] { "IsDisguised", "TricksterKit", "PossessionManager", "FindObjectOfType", "OverworldGame" })
            StringAssert.DoesNotContain(bad, src);
    }

    [Test]
    public void Wiring_RoomsReturnToTown()
    {
        StringAssert.Contains("SceneTransit.Go(town,", Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs"));
        StringAssert.Contains("OverworldSession.RecordRoom(door, won)", Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs"));
        StringAssert.Contains("driver.SkipStartDelay()", Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs"));
        StringAssert.Contains("if (OverworldSession.Active) return;", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs"));
        StringAssert.Contains("if (RoomOverride != null && RoomOverride.Length > 0) return RoomOverride;", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        StringAssert.Contains("AddComponent<OverworldRoomLink>()", Read("Scripts/Editor/OverworldBuilder.cs"));
        StringAssert.Contains("EditorBuildSettings.scenes = list.ToArray()", Read("Scripts/Editor/OverworldBuilder.cs"));
        StringAssert.Contains("OverworldPack.Parse(json)", Read("Scripts/Editor/LevelLibrary.cs"));
    }

    // ── S211：平滑切换 + 自动重建 ──────────────────────
    [Test]
    public void Transit_FadeHoldsUntilLoaded_ThenFadesIn()
    {
        var p = new SceneTransitPlan();
        Assert.IsTrue(p.Begin("门 1"));
        Assert.IsFalse(p.Begin("again"), "切换中再按不会叠加");
        Assert.IsFalse(p.Tick(0.1f, true)); Assert.Greater(p.Alpha, 0f); Assert.Less(p.Alpha, 1f);
        Assert.IsFalse(p.Tick(0.2f, true)); Assert.AreEqual(SceneTransitPlan.Phase.Loading, p.phase); Assert.AreEqual(1f, p.Alpha);
        for (int i = 0; i < 20; i++) Assert.IsFalse(p.Tick(0.1f, false), "没加载完不激活");
        Assert.IsTrue(p.Tick(0.1f, true), "加载好了 → 激活一次");
        Assert.IsFalse(p.Tick(0.1f, true), "只激活一次");
        p.Activated(); Assert.AreEqual(SceneTransitPlan.Phase.FadeIn, p.phase);
        p.Tick(1f, true); Assert.IsFalse(p.Busy); Assert.AreEqual(0f, p.Alpha);
    }

    [Test]
    public void Transit_NeverStuckBlack()
    {
        var p = new SceneTransitPlan(); p.Begin("x");
        bool act = false; for (int i = 0; i < 200 && !act; i++) act = p.Tick(0.1f, false);
        Assert.IsTrue(act, "加载一直没好也会在 maxLoadSeconds 后激活");
        var q = new SceneTransitPlan(); q.Begin("y"); q.Tick(0.3f, false); q.Abort(); Assert.IsFalse(q.Busy); Assert.AreEqual(0f, q.Alpha);
        var r = new SceneTransitPlan(); r.Begin("z"); r.Tick(0.3f, true); Assert.IsFalse(r.Tick(0.1f, true), "最短黑屏：标题卡读得清"); 
        Assert.Less(new SceneTransitPlan().MinTotalSeconds, 1.2f, "一次切换不拖沓");
    }

    [Test]
    public void Wiring_SmoothSwitch_AndAutoRebuild()
    {
        string game = Read("Scripts/Overworld/Runtime/OverworldGame.cs"), link = Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs");
        string tr = Read("Scripts/Overworld/Runtime/SceneTransit.cs"), b = Read("Scripts/Editor/OverworldBuilder.cs");
        StringAssert.Contains("SceneTransit.Go(scene,", game);
        StringAssert.Contains("if (SceneTransit.Busy) { UpdateVisuals(); return; }", game);
        StringAssert.Contains("SceneManager.GetActiveScene().path", game);
        StringAssert.Contains("if (!over || SceneTransit.Busy) return;", link);
        StringAssert.Contains("op.allowSceneActivation = false;", tr);
        StringAssert.Contains("DontDestroyOnLoad(go);", tr);
        StringAssert.Contains("Time.unscaledDeltaTime", tr);
        StringAssert.Contains("EditorApplication.playModeStateChanged += OnPlayMode;", b);
        StringAssert.Contains("EditorPrefs.SetString(TownHashKey, TownFingerprint(text));", b);
        StringAssert.Contains("names.Add(path);", b);
        StringAssert.Contains("AssetDatabase.DeleteAsset(fp);", b);
    }
}
