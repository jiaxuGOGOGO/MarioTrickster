using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using System.Linq;
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

    /// <summary>去掉 // 与 /// 注释，只留代码。</summary>
    static string CodeOnly(string src)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var line in src.Split('\n'))
        {
            int i = line.IndexOf("//", System.StringComparison.Ordinal);
            sb.AppendLine(i >= 0 ? line.Substring(0, i) : line);
        }
        return sb.ToString();
    }

    static int CountOf(string s, string token) { int n = 0, i = 0; while ((i = s.IndexOf(token, i)) >= 0) { n++; i += token.Length; } return n; }

    // ── 房间与规则 ────────────────────────────────────
    [Test]
    public void RoomIsValidReachableAndHasThreePrankKinds()
    {
        string report = Step1PrankRoomBuilder.ValidateAllVariants(out bool ok);
        Assert.IsTrue(ok, report);
        string ascii = Step1PrankRoomBuilder.RoomAscii;
        foreach (char c in "MTGo") Assert.AreEqual(1, CountOf(ascii, c.ToString()), "需要且只能有一个 " + c);
        Assert.GreaterOrEqual(CountOf(string.Join("\n", Step1PrankRoomBuilder.Room), "~"), 2, "固定火焰");
        Assert.GreaterOrEqual(CountOf(ascii, "K") + CountOf(ascii, "k"), 1, "大炮");
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
        StringAssert.Contains("AddComponent<Step1RoomReset>()", builder);
        StringAssert.Contains("marker.SetBuiltVersion(BuilderVersion)", builder);
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
        StringAssert.Contains("tuning.marioSpeedScale) * roundSpeedFactor", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"), "巡逻速度来自调参数据");
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
        // S187：藏身处 = 箱子 c / 草丛 b / 随机槽位 1（箱子或草丛，必有其一）
        int cover = 0; foreach (char c in room[standRow]) if (c == 'c' || c == 'b' || c == '1') cover++;
        Assert.GreaterOrEqual(cover, 3, "站立层至少 3 个藏身处（任何一局都保证有）");
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

    // ── S187：大炮 / 场景摆件 / 受控随机 / 主题 ─────────────────
    [Test]
    public void EveryRandomLayoutVariantIsValidAndReachable()
    {
        // 宪法 H1：随机只改藏身处与可选火，任何组合都必须可达
        string report = Step1PrankRoomBuilder.ValidateAllVariants(out bool ok);
        Assert.IsTrue(ok, report);
        StringAssert.Contains("layout variants valid", report);
    }

    [Test]
    public void LayoutPickIsDeterministicAndVaries()
    {
        var room = Step1PrankRoomBuilder.Room;
        CollectionAssert.AreEqual(Step1Layout.Resolve(room, 42), Step1Layout.Resolve(room, 42), "同种子必须可复现");
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 64; seed++) seen.Add(string.Join("|", Step1Layout.Resolve(room, seed)));
        Assert.Greater(seen.Count, 4, "不同种子要真的产生不同布局");
        foreach (string row in Step1Layout.Resolve(room, 7))
            foreach (char ch in row) Assert.IsFalse(Step1Layout.Slots.ContainsKey(ch), "解析后不能残留槽位字符");
        string runtime = Read("Scripts/Gameplay/Step1/Step1LayoutVariants.cs");
        StringAssert.Contains("Step1Layout.Pick(seed, options)", runtime, "运行时与测试共用同一个随机算法");
    }

    [Test]
    public void NewElementsAreRegisteredWithoutTouchingGeneratorCore()
    {
        var registry = AsciiElementRegistry.GetDefault();
        foreach (char c in "Kkcbd") Assert.IsNotNull(registry.GetEntry(c), "未登记: " + c);
        Assert.IsTrue(registry.GetEntry('c').isSolid, "箱子挡路");
        Assert.IsTrue(registry.GetEntry('b').isTrigger, "草丛可穿过");
        CollectionAssert.Contains(registry.GetEntry('b').componentTypeNames, "SightBlocker");
        CollectionAssert.Contains(registry.GetEntry('k').componentTypeNames, "CannonFacesLeft");
        // 只查代码行（注释里的字符表说明允许提到新元素）
        string gen = CodeOnly(Read("Scripts/LevelDesign/AsciiLevelGenerator.cs"));
        foreach (string banned in new[] { "SpawnCannon", "SpawnCrate", "SpawnBush", "PranksterCannon", "SceneryProp" })
            StringAssert.DoesNotContain(banned, gen, "零代码新增元素：生成器核心代码不得出现新元素专属逻辑");
        var theme = ScriptableObject.CreateInstance<LevelThemeProfile>();
        foreach (string key in new[] { "Cannon", "Crate", "Bush", "Decor" })
            Assert.IsTrue(System.Array.Exists(theme.elementSprites, m => m.elementKey == key), "主题缺少换图插槽 " + key);
    }

    [Test]
    public void BushBlocksSightUnlessViewerIsInside()
    {
        var bush = new GameObject("Bush_Test");
        var target = new GameObject("Target");
        try
        {
            Vector2 at = FarAway + new Vector2(100f, 0f);
            bush.transform.position = at + new Vector2(2f, 0f);
            var col = bush.AddComponent<BoxCollider2D>(); col.isTrigger = true; col.size = new Vector2(1f, 1.2f);
            bush.AddComponent<SightBlocker>();
            target.transform.position = at + new Vector2(4f, 0f);
            Physics2D.SyncTransforms();
            Assert.IsFalse(MarioSuspicionTracker.CanWitness(at, target.transform.position, 8f, target.transform), "草丛挡视线");
            Assert.IsTrue(MarioSuspicionTracker.CanWitness(at + new Vector2(2f, 0f), target.transform.position, 8f, target.transform), "走进草丛就看得见");
            Object.DestroyImmediate(bush.GetComponent<SightBlocker>());
            Physics2D.SyncTransforms();
            Assert.IsTrue(MarioSuspicionTracker.CanWitness(at, target.transform.position, 8f, target.transform), "普通触发器仍不挡视线（旧规则不变）");
        }
        finally { Object.DestroyImmediate(bush); Object.DestroyImmediate(target); }
    }

    [Test]
    public void CannonHasOneShotThenHumanLaunchAndIsPlayerOnly()
    {
        var go = new GameObject("Cannon_Test");
        try
        {
            go.transform.position = FarAway + new Vector2(200f, 0f);
            go.AddComponent<BoxCollider2D>();
            var cannon = go.AddComponent<PranksterCannon>();
            cannon.Configure(true, 1);
            Assert.IsTrue(cannon.HasAmmo);
            Assert.IsTrue(cannon.CanBeControlled(), "有炮弹才能开炮");
            Assert.IsFalse(cannon.CanHumanLaunch, "有炮弹时不能人肉发射");
            cannon.Configure(true, 0);
            Assert.IsFalse(cannon.CanBeControlled(), "没炮弹不能开炮");
            Assert.IsTrue(cannon.CanHumanLaunch, "打完可以当逃跑工具");
            cannon.OnLevelReset();
            Assert.IsFalse(cannon.HasAmmo, "0 发配置复位后仍是 0");
        }
        finally { Object.DestroyImmediate(go); }
        Vector2 v = PranksterCannon.LaunchVelocity(false, 20f, 40f);
        Assert.Less(v.x, 0f, "朝左炮往左飞"); Assert.Greater(v.y, 0f, "斜上方");
        Assert.AreEqual(20f, v.magnitude, 1e-3f);
        string ball = Read("Scripts/LevelElements/Traps/CannonBall.cs");
        StringAssert.Contains("GetComponentInParent<TricksterController>() != null) return;", ball, "玩家自己的炮不伤自己");
        StringAssert.Contains("TakeDamage", ball);
    }

    [Test]
    public void RustleNeverTellsMarioTheCause()
    {
        string eyes = Read("Scripts/Gameplay/Step1/MarioEyes.cs");
        StringAssert.Contains("MarioVision.CanSee(eye, facingRight, where, pendingRustle, t)", eyes, "只在看得见草丛时才知道晃了");
        string rustle = Read("Scripts/LevelElements/Props/RustleOnPass.cs");
        StringAssert.Contains("public static event Action<Transform> Rustled;", rustle, "只广播位置，不广播原因");
        float wind = RustleOnPass.NextWind(new System.Random(1), 7f, 16f);
        Assert.That(wind, Is.InRange(7f, 16f));
        Assert.IsTrue(float.IsPositiveInfinity(RustleOnPass.NextWind(new System.Random(1), 7f, 0f)), "≤0 关闭起风");
    }

    [Test]
    public void PersonalityRandomnessIsSeededAndBounded()
    {
        var t = Tuning();
        for (int seed = 0; seed < 50; seed++)
        {
            float f = RushMarioMind.RoundSpeedFactor(seed, t.roundSpeedVariance);
            Assert.That(f, Is.InRange(1f - t.roundSpeedVariance - 1e-4f, 1f + t.roundSpeedVariance + 1e-4f));
            Assert.AreEqual(f, RushMarioMind.RoundSpeedFactor(seed, t.roundSpeedVariance), "同种子同结果");
        }
        var a = new RushMarioMind(t); var b = new RushMarioMind(t);
        a.Reset(99); b.Reset(99);
        t.glanceChancePerSecond = 5f;
        for (int i = 0; i < 40; i++)
        {
            var p = new MarioPercept { marioPos = new Vector2(i * 0.1f, 0f), facingRight = true };
            Assert.AreEqual(a.Tick(Dt, p).intent, b.Tick(Dt, p).intent, "同种子行为一致（可复现）");
        }
    }

    [Test]
    public void ThemePresetsOnlyRecolorAndStayNullSafe()
    {
        Assert.IsNull(ThemePresets.Create(ThemePresets.Whitebox), "白盒 = 不换肤");
        Assert.IsNull(ThemePresets.Create("NotATheme"));
        foreach (string name in new[] { ThemePresets.AmusementPark, ThemePresets.CityPark, ThemePresets.MountainPark })
        {
            var p = ThemePresets.Create(name);
            Assert.IsNotNull(p, name);
            Assert.IsNull(p.GetElementSprite("Crate"), "预设只给颜色，Sprite 留空给美术（空插槽保留白盒）");
            Assert.IsTrue(p.GetElementColor("Bush").HasValue);
        }
        Assert.IsTrue(ThemePresets.IsKnown(Tuning().themePreset), "默认主题必须是已知预设");
    }

    // ── S188：元素说明书 / 摆放检查 / 美术插槽一致 / 减法 ──────────
    [Test]
    public void EveryRegisteredCharHasACatalogEntryWithMatchingThemeKey()
    {
        var reg = AsciiElementRegistry.GetDefault();
        foreach (char c in reg.GetAllRegisteredChars())
        {
            var info = ElementCatalog.Get(c);
            Assert.IsNotNull(info, $"'{c}' 已登记但元素说明书里没有说明（新增元素必须写说明）");
            Assert.AreEqual(reg.GetEntry(c).elementName, info.themeKey, $"'{c}' 主题键必须等于 Registry 名（物体名前缀），美术拖图才能对上");
            Assert.IsNotEmpty(info.zh); Assert.IsNotEmpty(info.what);
        }
        foreach (var info in ElementCatalog.All)
            Assert.IsNotNull(reg.GetEntry(info.ch), $"说明书里的 '{info.ch}' 没有在 Registry 登记");
    }

    [Test]
    public void EveryNonTerrainElementHasAThemeSlot()
    {
        var theme = ScriptableObject.CreateInstance<LevelThemeProfile>();
        var skip = new HashSet<string> { "Ground", "Platform", "Wall", "Air", "Space", "MarioSpawn", "TricksterSpawn" };
        var missing = new List<string>();
        foreach (var info in ElementCatalog.All)
        {
            if (skip.Contains(info.themeKey)) continue;
            if (!System.Array.Exists(theme.elementSprites, m => m.elementKey == info.themeKey)) missing.Add(info.themeKey);
        }
        CollectionAssert.IsEmpty(missing, "这些元素在主题里没有换图插槽：" + string.Join(",", missing));
    }

    [Test]
    public void PlacementCheckCatchesCommonMistakes()
    {
        System.Func<char, bool> solid = AsciiElementRegistry.GetDefault().IsSolid;
        var floating = new[] { "W.....W", "W..c..W", "W.....W", "W#####W" };
        Assert.IsTrue(ElementCatalog.PlacementIssues(floating, true, solid).Exists(i => i.Contains("脚下不是实心")), "悬空箱子");
        var blocked = new[] { "W.....W", "WK#...W", "W#####W" };
        Assert.IsTrue(ElementCatalog.PlacementIssues(blocked, true, solid).Exists(i => i.Contains("炮口")), "炮口贴墙");
        var twoLoot = new[] { "W.o.o.W", "W#####W" };
        Assert.IsTrue(ElementCatalog.PlacementIssues(twoLoot, true, solid).Exists(i => i.Contains("只能有 1 个")), "两个宝物");
        var spikes = new[] { "W..^..W", "W#####W" };
        Assert.IsTrue(ElementCatalog.PlacementIssues(spikes, true, solid).Exists(i => i.Contains("第 1 步")), "第 1 步不用地刺");
        Assert.IsEmpty(ElementCatalog.PlacementIssues(spikes, false, solid), "其他关卡允许地刺");
        var good = new[] { "W......W", "WK...c.W", "W######W" };
        Assert.IsEmpty(ElementCatalog.PlacementIssues(good, true, solid));
    }

    [Test]
    public void Step1RoomPassesPlacementCheckForEveryVariant()
    {
        string report = Step1PrankRoomBuilder.ValidateAllVariants(out bool ok);
        Assert.IsTrue(ok, report);
        StringAssert.Contains("ElementCatalog.PlacementIssues", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"), "构建器必须跑摆放检查");
    }

    [Test]
    public void LabelsResolveGeneratedNamesToCatalog()
    {
        Assert.AreEqual("Cannon", Step1ElementLabels.KeyOf("Cannon_18_3"));
        Assert.AreEqual("Collectible", Step1ElementLabels.KeyOf("LootObjective_Collectible_44_3"));
        Assert.AreEqual("GoalZone", Step1ElementLabels.KeyOf("EscapeGate_GoalZone_2_3"));
        Assert.IsNotNull(ElementCatalog.ByKey(Step1ElementLabels.KeyOf("Bush_7_3")));
    }

    [Test]
    public void LegendListsEveryElementAndStep1StripsUnusedLegacy()
    {
        string legend = ElementLegendExporter.Build();
        foreach (var info in ElementCatalog.All)
            if (info.ch != '.' && info.ch != ' ') StringAssert.Contains("`" + info.ch + "`", legend);
        string builder = Read("Scripts/Editor/Step1PrankRoomBuilder.cs");
        StringAssert.Contains("StripUnusedLegacy(gm.gameObject)", builder, "减法：第 1 步房间不跑用不到的旧系统");
        foreach (var t in Step1PrankRoomBuilder.Step1Unused)
            StringAssert.DoesNotContain(t.Name, CodeOnly(Read("Scripts/Gameplay/Step1/MarioEyes.cs")), "第 1 步马里奥感知不依赖被移除的系统");
    }

    // ── S189：死局分析 / 防卡死 / 关卡工坊 ─────────────────────
    static string[] Filled(string[] room) { var r = new string[room.Length]; for (int i = 0; i < room.Length; i++) r[i] = room[i].Replace('1', '.').Replace('2', '.').Replace('3', '.'); return r; }

    [Test]
    public void DeadlockAnalyzerPassesTheDefaultRoom()
    {
        foreach (var variant in LevelWorkshopModel.Variants(Step1PrankRoomBuilder.Room))
        {
            var report = LevelDeadlockAnalyzer.Analyze(variant);
            Assert.IsFalse(report.HasErrors, report.Summary() + "\n" + string.Join("\n", report.issues));
            Assert.Greater(report.standingCellsChecked, 20);
        }
    }

    [Test]
    public void DeadlockAnalyzerFindsPitWithoutExitAndWalledLoot()
    {
        var room = Filled(Step1PrankRoomBuilder.Room);
        int h = room.Length;
        // 1) 深坑（3 格深，超过 2.5 格跳跃高度）上架塌桥：塌掉后掉进去就出不来 → 死局；给坑里加单向台面 → 解除
        var deep = new[] { "WWWWWWWWWWWWWWWWWWWW", "W..................W", "W..................W", "W.G.M.........T..o.W",
                           "W########CCCC######W", "W########....######W", "W########....######W" };
        var r1 = LevelDeadlockAnalyzer.Analyze(deep);
        Assert.IsTrue(r1.HasErrors, "深坑塌桥必须报死局");
        Assert.IsTrue(r1.issues.Exists(i => i.message.Contains("塌桥")));
        var fixedPit = (string[])deep.Clone(); fixedPit[5] = "W########-...######W";
        Assert.IsFalse(LevelDeadlockAnalyzer.Analyze(fixedPit).HasErrors, "坑里有单向台面就能跳出来");
        // 2) 用高墙把宝物围起来：走不到宝物
        var walled = (string[])room.Clone();
        for (int y = 3; y <= 10; y++) { var rr = walled[h - 1 - y].ToCharArray(); rr[43] = 'W'; walled[h - 1 - y] = new string(rr); }
        var r2 = LevelDeadlockAnalyzer.Analyze(walled);
        Assert.IsTrue(r2.issues.Exists(i => i.message.Contains("走不到宝物")));
    }

    [Test]
    public void BlockerIsTemporaryNotDeadlock()
    {
        var report = LevelDeadlockAnalyzer.Analyze(Filled(Step1PrankRoomBuilder.Room));
        Assert.IsFalse(report.HasErrors);
        Assert.Greater(report.temporaryCells.Count, 0, "封路墙升起会暂时堵路（提示），但不是死局");
    }

    [Test]
    public void ReachableFromDoesNotChangeOriginalL2()
    {
        string ascii = Step1PrankRoomBuilder.RoomAscii;
        Assert.IsTrue(LevelReachabilityAnalyzer.Analyze(ascii).IsReachable);
        var cells = LevelReachabilityAnalyzer.ReachableFrom(ascii, 4, 3);
        Assert.IsTrue(cells.Contains(LevelReachabilityAnalyzer.CellKey(2, 3)), "从马里奥出生点能到出口");
        Assert.IsTrue(LevelReachabilityAnalyzer.Analyze(ascii).IsReachable, "收集模式结束后不影响原 L2");
    }

    [Test]
    public void BridgeRespawnRechecksBeforeColliderReturns()
    {
        string bridge = Read("Scripts/LevelElements/Platforms/CollapsingPlatform.cs");
        int respawning = bridge.IndexOf("case CollapseState.Respawning:");
        int recheck = bridge.IndexOf("if (waitForClearBelow && IsSomeoneBelow())", respawning);
        Assert.Greater(recheck, respawning, "渐显结束、碰撞体打开前必须再查一次桥下有没有人（S189 用户实测被封住）");
    }

    [Test]
    public void StuckRescueIsInstalledAndOnlyJudgesWhileRushing()
    {
        StringAssert.Contains("AddComponent<Step1StuckRescue>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        string src = Read("Scripts/Gameplay/Step1/Step1StuckRescue.cs");
        StringAssert.Contains("driver.Mind.State != MarioMindState.Running", src, "起疑/查看/找人时站着是正常表演");
        foreach (string token in new[] { "TricksterController", "IsDisguised", "TryCatch" })
            StringAssert.DoesNotContain(token, CodeOnly(src), "H4：救援不读捣蛋者");
        var t = Tuning();
        Assert.Greater(t.stuckSeconds, 3f);
    }

    [Test]
    public void WorkshopPaletteFollowsCatalogAndStep1Mode()
    {
        var step1 = LevelWorkshopModel.Palette(true).SelectMany(g => g.items).Select(i => i.ch).ToList();
        CollectionAssert.Contains(step1, 'K'); CollectionAssert.Contains(step1, 'b'); CollectionAssert.Contains(step1, '[');
        CollectionAssert.DoesNotContain(step1, '^', "第 1 步调色板不给地刺");
        var all = LevelWorkshopModel.Palette(false).SelectMany(g => g.items).Select(i => i.ch).ToList();
        CollectionAssert.Contains(all, '^');
        foreach (var info in ElementCatalog.All) if (info.ch != '.' && info.ch != ' ') CollectionAssert.Contains(all, info.ch, "说明书里的元素都能在工坊里找到");
    }

    [Test]
    public void WorkshopCheckMarksCellsAndNewRoomIsPlayable()
    {
        System.Func<char, bool> solid = AsciiElementRegistry.GetDefault().IsSolid;
        var fresh = LevelWorkshopModel.NewRoom(48, 12).Split('\n');
        var ok = LevelWorkshopModel.Check(fresh, true, solid);
        Assert.IsTrue(ok.Playable, ok.Headline + "\n" + string.Join("\n", ok.general) + string.Join("\n", ok.cells.Select(c => c.text)));
        var bad = (string[])fresh.Clone();
        var row = bad[4].ToCharArray(); row[20] = 'c'; bad[4] = new string(row); // 悬空箱子
        var r = LevelWorkshopModel.Check(bad, true, solid);
        Assert.IsFalse(r.Playable);
        Assert.IsTrue(r.cells.Exists(c => c.x == 20 && c.error), "问题要落在具体格子上");
        var def = LevelWorkshopModel.Check(Step1PrankRoomBuilder.Room, true, solid);
        Assert.IsTrue(def.Playable, "默认房间在工坊里也必须通过（含全部随机组合）");
    }

    [Test]
    public void DocumentAcceptsRandomSlotsAndLootMovesInsteadOfDuplicating()
    {
        Assert.IsTrue(LevelStudioDocument.TryParse("M1TGo\n#####", out var doc, out string err), err);
        doc.Paint(1, 1, 'o');
        Assert.AreEqual(1, doc.Grid.Count(c => c == 'o'), "宝物是唯一元素，画第二个 = 移动");
        doc.Paint(2, 1, '3');
        Assert.AreEqual('3', doc.Cell(2, 1));
        Assert.IsFalse(LevelStudioDocument.TryParse("M?TG\n####", out _, out _), "未知字符仍拒绝");
    }

    [Test]
    public void CustomRoomRebuildsWhenChanged()
    {
        string builder = Read("Scripts/Editor/Step1PrankRoomBuilder.cs");
        StringAssert.Contains("marker.BuiltRoomHash == RoomHash(Current)", builder, "改了自定义房间，▶ Play 必须自动重建");
        StringAssert.Contains("ValidateAllVariants(Current, out bool ok)", builder, "构建前检查的是实际要玩的房间");
        Assert.AreNotEqual(Step1PrankRoomBuilder.RoomHash(new[] { "W.M" }), Step1PrankRoomBuilder.RoomHash(new[] { "W.T" }));
    }

    [Test]
    public void WorkshopShortcutDoesNotClashWithUnityOrProjectMenus()
    {
        // S190 用户实测：Ctrl+Shift+L 与 Unity 自带 Assets/Generate Lighting 冲突
        string src = Read("Scripts/Editor/LevelWorkshopWindow.cs");
        StringAssert.DoesNotContain("%#l\"", src);
        StringAssert.Contains("%&w\"", src);
        // 项目内其它菜单不得占用同一组合
        foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts/Editor"), "*.cs"))
            if (!file.EndsWith("LevelWorkshopWindow.cs"))
                StringAssert.DoesNotContain("%&w\"", File.ReadAllText(file), Path.GetFileName(file));
    }

    // ── S191：编辑器看得清 + 美术换图不用手调尺寸 ─────────────────
    [Test]
    public void EveryPaletteColorHasReadableGlyph()
    {
        foreach (var info in ElementCatalog.All)
        {
            if (info.ch == '.' || info.ch == ' ') continue;
            var bg = ElementCatalog.EditorColor(info.ch); bg.a = 1f;
            float ratio = ElementCatalog.ContrastRatio(bg, ElementCatalog.TextColorOn(bg));
            Assert.GreaterOrEqual(ratio, 3.0f, $"'{info.ch}' {info.zh} 的字符看不清（对比度 {ratio:F2}）");
        }
        var mario = ElementCatalog.EditorColor('M');
        Assert.Greater(mario.r, 0.5f, "出生点不再是白色：马里奥红");
        Assert.Greater(ElementCatalog.EditorColor('T').b, 0.5f, "捣蛋者蓝");
        Assert.AreEqual(new Color(0.08f, 0.08f, 0.1f), ElementCatalog.TextColorOn(new Color(1f, 0.85f, 0.2f)), "亮黄底用黑字");
    }

    [Test]
    public void EveryArtElementHasAFitRule()
    {
        foreach (var info in ElementCatalog.All)
        {
            if (info.fit == ElementCatalog.ArtFit.None) continue;
            StringAssert.DoesNotContain("—", ElementCatalog.SuggestedPixels(info.ch), info.zh + " 需要建议尺寸");
        }
        Assert.AreEqual(ElementCatalog.ArtFit.Tile, ElementCatalog.Get('#').fit);
        Assert.AreEqual(ElementCatalog.ArtFit.Tile, ElementCatalog.Get('C').fit, "桥是连续多格，平铺");
        Assert.AreEqual(ElementCatalog.ArtFit.Fit, ElementCatalog.Get('c').fit, "箱子等比放入不变形");
        Assert.AreEqual(ElementCatalog.ArtFit.None, ElementCatalog.Get('M').fit);
        Assert.AreEqual("32×32", ElementCatalog.SuggestedPixels('c'));
    }

    [Test]
    public void ContainFitKeepsAspectAndBoxIsUntouched()
    {
        Assert.AreEqual(0.5f, SpriteAutoFit.ContainScale(new Vector2(2f, 1f), new Vector2(1f, 1f)), 1e-4f, "宽图按宽缩");
        Assert.AreEqual(0.8f, SpriteAutoFit.ContainScale(new Vector2(1f, 1.5f), new Vector2(0.9f, 1.2f)), 1e-4f, "高图按高缩");
        Assert.AreEqual(0f, ArtReadinessCheck.AspectDeviation(new Vector2(2f, 2f), Vector2.one), 1e-4f);
        Assert.Greater(ArtReadinessCheck.AspectDeviation(new Vector2(3f, 1f), Vector2.one), ArtReadinessCheck.AspectTolerance);

        // 视碰分离：Contain 只改 Visual，Root 碰撞体不变
        var root = new GameObject("Crate_1_1");
        try
        {
            var box = root.AddComponent<BoxCollider2D>(); box.size = Vector2.one;
            var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(64, 32);
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f), 32f);
            var fit = visual.AddComponent<SpriteAutoFit>();
            Assert.IsNull(visual.GetComponent<BoxCollider2D>(), "SpriteAutoFit 不能在 Visual 上自动加碰撞体");
            fit.SetFitMode(SpriteAutoFit.FitMode.Contain);
            fit.SetDisplayBox(Vector2.one, true);
            Assert.AreEqual(0.5f, visual.transform.localScale.x, 1e-3f, "2×1 的图放进 1×1：等比缩到 0.5");
            Assert.AreEqual(visual.transform.localScale.x, visual.transform.localScale.y, 1e-4f, "不变形");
            Assert.AreEqual(Vector2.one, box.size, "碰撞体不动");
            float bottom = visual.transform.localPosition.y - 0.5f * 0.5f;
            Assert.AreEqual(-0.5f, bottom, 1e-3f, "底边贴地");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ThemeAndApplyArtShareTheSameFitPath()
    {
        StringAssert.Contains("FitThemedSprite(child.gameObject, sr, elementKey)", Read("Scripts/LevelDesign/AsciiLevelGenerator.cs"), "主题换肤按说明书贴法适配");
        StringAssert.Contains("AsciiLevelGenerator.TryFitCatalogVisual(target, sr)", Read("Scripts/Editor/AssetApplyToSelected.cs"), "单个换皮与主题换肤同一套适配");
        StringAssert.DoesNotContain("[RequireComponent(typeof(BoxCollider2D))]", CodeOnly(Read("Scripts/Core/SpriteAutoFit.cs")));
    }

    // ── S192：编辑卡顿 / 每帧分配 ─────────────────────────
    [Test]
    public void WorkshopPaintsWithQuickCheckAndDefersFullCheck()
    {
        string win = Read("Scripts/Editor/LevelWorkshopWindow.cs");
        StringAssert.Contains("LevelWorkshopModel.QuickCheck(", win, "画的时候只跑快速检查");
        StringAssert.Contains("EditorApplication.update += Tick", win, "完整检查停笔后在编辑器节拍里跑");
        StringAssert.Contains("if (next != hoverCell)", win, "只在换格子时重画");
        StringAssert.DoesNotContain("new GUIStyle(cellLabel) { normal = { textColor = ElementCatalog.TextColorOn(bg)", win, "格子字不能每帧 new GUIStyle");
        var room = Step1PrankRoomBuilder.Room;
        System.Func<char, bool> solid = AsciiElementRegistry.GetDefault().IsSolid;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) LevelWorkshopModel.QuickCheck(room, true, solid);
        Assert.Less(sw.ElapsedMilliseconds / 20.0, 20.0, "快速检查必须足够快（画一格 < 20ms）");
    }

    [Test]
    public void FullCheckDedupesVariantsByPhysics()
    {
        var sigs = new HashSet<string>();
        int variants = 0;
        foreach (var v in LevelWorkshopModel.Variants(Step1PrankRoomBuilder.Room)) { variants++; sigs.Add(LevelWorkshopModel.PhysicsSignature(v)); }
        Assert.Less(sigs.Count, variants, "草丛/火等不影响物理的组合要合并，只查不同的物理布局");
        StringAssert.Contains("if (!seenPhysics.Add(signature)) continue;", Read("Scripts/Editor/LevelWorkshopModel.cs"));
        // 去重后结论不变：默认房间通过，坏房间仍能被抓到（见 DeadlockAnalyzerFindsPitWithoutExitAndWalledLoot）
        Assert.IsTrue(LevelWorkshopModel.Check(Step1PrankRoomBuilder.Room, true, AsciiElementRegistry.GetDefault().IsSolid).Playable);
    }

    [Test]
    public void PerFrameCodePathsDoNotAllocate()
    {
        string tracker = CodeOnly(Read("Scripts/Gameplay/MarioSuspicionTracker.cs"));
        StringAssert.DoesNotContain("Physics2D.LinecastAll(", tracker, "视线检测每帧多次调用，不能用会分配数组的 LinecastAll");
        StringAssert.DoesNotContain("Physics2D.RaycastAll(", CodeOnly(Read("Scripts/Gameplay/Step1/MarioVisionConeView.cs")));
        StringAssert.Contains("textCache", Read("Scripts/Gameplay/Step1/Step1Gui.cs"), "界面文字样式要缓存");
        // 视线结果与改动前一致（墙挡、单向台面不挡、草丛挡）由 WallBlocksSightButOneWayPlatformDoesNot / BushBlocksSightUnlessViewerIsInside 守护
    }

    // ── S193：连招手感 + 弹簧板 / 裂缝地板（可破坏地形）─────────────
    [Test]
    public void ComboFeelFollowsFightingGameRules()
    {
        var t = Tuning();
        float h1 = Step1ComboFeel.HitstopSeconds(1, t.hitstopBaseSeconds, t.hitstopPerStepSeconds, t.hitstopMaxSeconds);
        float h3 = Step1ComboFeel.HitstopSeconds(3, t.hitstopBaseSeconds, t.hitstopPerStepSeconds, t.hitstopMaxSeconds);
        float h9 = Step1ComboFeel.HitstopSeconds(9, t.hitstopBaseSeconds, t.hitstopPerStepSeconds, t.hitstopMaxSeconds);
        Assert.Greater(h3, h1, "段数越高顿帧越久");
        Assert.LessOrEqual(h9, t.hitstopMaxSeconds, "顿帧有上限（不拖慢节奏）");
        Assert.LessOrEqual(h9, 0.25f, "顿帧必须很短（< 0.25 秒）");
        float b2 = Step1ComboFeel.BonusStun(2, t.comboBonusStunSeconds, t.comboStunScaling);
        float b3 = Step1ComboFeel.BonusStun(3, t.comboBonusStunSeconds, t.comboStunScaling);
        Assert.AreEqual(0f, Step1ComboFeel.BonusStun(1, t.comboBonusStunSeconds, t.comboStunScaling));
        Assert.Less(b3, b2, "递减硬直：越往后追加越少（防无限控，H9）");
        Assert.AreNotEqual(Step1ComboFeel.TierName(2), Step1ComboFeel.TierName(5), "段位名随连招升级");
        var c = new Step1ComboCounter(4f);
        c.Register(0f, "hurt"); c.Register(1f, "hurt");
        int same = c.Score;
        var d = new Step1ComboCounter(4f);
        d.Register(0f, "hurt"); d.Register(1f, "launch");
        Assert.Greater(d.Score, same, "换不同机关比重复同一招分高");
        StringAssert.Contains("combo_score", Step1PlaytestLog.CsvHeader);
    }

    [Test]
    public void HitstopAndShakeStayOutOfAutomationAndPause()
    {
        string hs = CodeOnly(Read("Scripts/Gameplay/Step1/Step1Hitstop.cs"));
        StringAssert.Contains("Step1HandsOffCheck.IsRunning", hs, "H10 自动检查期间不顿帧");
        StringAssert.Contains("Step1Screen.HelpOpen", hs, "帮助/暂停时不顿帧");
        StringAssert.Contains("Time.unscaledTime", hs, "用真实时间计时，顿帧自己能结束");
        StringAssert.Contains("Step1HandsOffCheck.IsRunning", CodeOnly(Read("Scripts/Gameplay/Step1/Step1RoomCamera.cs")));
        foreach (string token in new[] { "TricksterController", "IsDisguised", "IsFullyBlended" })
        {
            StringAssert.DoesNotContain(token, CodeOnly(Read("Scripts/Gameplay/Step1/Step1PrankEvents.cs")), "H4：机关事件不带捣蛋者信息");
            StringAssert.DoesNotContain(token, CodeOnly(Read("Scripts/LevelElements/Pranks/SpringPad.cs")));
            StringAssert.DoesNotContain(token, CodeOnly(Read("Scripts/LevelElements/Pranks/CrackFloor.cs")));
        }
    }

    [Test]
    public void SpringAndCrackAreZeroCodeElementsWithTelegraph()
    {
        var reg = AsciiElementRegistry.GetDefault();
        Assert.IsTrue(reg.IsSolid('J') && reg.IsSolid('x'), "平时都是实心可站（平时安全，H10）");
        Assert.IsNotNull(ElementCatalog.Get('J')); Assert.IsNotNull(ElementCatalog.Get('x'));
        Assert.AreEqual(ElementCatalog.Role.PlayerPrank, ElementCatalog.Get('J').role);
        string gen = CodeOnly(Read("Scripts/LevelDesign/AsciiLevelGenerator.cs"));
        StringAssert.DoesNotContain("SpringPad", gen, "零代码扩展：不改生成器核心");
        StringAssert.DoesNotContain("CrackFloor", gen);
        var t = Tuning();
        Assert.Greater(t.springTelegraphSeconds, 0f, "H3：有预警");
        Assert.Greater(t.crackTelegraphSeconds, 0f, "H3：有预警");
        // 弹高：约 5–6 格，房间 12 行放得下
        float apex = SpringPad.ApexHeight(t.springLaunchSpeed, 24f);
        Assert.Greater(apex, 3f); Assert.Less(apex, 8f);
        System.Func<char, bool> solid = reg.IsSolid;
        Assert.IsTrue(ElementCatalog.PlacementIssues(new[] { "W..W..W", "W..J..W", "W#####W" }, true, solid).Exists(i => i.Contains("弹簧板")), "弹簧板头顶贴天花板要报");
        // 裂缝沿同一行蔓延
        var cells = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0), new Vector2(5, 0), new Vector2(1, 3) };
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, CrackFloor.ContiguousLine(cells, 1));
    }

    [Test]
    public void CrackFloorIsPermanentInDeadlockCheck()
    {
        var bad = new[] { "WWWWWWWWWWWWWWWWWWWW", "W..................W", "W..................W", "W.G.M.........T..o.W",
                          "W#########xxx######W", "W#########...######W", "W#########...######W", "W##################W" };
        var r = LevelDeadlockAnalyzer.Analyze(bad);
        Assert.IsTrue(r.HasErrors, "裂缝地板打开后掉进深坑出不来 = 死局");
        var stair = (string[])bad.Clone(); stair[5] = "W#########J..######W";
        Assert.IsFalse(LevelDeadlockAnalyzer.Analyze(stair).HasErrors, "坑里有台阶就能出来");
        // 默认房间（含弹簧板 + 裂缝地板 + 地下室）所有随机组合都没有死局
        var check = LevelWorkshopModel.Check(Step1PrankRoomBuilder.Room, true, reg().IsSolid);
        Assert.IsTrue(check.Playable, check.Headline);
        StringAssert.Contains("J", Step1PrankRoomBuilder.RoomAscii);
        StringAssert.Contains("x", Step1PrankRoomBuilder.RoomAscii);
    }
    [Test]
    public void PrisonSampleIsPlayableVerticalEscape()
    {
        var p = LevelWorkshopModel.PrisonSample;
        foreach (var row in p) Assert.AreEqual(p[0].Length, row.Length);
        string all = string.Join("\n", p);
        StringAssert.Contains("x", all, "样板房演示裂缝地板（楼层之间）");
        var check = LevelWorkshopModel.Check(p, true, reg().IsSolid);
        Assert.IsTrue(check.Playable, "两层监狱样板必须通过全部检查（含裂缝打开后的死局检查）：" + check.Headline);
        StringAssert.Contains("PrisonSample", Read("Scripts/Editor/LevelWorkshopWindow.cs"), "工坊工具条有'样板：两层监狱'按钮");
    }

    [Test]
    public void BananaPeelIsTelegraphedReadableSlide()
    {
        var t = Tuning();
        Assert.IsFalse(reg().IsSolid('n'), "香蕉皮可穿过（平时就是装饰）");
        Assert.AreEqual(ElementCatalog.Role.PlayerPrank, ElementCatalog.Get('n').role);
        Assert.Greater(t.bananaTelegraphSeconds, 0f, "H3：有预警");
        float d = BananaPeel.SlideDistance(t.bananaSlideSpeed, t.bananaSlipSeconds);
        Assert.GreaterOrEqual(d, 2f); Assert.LessOrEqual(d, 5f, "H8：滑行距离可读可预判（2–5 格）");
        StringAssert.DoesNotContain("TricksterController", CodeOnly(Read("Scripts/LevelElements/Pranks/BananaPeel.cs")), "H4");
        StringAssert.Contains("BananaPeelEvents.Slipped += HandleSlipped", Read("Scripts/Gameplay/Step1/Step1Combo.cs"), "滑倒计入连招");
    }

    [Test]
    public void ComboRoutesShowWhichTrapsChain()
    {
        var room = Step1PrankRoomBuilder.Room;
        var r = ComboRouteAnalyzer.Analyze(room, Tuning().comboRouteCells);
        Assert.GreaterOrEqual(r.BestGroupSize, 3, "默认房间至少有一套 3 个机关能连");
        Assert.GreaterOrEqual(r.BestGroupKinds, 3, "且至少 3 种不同机关（宪法：≥3 种坑法）");
        var far = new[] { "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW", "W.G.M..~......................~..o...W", "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW" };
        Assert.AreEqual(0, ComboRouteAnalyzer.Analyze(far, 10f).links.Count, "离得太远的机关不连线");
        Assert.GreaterOrEqual(ComboRouteAnalyzer.Analyze(LevelWorkshopModel.PrisonSample, 10f).BestGroupKinds, 3, "样板房也要能连招");
        StringAssert.Contains("DrawComboRoutes", Read("Scripts/Editor/LevelWorkshopWindow.cs"), "工坊有'连招路线'开关");
    }

    // ── S195：多层楼 / 监狱塔 ─────────────────────────
    [Test]
    public void FloorPlannerFindsStairsInEveryTowerAndSkipsFlatRooms()
    {
        Assert.IsFalse(LevelPathPlanner.NeedsPlanning(Step1PrankRoomBuilder.Room), "默认恶作剧房间（同层）不启用 → 马里奥行为完全不变");
        Assert.IsTrue(LevelPathPlanner.NeedsPlanning(LevelWorkshopModel.PrisonSample));
        for (int floors = 2; floors <= FloorStacker.MaxFloors; floors++)
            for (int seed = 0; seed < 4; seed++)
            {
                var g = FloorStacker.Build(floors, seed);
                var m = CellOfIn(g, 'M'); var o = CellOfIn(g, 'o'); var e = CellOfIn(g, 'G');
                Assert.IsNotNull(LevelPathPlanner.Path(g, m, o), $"{floors} 层 seed {seed}：马里奥找得到下到宝物的路");
                Assert.IsNotNull(LevelPathPlanner.Path(g, o, e), $"{floors} 层 seed {seed}：拿宝后找得到爬回出口的路");
            }
        var p = LevelPathPlanner.Path(LevelWorkshopModel.PrisonSample, CellOfIn(LevelWorkshopModel.PrisonSample, 'M'), CellOfIn(LevelWorkshopModel.PrisonSample, 'o'));
        var w = LevelPathPlanner.NextWaypoint(p);
        Assert.AreNotEqual(p[p.Count - 1].x, w.x, "路点是'先去楼梯口'，不是直冲宝物");
        foreach (string token in new[] { "TricksterController", "IsDisguised", "Trickster" })
            StringAssert.DoesNotContain(token, CodeOnly(Read("Scripts/LevelDesign/LevelPathPlanner.cs")), "H4：寻路只看地形和自己的目标");
        StringAssert.Contains("so.FindProperty(\"roomGrid\")", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
    }

    [Test]
    public void PrisonTowersArePlayableForEverySeed()
    {
        System.Func<char, bool> solid = reg().IsSolid;
        for (int floors = 2; floors <= FloorStacker.MaxFloors; floors += 3)
            for (int seed = 0; seed < 3; seed++)
            {
                var g = FloorStacker.Build(floors, seed);
                Assert.LessOrEqual(g.Length, LevelStudioDocument.MaxHeight, "塞得进工坊画布");
                var c = LevelWorkshopModel.Check(g, true, solid);
                Assert.IsTrue(c.Playable, $"{floors} 层 seed {seed}: {c.Headline}");
            }
        CollectionAssert.AreEqual(FloorStacker.Build(5, 9), FloorStacker.Build(5, 9), "同种子可复现");
        Assert.IsTrue(LevelWorkshopModel.Check(FloorStacker.AddFloorOnTop(LevelWorkshopModel.PrisonSample), true, solid).Playable, "加一层后仍可玩");
        Assert.AreEqual(Step1CameraMode.FrameBoth, Step1RoomCamera.AutoMode(Step1CameraMode.WholeRoom, new Rect(0, 0, 32, 45), Tuning().maxWholeRoomHeight), "高楼镜头自动框住两人");
        Assert.AreEqual(Step1CameraMode.WholeRoom, Step1RoomCamera.AutoMode(Step1CameraMode.WholeRoom, new Rect(0, 0, 48, 12), Tuning().maxWholeRoomHeight), "默认房间镜头不变");
    }

    // ── S196：箱庭（层间巧思 / 捷径 / 可破坏墙 / 纵览）─────────────────
    [Test]
    public void HakoniwaSampleHasIdentityLoopsAndShortcuts()
    {
        var g = LevelWorkshopModel.HakoniwaSample;
        foreach (var row in g) Assert.AreEqual(g[0].Length, row.Length);
        var check = LevelWorkshopModel.Check(g, true, reg().IsSolid);
        Assert.IsTrue(check.Playable, "箱庭样板必须通过死局检查（门/墙/裂缝全关与全开）：" + check.Headline);
        var h = HakoniwaAnalyzer.Analyze(g);
        Assert.GreaterOrEqual(h.floors.Count, 4);
        Assert.IsTrue(h.hasLoop, "箱庭核心：有环路");
        Assert.IsTrue(h.links.Exists(l => l.kind == "捷径门"), "有单向捷径门");
        Assert.IsTrue(h.links.Exists(l => l.kind == "裂墙"), "有秘密裂墙");
        var ids = new HashSet<string>(); foreach (var f in h.floors) ids.Add(f.Identity);
        Assert.AreEqual(h.floors.Count, ids.Count, "每层主机关不同（身份）");
    }

    [Test]
    public void PrisonTowerIsHakoniwaNotJustStackedFloors()
    {
        System.Func<char, bool> solid = reg().IsSolid;
        for (int floors = 2; floors <= FloorStacker.MaxFloors; floors += 3)
            for (int seed = 0; seed < 3; seed++)
            {
                var g = FloorStacker.Build(floors, seed, 32, out var names);
                var h = HakoniwaAnalyzer.Analyze(g);
                Assert.IsTrue(h.hasLoop, $"{floors} 层 seed {seed}：有环路");
                Assert.Greater(h.ShortcutSaves, 0, $"{floors} 层 seed {seed}：打开捷径后回程变短");
                int distinct = new HashSet<string>(names).Count;
                Assert.AreEqual(System.Math.Min(names.Count, FloorStacker.Themes.Length), distinct, "主题不重复（超过主题数才循环）");
                Assert.IsTrue(h.links.Exists(l => l.shortcut), "层间有要'打开'的路（裂墙/裂缝/捷径门）");
            }
    }

    [Test]
    public void CrackedWallAndDoorAreSafeAndSmashHasCost()
    {
        var r = reg();
        Assert.IsTrue(r.IsSolid('|') && r.IsSolid('%'), "门与裂墙默认按墙参与可达性（最坏情况）");
        StringAssert.Contains("|%", LevelDeadlockAnalyzer.PersistentOpeners, "打开后的状态也做死局检查");
        Assert.IsFalse(WallSmashAbility.CanSmash(true, 2, 0f), "伪装时不能砸（必须现形 = 代价）");
        Assert.IsFalse(WallSmashAbility.CanSmash(false, 0, 0f), "次数用完不能砸");
        Assert.IsFalse(WallSmashAbility.CanSmash(false, 2, 1f), "冷却中不能砸");
        Assert.IsTrue(WallSmashAbility.CanSmash(false, 2, 0f));
        Assert.Greater(Tuning().hearingRange, 0f, "砸墙有响声，马里奥听得见");
        StringAssert.Contains("CrackedWall.Smashed += eyes.NoteNoise", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
        foreach (string token in new[] { "TricksterController", "IsDisguised" })
            StringAssert.DoesNotContain(token, CodeOnly(Read("Scripts/Gameplay/Step1/MarioEyes.cs")), "H4：听见的只是声音位置");
        Assert.IsTrue(OneWayDoor.OnOpeningSide(new Vector2(5, 1), new Vector2(5.8f, 1), false, 0.9f), "从右边开");
        Assert.IsFalse(OneWayDoor.OnOpeningSide(new Vector2(5, 1), new Vector2(4.2f, 1), false, 0.9f), "左边推不开");
        Assert.AreEqual(-1, Step1HakoniwaEvents.Pick(1, 0, 1f), "没有裂墙就没有塌墙事件");
        Assert.AreEqual(Step1HakoniwaEvents.Pick(42, 3, 0.5f), Step1HakoniwaEvents.Pick(42, 3, 0.5f), "随机事件可复现");
        var picks = new HashSet<int>(); for (int s = 0; s < 50; s++) picks.Add(Step1HakoniwaEvents.Pick(s, 3, 0.5f));
        Assert.Greater(picks.Count, 2, "不同回合塌不同的墙 / 有时不塌");
        CollectionAssert.IsEmpty(MechanismExplorationPlan.MissingFromCatalog(r.GetAllRegisteredChars()), "新元素显式登记（不静默计数）");
    }

    static LevelPathPlanner.Cell CellOfIn(string[] g, char c)
    {
        for (int r = 0; r < g.Length; r++) { int x = g[r].IndexOf(c); if (x >= 0) return new LevelPathPlanner.Cell(x, g.Length - 1 - r); }
        return new LevelPathPlanner.Cell(-1, -1);
    }

    static AsciiElementRegistry reg() => AsciiElementRegistry.GetDefault();
}
