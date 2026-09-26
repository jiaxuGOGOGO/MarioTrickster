using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 设计宪法第 1 步：一间手工房 + 冲冲型马里奥。
/// 守住：H2（'?' 先于 '!'）、H4（只凭视锥/遮挡感知、不读附身真值）、H10 前提（火焰平时安全）、
/// 房间合法可达且至少 3 种恶作剧机关。注：需在 Unity Test Runner (EditMode) 中实跑。
/// </summary>
public class Step1RushMarioTests
{
    static MarioMindTuningSO Tuning() => ScriptableObject.CreateInstance<MarioMindTuningSO>();
    static string Read(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative)).Replace("\r\n", "\n"); // CRLF 克隆也能比对
    const float Dt = 0.05f;

    static MarioPercept Seen(Vector2 mario, Vector2 figure, bool prop, bool moving) => new MarioPercept
    { marioPos = mario, seesFigure = true, figurePos = figure, figureLooksLikeProp = prop, figureMoving = moving, scanReady = true };

    // ── H2：起疑条 ─────────────────────────────────────
    [Test]
    public void MeterCannotSkipOmenEvenWithHugeSpike()
    {
        var t = Tuning();
        var m = new SuspicionMeter(t);
        m.Add(1000f);
        m.Tick(Dt, 0f);
        Assert.AreEqual(SuspicionLevel.Curious, m.Level, "一次加满也必须先显示 '?'");
        float time = 0f;
        while (time < t.minOmenSeconds - Dt * 1.5f) { m.Tick(Dt, 1000f); time += Dt; Assert.AreEqual(SuspicionLevel.Curious, m.Level); }
        for (int i = 0; i < 4; i++) m.Tick(Dt, 1000f);
        Assert.AreEqual(SuspicionLevel.Alert, m.Level);
        Assert.AreEqual(1, m.OmenCount);
    }

    [Test]
    public void MeterDecaysBackToCalm()
    {
        var t = Tuning();
        var m = new SuspicionMeter(t);
        m.Add(t.curiousThreshold + 5f); m.Tick(Dt, 0f);
        Assert.AreEqual(SuspicionLevel.Curious, m.Level);
        for (int i = 0; i < 200; i++) m.Tick(Dt, 0f);
        Assert.AreEqual(SuspicionLevel.Calm, m.Level);
        Assert.AreEqual(0f, m.Value, 1e-4f);
    }

    // ── 心智状态机 ─────────────────────────────────────
    [Test]
    public void StillDisguiseIsSafeForever()
    {
        var mind = new RushMarioMind(Tuning());
        for (int i = 0; i < 400; i++)
        {
            var o = mind.Tick(Dt, Seen(Vector2.zero, new Vector2(3f, 0f), prop: true, moving: false));
            Assert.AreEqual(MarioMindState.Running, o.state, "静止的伪装不应引起任何怀疑");
            Assert.IsFalse(o.scan);
        }
    }

    [Test]
    public void MovingDisguiseLeadsToCuriousThenInvestigateAndScan()
    {
        var t = Tuning();
        var mind = new RushMarioMind(t);
        var seen = new List<MarioMindState>();
        mind.StateChanged += (a, b) => seen.Add(b);
        Vector2 prop = new Vector2(2f, 0f);
        bool scanned = false;
        for (int i = 0; i < 200 && !scanned; i++)
        {
            bool moving = mind.State == MarioMindState.Running || mind.State == MarioMindState.Curious;
            var o = mind.Tick(Dt, Seen(Vector2.zero, prop, prop: true, moving: moving));
            scanned |= o.scan;
        }
        Assert.GreaterOrEqual(seen.Count, 2);
        Assert.AreEqual(MarioMindState.Curious, seen[0], "H2：第一步必须是 '?'");
        Assert.AreEqual(MarioMindState.Investigating, seen[1]);
        Assert.IsTrue(scanned, "调查到附近必须扫描（扫描结果为真，H5）");
    }

    [Test]
    public void SeeingTheTricksterLeadsToChaseThenSearchThenRunning()
    {
        var t = Tuning();
        var mind = new RushMarioMind(t);
        var seen = new List<MarioMindState>();
        mind.StateChanged += (a, b) => seen.Add(b);
        Vector2 figure = new Vector2(4f, 0f);
        for (int i = 0; i < 60 && mind.State != MarioMindState.Chasing; i++) mind.Tick(Dt, Seen(Vector2.zero, figure, false, true));
        Assert.AreEqual(MarioMindState.Chasing, mind.State);
        Assert.AreEqual(MarioMindState.Curious, seen[0], "H2：追逐前必须先有 '?'");
        var o = mind.Tick(Dt, Seen(Vector2.zero, figure, false, true));
        Assert.AreEqual(figure, o.moveTarget.Value);

        for (int i = 0; i < 200 && mind.State == MarioMindState.Chasing; i++) mind.Tick(Dt, new MarioPercept { marioPos = Vector2.zero });
        Assert.AreEqual(MarioMindState.Searching, mind.State, "跟丢后去最后看见的位置找");
        for (int i = 0; i < 200 && mind.State == MarioMindState.Searching; i++) mind.Tick(Dt, new MarioPercept { marioPos = Vector2.zero });
        Assert.AreEqual(MarioMindState.Running, mind.State, "找不到就回去拿宝/回家");
    }

    [Test]
    public void CatchIsRequestedOnlyWhenSeenAndClose()
    {
        var t = Tuning();
        var mind = new RushMarioMind(t);
        for (int i = 0; i < 60 && mind.State != MarioMindState.Chasing; i++) mind.Tick(Dt, Seen(Vector2.zero, new Vector2(4f, 0f), false, true));
        Assert.IsFalse(mind.Tick(Dt, Seen(Vector2.zero, new Vector2(4f, 0f), false, true)).tryCatch);
        Assert.IsTrue(mind.Tick(Dt, Seen(Vector2.zero, new Vector2(t.catchRadius * 0.5f, 0f), false, true)).tryCatch);
        mind.OnCaught();
        Assert.AreEqual(MarioMindState.Running, mind.State);
        Assert.AreEqual("GOTCHA!", mind.Tick(Dt, new MarioPercept()).mark);
    }

    [Test]
    public void HurtShowsOuchAndRaisesSuspicion()
    {
        var mind = new RushMarioMind(Tuning());
        var o = mind.Tick(Dt, new MarioPercept { hurt = true });
        Assert.AreEqual("OUCH!", o.mark);
        Assert.Greater(mind.Meter.Value, 0f);
    }

    // ── H4：视锥与遮挡 ────────────────────────────────
    [Test]
    public void ConeRespectsFacingRangeAndNearSense()
    {
        var t = Tuning();
        Assert.IsTrue(MarioVision.InCone(Vector2.zero, true, new Vector2(5f, 0f), t.visionRange, t.visionHalfAngle, t.nearSenseRadius));
        Assert.IsFalse(MarioVision.InCone(Vector2.zero, false, new Vector2(5f, 0f), t.visionRange, t.visionHalfAngle, t.nearSenseRadius), "背后看不见");
        Assert.IsFalse(MarioVision.InCone(Vector2.zero, true, new Vector2(t.visionRange + 1f, 0f), t.visionRange, t.visionHalfAngle, t.nearSenseRadius), "太远看不见");
        Assert.IsFalse(MarioVision.InCone(Vector2.zero, true, new Vector2(0.5f, 5f), t.visionRange, t.visionHalfAngle, t.nearSenseRadius), "视锥外看不见");
        Assert.IsTrue(MarioVision.InCone(Vector2.zero, false, new Vector2(t.nearSenseRadius * 0.5f, 0f), t.visionRange, t.visionHalfAngle, t.nearSenseRadius), "贴身能察觉");
    }

    // [AI防坑警告] EditMode 测试跑在"当前打开的场景"里。刚生成的恶作剧房间正好铺在原点附近（墙 x=0、地面 y=0..2），
    // 在原点做视线测试会被房间几何挡住（S180 用户实测失败的根因）。几何夹具必须放到远离任何关卡的坐标。
    static readonly Vector2 FarAway = new Vector2(48000f, 48000f);

    [Test]
    public void WallBlocksSightButOneWayPlatformDoesNot()
    {
        var t = Tuning();
        var wall = new GameObject("Step1_Wall");
        var shelf = new GameObject("Step1_Shelf");
        try
        {
            Vector2 eye = FarAway;
            Physics2D.SyncTransforms();
            Assert.IsTrue(MarioVision.CanSee(eye, true, eye + new Vector2(4f, 0f), null, t), "夹具区必须是空的，否则测试被场景污染");

            wall.transform.position = eye + new Vector2(2f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 3f);
            Physics2D.SyncTransforms();
            Assert.IsFalse(MarioVision.CanSee(eye, true, eye + new Vector2(4f, 0f), null, t), "墙后看不见");
            Object.DestroyImmediate(wall); wall = null;

            shelf.transform.position = eye + new Vector2(2f, 1f);
            var col = shelf.AddComponent<BoxCollider2D>();
            col.size = new Vector2(3f, 0.25f);
            shelf.AddComponent<PlatformEffector2D>().useOneWay = true;
            col.usedByEffector = true;
            Physics2D.SyncTransforms();
            Assert.IsTrue(MarioVision.CanSee(eye, true, eye + new Vector2(4f, 2f), null, t), "单向台面不挡视线");
        }
        finally { if (wall != null) Object.DestroyImmediate(wall); Object.DestroyImmediate(shelf); }
    }

    // ── H4：源码契约 ──────────────────────────────────
    static readonly string[] Forbidden =
    {
        "TricksterPossessionGate", "CurrentAnchor", "IsHiddenAndArmed", "IsFullyBlended", "DisguiseSystem",
        "TricksterPossessionState", "CanBePossessed", "PossessionAnchor"
    };

    [Test]
    public void MindAndDriverNeverReadTricksterTruth()
    {
        foreach (string file in new[] { "Scripts/Gameplay/Step1/RushMarioMind.cs", "Scripts/Gameplay/Step1/MarioMindDriver.cs",
                                        "Scripts/Gameplay/Step1/SuspicionMeter.cs", "Scripts/Gameplay/Step1/MarioVision.cs",
                                        "Scripts/Gameplay/Step1/MarioEyes.cs" })
        {
            string src = Read(file);
            foreach (string token in Forbidden) StringAssert.DoesNotContain(token, src, $"[H4] {file} 读取了 {token}");
        }
        string mind = Read("Scripts/Gameplay/Step1/RushMarioMind.cs");
        StringAssert.DoesNotContain("TricksterController", mind, "[H4] 心智只能吃 MarioPercept");
        StringAssert.DoesNotContain("FindObjectOfType", mind);
        StringAssert.DoesNotContain("IsDisguised", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
    }

    [Test]
    public void EyesCheckSightBeforeReadingAppearance()
    {
        string eyes = Read("Scripts/Gameplay/Step1/MarioEyes.cs");
        int see = eyes.IndexOf("MarioVision.CanSee(eye, facingRight, pos");
        int look = eyes.IndexOf("figure.IsDisguised");
        Assert.GreaterOrEqual(see, 0); Assert.Greater(look, see, "[H4] 必须先判定看得见，才读外观");
        Assert.AreEqual(1, CountOf(eyes, "IsDisguised"), "外观只读一次，且在 CanSee 之内");
    }

    static int CountOf(string s, string token) { int n = 0, i = 0; while ((i = s.IndexOf(token, i)) >= 0) { n++; i += token.Length; } return n; }

    // ── 房间与规则 ────────────────────────────────────
    [Test]
    public void RoomIsValidReachableAndHasThreePrankKinds()
    {
        string report = Step1PrankRoomBuilder.Validate(out bool ok);
        Assert.IsTrue(ok, report);
        string ascii = Step1PrankRoomBuilder.RoomAscii;
        foreach (char c in "MTGo") Assert.AreEqual(1, CountOf(ascii, c.ToString()), "需要且只能有一个 " + c);
        Assert.GreaterOrEqual(CountOf(ascii, "~"), 2, "火焰");
        Assert.GreaterOrEqual(CountOf(ascii, "["), 1, "封路");
        Assert.GreaterOrEqual(CountOf(ascii, "C"), 1, "崩塌桥");
        foreach (char banned in "^PB") Assert.AreEqual(0, CountOf(ascii, banned.ToString()), "第 1 步房间不放 " + banned);
        foreach (string row in Step1PrankRoomBuilder.Room) Assert.AreEqual(Step1PrankRoomBuilder.Room[0].Length, row.Length);
    }

    [Test]
    public void FireTrapsAreIdleSafeInTheBuilder()
    {
        Assert.Greater(Step1PrankRoomBuilder.IdleSafeFireCoolOff, 10000f);
        string builder = Read("Scripts/Editor/Step1PrankRoomBuilder.cs");
        StringAssert.Contains("FindProperty(\"coolOffDuration\").floatValue = IdleSafeFireCoolOff", builder);
        StringAssert.Contains("follow.enabled = false", builder, "第 1 步不能再跟随马里奥");
    }

    [Test]
    public void PrankKindsMapPropTypes()
    {
        Assert.AreEqual("", Step1PlaytestLog.PrankKindOf(null));
        var dict = new Dictionary<string, int> { { "Fire", 2 }, { "Blocker", 1 } };
        var survey = new Step1RoundSurvey(true);
        survey.AnswerYesNo(true); survey.AnswerYesNo(false); survey.AnswerNumber(3); survey.AnswerNumber(5); survey.SubmitNote("he, jumped\nlate");
        string row = Step1PlaytestLog.CsvRow(new System.DateTime(2026, 1, 1), 3, "Trickster", "a,b", 12.3f, 2, 1, 4, 2, dict, survey);
        StringAssert.Contains("Blocker:1 Fire:2", row);
        StringAssert.Contains("a;b", row);
        StringAssert.Contains("yes,no,unfair:couldnt_read_him,5,he; jumped late,0", row);
        Assert.AreEqual(Step1PlaytestLog.CsvHeader.Split(',').Length, row.Split(',').Length);
    }

    // ── S181：宪法第 3 层每局问卷 ──────────────────────
    [Test]
    public void SurveySkipsCaughtQuestionWhenNeverCaught()
    {
        var s = new Step1RoundSurvey(false);
        Assert.AreEqual(Step1RoundSurvey.Step.Calculated, s.Current);
        Assert.IsFalse(s.AnswerNumber(4), "是/否题不接受数字");
        s.AnswerYesNo(true); s.AnswerYesNo(true);
        Assert.AreEqual(Step1RoundSurvey.Step.WantAgain, s.Current, "没被抓就不问服不服气");
        Assert.IsFalse(s.AnswerNumber(0)); Assert.IsFalse(s.AnswerNumber(6));
        s.AnswerNumber(2);
        Assert.AreEqual(2, s.WantAgain);
        s.SubmitNote("   ");
        Assert.IsTrue(s.IsDone);
        Assert.AreEqual("", s.Note);
        Assert.AreEqual("", s.CaughtVerdict);
    }

    [Test]
    public void SurveyCaughtReasonsMatchConstitutionTags()
    {
        // 宪法第 3 层：服气 + 四个不服气原因（没预兆 / 看不懂他 / 手滑 / 我露馅）
        Assert.AreEqual(5, Step1RoundSurvey.CaughtVerdicts.Length);
        var s = new Step1RoundSurvey(true);
        s.AnswerYesNo(false); s.AnswerYesNo(false);
        Assert.AreEqual(Step1RoundSurvey.Step.CaughtVerdict, s.Current);
        s.AnswerNumber(2);
        Assert.AreEqual("unfair:no_warning", s.CaughtVerdict);
    }

    [Test]
    public void RoundOverKeysAreBlockedDuringSurveyInSource()
    {
        string gm = Read("Scripts/Core/GameManager.cs");
        StringAssert.Contains("BlockRoundOverKeys == null || !BlockRoundOverKeys()", gm, "问卷未答完时 R/N 不能开下一局");
        StringAssert.Contains("GameManager.BlockRoundOverKeys = () => awaitingRating", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs"));
    }

    // ── S181：H10 无干预检查 + 每回合机关复位 ────────────
    [Test]
    public void HandsOffClearCountsOnlyLootAndHome()
    {
        var rs = new List<Step1HandsOffCheck.RoundResult>
        {
            new Step1HandsOffCheck.RoundResult { winner = "Mario", hadLoot = true },
            new Step1HandsOffCheck.RoundResult { winner = "Trickster", hadLoot = true },
            new Step1HandsOffCheck.RoundResult { winner = "Mario", hadLoot = false },
        };
        Assert.AreEqual(1, Step1HandsOffCheck.MarioClears(rs));
    }

    [Test]
    public void BuilderInstallsRoundResetAndHandsOffCheck()
    {
        string builder = Read("Scripts/Editor/Step1PrankRoomBuilder.cs");
        StringAssert.Contains("AddComponent<Step1RoomReset>().SetBuiltVersion(BuilderVersion)", builder);
        StringAssert.Contains("AddComponent<Step1HandsOffCheck>()", builder);
        Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 3, "改了房间生成逻辑必须升版本，旧场景才会自动重建");
        StringAssert.Contains("AddComponent<Step1Screen>()", builder, "S182：第 1 步必须装干净界面");
        StringAssert.Contains("LevelElementRegistry.ResetAll()", Read("Scripts/Gameplay/Step1/Step1RoomReset.cs"),
            "每回合必须走 OnLevelReset，否则上一局正在喷的火会一直烧");
    }

    [Test]
    public void HandsOffCheckNeverTouchesMarioDecisions()
    {
        string src = Read("Scripts/Gameplay/Step1/Step1HandsOffCheck.cs");
        foreach (string token in new[] { "ExplorationTarget", "RushMarioMind", "MarioMindDriver", "SetInputProvider" })
            StringAssert.DoesNotContain(token, src, "H10 检查只能观察，不能帮马里奥");
    }

    [Test]
    public void WholeRoomCameraFitsTheEntireRoom()
    {
        var room = new Rect(-0.5f, -0.5f, 36f, 10f);
        Step1RoomCamera.Compute(Step1CameraMode.WholeRoom, room, 16f / 9f, 0.6f, 9f, Vector2.zero, Vector2.one, out Vector2 c, out float size);
        Assert.AreEqual(room.center.x, c.x, 1e-3f);
        Assert.GreaterOrEqual(size * 16f / 9f * 2f, room.width, "整屏必须横向装下整间房");
        Assert.GreaterOrEqual(size * 2f, room.height);
        Step1RoomCamera.Compute(Step1CameraMode.FollowTrickster, room, 16f / 9f, 0.6f, 9f, Vector2.zero, new Vector2(30f, 3f), out Vector2 c2, out float size2);
        Assert.Less(size2, size + 1e-3f);
        Assert.Greater(c2.x, room.center.x, "跟随模式镜头朝捣蛋者移动");
    }

    [Test]
    public void LivesEndTheRoundAfterThreeCatches()
    {
        var t = Tuning();
        var go = new GameObject("Step1_Trickster");
        var mgr = new GameObject("Step1_Lives");
        try
        {
            go.AddComponent<Rigidbody2D>(); go.AddComponent<BoxCollider2D>();
            var figure = go.AddComponent<TricksterController>();
            var lives = mgr.AddComponent<TricksterLives>();
            lives.Configure(t, figure, null);
            Assert.AreEqual(3, lives.Lives);
            Assert.IsFalse(lives.TryCatch(new Vector2(10f, 0f)), "太远抓不到");
            Assert.IsTrue(lives.TryCatch(Vector2.zero));
            Assert.AreEqual(2, lives.Lives);
            Assert.IsTrue(lives.IsInvulnerable);
            Assert.IsFalse(lives.TryCatch(Vector2.zero), "重生无敌期内不能连抓");
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(mgr); }
    }

    // ── S182：清爽中英界面 ──────────────────────────────
    [Test]
    public void OutcomeClassificationMatchesRealReasonStrings()
    {
        // 原因字符串来自 GameManager / TricksterLives 源码，改了那边这里会失败，提醒同步文案。
        StringAssert.Contains("Time ran out", Read("Scripts/Core/GameManager.cs"));
        StringAssert.Contains("Health depleted", Read("Scripts/Core/GameManager.cs"));
        StringAssert.Contains("Trickster caught", Read("Scripts/Gameplay/Step1/TricksterLives.cs"));
        Assert.AreEqual(Step1Text.Outcome.MarioEscaped, Step1Text.Classify("Mario", "Route cleared. Try a new timing or an alternate route."));
        Assert.AreEqual(Step1Text.Outcome.TricksterCaughtOut, Step1Text.Classify("Mario", "Trickster caught 3 times."));
        Assert.AreEqual(Step1Text.Outcome.TimeUp, Step1Text.Classify("Trickster", "Time ran out. Try a shorter route or a longer timer."));
        Assert.AreEqual(Step1Text.Outcome.MarioKnockedOut, Step1Text.Classify("Trickster", "Health depleted. Observe the hazard before committing."));
        Assert.AreEqual(Step1Text.Outcome.HandsOffTimeout, Step1Text.Classify("Trickster", Step1Text.HandsOffTimeoutReason));
        Assert.IsTrue(Step1Text.PlayerWon(Step1Text.Outcome.TimeUp));
        Assert.IsFalse(Step1Text.PlayerWon(Step1Text.Outcome.MarioEscaped));
    }

    [Test]
    public void SurveyButtonsWalkTheSameSteps()
    {
        var s = new Step1RoundSurvey(true);
        Assert.AreEqual(5, s.StepCount); Assert.AreEqual(1, s.StepNumber);
        Assert.AreEqual(2, s.Options.Length);
        Assert.IsTrue(s.Choose(0)); Assert.AreEqual(true, s.Calculated);
        Assert.IsTrue(s.Choose(1)); Assert.AreEqual(false, s.NearMiss);
        Assert.AreEqual(5, s.Options.Length, "服气 + 4 个宪法原因");
        Assert.IsTrue(s.Choose(3)); Assert.AreEqual("unfair:slipped", s.CaughtVerdict);
        Assert.AreEqual(5, s.Options.Length);
        Assert.IsTrue(s.Choose(4)); Assert.AreEqual(5, s.WantAgain);
        Assert.AreEqual(0, s.Options.Length, "一句话步骤用输入框");
        Assert.AreEqual(5, s.StepNumber);

        var n = new Step1RoundSurvey(false);
        n.Choose(0); n.Choose(0);
        Assert.AreEqual(Step1RoundSurvey.Step.WantAgain, n.Current);
        Assert.AreEqual(3, n.StepNumber); Assert.AreEqual(4, n.StepCount);
        StringAssert.Contains("\n", n.Prompt, "中英两行");
    }

    [Test]
    public void EveryMarioStateHasBilingualText()
    {
        foreach (MarioMindState st in System.Enum.GetValues(typeof(MarioMindState)))
        {
            string line = Step1Text.MarioStateText(st, false, false);
            Assert.IsNotEmpty(line);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(line, "[\u4e00-\u9fff]") && System.Text.RegularExpressions.Regex.IsMatch(line, "[A-Za-z]"), st + " 需中英对照");
            StringAssert.Contains("\n", Step1Text.HeadIntent(st, ""));
        }
    }

    [Test]
    public void HandsOffDescribeSaysStuckOrCleared()
    {
        StringAssert.Contains("通关", Step1HandsOffCheck.Describe(new Step1HandsOffCheck.RoundResult { winner = "Mario", hadLoot = true, seconds = 9f }));
        StringAssert.Contains("卡住", Step1HandsOffCheck.Describe(new Step1HandsOffCheck.RoundResult { winner = "Trickster", reason = Step1Text.HandsOffTimeoutReason }));
    }

    [Test]
    public void CleanScreenHidesLegacyHudOnlyVisually()
    {
        string src = Read("Scripts/Gameplay/Step1/Step1Screen.cs");
        StringAssert.Contains("GlobalGameUICanvas", src);
        StringAssert.Contains("LootEscapeHUD", src);
        foreach (string token in new[] { "ExplorationTarget", "RushMarioMind", "SetInputProvider", "TryCatch" })
            StringAssert.DoesNotContain(token, src, "界面层不能碰玩法/马里奥决策");
    }

    [Test]
    public void RoundResetSkipsAbsentTrickster()
    {
        // S182 用户实测：H10 检查第 2 局 NullReferenceException at TricksterController.ResetForNewRound
        StringAssert.Contains("trickster.gameObject.activeInHierarchy", Read("Scripts/Core/GameManager.cs"));
        StringAssert.Contains("if (rb == null) return;", Read("Scripts/Enemy/TricksterController.cs"));
    }

    // ── S183：用户试玩反馈（马里奥被关坑里 / 太快 / 机关拦不住）───────
    [Test]
    public void BridgeClearAreaCoversThePitButNotTheFloorAbove()
    {
        // 桥格 (12..15, 2)，碰撞体 1×0.4；坑空格在 y=1；地面上站着的马里奥中心约 y=3。
        CollapsingPlatform.ClearBelowArea(new Vector2(13.5f, 2f), new Vector2(4f, 0.4f), 2f, 4.5f, out Vector2 c, out Vector2 size);
        var area = new Rect(c - size * 0.5f, size);
        foreach (float x in new[] { 12f, 13f, 14f, 15f, 16f })
            Assert.IsTrue(area.Contains(new Vector2(x, 1f)), "坑里 x=" + x + " 必须被检测到");
        Assert.IsFalse(area.Contains(new Vector2(10f, 3f)), "坑边地面上的人不应阻止重生");
        Assert.LessOrEqual(area.yMax, 2.21f, "检测区不能高过桥面");
    }

    [Test]
    public void BuilderMakesBridgePlayerOnlyAndSafe()
    {
        string builder = Read("Scripts/Editor/Step1PrankRoomBuilder.cs");
        StringAssert.Contains("\"collapseOnStep\").boolValue = false", builder, "马里奥踩桥不应自己塌（H10）");
        StringAssert.Contains("\"waitForClearBelow\").boolValue = true", builder, "桥下有人不重生（H9）");
        StringAssert.Contains("ConfigureBlockers(root, tuning)", builder);
        Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 4);
        // 其他场景默认行为不变
        string bridge = Read("Scripts/LevelElements/Platforms/CollapsingPlatform.cs");
        StringAssert.Contains("private bool collapseOnStep = true;", bridge);
        StringAssert.Contains("private bool waitForClearBelow = false;", bridge);
    }

    [Test]
    public void TrapHurtStunsMarioBriefly()
    {
        var t = Tuning();
        Assert.Greater(t.hurtStunSeconds, 0f);
        var mind = new RushMarioMind(t);
        var o = mind.Tick(Dt, new MarioPercept { hurt = true, marioPos = new Vector2(5f, 3f) });
        Assert.IsTrue(mind.IsStunned);
        Assert.AreEqual(new Vector2(5f, 3f), o.moveTarget.Value, "发晕时站住");
        Assert.IsFalse(o.scan); Assert.IsFalse(o.tryCatch);
        for (float time = 0f; time < t.hurtStunSeconds + 0.1f; time += Dt) mind.Tick(Dt, new MarioPercept { marioPos = new Vector2(5f, 3f) });
        Assert.IsFalse(mind.IsStunned, "晕完恢复");
    }

    [Test]
    public void SlowerMarioIsDataDrivenAndDefaultBotUnchanged()
    {
        var t = Tuning();
        Assert.Less(t.marioSpeedScale, 1f);
        Assert.GreaterOrEqual(t.startDelaySeconds, 4f);
        Assert.AreEqual(1f, new HeuristicBotInputProvider().MarioSpeedScale, "其他场景的 Bot 默认原速");
        StringAssert.Contains("hybrid.Bot.MarioSpeedScale = tuning.marioSpeedScale", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
    }

    [Test]
    public void OldTuningAssetGetsUpgradedOnce()
    {
        var t = Tuning();
        t.dataVersion = 0; t.startDelaySeconds = 2f; t.marioSpeedScale = 1f; t.seeTricksterPerSecond = 150f;
        Assert.IsTrue(t.UpgradeData());
        Assert.AreEqual(MarioMindTuningSO.CurrentDataVersion, t.dataVersion);
        Assert.AreEqual(4f, t.startDelaySeconds);
        t.startDelaySeconds = 6f; // 用户之后手动调参
        Assert.IsFalse(t.UpgradeData());
        Assert.AreEqual(6f, t.startDelaySeconds, "不覆盖手动调参");
    }

    // ── S184：更大的房间 + 遮挡 ─────────────────────────
    [Test]
    public void RoomHasSpaceAndCoverForTheCatAndMouseGame()
    {
        var room = Step1PrankRoomBuilder.Room;
        Assert.GreaterOrEqual(room[0].Length, 44, "出口到宝物的距离要足够埋伏");
        float marioToLoot = Step1PrankRoomBuilder.CellOf('o').x - Step1PrankRoomBuilder.CellOf('M').x;
        Assert.GreaterOrEqual(marioToLoot, 36f);
        int standRow = room.Length - 1 - 3;
        int crates = 0; foreach (char c in room[standRow]) if (c == '#') crates++;
        Assert.GreaterOrEqual(crates, 2, "站立层至少 2 个挡视线的箱子");
        // 高墙：同一列从 y4 往上连续都是墙，且 y3 是门洞（封路墙）
        int tallWalls = 0;
        for (int x = 1; x < room[0].Length - 1; x++)
        {
            bool tall = room[standRow][x] == '[';
            for (int y = 4; y <= 8 && tall; y++) tall = room[room.Length - 1 - y][x] == 'W';
            if (tall) tallWalls++;
        }
        Assert.GreaterOrEqual(tallWalls, 2, "至少两道带门洞的高墙把房间分区");
    }

    [Test]
    public void SignsFollowTheTemplate()
    {
        Assert.AreEqual(new Vector2(2f, 3f), Step1PrankRoomBuilder.CellOf('G'));
        StringAssert.DoesNotContain("new Vector2(32f, 8.3f)", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"), "标牌位置不能写死");
    }

    // ── S185：连招 + 反制更顺 ───────────────────────────
    [Test]
    public void ComboCounterChainsWithinWindowOnly()
    {
        var c = new Step1ComboCounter(4f);
        Assert.AreEqual(1, c.Register(0f));
        Assert.AreEqual(2, c.Register(3f));
        Assert.AreEqual(3, c.Register(6.5f));
        Assert.AreEqual(1, c.Register(11f), "超过窗口重新计");
        Assert.AreEqual(3, c.Max);
        c.Reset();
        Assert.AreEqual(0, c.Max);
        Assert.AreEqual(1, c.Register(100f));
    }

    [Test]
    public void ComboStunIsCapped()
    {
        var t = Tuning();
        var mind = new RushMarioMind(t);
        mind.Tick(Dt, new MarioPercept { hurt = true });
        mind.ExtendStun(10f, t.maxStunSeconds);
        float stunned = 0f;
        while (mind.IsStunned && stunned < 20f) { mind.Tick(Dt, new MarioPercept()); stunned += Dt; }
        Assert.LessOrEqual(stunned, t.maxStunSeconds + Dt * 2f, "不能无限控");
    }

    [Test]
    public void TrapTelegraphDecisionIsOneShotAndOptIn()
    {
        var bot = new HeuristicBotInputProvider();
        Assert.Less(bot.TrapCommitDistance, 0f, "其他场景保持旧行为");
        Assert.IsFalse(bot.HoldStill);
        Assert.IsFalse(bot.SkipReactionDelayForTerrain);
        string src = Read("Scripts/Core/HeuristicBotInputProvider.cs");
        StringAssert.Contains("if (_trapDecisionProp != prop)", src, "每次预警只决定一次，不再每帧重掷导致抖动");
        string driver = Read("Scripts/Gameplay/Step1/MarioMindDriver.cs");
        StringAssert.Contains("hybrid.Bot.HoldStill = Mind.IsStunned", driver);
        StringAssert.Contains("hybrid.Bot.TrapCommitDistance = tuning.trapCommitDistance", driver);
    }

    [Test]
    public void ComboLayerDoesNotReadTricksterTruth()
    {
        string src = Read("Scripts/Gameplay/Step1/Step1Combo.cs");
        foreach (string token in new[] { "TricksterController", "IsDisguised", "IsFullyBlended", "TricksterPossessionGate" })
            StringAssert.DoesNotContain(token, src, "H4：连招只看马里奥自己和机关事件");
        StringAssert.Contains("AddComponent<Step1Combo>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
    }

    // ── S186：从头顶跳过去太容易逃脱 ─────────────────────
    [Test]
    public void ChaseFollowsTheDirectionItLastSawYouRun()
    {
        var t = Tuning();
        var mind = new RushMarioMind(t);
        Vector2 mario = new Vector2(10f, 3f);
        // 看见本体 → 追
        MarioOrder o = default;
        for (int i = 0; i < 40 && mind.State != MarioMindState.Chasing; i++)
            o = mind.Tick(Dt, new MarioPercept { marioPos = mario, seesFigure = true, figurePos = new Vector2(12f, 3f), scanReady = true });
        Assert.AreEqual(MarioMindState.Chasing, mind.State);
        // 最后一眼：它正从头顶往左跑（速度 -8）
        mind.Tick(Dt, new MarioPercept { marioPos = mario, seesFigure = true, figurePos = new Vector2(10f, 4.5f), figureVelocity = new Vector2(-8f, 0f) });
        // 跟丢 0.5 秒后，追踪点应该在它原位置的左边（转身追），而不是停在原地
        for (int i = 0; i < 10; i++) o = mind.Tick(Dt, new MarioPercept { marioPos = mario });
        Assert.AreEqual(MarioMindState.Chasing, mind.State);
        Assert.Less(o.moveTarget.Value.x, 10f - 2f, "应往它逃跑的方向追");
        Assert.AreEqual(4.5f, o.moveTarget.Value.y, 1e-3f, "只推算水平方向");
    }

    [Test]
    public void ChaseSpeedIsFasterButStillEscapable()
    {
        var t = Tuning();
        Assert.Greater(t.chaseSpeedScale, t.marioSpeedScale, "追逐时要提速");
        Assert.Less(t.chaseSpeedScale * 9f, 8f, "马里奥追逐速度仍略慢于捣蛋者 8 格/秒：能甩掉，但要跑");
        StringAssert.Contains("Mind.State == MarioMindState.Chasing ? tuning.chaseSpeedScale", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
        StringAssert.Contains("p.figureVelocity = velocity;", Read("Scripts/Gameplay/Step1/MarioEyes.cs"), "速度只在 CanSee 之后由所见位置算出");
    }
}
