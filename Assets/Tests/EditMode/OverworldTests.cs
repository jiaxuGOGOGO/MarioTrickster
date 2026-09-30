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
}
