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
    static OverworldMap.Map Sample() => OverworldPack.Parse(OverworldPack.SampleText)[0];
    static string Read(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative)).Replace("\r\n", "\n");

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
        StringAssert.Contains("if (OverworldSession.Active || Step1QuickTest.On) { roundMode", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs")); // S227：不弹问卷，但照样记一行
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
        StringAssert.Contains("SceneTransit.Go(OverworldSession.RoomScenes[d.n],", game); // S213：规则搬进 OverworldTown，这里只切场景
        StringAssert.Contains("if (SceneTransit.Busy) { UpdateVisuals(); return; }", game);
        StringAssert.Contains("gameObject.scene.path", game); // S212：自己所在场景（不依赖当前激活场景）
        StringAssert.Contains("if (!over || SceneTransit.Busy) return;", link);
        StringAssert.Contains("op.allowSceneActivation = false;", tr);
        StringAssert.Contains("DontDestroyOnLoad(go);", tr);
        StringAssert.Contains("Time.unscaledDeltaTime", tr);
        StringAssert.Contains("EditorApplication.playModeStateChanged += OnPlayMode;", b);
        StringAssert.Contains("EditorPrefs.SetString(TownHashKey, TownFingerprint(text));", b);
        StringAssert.Contains("names.Add(path);", b);
        StringAssert.Contains("AssetDatabase.DeleteAsset(fp);", b);
    }

    // ── S212：更顺的转场 + 地图指引 + 工坊顺手 ──────────────
    [Test]
    public void Transit_EasedIris_AndHitchFrameDoesNotJump()
    {
        Assert.AreEqual(0f, SceneTransitPlan.Ease(0f)); Assert.AreEqual(1f, SceneTransitPlan.Ease(1f));
        Assert.Less(SceneTransitPlan.Ease(0.1f), 0.1f, "开头慢"); Assert.Greater(SceneTransitPlan.Ease(0.9f), 0.9f, "结尾慢");
        var p = new SceneTransitPlan(); p.Begin("x");
        Assert.LessOrEqual(p.Step(0.5f), SceneTransitPlan.MaxStep, "卡顿帧封顶");
        p.Tick(1f, true); Assert.AreEqual(SceneTransitPlan.Phase.Loading, p.phase);
        Assert.AreEqual(0.5f, p.Step(0.5f), "等加载时按真实时间（超时保护才准）");
        Assert.IsTrue(p.Tick(0.5f, true)); p.Activated();
        p.Tick(p.Step(0.6f), true); Assert.Greater(p.Alpha, 0.5f, "激活后第一帧即便卡了 0.6 秒，淡入也不会跳过大半");
        Assert.AreEqual(1f - p.Alpha, p.IrisOpen, 1e-5);
        p.SetFocus(2f, -1f); Assert.AreEqual(1f, p.focusX); Assert.AreEqual(0f, p.focusY);
        Assert.AreEqual(Mathf.Sqrt(800f * 800f + 600f * 600f), SceneTransitPlan.FarCorner(0f, 0f, 800, 600), 0.01f);
    }

    [Test]
    public void Guide_EdgeArrow_ClampsToBorder()
    {
        var on = OverworldGuide.EdgeArrow(0.5f, 0.7f); Assert.IsTrue(on.onScreen);
        var r = OverworldGuide.EdgeArrow(3f, 0.5f); Assert.IsFalse(r.onScreen); Assert.AreEqual(0.94f, r.x, 1e-4); Assert.AreEqual(0.5f, r.y, 1e-4); Assert.AreEqual(0f, r.angleDeg, 1e-3);
        var d = OverworldGuide.EdgeArrow(0.5f, -2f); Assert.AreEqual(0.06f, d.y, 1e-4); Assert.AreEqual(-90f, d.angleDeg, 1e-3);
        var b = OverworldGuide.EdgeArrow(0.7f, 0.5f, 0.06f, true); Assert.IsFalse(b.onScreen, "在镜头后面也要画箭头"); Assert.Less(b.x, 0.5f);
    }

    [Test]
    public void Guide_Race_AndScrubber_FollowSchedule()
    {
        var m = OverworldPack.Parse(OverworldPack.SampleText)[0]; var r = OverworldMap.Rules.Default;
        var sc = OverworldMap.Check(m, r).schedule; var st = sc.stops[0]; var T = OverworldMap.Find(m, 'T')[0]; var M = OverworldMap.Find(m, 'M')[0];
        var race = OverworldGuide.RaceTo(m, r, st.cell, st.door.minute, OverworldMap.DayStart, T.x + 0.5, T.y + 0.5, M.x + 0.5, M.y + 0.5, false);
        Assert.AreEqual(OverworldGuide.Verdict.Ahead, race.verdict, "06:00 从出生点出发，第一扇门来得及");
        var late = OverworldGuide.RaceTo(m, r, st.cell, st.door.minute, st.door.minute, M.x + 0.5, M.y + 0.5, st.path[st.path.Count - 2].x + 0.5, st.path[st.path.Count - 2].y + 0.5, false);
        Assert.AreEqual(OverworldGuide.Verdict.Behind, late.verdict, "他就在门口、你在他家 → 来不及");
        Assert.AreEqual(OverworldGuide.Verdict.Inside, OverworldGuide.RaceTo(m, r, st.cell, 0, 0, 0, 0, 0, 0, true).verdict);
        Assert.AreEqual(M, OverworldGuide.MarioAt(m, sc, OverworldMap.DayStart, r.marioSpeed, r.minutesPerSecond).cell);
        Assert.AreEqual(st.door.n, OverworldGuide.MarioAt(m, sc, (st.arrive + st.leave) / 2, r.marioSpeed, r.minutesPerSecond).insideDoor);
        var mid = OverworldGuide.MarioAt(m, sc, (st.depart + st.arrive) / 2, r.marioSpeed, r.minutesPerSecond).cell;
        Assert.IsTrue(st.path.Contains(mid) && !mid.Equals(M) && !mid.Equals(st.cell), "走路途中在路线中间");
        Assert.AreEqual(M, OverworldGuide.MarioAt(m, sc, OverworldMap.DayEnd, r.marioSpeed, r.minutesPerSecond).cell, "晚上到家");
    }

    [Test]
    public void Wiring_S212_GuideRetryAndWorkshop()
    {
        string game = Read("Scripts/Overworld/Runtime/OverworldGame.cs"), link = Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs"), tr = Read("Scripts/Overworld/Runtime/SceneTransit.cs");
        string gm = Read("Scripts/Core/GameManager.cs"), ws = Read("Scripts/Editor/OverworldWorkshopWindow.cs"), lw = Read("Scripts/Editor/LevelWorkshopWindow.cs");
        StringAssert.Contains("SceneTransit.RevealAt(new Vector3((float)tx, (float)ty, 0), cam);", game);
        StringAssert.Contains("SnapCamera();", game);
        StringAssert.Contains("OverworldGuide.RaceTo(", game);
        StringAssert.Contains("OverworldGuide.EdgeArrow(", game);
        StringAssert.Contains("Step1Keys.Held(KeyCode.Tab)", game);
        StringAssert.Contains("gameObject.scene.path", game);
        StringAssert.Contains("GameManager.RestartOverride = RestartRoom;", link);
        StringAssert.Contains("if (RestartOverride != null && RestartOverride()) return;", gm);
        Assert.Less(gm.IndexOf("RestartOverride()"), gm.IndexOf("EditorRestartHandler()"), "小镇房间的重开要在编辑器'退出 Play'之前接管");
        StringAssert.Contains("plan.Step(Time.unscaledDeltaTime)", tr);
        StringAssert.Contains("AudioListener.volume", tr);
        StringAssert.Contains("Resources.UnloadUnusedAssets()", tr);
        StringAssert.Contains("LevelWorkshopWindow.OpenRoom(d.room)", ws);
        StringAssert.Contains("OverworldGuide.MarioAt(", ws);
        StringAssert.Contains("SessionState.SetString(DraftKey", ws);
        StringAssert.Contains("public static bool OpenRoom(string name)", lw);
    }

    // ── S213：玩家视角模拟找出的问题 ──────────────

    [Test]
    public void Town_AmbushNeedsHimClose_AndNotSpotted()
    {
        var m = Sample(); var t = Tuning(); OverworldSession.NewDay(m.name, "Town");
        var town = new OverworldTown(m, t); var dc = town.doorCells[town.NextStop.n];
        town.tx = dc.x + 0.5; town.ty = dc.y - 0.5; // 06:00 就站在门 1 口
        town.Tick(1f / 30, new OverworldTown.Input { door = true });
        Assert.IsFalse(town.wantsEnter, "他还没出发：按 E 不算埋伏（以前直奔门口就全胜）");
        Assert.AreEqual(OverworldTown.Note.AmbushWait, town.hint);
        Assert.IsFalse(town.AmbushReady);
    }

    [Test]
    public void Town_FastForwardOnlyWhenQuiet_AndDayEndsAfterLastDoor()
    {
        var m = Sample(); var t = Tuning(); OverworldSession.NewDay(m.name, "Town");
        var town = new OverworldTown(m, t);
        double before = OverworldSession.Minute; town.Tick(1f, new OverworldTown.Input { fastForward = true });
        Assert.AreEqual(OverworldTown.FastForwardScale * t.overworldMinutesPerSecond, OverworldSession.Minute - before, 1e-3, "没事时快进 ×4");
        town.frozen = 2f; Assert.IsFalse(town.CanFastForward, "被定身时不能快进");
        OverworldSession.NextStop = town.stops.Count; town.frozen = 0f;
        town.Tick(1f / 30, new OverworldTown.Input());
        Assert.IsTrue(OverworldSession.DayOver, "最后一扇门结束 → 当天结算（不再干等他走回家）");
    }

    [Test]
    public void Town_ExitGrace_HeDoesNotSeeYouRightAfterRoom()
    {
        var m = Sample(); var t = Tuning(); OverworldSession.NewDay(m.name, "Town");
        var c = OverworldMap.Find(m, '1')[0];
        OverworldSession.HasPositions = true; OverworldSession.MarioX = c.x + 0.5; OverworldSession.MarioY = c.y + 0.5; OverworldSession.TricksterX = c.x + 0.5; OverworldSession.TricksterY = c.y - 0.5;
        OverworldSession.NextStop = 1; OverworldSession.Minute = 9 * 60 + 15;
        var town = new OverworldTown(m, t);
        Assert.Greater(town.exitGrace, 0f);
        for (int i = 0; i < 30; i++) town.Tick(1f / 30, new OverworldTown.Input());
        Assert.AreEqual(OverworldMarioState.Waiting, town.mind.State, "出门 1 秒内：你就在他背后也不起疑");
        Assert.AreEqual("…", town.lastOrder.mark, "看得见的'清点中'标记（H6）");
    }

    [Test]
    public void PlayerSim_HidingMatters_NoOneStuck()
    {
        var m = Sample(); var t = Tuning();
        var hider = OverworldBots.PlayDay(m, t, OverworldBots.Kind.Hider, true, 1);
        var stand = OverworldBots.PlayDay(m, t, OverworldBots.Kind.Follower, true, 1);
        var idle = OverworldBots.PlayDay(m, t, OverworldBots.Kind.Idle, true, 1);
        Assert.AreEqual(m.doors.Count, hider.ambush, "会躲的玩家每扇门都能埋伏");
        Assert.Less(stand.ambush, m.doors.Count, "站着不躲不能全胜");
        Assert.IsTrue(idle.dayEnded && hider.dayEnded && stand.dayEnded, "H10/H9：谁玩一天都会结束");
        Assert.Less(hider.longestIdle, 6.0, "有快进就不会干等");
    }

    [Test]
    public void Wiring_S213_GameUsesTownLogic()
    {
        string game = Read("Scripts/Overworld/Runtime/OverworldGame.cs"), town = Read("Scripts/Overworld/OverworldTown.cs");
        StringAssert.Contains("town.Tick(dt, input);", game);
        StringAssert.Contains("fastForward = Step1Keys.Held(KeyCode.Space)", game);
        StringAssert.DoesNotContain("mind.Tick(", game);
        StringAssert.Contains("figureLooksLikeProp = disguised", town);
        foreach (var bad in new[] { "FindObjectOfType", "UnityEngine.Input", "Step1Text" }) StringAssert.DoesNotContain(bad, town);
    }

    // ── S214：网页 ↔ Unity 同步 + 关卡切换 ──────────────
    [Test]
    public void Sync_UnitySaveKeepsWebPendingMechanics()
    {
        var old = "W....W\nWWWWWW\n# Name: t\n# Pending: Ж=磁铁 (2,1) (3,1)\n";
        var l = new LevelPack.Level { name = "t", rows = new[] { "W.#..W", "WWWWWW" } };
        LevelWorkshopModel.CarryPending(old, l);
        Assert.IsTrue(l.pending.ContainsKey('Ж'), "Unity 再存一次，网页画的新机制不丢");
        Assert.AreEqual(1, l.pending['Ж'].Count, "那一格在 Unity 里画了别的 → 只丢那一格");
        StringAssert.Contains("# Pending: Ж=磁铁 (3,1)", LevelPack.ToText(l));
        Assert.IsTrue(LevelWorkshopModel.SameGrid("A\nB\n# Name: x", "A\r\nB\n# Goal: y"));
        Assert.IsFalse(LevelWorkshopModel.SameGrid("A\nB", "A\nC"));
    }

    [Test]
    public void Wiring_S214_WebSyncAndSwitching()
    {
        string ws = Read("Scripts/Editor/WebSync.cs"), lw = Read("Scripts/Editor/LevelWorkshopWindow.cs"), ow = Read("Scripts/Editor/OverworldWorkshopWindow.cs");
        string lib = Read("Scripts/Editor/LevelLibrary.cs"), ob = Read("Scripts/Editor/OverworldBuilder.cs");
        StringAssert.Contains("public sealed class WebSync : AssetPostprocessor", ws);
        StringAssert.Contains("EditorApplication.delayCall += Flush;", ws);
        StringAssert.Contains("AssetDatabase.DeleteAsset(path);", ws);
        StringAssert.Contains("WebSync.BackupBeforeWrite(path, text);", lib);
        StringAssert.Contains("WebSync.BackupBeforeWrite(p, text);", ob);
        StringAssert.Contains("WebSync.Imported += OnWebImported;", lw);
        StringAssert.Contains("WebSync.Imported += OnWebImported;", ow);
        StringAssert.Contains("KeyCode.PageDown", lw);
        StringAssert.Contains("KeyCode.PageDown", ow);
        StringAssert.Contains("LevelWorkshopModel.CarryPending(", lw);
        StringAssert.Contains("OverworldBots.PlayDay(m, t, k, true, s)", ow);
        StringAssert.Contains("/Assets/Levels/Inbox/", File.ReadAllText(Path.Combine(Application.dataPath, "../.gitignore")));
    }

    // ── S215：全局总览 + 炸弹预算 + 菜单减法 ──────────────
    [Test]
    public void Ledger_FlagsTeachingOverloadAndRepeats()
    {
        var m = OverworldMap.Parse("# Overworld: t\n# Door: 1 | 08:00 | a\n# Door: 2 | 10:00 | b\n# Door: 3 | 12:00 | c\n" + string.Join("\n", OverworldMap.NewMap(20, 12)));
        var rows = m.rows.Select(r => r.ToCharArray()).ToArray();
        rows[3][3] = '1'; rows[3][6] = '2'; rows[3][9] = '3'; m.rows = rows.Select(r => new string(r)).ToArray();
        var rooms = new System.Collections.Generic.Dictionary<string, string[]> { { "a", new[] { "W~~CW" } }, { "b", new[] { "W~~nW" } }, { "c", new[] { "WJ[xKW" } } };
        var rep = CampaignLedger.Build(m, n => rooms.TryGetValue(n, out var g) ? g : null, 3, 3);
        Assert.AreEqual(3, rep.rooms.Count);
        Assert.AreEqual("火", rep.rooms[0].star); Assert.AreEqual(2, rep.rooms[0].firstTime.Count);
        CollectionAssert.AreEqual(new[] { "香蕉皮" }, rep.rooms[1].firstTime, "第二扇门只新教香蕉皮");
        Assert.IsTrue(rep.warnings.Exists(w => w.Contains("主角都是火")), "连着两扇门主角一样 → 提醒");
        Assert.IsTrue(rep.warnings.Exists(w => w.Contains("一次教太多")), "一扇门第一次出现 ≥3 种 → 提醒");
        Assert.AreEqual(6, rep.maxBombs);
    }

    [Test]
    public void Wiring_S215_BombBudgetAndMenus()
    {
        string pb = Read("Scripts/Editor/Step1PrankRoomBuilder.cs"), ob = Read("Scripts/Editor/OverworldBuilder.cs"), ow = Read("Scripts/Editor/OverworldWorkshopWindow.cs");
        StringAssert.Contains("tuning.bombsPerRound + Mathf.Max(0, ExtraBombs)", pb);
        StringAssert.Contains("Step1PrankRoomBuilder.ExtraBombs = OverworldTown.MaxBonusBombs;", ob);
        StringAssert.Contains("Step1PrankRoomBuilder.ExtraBombs = 0;", ob);
        Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 19, "加固规则变了 → 房间场景要重建");
        StringAssert.Contains("CampaignLedger.Build(map,", ow);
        StringAssert.Contains("LevelWorkshopWindow.Open();", ow);
        StringAssert.DoesNotContain("MarioTrickster/Overworld/Town Workshop", ob, "同一个窗口只留一个菜单入口");
        StringAssert.Contains("MarioTrickster/旧工具 (Legacy)/Build Test Scene", Read("Scripts/Editor/TestSceneBuilder.cs"));
    }

    // ── S217：大世界扩展 + 测试不卡人 ──────────────
    [Test]
    public void Resize_GrowsWorld_KeepsTownPlayable_AndFramed()
    {
        foreach (var (l, r, t, b) in new[] { (8, 8, 8, 8), (0, 16, 0, 0), (16, 0, 0, 0), (0, 0, 16, 0), (0, 0, 0, 16) })
        {
            var m = Sample(); int w0 = m.W, h0 = m.H; var old = m.rows.ToArray();
            var res = OverworldMap.Resize(m, l, r, t, b);
            Assert.IsTrue(res.ok, res.why); Assert.AreEqual(0, res.lost, "只扩展不丢东西");
            Assert.AreEqual(w0 + l + r, m.W); Assert.AreEqual(h0 + t + b, m.H);
            for (int x = 0; x < m.W; x++) { Assert.IsTrue(OverworldCatalog.Solid(m.rows[0][x])); Assert.IsTrue(OverworldCatalog.Solid(m.rows[m.H - 1][x])); }
            for (int y = 0; y < m.H; y++) { Assert.IsTrue(OverworldCatalog.Solid(m.rows[y][0])); Assert.IsTrue(OverworldCatalog.Solid(m.rows[y][m.W - 1])); }
            for (int y = 1; y < h0 - 1; y++) Assert.AreEqual(old[y].Substring(1, w0 - 2), m.rows[y + t].Substring(l + 1, w0 - 2), "老镇里面一格不变");
            var rep = OverworldMap.Check(m, OverworldMap.Rules.Default);
            Assert.IsTrue(rep.Playable, string.Join("\n", rep.issues));
            Assert.IsTrue(OverworldWalker.SimulateDay(m, OverworldMap.Rules.Default).ok, "H10：扩展后没人捣乱一天照样走完");
        }
    }

    [Test]
    public void Resize_OpensOldFenceTowardNewLand_AndRejectsOutOfRange()
    {
        var m = Sample(); int w0 = m.W;
        Assert.IsTrue(OverworldMap.Resize(m, 0, 10, 0, 0).ok);
        Assert.AreEqual('.', m.rows[m.H / 2][w0 - 1], "往东扩：老镇东边的围栏树拆掉，新地和老镇连成一片");
        Assert.AreEqual('t', m.rows[m.H / 2][0], "西边没扩：围栏不动");
        var big = Sample();
        Assert.IsTrue(OverworldMap.Resize(big, 0, OverworldMap.MaxW - big.W, 0, OverworldMap.MaxH - big.H).ok, "能扩到最大");
        Assert.GreaterOrEqual(OverworldMap.MaxW, 192); Assert.GreaterOrEqual(OverworldMap.MaxH, 128);
        Assert.IsFalse(OverworldMap.Resize(big, 1, 0, 0, 0).ok, "超过上限拒绝（不会画出打不开的图）");
        var crop = Sample(); var cr = OverworldMap.Resize(crop, -12, 0, 0, 0);
        Assert.IsTrue(cr.ok); Assert.Greater(cr.lost, 0, "裁掉有东西的地方 → 报告丢了几格（编辑器会先问）");
        var n = Sample(); n.notes.Clear(); n.notes.Add(new OverworldMap.Note { x = 3, y = 3, text = "a" });
        OverworldMap.Resize(n, 5, 0, 0, 2); Assert.AreEqual(8, n.notes[0].x); Assert.AreEqual(5, n.notes[0].y, "批注跟着平移");
    }

    [Test]
    public void Wiring_S217_NoFrozenScreen_AndQuickTest()
    {
        string game = Read("Scripts/Overworld/Runtime/OverworldGame.cs"), screen = Read("Scripts/Gameplay/Step1/Step1Screen.cs"), keys = Read("Scripts/Gameplay/Step1/Step1Keys.cs");
        StringAssert.Contains("if (helpOpen) { if (Step1Keys.AnyDown()) helpOpen = false;", game); // 说明面板任意键关（以前只有 H → 画面停住）
        StringAssert.Contains("Time.unscaledDeltaTime, 0.1f", game);                               // 小镇不受 timeScale 影响
        StringAssert.Contains("Time.timeScale = 1f;", game);
        StringAssert.Contains("Step1Text.OverworldControlsBar", game);
        StringAssert.Contains("Step1Text.OverworldWaitDepart(", game);
        StringAssert.Contains("Application.isFocused", game);
        StringAssert.Contains("groundPx", game);                                                   // 地面一张贴图（大世界不卡）
        StringAssert.Contains("Step1Keys.AnyDown()", screen);
        StringAssert.DoesNotContain("Input.anyKeyDown", screen);
        StringAssert.Contains("kb.anyKey.wasPressedThisFrame", keys);
        StringAssert.Contains("case KeyCode.R: return kb.rKey.wasPressedThisFrame;", keys);
        StringAssert.Contains("if (OverworldSession.Active || Step1QuickTest.On) { roundMode", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs")); // S227
        StringAssert.Contains("!Step1QuickTest.On", screen);
        StringAssert.Contains("ExecuteMenuItem(\"Window/General/Game\")", Read("Scripts/Editor/PlayFocus.cs"));
        StringAssert.Contains("WatchdogSeconds", Read("Scripts/Overworld/Runtime/SceneTransit.cs"));
        StringAssert.Contains("OverworldMap.Resize(map,", Read("Scripts/Editor/OverworldWorkshopWindow.cs"));
        StringAssert.Contains("RunHealthCheck()", Read("Scripts/Editor/TestHubWindow.cs"));
        StringAssert.Contains("KeyCode.F8", Read("Scripts/Gameplay/Step1/Step1Feedback.cs"));
        foreach (var bad in new[] { "SuspicionMeter.Add", "Mind.Meter", "SetInputProvider" }) StringAssert.DoesNotContain(bad, Read("Scripts/Gameplay/Step1/Step1Feedback.cs"), "反馈只记录，不碰玩法（H4）");
    }

    [Test]
    public void TinyZip_WritesReadableArchive()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mt_zip_test"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "反馈.md"), "你好");
        string zip = Path.Combine(Path.GetTempPath(), "mt_zip_test.zip");
        TinyZip.Write(zip, Directory.GetFiles(dir));
        var bytes = File.ReadAllBytes(zip);
        Assert.AreEqual(0x50, bytes[0]); Assert.AreEqual(0x4B, bytes[1], "PK 头");
        Assert.AreEqual(0xCBF43926u, TinyZip.Crc(System.Text.Encoding.ASCII.GetBytes("123456789")), "CRC32 标准校验值");
    }

    // ── S218：小镇大机关（巨炮 / 滚石 / 水塔）+ 连锁 + 天气 + 小镇↔房间联动 ──
    static OverworldMap.Map Big() => OverworldPack.Parse(OverworldPack.BigSampleText)[0];

    [Test]
    public void BigSample_PlayableWithThreeChain_AndStillPlayableAfterEverythingFires()
    {
        var m = Big(); var r = OverworldMap.Rules.Default;
        Assert.IsTrue(OverworldMap.Check(m, r).Playable, "星露大镇可玩");
        Assert.IsTrue(OverworldWalker.SimulateDay(m, r).ok, "H10：没人捣乱一天走得完");
        Assert.GreaterOrEqual(OverworldProps.LongestChain(m).Count, 3, "样板里有 3 连锁");
        // 最坏情况：所有滚石都滚、所有水塔都淹 → 地形只会"打开"（撞碎 / 变泥地都还能走），H1 不会被关死
        foreach (var c in OverworldProps.All(m))
        {
            char k = m.At(c.x, c.y);
            if (k == 'O') { for (int d = 0; d < 4; d++) foreach (var q in OverworldProps.Lane(m, c, d)) if (OverworldProps.Smashable(m.At(q.x, q.y))) OverworldMap.Set(m, q.x, q.y, '.'); OverworldMap.Set(m, c.x, c.y, '.'); }
            else if (k == 'U') foreach (var q in OverworldProps.Flood(m, c, OverworldProps.FloodRadius + 1)) OverworldMap.Set(m, q.x, q.y, 'g');
        }
        Assert.IsTrue(OverworldMap.Check(m, r).Playable);
        Assert.IsTrue(OverworldWalker.SimulateDay(m, r).ok);
    }

    [Test]
    public void BigProps_CheckCatchesTraps_H1()
    {
        var r = OverworldMap.Rules.Default;
        var a = Sample(); OverworldMap.Set(a, 26, 16, 'K');
        Assert.IsTrue(OverworldMap.Check(a, r).issues.Exists(i => i.sev == OverworldMap.Sev.Warn && i.text.Contains("找不到靶心")), "巨炮没有靶心 = 提醒（S219 起能坐进去自己瞄）");
        var b = Sample(); OverworldMap.Set(b, 2, 20, 'K'); OverworldMap.Set(b, 2, 1, 'X');
        for (int x = 1; x <= 6; x++) for (int y = 1; y <= 6; y++) if ((x == 1 || y == 6 || x == 6) && (b.At(x, y) == '.' || b.At(x, y) == '"')) OverworldMap.Set(b, x, y, 'f');
        Assert.IsTrue(OverworldMap.Check(b, r).issues.Exists(i => i.text.Contains("走不回马里奥的家")), "靶心在围栏里 = 被轰过去就困住");
        var lane = OverworldProps.Lane(Sample(), new OverworldMap.Cell(1, 20), 0);
        Assert.IsFalse(lane.Exists(c => c.x <= 0 || c.y <= 0), "滚石永远不碰最外圈");
    }

    [Test]
    public void BigProps_ChainInTown_TelegraphFirst_MarioLearnsFromExperience()
    {
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露大镇", "Town"); OverworldSession.Active = true;
        var t = Tuning(); var town = new OverworldTown(Big(), t); var k = new OverworldMap.Cell(26, 16);
        OverworldProps.Aim(town.map, k, out _, out int dir, out _); var mz = OverworldProps.MuzzleCells(town.map, k, dir)[1];
        town.mario.x = mz.x + 0.5; town.mario.y = mz.y + 0.5; town.mario.Clear(); town.tx = k.x - 1.5; town.ty = k.y + 0.5;
        Assert.IsTrue(town.Arm(k, 1, town.tx, town.ty));
        Assert.IsFalse(town.Arm(k, 1, town.tx, town.ty), "每天一次");
        var none = new OverworldTown.Input(); town.Tick(t.overworldBigFuseSeconds * 0.5f, none);
        Assert.IsFalse(town.marioFlying, "H3：预警期间不伤人");
        bool flew = false; int depth = 0;
        for (int i = 0; i < 360 && (i < 3 || town.BigBusy); i++) { town.Tick(1f / 30, none); flew |= town.marioFlying; foreach (var b in town.active) depth = Mathf.Max(depth, b.depth); }
        Assert.IsTrue(flew, "站在炮口 → 被轰飞");
        Assert.GreaterOrEqual(depth, 3, "炮弹落地 → 滚石 → 水塔（3 连）");
        Assert.IsTrue(OverworldSession.MarioWary.Contains('K'), "他记住了巨炮（自己的经历，H4）");
        Assert.Greater(OverworldSession.Changed.Count, 0, "地形被改了（当天有效）");
        OverworldSession.ResetStatics();
    }

    [Test]
    public void Weather_IsInputRandom_FirstDayClear_Reproducible()
    {
        Assert.AreEqual(OverworldEvents.Kind.Clear, OverworldEvents.Of("星露大镇", 1).kind, "第 1 天先学规则");
        var seen = new System.Collections.Generic.HashSet<OverworldEvents.Kind>();
        for (int d = 1; d <= 60; d++) { var a = OverworldEvents.Of("星露大镇", d); Assert.AreEqual(a.kind, OverworldEvents.Of("星露大镇", d).kind); seen.Add(a.kind); }
        Assert.AreEqual(5, seen.Count, "5 种天气都会出现");
        var m = Big(); OverworldEvents.ApplyTo(m, new OverworldEvents.Day { kind = OverworldEvents.Kind.Market, h = 0xFFFFFFFFu });
        int prev = -99; foreach (var d in m.doors.OrderBy(x => x.minute).ThenBy(x => x.n)) { Assert.GreaterOrEqual(d.minute, prev + 15); Assert.LessOrEqual(d.minute, OverworldMap.LatestDoor); prev = d.minute; }
    }

    [Test]
    public void Wiring_S218_TownRoomLinks_H4_Tuning()
    {
        string town = Read("Scripts/Overworld/OverworldTown.cs"), mind = Read("Scripts/Overworld/OverworldMind.cs"), link = Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs"), game = Read("Scripts/Overworld/Runtime/OverworldGame.cs");
        StringAssert.Contains("driver.AddStartDelay(OverworldSession.CarriedDaze)", link);          // 小镇砸晕 → 房间开局还晕着
        StringAssert.Contains("if (playerWon) ReloadDoor = door;", Read("Scripts/Overworld/OverworldSession.cs")); // 房间守住 → 小镇大机关重新装填
        StringAssert.Contains("OverworldSession.Changed", town);                                     // 改掉的地形跨场景保留（当天）
        StringAssert.Contains("if (p.heardNoise) { Meter.Add(p.noiseSuspicion);", mind);             // H2：声音只加起疑
        StringAssert.Contains("OverworldSession.MarioWary.Contains(b.kind)", town);                  // H4：只凭经历躲
        StringAssert.Contains("BigFx();", game); StringAssert.Contains("Step1Fx.Ring(", game);
        foreach (var bad in new[] { "FindObjectOfType", "UnityEngine.Input", "Step1Text", "UnityEngine.Random" }) StringAssert.DoesNotContain(bad, town);
        StringAssert.DoesNotContain("System.Random", Read("Scripts/Overworld/OverworldProps.cs"));   // 天气用自己的哈希（网页逐字一样）
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 19);
        var t = Tuning(); Assert.LessOrEqual(t.overworldBigStunSeconds, t.maxStunSeconds, "H9");
        Assert.Greater(t.overworldBigFuseSeconds, 0.5f, "H3：预警够看清");
    }

    // ═════ S219：巨炮自由瞄准 / 马里奥坐炮 / 山地视线 / 山洞 / 雷雨闪电 / 泥石流 / 酸雨 ═════
    private static OverworldMap.Map Mtn() => OverworldPack.Parse(OverworldPack.MountainSampleText)[0];

    [Test]
    public void S219_MountainSample_Playable_WorstCaseStillOpens()
    {
        var r = OverworldMap.Rules.Default; var m = Mtn();
        Assert.IsTrue(OverworldMap.Check(m, r).Playable, OverworldMap.Check(m, r).Headline);
        Assert.IsTrue(OverworldWalker.SimulateDay(m, r).ok);
        foreach (var c in OverworldMap.Find(m, '^')) { if (OverworldProps.MudDir(m, c) < 0) continue; foreach (var q in OverworldProps.MudLane(m, c)) if (OverworldProps.Muddable(m.At(q.x, q.y))) OverworldMap.Set(m, q.x, q.y, 'g'); OverworldMap.Set(m, c.x, c.y, 'g'); }
        OverworldEvents.ApplyTo(m, new OverworldEvents.Day { kind = OverworldEvents.Kind.Acid });
        Assert.IsTrue(OverworldMap.Check(m, r).Playable, "泥石流 + 酸雨只会打开地形（H1）");
        Assert.IsTrue(OverworldWalker.SimulateDay(m, r).ok);
    }

    [Test]
    public void S219_HillBlocksFlatSight_StandOnHillSeesOver()
    {
        var m = OverworldMap.Parse("# Overworld: los\n" + string.Join("\n", OverworldMap.NewMap(16, 12)));
        OverworldMap.Set(m, 7, 5, '^');
        Assert.IsFalse(OverworldMap.LineOfSight(m, 4.5, 5.5, 10.5, 5.5), "躲在山丘后面");
        OverworldMap.Set(m, 4, 5, '^');
        Assert.IsTrue(OverworldMap.LineOfSight(m, 4.5, 5.5, 10.5, 5.5), "站上山丘看得远");
        OverworldMap.Set(m, 7, 5, 'A');
        Assert.IsFalse(OverworldMap.LineOfSight(m, 4.5, 5.5, 10.5, 5.5), "山永远挡");
    }

    [Test]
    public void S219_YouRideCannon_AimAndLandWhereAimed_BadAimRefused()
    {
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露山镇", "Town"); OverworldSession.Active = true;
        var t = Tuning(); var town = new OverworldTown(Mtn(), t); var k = new OverworldMap.Cell(26, 16);
        town.tx = k.x - 0.5; town.ty = k.y + 0.5; OverworldSession.Minute = OverworldMap.DayStart;
        town.Tick(1f / 30, new OverworldTown.Input { door = true });
        Assert.IsTrue(town.Seated, "E 坐进巨炮");
        town.Tick(1f / 30, new OverworldTown.Input { aim = town.seat.dir + 1 });
        var land = town.AimLandingOf(k, town.seat.dir, town.seat.dist);
        town.Tick(1f / 30, new OverworldTown.Input { peel = true });
        bool flew = false; for (int i = 0; i < 200; i++) { town.Tick(1f / 30, new OverworldTown.Input()); flew |= town.youFlying; if (flew && !town.youFlying) break; }
        Assert.IsTrue(flew); Assert.Less(OverworldTown.Dist(town.tx, town.ty, land.x + 0.5, land.y + 0.5), 0.6f, "落在瞄的地方");
        var m2 = Mtn(); for (int x = 5; x <= 9; x++) for (int y = 30; y <= 34; y++) if (x == 5 || x == 9 || y == 30 || y == 34) OverworldMap.Set(m2, x, y, 'f');
        OverworldMap.Set(m2, 20, 32, 'K'); var kk = new OverworldMap.Cell(20, 32);
        Assert.IsFalse(OverworldProps.AimOk(m2, kk, 1, OverworldProps.AimLanding(m2, kk, 1, 13, -1), OverworldMap.Find(m2, 'M')[0]), "H1：瞄进死地不许打");
        OverworldSession.ResetStatics();
    }

    [Test]
    public void S219_MarioRidesCannonForShortcut_TamperMakesHimDizzy()
    {
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露山镇", "Town"); OverworldSession.Active = true;
        var t = Tuning(); var town = new OverworldTown(Mtn(), t);
        town.mario.x = 62.5; town.mario.y = 30.5; town.mario.Clear(); town.tx = 3.5; town.ty = 2.5; OverworldSession.NextStop = 3; OverworldSession.Minute = 16 * 60 + 31;
        bool rode = false, dizzy = false; for (int i = 0; i < 750 && OverworldSession.NextStop == 3; i++) { town.Tick(1f / 30, new OverworldTown.Input()); rode |= town.MarioSeated; dizzy |= town.lastOrder.state == OverworldMarioState.Dizzy; }
        Assert.IsTrue(rode, "坐炮快很多 → 他会坐（只凭公开的炮和地图，H4）"); Assert.IsFalse(dizzy, "自己坐 = 平稳落地");
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露山镇", "Town"); OverworldSession.Active = true;
        var t2 = new OverworldTown(Mtn(), t); t2.mario.x = 62.5; t2.mario.y = 30.5; t2.mario.Clear(); t2.tx = 65.5; t2.ty = 27.5; OverworldSession.NextStop = 3; OverworldSession.Minute = 16 * 60 + 31;
        bool tam = false, dz = false; for (int i = 0; i < 600 && !dz; i++) { t2.Tick(1f / 30, new OverworldTown.Input { peel = t2.MarioSeated && !tam }); tam |= t2.hint == OverworldTown.Note.CannonTamper; dz |= t2.lastOrder.state == OverworldMarioState.Dizzy; }
        Assert.IsTrue(tam, "他瞄准时你按 L 拨歪"); Assert.IsTrue(dz, "飞歪落地晕"); Assert.IsTrue(OverworldSession.MarioWary.Contains('K'), "以后不坐（吃过亏）");
        OverworldSession.ResetStatics();
    }

    [Test]
    public void S219_StormLightning_Mudslide_Caves_WeatherPoolFollowsMap()
    {
        var m = Mtn(); int storm = -1; for (int d = 2; d < 80 && storm < 0; d++) if (OverworldEvents.Of(m, d).kind == OverworldEvents.Kind.Storm) storm = d;
        Assert.Greater(storm, 1, "有路灯的图会有雷雨");
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露山镇", "Town", storm); OverworldSession.Active = true;
        var town = new OverworldTown(Mtn(), Tuning()); var lamp = new OverworldMap.Cell(45, 31);
        town.tx = lamp.x + 2.5; town.ty = lamp.y + 0.5; town.mario.x = lamp.x - 0.5; town.mario.y = lamp.y + 0.5; town.mario.Clear(); OverworldSession.Minute = OverworldMap.DayStart;
        town.Tick(1f / 30, new OverworldTown.Input { peel = true });
        bool dz = false; for (int i = 0; i < 120; i++) { town.Tick(1f / 30, new OverworldTown.Input()); dz |= town.lastOrder.state == OverworldMarioState.Dizzy; }
        Assert.AreEqual(1, OverworldSession.Lightnings); Assert.IsTrue(dz, "闪电旁的人晕"); Assert.GreaterOrEqual(OverworldSession.Mudslides, 1, "雷雨 = 湿，震到山坡 = 泥石流");
        var cv = OverworldMap.Find(town.map, 'h'); town.tx = cv[0].x + 0.5; town.ty = cv[0].y + 0.5; town.frozen = 0f; town.Tick(1f / 30, new OverworldTown.Input { door = true });
        Assert.Less(OverworldTown.Dist(town.tx, town.ty, cv[1].x + 0.5, cv[1].y + 0.5), 0.3f, "山洞一对：E 钻到另一头");
        var bare = OverworldMap.Parse("# Overworld: bare\n" + string.Join("\n", OverworldMap.NewMap(16, 12)));
        for (int d = 1; d <= 120; d++) { var k = OverworldEvents.Of(bare, d).kind; Assert.AreNotEqual(OverworldEvents.Kind.Storm, k); Assert.AreNotEqual(OverworldEvents.Kind.Acid, k); }
        OverworldSession.ResetStatics();
    }

    [Test]
    public void Wiring_S219_KeysH4Tuning()
    {
        string town = Read("Scripts/Overworld/OverworldTown.cs"), game = Read("Scripts/Overworld/Runtime/OverworldGame.cs");
        StringAssert.Contains("aim = Step1Keys.Down(KeyCode.RightArrow)", game);         // 两套输入都读
        StringAssert.Contains("AimVisuals();", game);                                     // 落点画出来（H3/H6）
        StringAssert.Contains("figureLooksLikeProp = disguised || seat != null", town);   // 坐在炮里 = 他只看见炮（H4）
        StringAssert.Contains("OverworldProps.AimOk(map", town);                          // 落点走不回家不许打（H1）
        StringAssert.Contains("if (OverworldSession.MarioWary.Contains('K')) return;", town); // 被耍过就不坐
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 20);
        var t = Tuning(); Assert.Greater(t.overworldCannonSeatSeconds, 0f, "H9：坐炮有时限"); Assert.Greater(t.overworldMarioCannonAimSeconds, 0.8f, "他瞄准时你来得及反应");
    }

    // ═════ S220：心 / 雷区 / 补心能量 / Q 雷云 / 防卡 ═════
    private static OverworldMap.Map StormTown() => OverworldPack.Parse(OverworldPack.StormSampleText)[0];

    [Test]
    public void S220_StormSample_Playable_RoundTrips_AndOldMapsUnchanged()
    {
        var m = StormTown();
        Assert.IsTrue(OverworldMap.Check(m, OverworldMap.Rules.Default).Playable, "星露雷镇可以试玩");
        Assert.AreEqual(2, m.storms.Count);
        var rt = OverworldMap.Parse(OverworldMap.ToText(m));
        Assert.AreEqual(OverworldMap.StormText(m.storms[0]), OverworldMap.StormText(rt.storms[0]));
        Assert.IsTrue(rt.storms[0].always); Assert.IsFalse(rt.storms[1].always);
        foreach (var txt in new[] { OverworldPack.SampleText, OverworldPack.BigSampleText, OverworldPack.MountainSampleText })
            StringAssert.DoesNotContain("storms", OverworldMap.ToJson(OverworldMap.Parse(txt)), "旧小镇的 JSON 不多一个字");
    }

    [Test]
    public void S220_StormVolley_Reproducible_InRange_InsideZone()
    {
        var m = StormTown();
        for (int z = 0; z < m.storms.Count; z++)
            for (int v = 0; v < 40; v++)
            {
                var a = OverworldStorm.Volley(m, z, 3, v); var b = OverworldStorm.Volley(m, z, 3, v); var st = m.storms[z];
                Assert.AreEqual(string.Join(";", a), string.Join(";", b), "同一天同一轮 → 同样的落点（可复现）");
                Assert.That(a.Count, Is.InRange(st.min, st.max), "每次劈 最少..最多 道");
                foreach (var c in a) { Assert.That(c.x, Is.InRange(st.x0, st.x1)); Assert.That(c.y, Is.InRange(st.y0, st.y1)); Assert.IsTrue(OverworldMap.Walkable(m, c.x, c.y)); }
            }
        var bad = StormTown(); bad.storms[0].min = 5; bad.storms[0].max = 3;
        Assert.IsFalse(OverworldMap.Check(bad, OverworldMap.Rules.Default).Playable, "最少 > 最多 = 红色错误");
    }

    [Test]
    public void S220_Hearts_Grace_KO_Pickups_Cloud()
    {
        var t = Tuning(); var inp = new OverworldTown.Input(); const float dt = 1f / 30;
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露雷镇", "Town", 1); OverworldSession.Active = true;
        var town = new OverworldTown(StormTown(), t);
        var hurt = typeof(OverworldTown).GetMethod("HurtYou", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        hurt.Invoke(town, null); hurt.Invoke(town, null);
        Assert.AreEqual(2, OverworldSession.YouHearts, "被打后 2.5 秒内不再掉心");
        var hp = OverworldMap.Find(town.map, '+')[0]; town.tx = hp.x + 0.5; town.ty = hp.y + 0.5; town.frozen = 0f; town.Tick(dt, inp);
        Assert.AreEqual(3, OverworldSession.YouHearts, "少了心踩 + 补 1 颗");
        town.tx = 50.5; town.ty = 19.5; town.frozen = 0f; town.Tick(dt, new OverworldTown.Input { weather = true });
        Assert.IsNull(town.cloud, "能量没满不能召唤雷云");
        OverworldSession.Energy = OverworldSession.MaxEnergy; town.Tick(dt, new OverworldTown.Input { weather = true });
        Assert.IsNotNull(town.cloud); Assert.AreEqual(0, OverworldSession.Energy, "召唤后能量清零");
        double cx = town.cloud.x; for (int i = 0; i < 60; i++) town.Tick(dt, inp);
        if (town.cloud != null) Assert.AreEqual(cx, town.cloud.x, "雷云停在召唤的地方（不跟着你）");
        OverworldSession.ResetStatics();
    }

    [Test]
    public void S221_NoStunLock_WaryMarioWaitsOutCloud_BotUsesQ()
    {
        var t = Tuning(); const float dt = 1f / 30;
        var hurt = typeof(OverworldTown).GetMethod("HurtYou", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hit = typeof(OverworldTown).GetMethod("HitMario", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        // 1) 保护期 = 晕 + 站起来后 overworldHurtGraceSeconds：保护期里再挨打不掉心、也不再晕（以前照样晕 → 连控）
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露雷镇", "Town", 1); OverworldSession.Active = true;
        var town = new OverworldTown(StormTown(), t);
        OverworldSession.YouHearts = 1; hurt.Invoke(town, null);
        Assert.AreEqual(1, OverworldSession.Kos); float ko = town.frozen;
        Assert.AreEqual(town.GraceAfter(ko), town.YouGrace, 1e-4f, "保护期 = 晕倒秒数 + 站起来后的秒数");
        for (int i = 0; i < 30 * 3 + 5; i++) town.Tick(dt, new OverworldTown.Input());
        hurt.Invoke(town, null);
        Assert.AreEqual(1, OverworldSession.Kos, "站起来后的保护期里不会被再次击倒");
        Assert.LessOrEqual(town.frozen, 0f, "保护期里也不会再被定身");
        hit.Invoke(town, new object[] { 'i', false }); float g = town.MarioGrace; hit.Invoke(town, new object[] { 'i', false });
        Assert.AreEqual(2, OverworldSession.MarioHearts, "他保护期内不再掉心"); Assert.Greater(g, t.overworldBigStunSeconds, "他的保护期比晕的时间长");
        // 2) 被劈过的他：雷云挡在路上 → 在云外等（WAIT STORM），云散了接着走（H10）
        OverworldSession.ResetStatics(); OverworldSession.NewDay("星露雷镇", "Town", 1); OverworldSession.Active = true;
        town = new OverworldTown(StormTown(), t); OverworldSession.MarioWary.Add('i');
        OverworldSession.Minute = town.stops[0].minute + 2; OverworldSession.Energy = OverworldSession.MaxEnergy;
        for (int i = 0; i < 3; i++) town.Tick(dt, new OverworldTown.Input());
        var route = OverworldMap.Path(town.map, OverworldGuide.Near(town.map, town.mario.x, town.mario.y), town.doorCells[town.stops[0].n]);
        Assume.That(route != null && route.Count > 9);
        var c = route[7]; town.tx = c.x + 0.5; town.ty = c.y + 0.5; town.Tick(dt, new OverworldTown.Input { weather = true });
        Assert.IsNotNull(town.cloud); town.tx = 3.5; town.ty = 2.5;
        bool waited = false; for (int i = 0; i < 30 * 30 && !town.marioInside; i++) { town.Tick(dt, new OverworldTown.Input()); if (town.lastOrder.intent == "WAIT STORM") waited = true; }
        Assert.IsTrue(waited, "吃过亏的他看见雷云挡路会在外面等"); Assert.IsTrue(town.marioInside, "云散了他接着走、照样进门（H10）");
        Assert.AreEqual(0, OverworldSession.MarioHeartsLost, "等在外面 = 不会被劈");
        // 3) 捣蛋型机器人会捡能量、按 Q
        int clouds = 0; for (int d = 1; d <= 2; d++) { OverworldBots.PlayDay(StormTown(), t, OverworldBots.Kind.Prankster, true, d, d); clouds += OverworldSession.Clouds; }
        Assert.Greater(clouds, 0, "捣蛋型会攒满能量按 Q");
        OverworldSession.ResetStatics();
        StringAssert.DoesNotContain("TricksterPossessionGate", Read("Scripts/Overworld/OverworldTown.cs"));
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 22);
    }

    [Test]
    public void Wiring_S220_NoLagEditor_ArtPipeline_Tuning21()
    {
        string w = Read("Scripts/Editor/OverworldWorkshopWindow.cs"), b = Read("Scripts/Editor/OverworldBuilder.cs"), lib = Read("Scripts/Editor/LevelLibrary.cs");
        StringAssert.Contains("Set(x, y, paint); Touch(); }", w);              // 拖动只改格子，不整张检查
        StringAssert.Contains("EditorApplication.delayCall += () => { if (this != null && map != null) Recheck(); }", w); // OnFocus 不在 OnGUI 中途改状态
        StringAssert.Contains("problemCache", b);                              // 房间验证结果缓存
        StringAssert.Contains("Signature(Folder, \"*.txt\")", lib);            // 关卡库列表缓存
        StringAssert.Contains("Resources.Load<Texture2D>(\"OverworldArt/\"", Read("Scripts/Overworld/Runtime/OverworldGame.cs")); // 美术同名换图
        StringAssert.Contains("filterMode = FilterMode.Point", Read("Scripts/Editor/OverworldArtTools.cs"));
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 21);
        var t = Tuning(); Assert.Greater(t.overworldBoltTelegraphSeconds, 0.9f, "闪电预警够看清"); Assert.Greater(t.overworldHurtGraceSeconds, 1f);
    }

    // S222：被抓要说原因（SpyParty 教训）；机器人手抖模式让每个种子真的不一样（统计样本独立）
    [Test]
    public void CaughtReason_UsesSameSightInputs()
    {
        var m = Sample();
        var r = new OverworldMap.SightRules { range = 8, nightRange = 3, halfAngleDeg = 60, nearRadius = 1.2, grassRadius = 1, lampRadius = 2.5, night = true };
        var lamps = new System.Collections.Generic.List<OverworldMap.Cell> { new OverworldMap.Cell(10, 5) };
        Assert.AreEqual(OverworldMap.SeenWhy.Lamp, OverworldMap.WhySeen(m, lamps, 4.5, 5.5, 10.5, 5.5, r, false));
        Assert.AreEqual(OverworldMap.SeenWhy.DisguiseMoved, OverworldMap.WhySeen(m, lamps, 4.5, 5.5, 6.5, 5.5, r, true));
        Assert.AreEqual(OverworldMap.SeenWhy.Near, OverworldMap.WhySeen(m, lamps, 4.5, 5.5, 5.2, 5.5, r, false));
        StringAssert.Contains("路灯", Step1Text.OverworldCaughtWhy(OverworldMap.SeenWhy.Lamp));
        StringAssert.Contains("木箱", Step1Text.OverworldCaughtWhy(OverworldMap.SeenWhy.DisguiseMoved));
    }

    [Test]
    public void Bots_HumanNoise_GivesIndependentDays_AndHiderStillWins()
    {
        var t = Tuning(); var times = new System.Collections.Generic.HashSet<double>(); int full = 0;
        for (int s = 1; s <= 10; s++)
        {
            var r = OverworldBots.PlayDay(Sample(), t, OverworldBots.Kind.Hider, true, s, 1, true);
            times.Add(System.Math.Round(r.realSeconds, 1)); if (r.ambush == r.doors) full++;
            Assert.IsTrue(r.dayEnded);
        }
        OverworldSession.ResetStatics();
        Assert.Greater(times.Count, 2, "手抖模式下不同种子应该玩出不同的一天");
        Assert.AreEqual(10, full, "会躲的玩家每天都能全部埋伏（H10）");
    }

    [Test]
    public void S226_MountainGate4Hill_MudslideReachesMariosRoad()
    {
        var m = OverworldPack.Parse(OverworldPack.MountainSampleText)[0];
        var hill = new OverworldMap.Cell(33, 16);
        Assert.AreEqual('^', m.At(33, 16)); Assert.GreaterOrEqual(OverworldProps.MudDir(m, hill), 0, "门 4 旁的山坡会流泥");
        Assert.Greater(OverworldProps.MudLane(m, hill).Count, 0, "泥冲到大路上（他每天都走）");
    }

    // ── S228：小镇声音能到 '?'（修同帧衰减）、钟楼、房间大炮轰出窗户 ──
    [Test]
    public void S228_OneShotSuspicion_NotEatenBySameFrameDecay()
    {
        var t = Tuning(); var m = new SuspicionMeter(t);
        m.Add(t.curiousThreshold); m.Tick(1f / 60f, 0f);
        Assert.AreEqual(SuspicionLevel.Curious, m.Level, "刚好到阈值的一声也要到 '?'（以前同帧衰减把它抹到阈值以下）");
        m.Tick(1f / 60f, 0f); Assert.Less(m.Value, t.curiousThreshold, "下一帧照常衰减");
        Assert.GreaterOrEqual(t.overworldNoiseSuspicion, t.curiousThreshold + 10f, "大机关的声音到 ? 后还能停一会儿");
        Assert.GreaterOrEqual(t.minOmenSeconds, 0.43f, "? → ! ≥ 二选一反应时间");
        Assert.GreaterOrEqual(t.reactionDelay, 0.19f, "马里奥躲机关不比人快");
    }

    [Test]
    public void S228_BellTower_WholeTownHears_MarioLooks_LearnsIfFooled()
    {
        var m = Big(); var r = OverworldMap.Rules.Default;
        Assert.AreEqual(1, OverworldMap.Find(m, 'B').Count, "星露大镇放了一座钟楼");
        Assert.IsTrue(OverworldMap.Check(m, r).Playable);
        Assert.IsTrue(OverworldProps.IsBig('B'));
        Assert.IsTrue(OverworldTown.BellFooled(100, 99, 4f), "钟响 1 分钟（0.25 秒）内挨砸 = 上当");
        Assert.IsFalse(OverworldTown.BellFooled(100, 70, 4f), "隔了 30 分钟 = 不算");
        var src = Read("Scripts/Overworld/OverworldTown.cs");
        StringAssert.Contains("if (bell && OverworldSession.MarioWary.Contains('B'))", src);
        StringAssert.Contains("if (o.state == OverworldMarioState.Curious) FaceToward(mind.Focus);", src);
        StringAssert.Contains("Step1Readability.TownBellRadius(tuning), true)", src);
        Assert.AreEqual(Tuning().overworldNoiseRange * 2f, Step1Readability.TownBellRadius(Tuning()), 1e-4f, "圈和判定同一个数");
        StringAssert.Contains("钟楼", OverworldCatalog.HarmOf('B'));
    }

    [Test]
    public void S228_RoomCannonHit_FlingsMarioOutOfDoor_SafeLanding()
    {
        var m = Big(); var home = OverworldMap.Find(m, 'M')[0];
        foreach (var d in m.doors)
        {
            var dc = OverworldMap.Find(m, (char)('0' + d.n))[0];
            var land = OverworldProps.WindowLanding(m, dc, 6, home);
            Assert.IsTrue(land.HasValue, "门 " + d.n + " 有落点");
            Assert.IsNotNull(OverworldMap.Path(m, land.Value, home), "H1：落点走得回家");
        }
        var link = Read("Scripts/Overworld/Runtime/OverworldRoomLink.cs");
        StringAssert.Contains("CannonBall.HitMario += NoteCannonHit", link);
        StringAssert.Contains("CannonBall.HitMario -= NoteCannonHit", link);
        StringAssert.Contains("OverworldSession.RecordWindowFling(door)", link);
        OverworldSession.ResetStatics(); OverworldSession.RecordWindowFling(2); Assert.AreEqual(2, OverworldSession.WindowFlingDoor);
        OverworldSession.NewDay("x", "y"); Assert.AreEqual(0, OverworldSession.WindowFlingDoor, "新的一天清掉");
    }

    [Test]
    public void S228_SelfSnare_ShorterThanMario_NoDeadZone()
    {
        var t = Tuning();
        Assert.AreEqual(t.snareSeconds, SnareTrap.HoldFor(true, t.snareSeconds, t.snareSelfSeconds), 1e-4f);
        Assert.Less(SnareTrap.HoldFor(false, t.snareSeconds, t.snareSelfSeconds), 10f, "宪法 P4：你自己不会干等 10 秒");
        Assert.LessOrEqual(SnareTrap.HoldFor(false, 2f, 5f), 2f, "你不会比马里奥吊得久");
    }
}
