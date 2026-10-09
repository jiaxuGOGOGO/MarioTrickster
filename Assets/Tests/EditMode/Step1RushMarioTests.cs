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
        survey.AnswerYesNo(true); survey.AnswerYesNo(false); survey.AnswerYesNo(true); survey.AnswerNumber(3); survey.AnswerNumber(5); survey.SubmitNote("he, jumped\nlate");
        string row = Step1PlaytestLog.CsvRow(new System.DateTime(2026, 1, 1), 3, "Trickster", "a,b", 12.3f, 2, 1, 4, 2, dict, survey);
        StringAssert.Contains("Blocker:1 Fire:2", row);
        StringAssert.Contains("a;b", row);
        StringAssert.Contains("yes,no,unfair:couldnt_read_him,5,he; jumped late,0", row);
        Assert.IsTrue(row.EndsWith(",yes,0,"), "S223：laughed；S227：调参版本 + 模式");
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
        Assert.AreEqual(Step1RoundSurvey.Step.Laughed, s.Current, "S223：差点被发现之后问有没有笑出来");
        s.AnswerYesNo(false);
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
        s.AnswerYesNo(false); s.AnswerYesNo(false); s.AnswerYesNo(false);
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
        foreach (string token in new[] { "ExplorationTarget", "RushMarioMind", "SetInputProvider", "Mind.Tick", "AddStartDelay" })
            StringAssert.DoesNotContain(token, src, "H10 检查只能观察，不能帮马里奥（S241：读性格名字/订阅受伤可以，改决策不行）");
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
        Assert.AreEqual(6, s.StepCount); Assert.AreEqual(1, s.StepNumber);
        Assert.AreEqual(2, s.Options.Length);
        Assert.IsTrue(s.Choose(0)); Assert.AreEqual(true, s.Calculated);
        Assert.IsTrue(s.Choose(1)); Assert.AreEqual(false, s.NearMiss);
        Assert.AreEqual(2, s.Options.Length, "S223：笑了吗 = 是/否");
        Assert.IsTrue(s.Choose(0)); Assert.AreEqual(true, s.Laughed);
        Assert.AreEqual(5, s.Options.Length, "服气 + 4 个宪法原因");
        Assert.IsTrue(s.Choose(3)); Assert.AreEqual("unfair:slipped", s.CaughtVerdict);
        Assert.AreEqual(5, s.Options.Length);
        Assert.IsTrue(s.Choose(4)); Assert.AreEqual(5, s.WantAgain);
        Assert.AreEqual(0, s.Options.Length, "一句话步骤用输入框");
        Assert.AreEqual(6, s.StepNumber);

        var n = new Step1RoundSurvey(false);
        n.Choose(0); n.Choose(0); n.Choose(1);
        Assert.AreEqual(Step1RoundSurvey.Step.WantAgain, n.Current);
        Assert.AreEqual(4, n.StepNumber); Assert.AreEqual(5, n.StepCount);
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
    public void CleanScreenHasNoLegacyHud()
    {
        string src = Read("Scripts/Gameplay/Step1/Step1Screen.cs");
        // S239：旧界面（GlobalGameUICanvas / LootEscapeHUD / SuspicionHUD）已整个删掉，干净界面不用再去藏它们
        foreach (string gone in new[] { "GlobalGameUICanvas", "LootEscapeHUD", "SuspicionHUD", "HideLegacyHud" })
            StringAssert.DoesNotContain(gone, CodeOnly(src));
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
            Assert.IsFalse(SightLine.CanWitness(at, target.transform.position, 8f, target.transform), "草丛挡视线");
            Assert.IsTrue(SightLine.CanWitness(at + new Vector2(2f, 0f), target.transform.position, 8f, target.transform), "走进草丛就看得见");
            Object.DestroyImmediate(bush.GetComponent<SightBlocker>());
            Physics2D.SyncTransforms();
            Assert.IsTrue(SightLine.CanWitness(at, target.transform.position, 8f, target.transform), "普通触发器仍不挡视线（旧规则不变）");
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
        // S239：旧系统已从代码里整个删掉，不再需要构建时剥离
        foreach (string gone in new[] { "Scripts/Gameplay/MarioSuspicionTracker.cs", "Scripts/Gameplay/TricksterHeatMeter.cs", "Scripts/Gameplay/AlarmCrisisDirector.cs", "Scripts/Gameplay/PropComboTracker.cs", "Scripts/UI/GlobalGameUICanvas.cs" })
            Assert.IsFalse(System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, gone)), gone + " 已删（S239），别加回来");
        StringAssert.DoesNotContain("StripUnusedLegacy", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
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
        StringAssert.Contains("st != MarioMindState.Running && !looking", src, "S241：追人/找人时站着是正常表演；起疑张望 = 暂停不清零");
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
        Assert.IsFalse(LevelStudioDocument.TryParse("M&TG\n####", out _, out _), "未知字符仍拒绝");
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
        string tracker = CodeOnly(Read("Scripts/Gameplay/SightLine.cs"));
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
        // 弹高（S216：被弹飞全程有重力 launchGravity）：约 2–3 格，头顶空 4 格放得下
        float apex = SpringPad.ApexHeight(t.springLaunchSpeed, t.launchGravity);
        Assert.Greater(apex, 2f); Assert.Less(apex, ElementCatalog.SpringHeadroomCells - 0.9f);
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
        Assert.IsFalse(TricksterKit.CanBomb(true, false, 3, 0f), "伪装时不能放炸弹（必须现形 = 代价）");
        Assert.IsFalse(TricksterKit.CanBomb(false, true, 3, 0f), "缩小时不能放");
        Assert.IsFalse(TricksterKit.CanBomb(false, false, 0, 0f), "炸弹用完不能放");
        Assert.IsFalse(TricksterKit.CanBomb(false, false, 3, 1f), "冷却中不能放");
        Assert.IsTrue(TricksterKit.CanBomb(false, false, 3, 0f));
        Assert.Greater(Tuning().hearingRange, 0f, "爆炸有响声，马里奥听得见");
        StringAssert.Contains("CrackedWall.Smashed += eyes.NoteNoise", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
        // H4：MarioEyes 本来就持有捣蛋者（用于"看"），这里只检查"听"的入口：只收一个位置，不碰捣蛋者
        string eyes = CodeOnly(Read("Scripts/Gameplay/Step1/MarioEyes.cs"));
        int a = eyes.IndexOf("public void NoteNoise("), b = eyes.IndexOf("public void Forget(");
        Assert.IsTrue(a >= 0 && b > a, "有 NoteNoise 入口");
        string hear = eyes.Substring(a, b - a);
        StringAssert.Contains("NoteNoise(Vector2 where)", hear, "只收声音位置");
        foreach (string token in new[] { "figure", "TricksterController", "IsDisguised" })
            StringAssert.DoesNotContain(token, hear, "H4：听见的只是声音位置");
        Assert.IsTrue(OneWayDoor.OnOpeningSide(new Vector2(5, 1), new Vector2(5.8f, 1), false, 0.9f), "从右边开");
        Assert.IsFalse(OneWayDoor.OnOpeningSide(new Vector2(5, 1), new Vector2(4.2f, 1), false, 0.9f), "左边推不开");
        Assert.AreEqual(-1, Step1HakoniwaEvents.Pick(1, 0, 1f), "没有裂墙就没有塌墙事件");
        Assert.AreEqual(Step1HakoniwaEvents.Pick(42, 3, 0.5f), Step1HakoniwaEvents.Pick(42, 3, 0.5f), "随机事件可复现");
        var picks = new HashSet<int>(); for (int s = 0; s < 50; s++) picks.Add(Step1HakoniwaEvents.Pick(s, 3, 0.5f));
        Assert.Greater(picks.Count, 2, "不同回合塌不同的墙 / 有时不塌");
        CollectionAssert.IsEmpty(ElementCatalog.Unexplained(r.GetAllRegisteredChars()), "新元素必须写进元素说明书（S238：以前查旧探索计划，探索系统已删）");
    }

    // ── S197：技能包 / 限制地形 / 图例 / 修复 ─────────────────
    [Test]
    public void TricksterKitIsDataDrivenAndHasCosts()
    {
        var t = Tuning();
        Assert.AreEqual(3, t.bombsPerRound, "默认 3 枚炸弹（可配置）");
        Assert.Greater(t.bombFuseSeconds, 0f, "H3：炸弹有引信预警");
        Assert.IsTrue(TricksterBomb.InBlast(Vector2.zero, new Vector2(1f, 0.5f), t.bombRadius), "小范围");
        Assert.IsFalse(TricksterBomb.InBlast(Vector2.zero, new Vector2(3f, 0f), t.bombRadius), "不是全屏清图");
        Assert.IsTrue(TricksterKit.CanShrink(false, 2)); Assert.IsFalse(TricksterKit.CanShrink(false, 0), "缩小有次数限制");
        Assert.Less(t.shrinkScale, 1f);
        StringAssert.Contains("TricksterKit.BlocksPranks", Read("Scripts/Enemy/TricksterController.cs"), "缩小时不能触发机关（代价）");
        StringAssert.Contains("Step1Keys.Down(KeyCode.B)", Read("Scripts/Gameplay/Step1/TricksterKit.cs"), "B 键同时读新旧输入系统（修'按 B 没反应'）");
        StringAssert.Contains("Keyboard.current", Read("Scripts/Gameplay/Step1/Step1Keys.cs"));
        StringAssert.Contains("AddComponent<TricksterKit>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        // 跳跃力：能跳上 2.5 格（与马里奥/可达性分析一致）
        float apex = t.tricksterJumpPower * t.tricksterJumpPower / (2f * 80f);
        Assert.GreaterOrEqual(apex, 2.45f, "捣蛋者要跳得上关卡里的 2 格台阶（原 18 只有 2.0 格）");
    }

    [Test]
    public void VentsPairTopDownLeftRightAndTimeStopIsTelegraphed()
    {
        var pos = new List<Vector2> { new Vector2(10, 1), new Vector2(3, 9), new Vector2(20, 9), new Vector2(5, 1) };
        Assert.AreEqual(2, Vent.PairOf(pos, 1), "上层左 ↔ 上层右");
        Assert.AreEqual(0, Vent.PairOf(pos, 3), "下层左 ↔ 下层右");
        Assert.AreEqual(-1, Vent.PairOf(new List<Vector2> { Vector2.zero }, 0), "落单没有配对");
        Assert.IsTrue(ElementCatalog.PlacementIssues(new[] { "WWWWWW", "W.O..W", "W####W" }, true, reg().IsSolid).Exists(i => i.Contains("成对")), "单个通风管要报");
        var t = Tuning();
        Assert.Greater(t.timeStopWarnSeconds, 0f, "H3：时间静止有预警");
        Assert.IsFalse(MarioTimeStop.ShouldTrigger(true, false, 0, 0f, false), "次数用完不用");
        Assert.IsFalse(MarioTimeStop.ShouldTrigger(true, false, 1, 5f, false), "冷却中不用");
        Assert.IsFalse(MarioTimeStop.ShouldTrigger(false, false, 1, 0f, false), "没追你、没挨坑时不用");
        Assert.IsTrue(MarioTimeStop.ShouldTrigger(true, false, 1, 0f, false));
        foreach (string token in new[] { "IsDisguised", "IsFullyBlended" })
            StringAssert.DoesNotContain(token, CodeOnly(Read("Scripts/Gameplay/Step1/MarioTimeStop.cs")), "H4：只根据马里奥自己看见的决定");
    }

    [Test]
    public void SlowTerrainNeverCreatesDeadlocks()
    {
        var r = reg();
        foreach (char c in "wgO") { Assert.IsFalse(r.IsSolid(c), c + " 可穿过"); Assert.IsFalse(r.GetHazardChars().Contains(c), c + " 不致死 → 不影响可达性"); }
        Assert.IsTrue(ElementCatalog.PlacementIssues(new[] { "WWWWWWWW", "W.wwww.W", "W######W" }, true, r.IsSolid).Exists(i => i.Contains("毒池")), "毒池太宽要报");
        Assert.IsFalse(ElementCatalog.PlacementIssues(new[] { "WWWWWWWW", "W..ww..W", "W######W" }, true, r.IsSolid).Exists(i => i.Contains("毒池")));
        StringAssert.Contains("SlowTerrain.CurrentMarioSpeedScale", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
        CollectionAssert.IsEmpty(ElementCatalog.Unexplained(r.GetAllRegisteredChars()), "新元素必须写进元素说明书（S238：以前查旧探索计划，探索系统已删）");
        Assert.IsTrue(LevelWorkshopModel.Check(LevelWorkshopModel.HakoniwaSample, true, r.IsSolid).Playable, "样板加了通风管/毒池/黏胶后仍可玩");
    }

    [Test]
    public void FloorPlannerOnlyPlansJumpsTheBotCanTake()
    {
        // 往上跳的路点：先站到起跳点再换目标（修"马里奥走到平台正下方撞头卡住"）
        var path = new List<LevelPathPlanner.Cell> { new LevelPathPlanner.Cell(2, 1), new LevelPathPlanner.Cell(3, 1), new LevelPathPlanner.Cell(4, 3) };
        Assert.AreEqual(3, LevelPathPlanner.NextWaypoint(path, 2f).x, "还没到起跳点 → 先去起跳点");
        Assert.AreEqual(3, LevelPathPlanner.NextWaypoint(path, 3f).y, "站到起跳点 → 目标换成落点（AI 此时起跳）");
        Assert.LessOrEqual(LevelPathPlanner.JumpUpSide, 2, "向上跳的水平距离不超过 AI 的起跳判定（2.25 格）");
        foreach (var g in new[] { LevelWorkshopModel.HakoniwaSample, LevelWorkshopModel.PrisonSample, FloorStacker.Build(6, 3) })
            Assert.Greater(HakoniwaAnalyzer.RouteLength(g), 0, "样板与监狱塔都能按 AI 真实跳法走通");
    }

    [Test]
    public void WorkshopSurvivesPlayModeAndLegendExists()
    {
        string win = Read("Scripts/Editor/LevelWorkshopWindow.cs");
        StringAssert.Contains("[NonSerialized] private IList<string> shownCache;", win, "进出 Play 模式后缓存不能半恢复（修'回来后工坊全空白'）");
        StringAssert.Contains("shownCache == null || shownCache.Count != doc.Height", win);
        StringAssert.Contains("[NonSerialized] private string parsedSource;", win);
        foreach (var (ch, _) in Step1MapLegend.Entries) Assert.IsNotNull(ElementCatalog.Get(ch), $"图例里的 '{ch}' 必须在元素说明书里");
        StringAssert.Contains("AddComponent<Step1MapLegend>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
    }

    // ── S198：可炸地形 / 伤害 / 塌桥 / 大炮 / 通风口 / 绳套 / 道具 / 修卡住与贴墙 ───────
    [Test]
    public void BombDestroysOrdinaryTerrainButNotBordersOrPranks()
    {
        Assert.IsTrue(Step1PrankRoomBuilder.DestructibleLocked(0, 5, 48, 12), "左外墙不可炸");
        Assert.IsTrue(Step1PrankRoomBuilder.DestructibleLocked(47, 5, 48, 12), "右外墙不可炸");
        Assert.IsTrue(Step1PrankRoomBuilder.DestructibleLocked(10, 0, 48, 12), "最底层地面不可炸（不会掉出地图）");
        Assert.IsFalse(Step1PrankRoomBuilder.DestructibleLocked(10, 3, 48, 12), "内部地面/墙可炸");
        Assert.IsTrue(Step1PrankRoomBuilder.TryParseCell("Ground_3_9_w12", out int x, out int y, out int w) && x == 3 && y == 9 && w == 12);
        Assert.IsTrue(Step1PrankRoomBuilder.TryParseCell("Wall_15_16", out x, out y, out w) && w == 1);
        var hit = Destructible.CellsInBlast(0, 5, 10, new bool[10], new Vector2(4f, 5f), 1.6f);
        CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, hit, "只炸半径内的格");
        var locked = new bool[10]; locked[4] = true;
        CollectionAssert.DoesNotContain(Destructible.CellsInBlast(0, 5, 10, locked, new Vector2(4f, 5f), 1.6f), 4, "锁定格不炸");
        var gone = new bool[6]; gone[2] = gone[3] = true;
        var segs = Destructible.Segments(gone);
        Assert.AreEqual(2, segs.Count, "长条被炸断成两段");
        Assert.AreEqual((0, 1), segs[0]); Assert.AreEqual((4, 5), segs[1]);
        string kit = CodeOnly(Read("Scripts/Gameplay/Step1/TricksterKit.cs"));
        StringAssert.Contains("Destructible.All", kit, "炸弹炸普通地形");
        StringAssert.Contains("prop.BlowUp()", kit, "炸弹炸箱子/草丛/装饰");
        StringAssert.Contains("health.TakeDamage(damageMario)", kit, "炸到马里奥掉血");
        StringAssert.Contains("lives.HitBySelf(damageSelf)", kit, "炸到自己掉命");
        StringAssert.Contains("Destructible.RestoreAll()", Read("Scripts/Gameplay/Step1/Step1RoomReset.cs"), "回合重置复原");
        Assert.AreEqual(1, Tuning().bombDamageMario); Assert.AreEqual(1, Tuning().bombDamageSelf);
        StringAssert.Contains("MarkDestructibles(root, room, tuning)", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
    }

    [Test]
    public void WholeBridgeCollapsesAndCannonAims()
    {
        var cells = new List<Vector2> { new Vector2(21, 3), new Vector2(22, 3), new Vector2(23, 3), new Vector2(24, 3), new Vector2(30, 3) };
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, CollapsingPlatform.Span(cells, 2), "按 L 整座桥一起塌（修'塌桥没反应'）");
        StringAssert.Contains("so.FindProperty(\"collapseWholeSpan\").boolValue = true", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        var r = PranksterCannon.Aim(true, 0f, Vector2.left, 15f, -45f, 75f);
        Assert.IsFalse(r.faceRight, "← 调头");
        r = PranksterCannon.Aim(true, 0f, Vector2.up, 15f, -45f, 75f);
        Assert.AreEqual(15f, r.angle, 0.01f, "↑ 抬高");
        r = PranksterCannon.Aim(true, 75f, Vector2.up, 15f, -45f, 75f);
        Assert.AreEqual(75f, r.angle, 0.01f, "有上限");
        Assert.Greater(PranksterCannon.AimDirection(true, 45f).y, 0.6f);
        StringAssert.Contains("cannon.Nudge(direction)", Read("Scripts/Enemy/TricksterController.cs"), "伪装控制大炮时方向键瞄准");
        Assert.AreEqual(30f, Tuning().cannonLaunchCooldown, 0.01f, "人肉炮冷却 30 秒（可配置）");
        Assert.IsTrue(PranksterCannon.WorthLaunching(new Vector2(10, 1), new Vector2(20, 1), true, 18f, 40f), "炮口朝目标且够远 → 马里奥会钻");
        Assert.IsFalse(PranksterCannon.WorthLaunching(new Vector2(10, 1), new Vector2(2, 1), true, 18f, 40f), "炮口背对目标 → 不钻");
        StringAssert.Contains("MarioMayUse(mario)", Read("Scripts/LevelElements/Traps/PranksterCannon.cs"), "马里奥也能钻炮");
    }

    [Test]
    public void VentsWorkFromSidesAndSnareHolds()
    {
        Assert.IsTrue(Vent.WantsEnter(true, false, false, false, false), "↓ 永远能进");
        Assert.IsTrue(Vent.WantsEnter(false, true, false, true, false), "左边贴墙 → 按 ← 进（墙上通风口）");
        Assert.IsFalse(Vent.WantsEnter(false, true, false, false, false), "地上的管口按 ← 不会误进");
        Assert.IsTrue(Vent.WantsEnter(false, false, true, false, true), "右边贴墙 → 按 → 进");
        var (l, rr) = Step1PrankRoomBuilder.VentWalls(new[] { "WWWWW", "WO..W", "W###W" }, 1, 1);
        Assert.IsTrue(l); Assert.IsFalse(rr);
        Assert.AreEqual(10.3f, SnareTrap.TotalHold(0.3f, Tuning().snareSeconds), 0.01f, "绳套吊 10 秒（可配置）");
        Assert.IsTrue(ElementCatalog.PlacementIssues(new[] { "WWWWWW", "W#...W", "W.Y..W", "W####W" }, true, reg().IsSolid).Exists(i => i.Contains("绳套")), "绳套头顶要空 2 格");
        Assert.IsFalse(reg().IsSolid('Y')); Assert.IsFalse(reg().GetHazardChars().Contains('Y'), "绳套不致死 → 不影响可达性");
    }

    [Test]
    public void PickupsAreFairReproducibleAndReversing()
    {
        var a = RandomPickups.Pick(7, 4, 2); var b = RandomPickups.Pick(7, 4, 2);
        CollectionAssert.AreEqual(a, b, "同种子可复现");
        Assert.AreEqual(2, a.Count, "每局亮 2 个");
        Assert.AreNotEqual(a[0].spot, a[1].spot, "不重复");
        for (int k = 0; k < 8; k++)
        {
            Assert.Contains(RandomPickups.Resolve(k, true), RandomPickups.ForTrickster, "你捡到的是捣蛋者道具");
            Assert.Contains(RandomPickups.Resolve(k, false), RandomPickups.ForMario, "他捡到的是马里奥道具（同一箱子，反转）");
        }
        StringAssert.Contains("RandomPickups.MarioXRayUntil", Read("Scripts/Gameplay/Step1/MarioEyes.cs"), "透视道具走马里奥感知");
        StringAssert.Contains("RandomPickups.TricksterInvisibleUntil", Read("Scripts/Gameplay/Step1/MarioEyes.cs"));
        CollectionAssert.IsEmpty(ElementCatalog.Unexplained(reg().GetAllRegisteredChars()), "新元素必须写进元素说明书（S238：以前查旧探索计划，探索系统已删）");
        Assert.IsTrue(LevelWorkshopModel.Check(LevelWorkshopModel.HakoniwaSample, true, reg().IsSolid).Playable, "样板加绳套/道具后仍可玩");
    }

    [Test]
    public void BackAndForthJumpingCountsAsStuckAndNoWallSticking()
    {
        var bounce = new float[200]; for (int i = 0; i < 200; i++) bounce[i] = 5f + (i % 10 < 5 ? 0.8f : -0.8f);
        Assert.Greater(Step1StuckRescue.SecondsUntilStuck(bounce, 0.05f, 6f, 1.5f), 0f, "原地来回跳（离目标没变近）= 卡住 → 救援");
        var progress = new float[200]; for (int i = 0; i < 200; i++) progress[i] = 20f - i * 0.1f;
        Assert.AreEqual(-1f, Step1StuckRescue.SecondsUntilStuck(progress, 0.05f, 6f, 1.5f), "一直在靠近目标 = 不卡");
        Assert.IsTrue(MarioMindDriver.UseAuthoredSteering(new Vector2(5, 4), new Vector2(5, 1)), "路点在头顶 → 对准再跳（不左右徘徊）");
        Assert.IsFalse(MarioMindDriver.UseAuthoredSteering(new Vector2(9, 1), new Vector2(5, 1)));
        StringAssert.Contains("if (HitsWall(side)) _frameVelocity.x = 0f;", Read("Scripts/Enemy/TricksterController.cs"), "空中朝墙推不会粘在墙上");
    }

    // ── S199：油桶连锁 / 铁笼 / 诱饵 / 警报 / 踢门 ─────────────────
    [Test]
    public void OilBarrelsChainAndShareBombRules()
    {
        var barrels = new List<Vector2> { new Vector2(0, 1), new Vector2(1.5f, 1), new Vector2(3f, 1), new Vector2(9, 1) };
        var first = OilBarrel.ChainTargets(barrels, barrels[0], Tuning().oilRadius, 0);
        CollectionAssert.Contains(first, 1, "爆炸点燃旁边的桶");
        CollectionAssert.DoesNotContain(first, 3, "远的桶不受影响");
        var second = OilBarrel.ChainTargets(barrels, barrels[1], Tuning().oilRadius, 1);
        CollectionAssert.Contains(second, 2, "连锁：第二个桶再点燃第三个");
        string kit = CodeOnly(Read("Scripts/Gameplay/Step1/TricksterKit.cs"));
        StringAssert.Contains("public static void Blast(", kit, "炸弹与油桶共用一套爆炸规则");
        StringAssert.Contains("barrel.Ignite()", kit, "炸弹点燃油桶");
        StringAssert.Contains("fire.IsFiring", Read("Scripts/LevelElements/Pranks/OilBarrel.cs"), "喷火点燃油桶");
        Assert.Greater(Tuning().oilFuseSeconds, 0f, "H3：点燃后有引信");
        Assert.IsTrue(reg().IsSolid('U'), "死局检查按实心算（最坏情况：不炸）");
    }

    [Test]
    public void CageDecoyAlarmAndDoorKick()
    {
        Assert.AreEqual(3f, Tuning().cageSeconds, 0.01f, "铁笼关 3 秒后自动打开");
        StringAssert.Contains("if (timer <= 0f) Release();", Read("Scripts/LevelElements/Pranks/IronCage.cs"), "H9：必然放人");
        Assert.IsTrue(ElementCatalog.PlacementIssues(new[] { "WWWWWW", "W#...W", "W.Q..W", "W####W" }, true, reg().IsSolid).Exists(i => i.Contains("铁笼")), "铁笼头顶要空");
        Assert.IsFalse(DecoyAbility.CanDecoy(true, false, 1, false), "伪装时不能放诱饵");
        Assert.IsFalse(DecoyAbility.CanDecoy(false, false, 0, false), "次数用完");
        Assert.IsFalse(DecoyAbility.CanDecoy(false, false, 1, true), "同时只能一个");
        Assert.IsTrue(Decoy.SeenThrough(new Vector2(0, 0), new Vector2(2, 0), 2.5f, false), "走近识破");
        Assert.IsFalse(Decoy.SeenThrough(new Vector2(0, 0), new Vector2(8, 0), 2.5f, false), "远处看不穿");
        Assert.IsTrue(Decoy.SeenThrough(new Vector2(0, 0), new Vector2(8, 0), 2.5f, true), "透视道具直接看穿");
        string eyes = CodeOnly(Read("Scripts/Gameplay/Step1/MarioEyes.cs"));
        StringAssert.Contains("MarioVision.CanSee(eye, facingRight, dp, decoy.transform, t)", eyes, "H4：诱饵与真身同一视锥/遮挡规则");
        Assert.AreEqual(Step1HakoniwaEvents.AlarmAt(5, 0.4f, 25f, 70f), Step1HakoniwaEvents.AlarmAt(5, 0.4f, 25f, 70f), "警报可复现");
        Assert.AreEqual(-1f, Step1HakoniwaEvents.AlarmAt(5, 0f, 25f, 70f), "概率 0 = 没有警报");
        int alarms = 0; for (int sd = 0; sd < 100; sd++) { float at = Step1HakoniwaEvents.AlarmAt(sd, 0.4f, 25f, 70f); if (at >= 0f) { alarms++; Assert.That(at, Is.InRange(25f, 70f)); } }
        Assert.That(alarms, Is.InRange(20, 60), "大约 40% 的回合有警报");
        StringAssert.Contains("p.alarm", Read("Scripts/Gameplay/Step1/RushMarioMind.cs"), "警报走公开环境状态，不读捣蛋者信息");
        Assert.IsTrue(MarioDoorKick.ShouldKick(new Vector2(4.4f, 1), new Vector2(5, 1), false, new Vector2(20, 1), 0.9f), "在打不开的一侧、目标在门后 → 踢");
        Assert.IsFalse(MarioDoorKick.ShouldKick(new Vector2(5.6f, 1), new Vector2(5, 1), false, new Vector2(0, 1), 0.9f), "在能开的一侧 → 不踢（推开就行）");
        Assert.IsFalse(MarioDoorKick.ShouldKick(new Vector2(4.4f, 1), new Vector2(5, 1), false, new Vector2(0, 1), 0.9f), "目标在自己这边 → 不踢");
        StringAssert.Contains("AddComponent<MarioDoorKick>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        CollectionAssert.IsEmpty(ElementCatalog.Unexplained(reg().GetAllRegisteredChars()), "新元素必须写进元素说明书（S238：以前查旧探索计划，探索系统已删）");
        Assert.IsTrue(LevelWorkshopModel.Check(LevelWorkshopModel.HakoniwaSample, true, reg().IsSolid).Playable, "样板加油桶/铁笼后仍可玩");
    }

    // ── S200：以身入局——连锁编排 / 挑衅 / 绊线 / 马里奥学习 ─────────
    [Test]
    public void ChainPlanLinksFiresOnArrivalAndKeepsTelegraph()
    {
        var list = new List<string>();
        Assert.AreEqual(0, ChainPlan.Toggle(list, "a", 2), "第 1 环");
        Assert.AreEqual(1, ChainPlan.Toggle(list, "b", 2), "第 2 环");
        Assert.AreEqual(-2, ChainPlan.Toggle(list, "c", 2), "满了");
        Assert.AreEqual(-1, ChainPlan.Toggle(list, "a", 2), "再按一次取消");
        CollectionAssert.AreEqual(new[] { "b" }, list);
        // 预判：他正以 4 格/秒往右走，机关预警 0.8 秒 → 离机关 3.2 格时就该触发（落点刚好）
        Assert.IsTrue(ChainPlan.ShouldFire(new Vector2(6.8f, 1), new Vector2(4f, 0), new Vector2(10f, 1), 0.8f, 0.8f), "预判落点正好 → 触发");
        Assert.IsFalse(ChainPlan.ShouldFire(new Vector2(2f, 1), new Vector2(4f, 0), new Vector2(10f, 1), 0.8f, 0.8f), "太远 → 还不触发");
        Assert.IsFalse(ChainPlan.ShouldFire(new Vector2(13f, 1), new Vector2(4f, 0), new Vector2(10f, 1), 0.8f, 0.8f), "已经冲过去 → 不触发");
        Assert.IsFalse(ChainPlan.ShouldFire(new Vector2(9.8f, 5), new Vector2(0f, 0), new Vector2(10f, 1), 0.8f, 0.8f), "不在同一层 → 不触发");
        Assert.IsTrue(ChainPlan.ShouldFireCannon(new Vector2(14, 1), new Vector2(10, 1), true, 6f), "炮口前方 → 开炮");
        Assert.IsFalse(ChainPlan.ShouldFireCannon(new Vector2(6, 1), new Vector2(10, 1), true, 6f), "炮口后面 → 不开");
        var order = ChainPlan.AutoOrder(new[] { new Vector2(8, 1), new Vector2(3, 1), new Vector2(30, 1), new Vector2(5, 1) }, new Vector2(1, 1), 10f, 4);
        CollectionAssert.AreEqual(new[] { 1, 3, 0 }, order, "一键布置：由近到远，超出范围的不编");
        string plan = CodeOnly(Read("Scripts/Gameplay/Step1/ChainPlan.cs"));
        StringAssert.Contains("l.OnTricksterActivate(", plan, "H3：自动触发走机关自己的预警流程，不跳过预警");
        StringAssert.Contains("l.CanBeControlled()", plan, "冷却中的机关不会被强制触发");
        StringAssert.Contains("ChainPlan.LinkFired += eyes.NoteChainLink", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"), "连锁触发的机关被他看见照样起疑");
        StringAssert.Contains("ChainPlan.Clicked += eyes.NoteNoiseNear", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"), "代价：编号咔哒声");
        StringAssert.Contains("AddComponent<ChainPlan>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
    }

    [Test]
    public void TauntTripwireAndMarioLearning()
    {
        Assert.IsFalse(TauntAbility.CanTaunt(true, 3, 0f), "伪装时不能挑衅");
        Assert.IsFalse(TauntAbility.CanTaunt(false, 0, 0f));
        Assert.IsFalse(TauntAbility.CanTaunt(false, 3, 1f), "冷却");
        Assert.IsTrue(TauntAbility.CanTaunt(false, 3, 0f));
        var t = Tuning();
        var mind = new RushMarioMind(t);
        mind.Tick(Dt, new MarioPercept { marioPos = Vector2.zero, heardTaunt = true, tauntPos = new Vector2(6, 0) });
        Assert.GreaterOrEqual(mind.Meter.Value, t.curiousThreshold);
        Assert.AreEqual(6f, mind.Focus.x, 0.01f, "注意力转向挑衅的位置");
        StringAssert.Contains("NoteTaunt(Vector2 where)", Read("Scripts/Gameplay/Step1/MarioEyes.cs"), "H4：挑衅只给一个位置");
        // 学习层
        var m2 = new RushMarioMind(t);
        m2.Tick(Dt, new MarioPercept { marioPos = new Vector2(10, 1), hurt = true });
        Assert.AreEqual(1, m2.HurtSpots.Count, "记住被坑的地方");
        Assert.IsTrue(RushMarioMind.NearHurtSpot(m2.HurtSpots, new Vector2(8, 1), true, t.cautiousRadius), "前方是被坑过的地方 → 小心");
        Assert.IsFalse(RushMarioMind.NearHurtSpot(m2.HurtSpots, new Vector2(8, 1), false, t.cautiousRadius), "背对着 → 不管");
        Assert.IsFalse(RushMarioMind.NearHurtSpot(m2.HurtSpots, new Vector2(2, 1), true, t.cautiousRadius), "离得远 → 不管");
        m2.Reset(1); Assert.AreEqual(0, m2.HurtSpots.Count, "每回合忘掉");
        Assert.Less(t.cautiousSpeedScale, 1f); Assert.Greater(t.cautiousSpeedScale, 0.3f, "小心只是放慢，不会停");
        StringAssert.Contains("Mind.Cautious ? tuning.cautiousSpeedScale", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
        // 绊线
        Assert.IsFalse(reg().IsSolid('R'), "绊线不挡路");
        StringAssert.Contains("GetComponentInParent<MarioController>()", Read("Scripts/LevelElements/Pranks/Tripwire.cs"), "只有马里奥会踩响");
        StringAssert.Contains("Tripwire.Tripped += HandleTripped", Read("Scripts/Gameplay/Step1/ChainPlan.cs"), "绊线启动连锁");
        StringAssert.Contains("Tripwire.Tripped += HandleTripped", Read("Scripts/Gameplay/Step1/Step1Combo.cs"), "绊到计入连招");
        Assert.IsTrue(ComboRouteAnalyzer.IsChainPart('R') && ComboRouteAnalyzer.IsChainPart('U'), "工坊连招路线把绊线/油桶算进去");
        foreach (var sample in new[] { LevelWorkshopModel.LureSample, LevelWorkshopModel.HakoniwaSample, Step1PrankRoomBuilder.Room })
            Assert.IsTrue(LevelWorkshopModel.Check(sample, true, reg().IsSolid).Playable, "加绊线后样板仍可玩");
        Assert.GreaterOrEqual(ComboRouteAnalyzer.Analyze(LevelWorkshopModel.LureSample, 10f).BestGroupKinds, 5, "诱捕走廊一套连锁至少 5 种机关");
        CollectionAssert.IsEmpty(ElementCatalog.Unexplained(reg().GetAllRegisteredChars()), "新元素必须写进元素说明书（S238：以前查旧探索计划，探索系统已删）");
    }

    // ── S202：连锁回放 / 策略模拟（炸弹困人→加固）/ 陷阱试探 / 马里奥躲闪与捡道具 ─────────
    [Test]
    public void ChainReplayTriggersOnPerfectChainAndNeverDuringAutoCheck()
    {
        var t = Tuning();
        Assert.IsTrue(ChainReplay.ShouldReplay(true, 3, 0, t.replayMinCombo, false, false), "完美连锁（≥3 环）回放");
        Assert.IsFalse(ChainReplay.ShouldReplay(true, 2, 0, t.replayMinCombo, false, false), "2 环不回放");
        Assert.IsTrue(ChainReplay.ShouldReplay(true, 0, t.replayMinCombo, t.replayMinCombo, false, false), "大连招也回放");
        Assert.IsFalse(ChainReplay.ShouldReplay(true, 5, 9, 4, true, false), "自动检查期间绝不回放（H10 不受影响）");
        Assert.IsFalse(ChainReplay.ShouldReplay(true, 5, 9, 4, false, true), "冷却中不回放");
        Assert.IsFalse(ChainReplay.ShouldReplay(false, 5, 9, 4, false, false), "可关闭");
        var fs = new List<ChainReplay.Frame> { new ChainReplay.Frame { t = 0f, mario = new Vector2(0, 0) }, new ChainReplay.Frame { t = 1f, mario = new Vector2(10, 0) } };
        Assert.AreEqual(5f, ChainReplay.Sample(fs, 0.5f, true).x, 0.01f, "回放插值");
        Assert.AreEqual(1, ChainReplay.TrimIndex(fs, 6f, 5.5f), "环形缓冲只留最近几秒");
        Assert.Less(t.replaySpeed, 1f, "慢动作");
        string src = CodeOnly(Read("Scripts/Gameplay/Step1/ChainReplay.cs"));
        StringAssert.Contains("Time.timeScale = restoreScale", src, "回放结束恢复速度（H9）");
        StringAssert.Contains("Step1Keys.Down(KeyCode.Space)", src, "可跳过");
        StringAssert.Contains("ChainPlan.PerfectChain += HandlePerfect", src);
        StringAssert.Contains("ChainReplay.Playing", Read("Scripts/Gameplay/Step1/Step1Hitstop.cs"), "顿帧不和回放抢 timeScale");
        StringAssert.Contains("AddComponent<ChainReplay>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
    }

    [Test]
    public void StrategySimFindsBombTrapsAndReinforcementRemovesThem()
    {
        // 一个坑：马里奥在右边出生，唯一回出口的路是左边的两级台阶；炸掉台阶 → 困在坑里
        var pit = new[] { "WWWWWWWWWWWW", "W..........W", "W.Go.....M.W", "W###...####W", "W.....--...W", "W..........W", "W...--.....W", "W..........W", "WWWWWWWWWWWW" };
        var t = Tuning();
        var trap = StrategySim.FindBombTrap(pit, t.bombsPerRound, t.bombRadius);
        Assert.IsNotNull(trap, "最坏的对手能用炸弹把马里奥困在坑里");
        var locked = StrategySim.Reinforce(pit, t.bombsPerRound, t.bombRadius, out var first, out bool still);
        Assert.IsNotNull(first);
        Assert.Greater(locked.Count, 0, "加固了承重格");
        Assert.IsFalse(still, "加固后困不住（H9）");
        Assert.IsNull(StrategySim.FindBombTrap(pit, t.bombsPerRound, t.bombRadius, locked));
        // 爆炸格：外圈与底层永远炸不掉
        foreach (var c in StrategySim.BlastCells(pit, new StrategySim.Cell(1, 1), 3f, null)) { Assert.Greater(c.x, 0); Assert.Greater(c.y, 0); }
        // 每个样板 + 默认房间：加固后都困不住
        foreach (var g in new[] { Step1PrankRoomBuilder.Room, LevelWorkshopModel.PrisonSample, LevelWorkshopModel.LureSample, LevelWorkshopModel.HakoniwaSample })
        {
            StrategySim.Reinforce(g.Select(Step1Layout.StripSlots).ToList(), t.bombsPerRound, t.bombRadius, out _, out bool s2, 24, t.oilRadius);
            Assert.IsFalse(s2, "样板加固后炸弹困不住马里奥");
        }
        var rep = StrategySim.Analyze(Step1PrankRoomBuilder.Room, 5f, t.startDelaySeconds, 0, t.bombRadius);
        Assert.IsNotNull(rep.route, "默认房间马里奥能按寻路走完");
        Assert.Greater(rep.onRoute.Count, 3, "路线上途经多个机关");
        for (int i = 1; i < rep.onRoute.Count; i++) Assert.LessOrEqual(rep.onRoute[i - 1].at, rep.onRoute[i].at, "时间线按顺序");
        Assert.IsTrue(t.reinforceAgainstBombs);
        StringAssert.Contains("StrategySim.Reinforce(", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"), "构建房间时自动加固");
        StringAssert.Contains("DrawStrategy", Read("Scripts/Editor/LevelWorkshopWindow.cs"), "工坊有'策略模拟'开关");
        Assert.AreEqual(StrategySim.Hash("a\nb"), StrategySim.Hash("a\r\nb"), "指纹不受换行符影响");
    }

    [Test]
    public void TrapProbeAndTrackRecordingForAutoCheck()
    {
        Assert.IsTrue(Step1TrapProbe.CanProbe(0, 1, true));
        Assert.IsFalse(Step1TrapProbe.CanProbe(1, 1, true), "每个机关每局只试探一次");
        Assert.IsFalse(Step1TrapProbe.CanProbe(0, 1, false), "冷却中的不触发");
        string probe = CodeOnly(Read("Scripts/Gameplay/Step1/Step1TrapProbe.cs"));
        StringAssert.Contains("p.OnTricksterActivate(", probe, "H3：试探走机关自己的预警");
        StringAssert.Contains("ChainPlan.ShouldFire(", probe, "与连锁接力同一套预判");
        StringAssert.Contains("TrapProbeMenu", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"), "菜单：Trap Probe");
        var visits = new Dictionary<int, int> { { 3 * 1000 + 2, 5 }, { 4 * 1000 + 2, 1 } };
        string text = Step1HandsOffCheck.TrackText("abc", visits, new[] { 7 * 1000 + 1 });
        var (room, v2, stuck) = Step1HandsOffCheck.ParseTrack(text);
        Assert.AreEqual("abc", room);
        Assert.AreEqual(5, v2[3002]);
        Assert.IsTrue(stuck.Contains(7001), "卡住点也记下");
        StringAssert.Contains("mode,rescues,hurts", Read("Scripts/Gameplay/Step1/Step1HandsOffCheck.cs"), "CSV 多了模式/救援/被坑次数");
        StringAssert.Contains("figure != null && !ProbeMode", Read("Scripts/Gameplay/Step1/Step1HandsOffCheck.cs"), "试探模式捣蛋者留在场上");
    }

    [Test]
    public void MarioDodgesVisibleDangerAndGrabsNearbyPickups()
    {
        var t = Tuning();
        var d = RushMarioMind.DodgeTarget(new Vector2(10, 1), new Vector2(11, 1), 1.6f, t.dodgeMargin);
        Assert.IsTrue(d.HasValue, "在爆炸圈里 → 退");
        Assert.Less(d.Value.x, 11f - 1.6f, "退到圈外（远离炸弹那一边）");
        Assert.IsFalse(RushMarioMind.DodgeTarget(new Vector2(3, 1), new Vector2(11, 1), 1.6f, t.dodgeMargin).HasValue, "离得远 → 不管");
        Assert.IsFalse(RushMarioMind.DodgeTarget(new Vector2(10, 6), new Vector2(11, 1), 1.6f, t.dodgeMargin).HasValue, "不在同一层 → 不管");
        var mind = new RushMarioMind(t);
        var o = mind.Tick(Dt, new MarioPercept { marioPos = new Vector2(10, 1), dangerPos = new Vector2(11, 1), dangerRadius = 1.6f });
        Assert.AreEqual("DODGE", o.intent); Assert.IsTrue(mind.Dodging);
        o = mind.Tick(Dt, new MarioPercept { marioPos = new Vector2(10, 1), seesPickup = true, pickupPos = new Vector2(12, 1) });
        Assert.AreEqual("GRAB", o.intent, "看见道具箱 → 顺路捡");
        Assert.AreEqual(12f, o.moveTarget.Value.x, 0.01f);
        Assert.IsFalse(RushMarioMind.WorthPickup(new Vector2(0, 1), new Vector2(20, 1), t.pickupDetourCells), "太远不绕");
        for (int i = 0; i < 80; i++) o = mind.Tick(Dt, new MarioPercept { marioPos = new Vector2(10, 1), seesPickup = true, pickupPos = new Vector2(12, 1) });
        Assert.AreNotEqual("GRAB", o.intent, "够不着的道具箱 3 秒后放弃（不会一直原地走，H10）");
        string eyes = CodeOnly(Read("Scripts/Gameplay/Step1/MarioEyes.cs"));
        StringAssert.Contains("MarioVision.CanSee(eye, facingRight, bp, b.transform, t)", eyes, "H4：炸弹要看得见才躲");
        StringAssert.Contains("driver.Mind.Dodging", Read("Scripts/Gameplay/Step1/Step1StuckRescue.cs"), "躲闪时不误判卡住");
    }

    // ── S203：马里奥性格（冲冲型 / 谨慎型 / 贪财型）─────────
    [Test]
    public void PersonalitiesAreSeededVisibleAndDifferent()
    {
        var t = Tuning();
        Assert.AreEqual(Step1PersonalityChoice.Random, t.marioPersonality, "S238：默认随机性格（一个下拉，以前是两个设置）");
        Assert.AreEqual(MarioPersonality.Roll(42, 1, 1, 1), MarioPersonality.Roll(42, 1, 1, 1), "同种子同性格（可复现）");
        Assert.AreEqual(MarioPersonalityKind.Rush, MarioPersonality.Roll(42, 0, 0, 0), "权重全 0 = 冲冲型");
        Assert.AreEqual(MarioPersonalityKind.Greedy, MarioPersonality.Roll(42, 0, 0, 1));
        var seen = new HashSet<MarioPersonalityKind>();
        for (int sd = 0; sd < 60; sd++) seen.Add(MarioPersonality.Roll(sd, 1, 1, 1));
        Assert.AreEqual(3, seen.Count, "三种性格都会出现");
        var rush = MarioPersonality.For(MarioPersonalityKind.Rush, t);
        var cau = MarioPersonality.For(MarioPersonalityKind.Cautious, t);
        var gre = MarioPersonality.For(MarioPersonalityKind.Greedy, t);
        Assert.IsTrue(cau.avoidHurtSpots && !rush.avoidHurtSpots && !gre.avoidHurtSpots, "只有谨慎型绕开被坑点");
        Assert.Less(cau.pickupDetour, 0f, "谨慎型不绕路捡道具");
        Assert.IsTrue(gre.pickupAnyFloor && gre.pickupDetour > rush.pickupDetour, "贪财型跨层、更远也去抢");
        Assert.IsFalse(gre.slowNearHurtSpots, "贪财型不长记性");
        Assert.Greater(cau.suspicionScale, 1f, "谨慎型更容易起疑");
        Assert.IsTrue(RushMarioMind.WantsPickup(gre, new Vector2(0, 1), new Vector2(10, 6)), "贪财型：别的楼层 10 格外也去");
        Assert.IsFalse(RushMarioMind.WantsPickup(rush, new Vector2(0, 1), new Vector2(10, 6)), "冲冲型：不跨层");
        Assert.IsFalse(RushMarioMind.WantsPickup(cau, new Vector2(0, 1), new Vector2(1, 1)), "谨慎型：就在旁边也不捡");
        var mind = new RushMarioMind(t) { ForcedPersonality = MarioPersonalityKind.Cautious };
        mind.Reset(7);
        Assert.AreEqual(MarioPersonalityKind.Cautious, mind.Personality);
        Assert.IsTrue(mind.ShowingPersonality, "开局亮出性格（H6）");
        StringAssert.Contains("tr.zh", Read("Scripts/Gameplay/Step1/MarioMindLabel.cs"), "头顶显示性格");
        StringAssert.Contains("Mind.Traits.speedScale", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"));
        Assert.AreEqual(MarioPersonalityKind.Rush, MarioMindDriver.CycledPersonality(1));
        Assert.AreEqual(MarioPersonalityKind.Cautious, MarioMindDriver.CycledPersonality(2));
        Assert.AreEqual(MarioPersonalityKind.Greedy, MarioMindDriver.CycledPersonality(3), "自动检查轮流测三种性格（H10）");
        StringAssert.Contains(",personality", Step1PlaytestLog.CsvHeader, "试玩记录写性格");
    }

    [Test]
    public void CautiousDetoursAroundHurtSpotsButNeverGetsStuck()
    {
        // 两条路：地面直走 / 台阶上高路绕过去
        var two = new[] { "WWWWWWWWWWWWWWWW", "W..............W", "W..............W", "W..............W", "W...--------...W", "W..............W", "W.-..........-.W", "W.M.........G..W", "W##############W" };
        var w = DetourPlanner.Detour(two, 2, 1, 12, 1, 2f, new List<(int, int)> { (7, 1) }, 2f, out bool d);
        Assert.IsTrue(d, "被坑点在地面 → 走高路");
        Assert.Greater(w.y, 1, "下一个路点在上面");
        // 只有一条路：绕不开 → 走原路（不会停住）
        var one = new[] { "WWWWWWWWWWWW", "W..........W", "W.M......G.W", "W##########W" };
        DetourPlanner.Detour(one, 2, 1, 9, 1, 2f, new List<(int, int)> { (5, 1) }, 2f, out bool d1);
        Assert.IsFalse(d1, "绕不开 → 原路（H1/H10）");
        Assert.IsTrue(MarioPersonality.ShouldHop(new[] { new Vector2(6, 1) }, new Vector2(5, 1), true), "绕不开时跳过被坑点");
        Assert.IsFalse(MarioPersonality.ShouldHop(new[] { new Vector2(6, 1) }, new Vector2(5, 1), false), "背对着不跳");
        Assert.AreEqual(0, MarioMindDriver.SpotsAhead(new[] { new Vector2(5, 1) }, new Vector2(5.5f, 1), 2f).Count, "刚被坑的地方（脚下）不算，避免原地卡住");
        var hop = CodeOnly(Read("Scripts/Core/HeuristicBotInputProvider.cs"));
        StringAssert.Contains("if (JumpRequest && _mario.IsGrounded)", hop);
        // 诱捕走廊有高路：谨慎型被坑后能绕
        DetourPlanner.Detour(LevelWorkshopModel.LureSample, 5, 8, 44, 8, 5f, new List<(int, int)> { (21, 8) }, 2f, out bool dl);
        Assert.IsTrue(dl, "诱捕走廊：谨慎型能走高路绕开火");
        Assert.IsTrue(LevelWorkshopModel.Check(LevelWorkshopModel.LureSample, true, reg().IsSolid).Playable);
        Assert.IsNotNull(LevelPathPlanner.Path(two, new LevelPathPlanner.Cell(2, 1), new LevelPathPlanner.Cell(12, 1), null), "不带禁区 = 原寻路");
    }

    // ── S204：网页关卡设计台 ↔ Unity 工坊 ─────────
    [Test]
    public void WorkshopImportsWebStudioJson()
    {
        string json = "{\"v\":1,\"grid\":[\"WWWWWW\",\"W.GAMW\",\"W|.oTW\",\"W####W\"],\"name\":\"x\",\"notes\":[{\"x\":1,\"y\":1,\"text\":\"hi\"}]}";
        var rows = LevelWorkshopModel.GridFromStudioJson(json, out string note);
        Assert.IsNotNull(rows);
        Assert.AreEqual(4, rows.Length);
        Assert.AreEqual("W.G.MW", rows[1], "网页里的新机制提案（未登记字符 A）先换成空气");
        StringAssert.Contains("A", note, "并告诉用户哪些提案还没实现");
        Assert.AreEqual("W|.oTW", rows[2], "已登记字符原样保留");
        Assert.IsNull(LevelWorkshopModel.GridFromStudioJson("{}", out _), "不是设计台文件 → 拒绝");
        string build = File.ReadAllText(Path.Combine(Application.dataPath, "..", "tools", "LevelStudioWeb", "build.py"));
        StringAssert.Contains("ElementCatalog.cs", build, "网页元素表从项目源码生成（单一来源）");
        StringAssert.Contains("GridFromStudioJson", Read("Scripts/Editor/LevelWorkshopWindow.cs"));
    }

    // ── S206：关卡库 / 关卡包 / 搭建范围 ─────────
    [Test]
    public void LevelPackRoundTripKeepsNamesNotesAndPendingMechanics()
    {
        string json = "{\"type\":\"mariotrickster-levelpack\",\"v\":1,\"levels\":[{\"id\":\"a1\",\"name\":\"我的第一关\",\"goal\":\"先挑衅\",\"grid\":[\"WWWWWWWWWWWW\",\"W..........W\",\"W..........W\",\"W..........W\",\"W.G.MA..oT.W\",\"W##########W\"],\"notes\":[{\"x\":5,\"y\":1,\"text\":\"磁铁放这\"}]},{\"name\":\"第二关\",\"grid\":[\"WWWWWWWWWWWW\",\"W..........W\",\"W..........W\",\"W..........W\",\"W.G.M...oT.W\",\"W##########W\"]}],\"proposals\":[{\"c\":\"A\",\"zh\":\"磁铁陷阱\",\"status\":\"go\"}]}";
        var levels = LevelPack.Parse(json, c => reg().GetEntry(c) != null, out string err);
        Assert.IsNotNull(levels, err);
        Assert.AreEqual(2, levels.Count, "一个关卡包多关");
        var a = levels[0];
        Assert.AreEqual("我的第一关", a.name);
        Assert.AreEqual("W.G.M...oT.W", a.rows[4], "还没实现的新机制先当空气");
        CollectionAssert.AreEqual(new[] { (5, 1) }, a.pending['A'], "但记住位置，实现后能还原");
        Assert.AreEqual("磁铁陷阱", a.pendingNames['A']);
        string text = LevelPack.ToText(a);
        Assert.AreEqual("我的第一关", LevelPack.NameOf(text), "名字写进文件");
        CollectionAssert.AreEqual(new[] { 'A' }, LevelPack.PendingOf(text));
        StringAssert.Contains("# Note: (5,1) 磁铁放这", text, "批注跟着关卡走");
        Assert.IsTrue(LevelStudioDocument.TryParse(text, out var doc, out string perr), perr);
        Assert.AreEqual(6, doc.Height, "元数据行不会被当成网格");
        Assert.IsTrue(LevelWorkshopModel.Check(a.rows, true, reg().IsSolid).Playable);
        Assert.AreEqual("a_b_c", LevelPack.SafeFileName("a/b:c"), "文件名去掉非法字符");
        Assert.IsNull(LevelPack.Parse("{\"x\":1}", c => true, out _), "不是关卡包 → 拒绝");
        Assert.IsNull(MiniJson.Parse("[1,2", out string jerr)); StringAssert.Contains("JSON", jerr);
        StringAssert.Contains("Assets/Levels/Library", Read("Scripts/Editor/LevelLibrary.cs"), "关卡库在项目里（进 git，换账号也在）");
        StringAssert.Contains("ImportPack", Read("Scripts/Editor/LevelWorkshopWindow.cs"), "工坊：关卡库 ▾ → 导入网页关卡包");
    }

    [Test]
    public void BuildScopeIsExplicit()
    {
        System.Func<char, bool> s = reg().IsSolid;
        foreach (var g in new[] { LevelWorkshopModel.PrisonSample, LevelWorkshopModel.LureSample, LevelWorkshopModel.HakoniwaSample, Step1PrankRoomBuilder.ResolvedRoom(0) })
            CollectionAssert.IsEmpty(LevelWorkshopModel.BoundsIssues(g, s), "样板都在搭建范围内");
        for (int f = 2; f <= FloorStacker.MaxFloors; f++) CollectionAssert.IsEmpty(LevelWorkshopModel.BoundsIssues(FloorStacker.Build(f, 0), s));
        Assert.IsTrue(LevelWorkshopModel.BoundsIssues(new[] { "WWWWWWWWWWWW", "W..........W", "W..........W", "W..........W", "W.G.M...oT..", "W##########W" }, s).Exists(m => m.Contains("最左和最右")), "右边开口 → 报错");
        Assert.IsTrue(LevelWorkshopModel.BoundsIssues(new[] { "WWWW", "W.MW", "WWWW" }, s).Exists(m => m.Contains("太小")), "太小 → 报错");
        Assert.AreEqual(12, LevelWorkshopModel.MinWidth); Assert.AreEqual(6, LevelWorkshopModel.MinHeight);
        Assert.AreEqual(128, LevelStudioDocument.MaxWidth); Assert.AreEqual(48, LevelStudioDocument.MaxHeight);
    }

    // ── S207：大房间镜头（死亡细胞式）+ 屏外箭头 + 小地图 + 按路线放宽时间 ─────────
    [Test]
    public void BigRoomCameraPicksSmartFollowOnlyWhenNeeded()
    {
        var t = Tuning();
        Assert.AreEqual(Step1CameraMode.SmartFollow, t.bigRoomCamera, "大房间默认智能跟随");
        Assert.AreEqual(Step1CameraMode.SmartFollow, Step1RoomCamera.AutoMode(Step1CameraMode.WholeRoom, new Rect(0, 0, 94, 15), t.maxWholeRoomHeight, t.maxWholeRoomWidth, t.bigRoomCamera), "宽房间 → 智能跟随（不再缩成小框）");
        Assert.AreEqual(Step1CameraMode.SmartFollow, Step1RoomCamera.AutoMode(Step1CameraMode.WholeRoom, new Rect(0, 0, 48, 45), t.maxWholeRoomHeight, t.maxWholeRoomWidth, t.bigRoomCamera), "高楼 → 智能跟随");
        Assert.AreEqual(Step1CameraMode.WholeRoom, Step1RoomCamera.AutoMode(Step1CameraMode.WholeRoom, new Rect(0, 0, 48, 15), t.maxWholeRoomHeight, t.maxWholeRoomWidth, t.bigRoomCamera), "默认房间照旧看整屏");
        Assert.AreEqual(Step1CameraMode.FollowTrickster, Step1RoomCamera.AutoMode(Step1CameraMode.FollowTrickster, new Rect(0, 0, 94, 15), t.maxWholeRoomHeight, t.maxWholeRoomWidth, t.bigRoomCamera), "你手动选的模式不被改");
        Assert.AreEqual((Step1CameraMode)3, Step1CameraMode.SmartFollow, "新枚举只加在末尾（旧存档不串）");
    }

    [Test]
    public void SmartViewFollowsYouFramesMarioWhenCloseAndStaysInRoom()
    {
        var room = new Rect(-0.5f, -0.5f, 94, 15);
        Step1RoomCamera.SmartView(room, 16f / 9f, 0.5f, new Vector2(47, 5), null, 12, 18, 16, 3, out var c, out float size, out bool both);
        Assert.AreEqual(6f, size, 0.01f, "一屏 12 格高"); Assert.IsFalse(both);
        Assert.AreEqual(50f, c.x, 0.01f, "朝前多看 3 格");
        Step1RoomCamera.SmartView(room, 16f / 9f, 0.5f, new Vector2(47, 5), new Vector2(57, 5), 12, 18, 16, 3, out c, out size, out both);
        Assert.IsTrue(both, "马里奥靠近 → 两人都框进来"); Assert.AreEqual(52f, c.x, 0.01f);
        Step1RoomCamera.SmartView(room, 16f / 9f, 0.5f, new Vector2(47, 5), new Vector2(90, 5), 12, 18, 16, 3, out c, out size, out both);
        Assert.IsFalse(both, "离得远 → 只跟你（屏外箭头指他）"); Assert.AreEqual(6f, size, 0.01f);
        Step1RoomCamera.SmartView(room, 16f / 9f, 0.5f, new Vector2(1, 1), null, 12, 18, 16, 3, out c, out size, out _);
        Assert.GreaterOrEqual(c.x - size * 16f / 9f, room.xMin - 0.5f - 0.01f, "左边不看出房间外");
        Assert.GreaterOrEqual(c.y - size, room.yMin - 0.5f - 0.01f, "下边不看出房间外");
        Assert.AreEqual(5f, Step1RoomCamera.DeadZoneFollow(5f, 6f, 1.5f), "小跳不动镜头");
        Assert.AreEqual(7.5f, Step1RoomCamera.DeadZoneFollow(5f, 9f, 1.5f), 0.001f, "离开死区只跟超出的部分");
        Assert.AreEqual(9f, Step1RoomCamera.DeadZoneFollow(float.NaN, 9f, 1.5f), "第一帧直接对准");
    }

    [Test]
    public void OffscreenArrowAndMiniMapMath()
    {
        Assert.IsTrue(Step1OffscreenMarkers.Offscreen(new Vector3(1.3f, 0.5f, 1f)));
        Assert.IsFalse(Step1OffscreenMarkers.Offscreen(new Vector3(0.5f, 0.5f, 1f)));
        Assert.IsTrue(Step1OffscreenMarkers.Offscreen(new Vector3(0.5f, 0.5f, -1f)), "在镜头后面也算屏外");
        var p = Step1OffscreenMarkers.EdgePoint(new Vector2(3f, 0.5f), 0.05f);
        Assert.AreEqual(0.95f, p.x, 0.001f); Assert.AreEqual(0.5f, p.y, 0.001f);
        p = Step1OffscreenMarkers.EdgePoint(new Vector2(0.5f, -2f), 0.05f); Assert.AreEqual(0.05f, p.y, 0.001f);
        Assert.AreEqual("?", Step1OffscreenMarkers.MarkOf(MarioMindState.Curious), "H6：和头顶符号同一含义");
        Assert.AreEqual("!", Step1OffscreenMarkers.MarkOf(MarioMindState.Investigating));
        var map = Step1MiniMap.Layout(1920, 94, 15, 300, 120, 10);
        Assert.AreEqual(1920 - 10, map.xMax, 0.01f, "贴右上角"); Assert.LessOrEqual(map.width, 300.01f); Assert.LessOrEqual(map.height, 120.01f);
        Assert.AreEqual(map.width / map.height, 94f / 15f, 0.01f, "按房间比例");
        var pt = Step1MiniMap.ToMap(map, 94, 15, new Vector2(0, 0)); Assert.Less(pt.x, map.x + 5); Assert.Greater(pt.y, map.yMax - 5);
        Assert.AreEqual(new Vector2(90, 8), Step1MiniMap.CellOf(LevelWorkshopModel.LongHallSample, 'o'));
        string cam = Read("Scripts/Gameplay/Step1/Step1RoomCamera.cs");
        StringAssert.Contains("Step1Keys.Down(KeyCode.C)", cam, "C 换镜头走统一按键表");
        foreach (var f in new[] { "Scripts/Gameplay/Step1/Step1OffscreenMarkers.cs", "Scripts/Gameplay/Step1/Step1MiniMap.cs" })
        {
            string src = Read(f);
            StringAssert.Contains("Step1HandsOffCheck.IsRunning", src, "自动检查时不画");
        }
        foreach (var f in new[] { "Scripts/Gameplay/Step1/RushMarioMind.cs", "Scripts/Gameplay/Step1/MarioMindDriver.cs", "Scripts/Gameplay/Step1/MarioEyes.cs" })
        {
            string src = Read(f);
            StringAssert.DoesNotContain("Step1MiniMap", src, "H4：小地图是给玩家看的，马里奥不读");
            StringAssert.DoesNotContain("Step1OffscreenMarkers", src, "H4：屏外箭头是给玩家看的，马里奥不读");
        }
        string builder = Read("Scripts/Editor/Step1PrankRoomBuilder.cs");
        StringAssert.Contains("AddComponent<Step1OffscreenMarkers>()", builder); StringAssert.Contains("AddComponent<Step1MiniMap>()", builder);
        Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 18, "新场景组件 → 构建器版本 +1");
    }

    [Test]
    public void LongLevelsGetMoreTimeButDefaultRoomUnchanged()
    {
        var t = Tuning();
        Assert.AreEqual(150f, StrategySim.RoundTimeLimit(t.roundTimeLimit, 20f, t.roundTimePerRouteSecond), "默认房间（一趟 ≈20 秒）不变");
        Assert.AreEqual(70f, StrategySim.HandsOffTimeout(t.autoCheckRoundTimeoutSeconds, 20f, t.handsOffTimePerRouteSecond, t.handsOffTimeMargin), "默认房间自动检查超时不变");
        Assert.AreEqual(180f, StrategySim.RoundTimeLimit(150f, 60f, 3f), "长关卡放宽");
        Assert.AreEqual(140f, StrategySim.HandsOffTimeout(70f, 60f, 2f, 20f), "长关卡自动检查不误判卡住");
        Assert.AreEqual(150f, StrategySim.RoundTimeLimit(150f, 0f, 3f), "走不通 → 按基础时间");
        Assert.Greater(Step1PrankRoomBuilder.RouteSeconds(LevelWorkshopModel.LongHallSample, t), Step1PrankRoomBuilder.RouteSeconds(LevelWorkshopModel.LureSample, t), "长廊比诱捕走廊路线长");
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 15, "新调参字段 → 数据版本 +1（旧资产自动补默认值）");
    }

    [Test]
    public void LongHallSampleIsPlayableAndInScope()
    {
        System.Func<char, bool> s = reg().IsSolid;
        var g = LevelWorkshopModel.LongHallSample;
        Assert.AreEqual(94, g[0].Length); Assert.AreEqual(15, g.Length);
        Assert.IsTrue(LevelWorkshopModel.Check(g, true, s).Playable, LevelWorkshopModel.Check(g, true, s).Headline);
        CollectionAssert.IsEmpty(LevelWorkshopModel.BoundsIssues(g, s));
        StringAssert.Contains("LongHallSample", Read("Scripts/Editor/LevelWorkshopWindow.cs"), "工坊有按钮");
    }

    // ── S207：工坊/网页的移动工具（已放的东西能点住拖走）──
    [Test]
    public void MoveToolSelectsDragsCopiesAndKeepsFrame()
    {
        var g = new[] { "WWWWWWW", "W.....W", "W.^^..W", "W#####W", "WWWWWWW" };
        var sel = LevelWorkshopModel.SelectAt(g, 2, 2).Value;
        Assert.AreEqual(2, sel.x0); Assert.AreEqual(3, sel.x1, "相连的同种一起选");
        Assert.IsNull(LevelWorkshopModel.SelectAt(g, 0, 0), "外圈选不中"); Assert.IsNull(LevelWorkshopModel.SelectAt(g, 1, 3), "空气选不中");
        Assert.AreEqual(2, LevelWorkshopModel.SelectAt(g, 2, 1).Value.x1, "地面只选一格");
        CollectionAssert.AreEqual(new[] { "WWWWWWW", "W..^^.W", "W.....W", "W#####W", "WWWWWWW" }, LevelWorkshopModel.MoveBlock(g, sel, 1, 1));
        var cl = LevelWorkshopModel.ClampMove(7, 5, sel, 9, 9); Assert.AreEqual(2, cl.dx); Assert.AreEqual(1, cl.dy, "不会推进外圈");
        CollectionAssert.AreEqual(new[] { "WWWWWWW", "W.....W", "W.^^..W", "W##^^#W", "WWWWWWW" }, LevelWorkshopModel.PasteBlock(g, LevelWorkshopModel.CopyBlock(g, sel), 3, 1));
        var u = new[] { "WWWWWWW", "W.....W", "W.M.o.W", "W#####W", "WWWWWWW" };
        var all = new LevelWorkshopModel.Sel(1, 1, 5, 3);
        Assert.AreEqual(0, LevelWorkshopModel.CopyBlock(u, new LevelWorkshopModel.Sel(1, 2, 5, 2)).Count, "马里奥/宝物不复制（只能有一个）");
        StringAssert.Contains("M", LevelWorkshopModel.ClearBlock(u, all)[2], "清空不删唯一元素");
        StringAssert.Contains("移动", LevelWorkshopModel.ToolHint(LevelWorkshopModel.Tool.Move, false));
        string web = File.ReadAllText(Path.Combine(Application.dataPath, "..", "tools", "LevelStudioWeb", "logic.js"));
        foreach (var fn in new[] { "function selectAt", "function moveBlock", "function clampMove", "function copyBlock", "function pasteBlock", "function clearBlock", "function cameraPlan" })
            StringAssert.Contains(fn, web, "网页与 Unity 同规则：" + fn);
        string app = File.ReadAllText(Path.Combine(Application.dataPath, "..", "tools", "LevelStudioWeb", "app.js"));
        StringAssert.Contains("rules: 'S208'", app);
    }

    // ── S208：新建关卡向导 / 模式印章 / 节奏条 / 转移点提示（网页与 Unity 同规则 LevelBlueprint）──
    [Test]
    public void WizardDraftsArePlayableCleanAndDeterministic()
    {
        foreach (char star in LevelBlueprint.WizardStars)
            foreach (int sec in new[] { 20, 30, 40 })
            {
                var d = LevelBlueprint.Wizard(star, sec, "");
                Assert.AreEqual(LevelBlueprint.WidthFor(sec), d.grid[0].Length);
                Assert.IsTrue(LevelWorkshopModel.Check(d.grid, true, reg().IsSolid).Playable, $"向导 {star} {sec}s 生成后就能玩");
                Assert.AreEqual(5, d.beats.Length); Assert.AreEqual(4, d.notes.Count, "每段一条批注");
                CollectionAssert.AreEqual(d.grid, LevelBlueprint.Wizard(star, sec, "").grid, "同样的选择 → 同样的草稿");
                var st = StrategySim.Analyze(d.grid, 5f, 4f, 3, 1.6f, 1.5f, 3f, 1.8f);
                Assert.IsNotNull(st.route); Assert.IsFalse(st.trapAfterReinforce, "炸弹困不住（H1/H9）");
                var route = st.route.Select(c => (c.x, c.y)).ToList(); var times = new List<float> { 4f }; float len = 0;
                for (int i = 1; i < route.Count; i++) { len += (float)System.Math.Sqrt((route[i].x - route[i - 1].x) * (route[i].x - route[i - 1].x) + (route[i].y - route[i - 1].y) * (route[i].y - route[i - 1].y)); times.Add(4f + len / 5f); }
                var passes = LevelBlueprint.Passes(route, times, st.onRoute.Select(s => (s.x, s.y)));
                Assert.Greater(passes.Count, st.onRoute.Count - 1, "回程也算经过");
                Assert.AreEqual(0, LevelBlueprint.CoverHints(d.grid, st.onRoute.Select(s => (s.ch, s.x, s.y))).Count, $"向导 {star} {sec}s：每个机关旁都有地方躲");
            }
        StringAssert.Contains("被塌桥坑", LevelBlueprint.Wizard('C', 20, "被塌桥坑").goal, "一句话点子写进设计意图");
    }

    [Test]
    public void PatternStampsRhythmAndCoverFollowRules()
    {
        var baseRoom = LevelBlueprint.Wizard('~', 20, "").grid.ToArray();
        int row = baseRoom.Length - 1 - 3;
        baseRoom[row] = new string(baseRoom[row].Select((ch, i) => i > 5 && i < baseRoom[row].Length - 5 && "MGoT".IndexOf(ch) < 0 ? '.' : ch).ToArray());
        for (int r = baseRoom.Length - 3; r < baseRoom.Length - 1; r++) baseRoom[r] = "W" + new string('#', baseRoom[r].Length - 2) + "W";
        Assert.AreEqual(8, LevelBlueprint.Patterns.Length);
        foreach (var p in LevelBlueprint.Patterns)
            Assert.IsTrue(LevelWorkshopModel.Check(LevelBlueprint.Stamp(baseRoom, p, 12, 3), true, reg().IsSolid).Playable, "印章单独盖在空房间里可玩：" + p.zh);
        var g = new[] { "WWWWWWWW", "W......W", "W.M..o.W", "W######W" };
        var stamped = LevelBlueprint.Stamp(g, LevelBlueprint.Get("ambush"), 2, 1);
        Assert.AreEqual('M', stamped[2][2], "印章不覆盖 M/T/G/o"); Assert.AreEqual('W', LevelBlueprint.Stamp(g, LevelBlueprint.Get("slide"), 1, 1)[2][7], "印章不动外圈");
        var (segs, warn) = LevelBlueprint.Rhythm(new[] { 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f }, 30f);
        Assert.IsTrue(segs.Any(s => s.busy && s.b - s.a >= 8f)); Assert.AreEqual(2, warn.Count, "连续紧张 ≥8 秒 + 后面 16 秒没事 → 两条提醒");
        Assert.AreEqual(0, LevelBlueprint.Rhythm(new[] { 6f, 12f, 18f }, 22f).warn.Count, "红绿交替 → 没有提醒");
        var open = new[] { "WWWWWWWWWWWWWWW", "W.............W", "W......~......W", "W#############W" };
        Assert.AreEqual(1, LevelBlueprint.CoverHints(open, new[] { ('~', 7, 1) }).Count, "5 格内没有草丛/箱子 → 提示");
        Assert.AreEqual(0, LevelBlueprint.CoverHints(new[] { open[0], open[1], "W...b..~......W", open[3] }, new[] { ('~', 7, 1) }).Count, "旁边有草丛 → 不提示");
        // 网页与 Unity 同规则（同名函数 + 同一份印章数据）
        string web = File.ReadAllText(Path.Combine(Application.dataPath, "..", "tools", "LevelStudioWeb", "logic.js"));
        foreach (var fn in new[] { "function stampPattern", "function wizardLevel", "function beatBounds", "function routePasses", "function rhythm", "function coverHints" })
            StringAssert.Contains(fn, web, "网页与 Unity 同规则：" + fn);
        foreach (var p in LevelBlueprint.Patterns)
            StringAssert.Contains($"id: '{p.id}', zh: '{p.zh}', def: '{p.def}', stand: {p.stand}, rows: [{string.Join(", ", p.rows.Select(r => "'" + r + "'"))}]", web, "印章数据两边一致：" + p.zh);
        StringAssert.Contains($"RHYTHM = {{ busyHalf: {LevelBlueprint.BusyHalf}, maxBusy: {LevelBlueprint.MaxBusy}, maxIdle: {LevelBlueprint.MaxIdle}, startGrace: {LevelBlueprint.StartGrace} }}", web);
        StringAssert.Contains($"COVER_CHARS = '{LevelBlueprint.CoverChars}'", web);
        string win = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Editor", "LevelWorkshopWindow.cs"));
        StringAssert.Contains("WizardMenu()", win); StringAssert.Contains("StampMenu()", win); StringAssert.Contains("DrawBlueprint(canvas, size)", win);
        string app = File.ReadAllText(Path.Combine(Application.dataPath, "..", "tools", "LevelStudioWeb", "app.js"));
        StringAssert.Contains("if (S.firstRun) setTimeout(openWizard", app, "第一次打开网页直接弹向导");
    }

    // ── S209：用户截图——马里奥在宝物上方台边左右徘徊拿不到；捣蛋者跳到墙边伪装后悬在半空 ──
    [Test]
    public void MarioWalksOffLedgeWhenGoalIsDirectlyBelow()
    {
        Assert.AreEqual(0f, LevelPathPlanner.SteerX(0.1f, 0f, true, 1f), "同一高度对准了 = 停下（不变）");
        Assert.AreEqual(1f, LevelPathPlanner.SteerX(0.5f, 0f, true, -1f), "离得远就朝目标走（不变）");
        Assert.AreEqual(1f, LevelPathPlanner.SteerX(0.1f, -2f, true, -1f), "宝物在台边正下方：继续朝宝物那侧走下台边（旧规则这里停住 = 截图的徘徊）");
        Assert.AreEqual(-1f, LevelPathPlanner.SteerX(0f, -2f, true, -1f), "正好对准：按朝向走下去");
        Assert.AreEqual(0f, LevelPathPlanner.SteerX(0.1f, -2f, false, 1f), "已经在空中：不再推，直直落下");
        StringAssert.Contains("LevelPathPlanner.SteerX(dx, dy, _mario.IsGrounded, facingDir)", Read("Scripts/Core/HeuristicBotInputProvider.cs"), "游戏里的 AI 用同一条规则");
    }

    [Test]
    public void EverySampleTowerAndWizardLevelCanBeWalkedTheWayMarioWalks()
    {
        var levels = new List<(string, string[])> { ("Hakoniwa", LevelWorkshopModel.HakoniwaSample), ("Prison", LevelWorkshopModel.PrisonSample), ("Lure", LevelWorkshopModel.LureSample), ("LongHall", LevelWorkshopModel.LongHallSample) };
        for (int f = 2; f <= FloorStacker.MaxFloors; f++) levels.Add(("tower" + f, FloorStacker.Build(f, 0)));
        foreach (char star in LevelBlueprint.WizardStars) levels.Add(("wizard " + star, LevelBlueprint.Wizard(star, 30, "").grid));
        foreach (var (name, g) in levels)
            Assert.IsTrue(LevelRouteFollower.Run(g).ok, name + "：" + LevelRouteFollower.Run(g).Summary);
        Assert.IsFalse(LevelRouteFollower.Run(LevelWorkshopModel.HakoniwaSample, true).ok, "旧转向规则在地下监狱样板里会卡住（复现截图）");
        StringAssert.Contains("LevelRouteFollower.Run(variant)", Read("Scripts/Editor/LevelWorkshopModel.cs"), "工坊检查按马里奥走法走一遍");
    }

    [Test]
    public void StuckRescueMovesMarioForwardAlongHisRoute()
    {
        var g = LevelWorkshopModel.HakoniwaSample;
        var loot = CellOfIn(g, 'o');
        var p = Step1StuckRescue.RescueAlongRoute(g, new Vector2(43f, 3f), new Vector2(loot.x, loot.y), null);
        Assert.IsTrue(p.HasValue, "能沿路线找到下一个站位");
        Assert.Less(Mathf.Abs(p.Value.x - loot.x) + Mathf.Abs(p.Value.y - loot.y), Mathf.Abs(43f - loot.x) + Mathf.Abs(3f - loot.y), "救援后离宝物更近（原来放回原处 → 又卡住）");
        StringAssert.Contains("RescueCandidates(roomGrid, pos, goal, SafeCells(), repeat ? 5f : 2f)", Read("Scripts/Gameplay/Step1/Step1StuckRescue.cs")); // S240：沿路线往前放（候选列表第一批）
    }

    [Test]
    public void BodiesArePushedOutOfWallsAndWallCheckRunsAfterInput()
    {
        Assert.AreEqual(Vector2.zero, BodyUnstick.PushFor(false, Vector2.right, 0.1f, 0.02f, 0.5f), "没重叠不推");
        Assert.AreEqual(Vector2.zero, BodyUnstick.PushFor(true, Vector2.right, -0.01f, 0.02f, 0.5f), "擦边（接触偏移）不推");
        var push = BodyUnstick.PushFor(true, Vector2.right, -0.2f, 0.02f, 0.5f);
        Assert.Less(push.x, -0.2f); Assert.AreEqual(0f, push.y, 1e-4f); // 墙在右边 → 往左推出来
        Assert.AreEqual(0.5f, BodyUnstick.PushFor(true, Vector2.down, -3f, 0.02f, 0.5f).magnitude, 1e-4f, "每帧最多推 0.5 格（不瞬移）");
        float oldBottom = -0.025f - 0.95f * 0.5f;
        Assert.AreEqual(oldBottom, DisguiseSystem.FeetAlignedOffsetY(-0.025f, 0.95f, 1.2f) - 1.2f * 0.5f, 1e-4f, "伪装变大后脚底仍在原位（往上长，不长进地里）");
        string tc = Read("Scripts/Enemy/TricksterController.cs");
        StringAssert.Contains("BodyUnstick.Resolve(rb, boxCollider, groundLayer)", tc);
        int fixedStep = tc.IndexOf("private void FixedUpdate()");
        Assert.Greater(tc.IndexOf("HitsWall(_frameVelocity.x > 0f", fixedStep), tc.IndexOf("HandleDirection();", fixedStep), "贴墙判定在方向键之后（原来在之前 → 方向键又把朝墙速度加回去 = 粘墙）");
        StringAssert.Contains("BodyUnstick.Resolve(rb, boxCollider, groundLayer)", Read("Scripts/Player/MarioController.cs"));
    }

    // ── S216：手感 / 视觉 ─────────────────────────────────
    [Test]
    public void S216_LaunchArcsHaveGravityAndFitRooms()
    {
        var t = Tuning();
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 18);
        // 旧 bug：硬直期往上飞没有重力。现在每一步都在减速
        var v = Step1Feel.StunStep(new Vector2(0f, 15f), false, 0.02f, t.launchGravity, 40f, 0f, 0f, false);
        Assert.Less(v.y, 15f, "往上飞也受重力");
        var spring = Step1Feel.Simulate(new Vector2(t.springForwardPush, t.springLaunchSpeed), t.launchGravity, 40f, t.launchAirDrag, t.launchGroundFriction);
        Assert.Greater(spring.apex, 2f); Assert.Less(spring.apex, ElementCatalog.SpringHeadroomCells - 0.9f, "弹簧弹高 + 身高 < 头顶空格（以前 0.6 秒匀速飘 9 格撞天花板）");
        var blast = Step1Feel.Simulate(TricksterBomb.BlastVelocity(Vector2.zero, new Vector2(0.1f, 0f), t.bombRadius, t.bombKnockback, t.blastLift), t.launchGravity, 40f, t.launchAirDrag, t.launchGroundFriction);
        Assert.Greater(blast.apex, 0.4f, "炸飞看得出往上掀"); Assert.Less(blast.range + blast.slideAfter, 4f, "以前被横着推 8 格");
        Assert.Greater(TricksterBomb.BlastVelocity(Vector2.zero, new Vector2(0.1f, 0f), 2f, 6f, 8f).x, TricksterBomb.BlastVelocity(Vector2.zero, new Vector2(1.9f, 0f), 2f, 6f, 8f).x, "离爆心越近越猛");
        var hurt = Step1Feel.Simulate(new Vector2(5f, KnockbackHelper.HurtLift(2f, t.hurtLift)), t.launchGravity, 40f, t.launchAirDrag, t.launchGroundFriction);
        Assert.Greater(hurt.apex, 0.2f, "受伤有'哎哟'小跳"); Assert.Less(hurt.range + hurt.slideAfter, 3f);
        // 落地才恢复控制；H9：最多多等 landGrace 秒
        Assert.IsFalse(Step1Feel.StunOver(-0.1f, true, false, 1.5f));
        Assert.IsTrue(Step1Feel.StunOver(-0.1f, true, true, 1.5f));
        Assert.IsTrue(Step1Feel.StunOver(-1.6f, true, false, 1.5f), "H9：半空卡住也会结束");
        Assert.IsTrue(Step1Feel.StunOver(0f, false, false, 1.5f), "普通受伤到时间就好");
        // 香蕉皮：落地不刹车
        var slide = Step1Feel.StunStep(new Vector2(7f, 0f), true, 0.1f, 40f, 40f, 2f, 40f, true);
        Assert.AreEqual(7f, slide.x, 1e-3f);
        Assert.Less(Step1Feel.StunStep(new Vector2(7f, 0f), true, 0.1f, 40f, 40f, 2f, 40f, false).x, 7f, "别的落地会刹停");
    }

    [Test]
    public void S216_VisualCurvesAreSmoothAndReadable()
    {
        Assert.Greater(Step1Feel.TelegraphRate(8f, 1f), Step1Feel.TelegraphRate(8f, 0f), "预警越接近发动越急");
        Vector2 a = Step1Feel.TelegraphShake(1f, 0.5f, 0.05f), b = Step1Feel.TelegraphShake(1.016f, 0.5f, 0.05f);
        Assert.Less((a - b).magnitude, 0.05f, "预警抖动是平滑的（以前每帧随机跳）");
        Assert.Less(Step1Feel.SpringPadScaleY(0f), 1f, "弹簧先压下"); Assert.Greater(Step1Feel.SpringPadScaleY(0.08f), 1f, "再弹出");
        Assert.AreEqual(1f, Step1Feel.SpringPadScaleY(0.9f), 1e-3f, "最后回到原样");
        Step1Feel.Ring(0f, 0.25f, out float s0, out float a0); Step1Feel.Ring(1f, 0.25f, out float s1, out float a1);
        Assert.AreEqual(0.25f, s0, 1e-3f); Assert.AreEqual(1f, s1, 1e-3f); Assert.AreEqual(1f, a0, 1e-3f); Assert.AreEqual(0f, a1, 1e-3f);
        Assert.AreEqual(1f, Step1Feel.HurtTint(0.01f, 0.18f)); Assert.AreEqual(0f, Step1Feel.HurtTint(0.5f, 0.18f));
        Vector2 s = Step1Feel.ShakeOffset(3f, 0.3f, 1f), s2 = Step1Feel.ShakeOffset(3.01f, 0.3f, 1f);
        Assert.LessOrEqual(s.magnitude, 0.3f * 1.5f); Assert.Less((s - s2).magnitude, 0.12f, "震屏平滑");
        Assert.AreEqual(Vector2.zero, Step1Feel.ShakeOffset(3f, 0.3f, 0f), "结束干净");
        Assert.AreEqual(0f, Step1Feel.DropProgress(0f, 0.12f)); Assert.AreEqual(1f, Step1Feel.DropProgress(0.12f, 0.12f));
    }

    [Test]
    public void S216_Wiring()
    {
        string mc = CodeOnly(Read("Scripts/Player/MarioController.cs")), tc = CodeOnly(Read("Scripts/Enemy/TricksterController.cs"));
        StringAssert.Contains("Step1Feel.StunStep(", mc); StringAssert.Contains("Step1Feel.StunStep(", tc);
        StringAssert.Contains("Step1Feel.StunOver(", mc); StringAssert.Contains("Step1Feel.StunOver(", tc);
        StringAssert.Contains("ApplyKnockbackStun(airStunSeconds, true, false)", Read("Scripts/LevelElements/Pranks/SpringPad.cs"));
        StringAssert.Contains("ApplyKnockbackStun(slipSeconds, false, true)", Read("Scripts/LevelElements/Pranks/BananaPeel.cs"));
        StringAssert.Contains("ApplyKnockbackStun(stun, true, false)", Read("Scripts/Gameplay/Step1/TricksterKit.cs"));
        StringAssert.Contains("Step1Fx.Link(", Read("Scripts/Gameplay/Step1/ChainPlan.cs"), "连锁看得见导火线");
        StringAssert.Contains("Step1Feel.ShakeOffset(", Read("Scripts/Gameplay/Step1/Step1RoomCamera.cs"));
        StringAssert.Contains("Step1Feel.HurtTint(", Read("Scripts/Player/PlayerHealth.cs"));
        StringAssert.Contains("LaunchFeel.Apply(tuning)", Read("Scripts/Gameplay/Step1/Step1Combo.cs"));
        StringAssert.Contains("Step1Feel.TelegraphRate(", Read("Scripts/Ability/ControllablePropBase.cs"));
        // 特效不碰物理（H4：马里奥不读特效）
        string fx = CodeOnly(Read("Scripts/Gameplay/Step1/Step1Fx.cs"));
        StringAssert.DoesNotContain("Collider2D", fx); StringAssert.DoesNotContain("Rigidbody2D", fx);
        foreach (var f in new[] { "RushMarioMind", "MarioMindDriver", "MarioEyes", "MarioVision" })
            StringAssert.DoesNotContain("Step1Fx", CodeOnly(Read("Scripts/Gameplay/Step1/" + f + ".cs")), "H4：马里奥不看特效");
        Assert.LessOrEqual(Step1Fx.MaxAlive, 120, "同屏特效上限");
    }

    // ── S223：马里奥中招反应（总方案阶段 B，纯画面）──────────────────────
    [Test]
    public void Reaction_EveryComboKindHasOneFixedBeat_AndNeverOutlastsItsStun()
    {
        var t = MarioMindTuningSO.LoadOrDefault();
        foreach (var kind in new[] { "hurt", "trip", "slip", "launch", "cage", "snare", "drop", "pit", "stop" })
        {
            Assert.IsTrue(MarioReaction.TryGet(MarioReaction.Default, kind, out var b), kind + " 没有反应（H6：每种坑一种固定反应）");
            Assert.IsTrue(MarioReaction.TryGet(MarioReaction.Default, kind, out var b2) && b2.pose == b.pose, "同一种坑永远同一个动作");
            StringAssert.Contains("\n", MarioReaction.Line(b), "台词中英两行");
        }
        Assert.LessOrEqual(MarioReaction.Default[0].Held, t.hurtStunSeconds, "被烧到：演戏不超过本来就晕的时间（H9 不延长）");
        MarioReaction.TryGet(MarioReaction.Default, "trip", out var trip); Assert.LessOrEqual(trip.Held, t.tripStunSeconds);
        MarioReaction.TryGet(MarioReaction.Default, "slip", out var slip); Assert.LessOrEqual(slip.Held, t.bananaSlipSeconds);
        MarioReaction.TryGet(MarioReaction.Default, "launch", out var launch); Assert.LessOrEqual(launch.Held, t.springAirStunSeconds);
        MarioReaction.TryGet(MarioReaction.Default, "cage", out var cage); Assert.LessOrEqual(cage.Held, t.cageSeconds);
        MarioReaction.TryGet(MarioReaction.Default, "snare", out var snare); Assert.LessOrEqual(snare.Held, t.snareSeconds);
        foreach (var k in new[] { "drop", "pit", "stop" }) { MarioReaction.TryGet(MarioReaction.Default, k, out var b); Assert.LessOrEqual(b.Total, MarioReaction.MaxUnstunnedTotal, k + "：边走边演，不能长"); }
    }

    [Test]
    public void Reaction_ThreeBeats_EndAtIdentity_AndChainSkipsFreeze()
    {
        MarioReaction.TryGet(MarioReaction.Default, "hurt", out var b);
        Assert.AreEqual(0, MarioReaction.Sample(b, 0.1f, 1f).phase, "先愣住");
        Assert.Greater(MarioReaction.Sample(b, 0.1f, 1f).sx, 1f, "愣住 = 压扁");
        Assert.AreEqual(1, MarioReaction.Sample(b, b.freeze + 0.1f, 1f).phase);
        Assert.AreEqual(2, MarioReaction.Sample(b, b.Held + 0.05f, 1f).phase);
        var end = MarioReaction.Sample(b, b.Total + 0.01f, 1f);
        Assert.AreEqual(1f, end.sx); Assert.AreEqual(1f, end.sy); Assert.AreEqual(0f, end.rotDeg);
        Assert.AreEqual(b.freeze, MarioReaction.StartTime(b, true), "连锁中跳过愣住，不越连越拖");
        Assert.AreEqual(0f, MarioReaction.StartTime(b, false));
        MarioReaction.TryGet(MarioReaction.Default, "launch", out var spin);
        Assert.AreEqual(-MarioReaction.Sample(spin, spin.freeze + spin.act * 0.5f, 1f).rotDeg, MarioReaction.Sample(spin, spin.freeze + spin.act * 0.5f, -1f).rotDeg, 0.001f, "按朝向转");
    }

    [Test]
    public void Reaction_DataFileMatchesDefaults_AndBadFileFallsBack()
    {
        var file = MarioReaction.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Resources/MarioReactions.json")), out string err);
        Assert.AreEqual("", err);
        Assert.AreEqual(MarioReaction.ToJson(MarioReaction.Default), MarioReaction.ToJson(file), "数据文件 = 默认表");
        var bad = MarioReaction.Parse("{oops", out string e2);
        Assert.AreNotEqual("", e2); Assert.AreEqual(MarioReaction.Default.Length, bad.Length, "写坏了也不会让游戏坏");
        var part = MarioReaction.Parse("{\"reactions\":[{\"kind\":\"hurt\",\"act\":0.5}]}", out _);
        Assert.AreEqual(MarioReaction.Default.Length, part.Length, "缺的种类用默认补上");
        MarioReaction.TryGet(part, "hurt", out var h); Assert.AreEqual(0.5f, h.act, 0.001f);
    }

    [Test]
    public void Reaction_IsVisualOnly_WiredThroughComboEvent()
    {
        string view = Read("Scripts/Gameplay/Step1/MarioReactionView.cs");
        StringAssert.Contains("ComboRegistered += HandleCombo", view);
        StringAssert.Contains("visual == transform) visual = null", view, "外观 = 身体时不演（不动碰撞体）");
        foreach (var banned in new[] { "ApplyKnockbackStun", "ExtendStun", "velocity", "MarioSpeedScale", "TricksterController" })
            StringAssert.DoesNotContain(banned, view, "反应只是画面：" + banned);
        StringAssert.Contains("AddComponent<MarioReactionView>()", Read("Scripts/Gameplay/Step1/Step1Combo.cs"), "旧场景自动挂上");
        StringAssert.Contains("reaction.CurrentLine", Read("Scripts/Gameplay/Step1/MarioMindLabel.cs"));
        StringAssert.Contains("laughed", Step1PlaytestLog.CsvHeader);
    }

    // ── S224：总方案阶段 C（可读性收尾）+ 少等待 ──
    [Test]
    public void Readability_ConeFillGrowsWithSuspicion_NeverPastWalls()
    {
        Assert.AreEqual(0f, Step1Readability.FillReach(0f, 9f, 9f), 1e-4f, "平静 = 不灌");
        Assert.AreEqual(4.5f, Step1Readability.FillReach(0.5f, 9f, 9f), 1e-4f, "一半起疑 = 灌一半");
        Assert.AreEqual(3f, Step1Readability.FillReach(1f, 3f, 9f), 1e-4f, "墙后面不灌");
        Assert.AreEqual(9f, Step1Readability.FillReach(2f, 9f, 9f), 1e-4f, "超过 1 按 1");
        Assert.AreNotEqual(Step1Readability.FillColor(SuspicionLevel.Curious), Step1Readability.FillColor(SuspicionLevel.Alert), "黄 = ?，红 = !");
        var t = Tuning();
        Assert.AreEqual(t.hearingRange, Step1Readability.SoundRadius(Step1Readability.Sound.Taunt, t), 1e-4f, "声音圈 = MarioEyes 的听力范围");
        Assert.AreEqual(t.hearingRange / 3f, Step1Readability.SoundRadius(Step1Readability.Sound.Vent, t), 1e-4f, "通风管咣当 = NoteNoiseNear 的 1/3");
        StringAssert.Contains("hearingRange / 3f", Read("Scripts/Gameplay/Step1/MarioEyes.cs"), "圈和判定同一个数");
    }

    [Test]
    public void Readability_SoundRingsAndFill_AreVisualOnly()
    {
        string rings = Read("Scripts/Gameplay/Step1/Step1SoundRings.cs");
        foreach (var ev in new[] { "TauntAbility.Taunted +=", "TricksterBomb.Exploded +=", "CrackedWall.Smashed +=", "Vent.Clanged +=", "ChainPlan.Clicked +=" })
            StringAssert.Contains(ev, rings, "和 MarioMindDriver 喂给 MarioEyes 的是同一组声音事件");
        StringAssert.DoesNotContain("RustleOnPass", rings, "草晃是看见，不是听见：不画声音圈");
        foreach (var banned in new[] { "MarioEyes", "Meter.Add", "NoteNoise", "Collider", "Rigidbody" })
            StringAssert.DoesNotContain(banned, rings, "声音圈只是画面：" + banned);
        StringAssert.Contains("AddComponent<Step1SoundRings>()", Read("Scripts/Gameplay/Step1/Step1Combo.cs"), "旧场景自动挂上");
        string cone = Read("Scripts/Gameplay/Step1/MarioVisionConeView.cs");
        StringAssert.Contains("Step1Readability.FillReach(meter.Normalized, clear[i], t.visionRange)", cone);
        StringAssert.DoesNotContain("Meter.Set", cone); StringAssert.DoesNotContain("Meter.Add", cone);
        // H4：马里奥侧代码不因为画面而读捣蛋者
        foreach (var f in new[] { "Scripts/Gameplay/Step1/MarioEyes.cs", "Scripts/Gameplay/Step1/RushMarioMind.cs" })
            StringAssert.DoesNotContain("Step1SoundRings", Read(f));
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 23);
        Assert.IsTrue(Tuning().visionConeFill && Tuning().soundRings);
    }

    [Test]
    public void NearMiss_SegmentsPeaksAndCaught()
    {
        var log = new NearMissLog();
        log.Feed(false, 0f, "06:00", "");
        log.Feed(true, 0.4f, "07:00", ""); log.Feed(true, 0.7f, "07:01", "Rustle"); log.Feed(false, 0f, "07:02", "");
        log.Feed(true, 0.5f, "09:00", "Open"); log.Caught("09:01", "Open");
        log.Feed(true, 0.9f, "11:00", "Lamp"); log.Feed(false, 0f, "11:01", "");
        log.Feed(true, 0.36f, "12:00", ""); log.Feed(false, 0f, "12:01", "");
        Assert.AreEqual(4, log.All.Count);
        Assert.AreEqual(3, log.NearMisses, "被抓那段不算差点");
        var top = log.Closest(2);
        Assert.AreEqual("11:00", top[0].clock); Assert.AreEqual(0.9f, top[0].peak, 1e-4f);
        Assert.AreEqual("07:00", top[1].clock); Assert.AreEqual("Rustle", top[1].why, "原因取第一次有的");
        StringAssert.Contains("草晃了", Step1Text.NearMissLines(top, log.NearMisses));
        StringAssert.Contains("没怀疑过", Step1Text.NearMissLines(new List<NearMissLog.Moment>(), 0));
        StringAssert.Contains("NearMiss.Caught(", Read("Scripts/Overworld/OverworldTown.cs"));
        StringAssert.Contains("Step1Text.NearMissLines(", Read("Scripts/Overworld/OverworldSession.cs"));
        StringAssert.Contains("RoomNearMissHint(", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs"), "房间问卷'差点被发现'题给参考");
    }

    [Test]
    public void LessWaiting_AutoFastArmedAmbushAndEnterToStart()
    {
        // 小镇：不碰键盘一会儿自动快进；在门口按过一次 E = 预约，他走近自动进门（规则还是 AmbushReady）
        var m = OverworldPack.Parse(OverworldPack.SampleText)[0];
        var t = Tuning();
        OverworldSession.NewDay(m.name, "Town", 1); OverworldSession.Active = true;
        var town = new OverworldTown(m, t) { autoFastIdleSeconds = t.overworldAutoFastIdleSeconds };
        bool sped = false;
        for (int i = 0; i < 30 * 5; i++) { town.Tick(1f / 30f, new OverworldTown.Input()); if (town.timeScale > 1f) sped = true; }
        Assert.IsTrue(sped, "不碰键盘 1.5 秒后自动快进");
        town.Tick(1f / 30f, new OverworldTown.Input { h = 1f });
        Assert.AreEqual(1f, town.timeScale, "一碰方向键立刻恢复正常速度");
        string src = Read("Scripts/Overworld/OverworldTown.cs");
        StringAssert.Contains("if (AmbushReady) { ambushArmed = false; EnterRoom(next, OverworldMind.DoorOutcome.Ambush); }", src, "预约埋伏用的还是同一条埋伏规则");
        StringAssert.Contains("if (Spotted) { ambushArmed = false; Hint(Note.Spotted); return; }", src, "被盯上就取消预约");
        StringAssert.Contains("town.autoFastIdleSeconds = tuning.overworldAutoFastIdleSeconds", Read("Scripts/Overworld/Runtime/OverworldGame.cs"));
        // 房间：开局等待按 Enter 马上开始（自动检查时不认）
        string drv = Read("Scripts/Gameplay/Step1/MarioMindDriver.cs");
        StringAssert.Contains("!Step1HandsOffCheck.IsRunning && !Step1Screen.HelpOpen && !Step1PlaytestLog.IsTyping && startDelay < tuning.startDelaySeconds - 0.3f && Step1Keys.Down(KeyCode.Return)", drv);
        StringAssert.Contains("Enter", Step1Text.MarioStateText(MarioMindState.Running, true, false, 3f));
        OverworldSession.ResetStatics();
    }

    // ── S225：按了就有反应 + 他说为什么起疑 + 卡住救援一句话 ──
    [Test]
    public void FailFeedback_EveryAbilityReasonHasChinese_DisplayOnly()
    {
        string fallback = Step1Text.AbilityFailZh("???");
        foreach (var r in new[] { "Too small to trigger props!", "Must be disguised to control props!", "Stay still to blend in first!", "Ability not ready!", "No controls remaining!",
            "No controllable prop nearby!", "Possession gate blocked: Blending", "No cannonballs left! Undisguise, stand in the cannon and press Down to sit in.", "Prop on cooldown!",
            "Prop already active!", "Prop uses exhausted!", "Prop not ready!", "Not enough energy to disguise!" })
        {
            string z = Step1Text.AbilityFailZh(r);
            Assert.AreNotEqual(fallback, z, "没翻译：" + r);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(z, "[\u4e00-\u9fff]"), r);
        }
        StringAssert.Contains("3 秒", Step1Text.DisguiseFailZh(false, false, 2.2f, false), "冷却说还要几秒");
        Assert.IsNotNull(Step1Text.DisguiseFailZh(true, false, 0f, false), "缩小时");
        Assert.IsNotNull(Step1Text.DisguiseFailZh(false, true, 0f, false), "刚被发现");
        Assert.IsNull(Step1Text.DisguiseFailZh(false, false, 0f, false), "能变 = 不提示");
        string ff = Read("Scripts/Gameplay/Step1/Step1FailFeedback.cs");
        StringAssert.Contains("self.OnAbilityFailed += OnAbilityFailed", ff);
        StringAssert.Contains("disguise.OnDisguiseFailed += OnAbilityFailed", ff);
        foreach (var banned in new[] { "OnAbilityPressed(", "OnDisguisePressed(", "ToggleDisguise", "TryConsume", "velocity" })
            StringAssert.DoesNotContain(banned, ff, "只显示原因，不按键、不改数值：" + banned);
        StringAssert.Contains("AddComponent<Step1FailFeedback>()", Read("Scripts/Gameplay/Step1/Step1Combo.cs"), "旧场景自动挂上");
    }

    [Test]
    public void Mind_SaysWhyItIsSuspicious_TieredAndFromPerceptOnly()
    {
        Assert.AreEqual(SuspicionCause.SawYou, RushMarioMind.StrongestCause(true, true, true, true, true, true), "看见你最具体");
        Assert.AreEqual(SuspicionCause.Hurt, RushMarioMind.StrongestCause(false, false, true, true, true, true));
        Assert.AreEqual(SuspicionCause.Rustle, RushMarioMind.StrongestCause(false, false, false, true, false, false));
        Assert.AreEqual(SuspicionCause.None, RushMarioMind.StrongestCause(false, false, false, false, false, false));
        var t = Tuning(); var mind = new RushMarioMind(t); mind.Reset(1);
        MarioOrder o = default;
        for (int i = 0; i < 10 && mind.State == MarioMindState.Running; i++)
            o = mind.Tick(Dt, new MarioPercept { marioPos = Vector2.zero, heardTaunt = true, tauntPos = new Vector2(3f, 0f), facingRight = true });
        Assert.AreEqual(MarioMindState.Curious, o.state, "挑衅 → ?");
        Assert.AreEqual(SuspicionCause.Taunt, o.cause); Assert.AreEqual(1, o.causeTimes);
        StringAssert.Contains("叫我", Step1Text.CauseIntent(o.state, o.cause, o.causeTimes), "说出原因");
        Assert.AreNotEqual(Step1Text.CauseIntent(MarioMindState.Curious, SuspicionCause.Taunt, 1), Step1Text.CauseIntent(MarioMindState.Curious, SuspicionCause.Taunt, 3), "第 3 次起换短句");
        Assert.IsNull(Step1Text.CauseIntent(MarioMindState.Chasing, SuspicionCause.SawYou, 1), "追人时照旧喊'站住'");
        StringAssert.Contains("坑过", Step1Text.HeadIntent(MarioMindState.Running, "CAREFUL"), "小心时说他记得这儿挨过坑");
        foreach (SuspicionCause c in System.Enum.GetValues(typeof(SuspicionCause)))
            if (c != SuspicionCause.None) StringAssert.Contains("\n", Step1Text.CauseIntent(MarioMindState.Curious, c, 1), "两行：" + c);
        string label = Read("Scripts/Gameplay/Step1/MarioMindLabel.cs");
        StringAssert.Contains("Step1Text.CauseIntent(order.state, order.cause, order.causeTimes)", label);
        StringAssert.Contains("MarioMindLabel.RaiseRescued()", Read("Scripts/Gameplay/Step1/Step1StuckRescue.cs"), "卡住救援头顶一句话");
        foreach (var banned in new[] { "TricksterPossessionGate", "IsFullyBlended", "DisguiseSystem", "TricksterController" })
            StringAssert.DoesNotContain(banned, Read("Scripts/Gameplay/Step1/RushMarioMind.cs"), "H4：原因只来自感知 " + banned);
    }

    // ═════════ S231：第三轮调研落地 ═════════
    [Test]
    public void S231_ThirdTimeLine_VersionCompare_RarestTrick_ArtAudit()
    {
        MarioReaction.TryGet(MarioReaction.Default, "slip", out var b);
        Assert.AreEqual(MarioReaction.Line(b), MarioReaction.Line(b, 2));
        StringAssert.Contains("又是这个", MarioReaction.Line(b, 3));
        Assert.AreEqual(0.875, Step1ExitReport.A12(new[] { 3, 4 }, new[] { 3, 2 }), 1e-9);
        Assert.AreEqual(2.5f, Step1ExitReport.Median(new[] { 1, 2, 3, 4 }));
        Step1ExitReport.Round R(int v, int w, params string[] k) => new Step1ExitReport.Round { time = System.DateTime.Now, winner = "Mario", wantAgain = w, version = v, kinds = new System.Collections.Generic.List<string>(k) };
        var few = new System.Collections.Generic.List<Step1ExitReport.Round> { R(27, 5), R(26, 1) };
        StringAssert.Contains("每边要", Step1ExitReport.CompareVersions(few));
        var hist = new System.Collections.Generic.List<Step1ExitReport.Round>(); for (int i = 0; i < 6; i++) hist.Add(R(27, 3, "Fire"));
        StringAssert.Contains("铁笼", Step1ExitReport.RarestLine(hist, new[] { "Fire", "Cage" }));
        Assert.AreEqual("", Step1ExitReport.RarestLine(hist, new[] { "Fire" }));
        foreach (var key in OverworldArt.Icons.Keys) CollectionAssert.IsEmpty(OverworldArt.Audit(OverworldArt.Pixels(key), OverworldArt.Size), key);
        var view = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Gameplay/Step1/MarioReactionView.cs"));
        StringAssert.Contains("MarioReaction.Line(beat, nth)", view);
    }

    // ═════════ S235：掉出房间（用户实测：捣蛋者掉出去就回不来、血不掉、马里奥照样跑） ═════════
    [Test]
    public void S235_FallOutOfRoom_GuardWalls_BackToSpawn_LoseLife()
    {
        // 出界判定：站在外墙旁边不算，越过外墙中线才算
        Assert.IsFalse(Step1Bounds.IsOut(new Vector2(0.9f, 3f), 48, 12), "贴着左墙站着不算出界");
        Assert.IsFalse(Step1Bounds.IsOut(new Vector2(46.1f, 10.4f), 48, 12), "右上角里面不算");
        Assert.IsTrue(Step1Bounds.IsOut(new Vector2(-0.1f, 3f), 48, 12), "左边出去");
        Assert.IsTrue(Step1Bounds.IsOut(new Vector2(20f, -0.2f), 48, 12), "掉到地面下面");
        Assert.IsTrue(Step1Bounds.IsOut(new Vector2(20f, 11.2f), 48, 12), "飞出天花板");
        Assert.IsFalse(Step1Bounds.IsOut(new Vector2(-50f, -50f), 0, 0), "没有房间信息 = 不判");
        // 推出墙只往里推
        var c = Step1Bounds.ClampInside(new Vector2(-0.3f, 3f), 48, 12, new Vector2(0.4f, 0.475f));
        Assert.Greater(c.x, 0.5f); Assert.AreEqual(3f, c.y, 1e-4);
        // 看不见的墙把房间外一圈全挡住，房间里一格不挡
        for (int x = -2; x <= 49; x++) { Assert.IsTrue(Step1Bounds.InGuard(new Vector2(x, -1.5f), 48, 12)); Assert.IsTrue(Step1Bounds.InGuard(new Vector2(x, 12.5f), 48, 12)); }
        for (int y = -2; y <= 13; y++) { Assert.IsTrue(Step1Bounds.InGuard(new Vector2(-1.5f, y), 48, 12)); Assert.IsTrue(Step1Bounds.InGuard(new Vector2(48.5f, y), 48, 12)); }
        Assert.IsFalse(Step1Bounds.InGuard(new Vector2(1f, 1f), 48, 12)); Assert.IsFalse(Step1Bounds.InGuard(new Vector2(46f, 10f), 48, 12));
        // 掉几条命：无敌期 0；命不够就掉到 0
        Assert.AreEqual(1, Step1Bounds.LivesLost(3, 1, false)); Assert.AreEqual(0, Step1Bounds.LivesLost(3, 1, true)); Assert.AreEqual(1, Step1Bounds.LivesLost(1, 2, false)); Assert.AreEqual(0, Step1Bounds.LivesLost(3, 0, false));
        Assert.AreEqual(Step1Text.Outcome.TricksterFellOut, Step1Text.Classify("Mario", TricksterLives.FellOutReason));
        Assert.IsFalse(Step1Text.PlayerWon(Step1Text.Outcome.TricksterFellOut));
        // 被传送走 = 绳套 / 铁笼 / 炮放人
        Assert.IsTrue(Step1Bounds.Teleported(new Vector2(3f, 3f), new Vector2(20f, 4.5f), 1.5f)); Assert.IsFalse(Step1Bounds.Teleported(new Vector2(20f, 3f), new Vector2(20f, 4.5f), 1.5f));
        // 默认房间外圈没有"会塌 / 能穿"的口子；外圈放单向台面 → 工坊提醒
        CollectionAssert.IsEmpty(Step1Bounds.BorderLeaks(Step1PrankRoomBuilder.Room));
        var leaky = (string[])Step1PrankRoomBuilder.Room.Clone(); leaky[0] = "WWWW----" + leaky[0].Substring(8);
        Assert.AreEqual(4, Step1Bounds.BorderLeaks(leaky).Count);
        var t = Tuning(); t.dataVersion = 26; t.fallOutLivesLost = 0; t.UpgradeData();
        Assert.AreEqual(1, t.fallOutLivesLost); Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 27); Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 21);
        // 接线：构建器挂守卫；推出墙用房间范围夹；你出界走 FellOut；装填 / 绳套 / 铁笼被传送走就放人
        StringAssert.Contains("AddComponent<Step1RoomGuard>().Configure(tuning, room[0].Length, room.Length)", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        StringAssert.Contains("Step1Bounds.ClampInside(", Read("Scripts/Core/BodyUnstick.cs"));
        StringAssert.Contains("lives.FellOut(", Read("Scripts/Gameplay/Step1/Step1RoomGuard.cs"));
        StringAssert.Contains("rescue.RescueNow(", Read("Scripts/Gameplay/Step1/Step1RoomGuard.cs"));
        foreach (var f in new[] { "Scripts/LevelElements/Pranks/SnareTrap.cs", "Scripts/LevelElements/Pranks/IronCage.cs", "Scripts/LevelElements/Traps/PranksterCannon.cs" })
            StringAssert.Contains("Step1Bounds.Teleported(", Read(f), f);
        // 小镇：身体压进墙 / 地图外 → 挪到最近能站的格
        var m = OverworldPack.Parse(OverworldPack.SampleText)[0];
        var (ux, uy) = OverworldMap.Unstick(m, -3, -3); Assert.IsTrue(OverworldMap.Free(m, ux, uy));
        StringAssert.Contains("Unstick();", Read("Scripts/Overworld/OverworldTown.cs"));
    }

    // ═════════ S236：功能地图（防遗忘）+ 交互修复 ═════════
    [Test]
    public void S236_FeatureMap_CoversEveryMenu_AndInteractionFixes()
    {
        // 每个 MarioTrickster 菜单都被功能地图登记（新加菜单不登记 = 红）
        var root = Path.Combine(Application.dataPath, "Scripts");
        foreach (var cs in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(cs), "\\[MenuItem\\(\"(MarioTrickster/[^\"]+)\"(?!\\s*,\\s*true)"))
                Assert.IsTrue(FeatureMap.Covers(m.Groups[1].Value), "菜单没登记进功能地图：" + m.Groups[1].Value);
        // 每项的锚点代码都还在
        foreach (var f in FeatureMap.All)
        {
            var path = Path.GetFullPath(Path.Combine(root, f.anchorFile));
            Assert.IsTrue(File.Exists(path) && File.ReadAllText(path).Contains(f.anchorText), "功能地图锚点找不到：" + f.id);
        }
        Assert.AreEqual(FeatureMap.All.Length, FeatureMap.All.Select(f => f.id).Distinct().Count(), "id 不重复");
        foreach (var g in FeatureMap.Goals) foreach (var id in g.ids) Assert.IsNotNull(FeatureMap.Get(id), g.title + " → " + id);
        StringAssert.Contains("MarioTrickster/📖 开始页 Start Here %&h", Read("Scripts/Editor/StartHereWindow.cs"));
        StringAssert.Contains("StartHereWindow.Open()", Read("Scripts/Editor/TestHubWindow.cs"));
        StringAssert.Contains("Step1PrankRoomBuilder.HandsOffMenu", Read("Scripts/Editor/TestHubWindow.cs"));
        // 交互修复：被自己炸光命不再显示"马里奥带着宝物逃走了"
        Assert.AreEqual(Step1Text.Outcome.TricksterSelfHit, Step1Text.Classify("Mario", TricksterLives.SelfHitReason));
        StringAssert.DoesNotContain("逃走", Step1Text.Headline(Step1Text.Outcome.TricksterSelfHit));
        // 问卷数字两套输入都读；F9 = 技能无限，且不算进出口
        StringAssert.Contains("Step1Keys.Digit1to5()", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs"));
        StringAssert.DoesNotContain("Input.GetKeyDown(KeyCode.Alpha0", Read("Scripts/Gameplay/Step1/Step1PlaytestLog.cs"));
        StringAssert.Contains("Step1QuickTest.NoLimits = noCooldownMode", Read("Scripts/Core/GameManager.cs"));
        var rs = new List<Step1ExitReport.Round>();
        for (int i = 0; i < 6; i++) rs.Add(new Step1ExitReport.Round { time = new System.DateTime(2026, 10, 8, 20, i, 0), winner = "Mario", reason = "x", seconds = 30, mode = i < 4 ? "f9" : "room", version = 27 });
        Assert.AreEqual(2, Step1ExitReport.Analyze(rs).rounds, "F9 测试的局不算出口");
        // 冻住的身体不再沉进地板
        StringAssert.Contains("if (rb.isKinematic && _isKnockbackStunned) { _frameVelocity = Vector2.zero; rb.velocity = Vector2.zero; return; }", Read("Scripts/Enemy/TricksterController.cs"));
        StringAssert.Contains("if (rb.isKinematic && _isKnockbackStunned) { _frameVelocity = Vector2.zero; rb.velocity = Vector2.zero; return; }", Read("Scripts/Player/MarioController.cs"));
        // H4：马里奥侧不读测试开关
        foreach (var f in new[] { "RushMarioMind.cs", "MarioMindDriver.cs", "SuspicionMeter.cs", "MarioVision.cs", "MarioEyes.cs" })
            StringAssert.DoesNotContain("Step1QuickTest", Read("Scripts/Gameplay/Step1/" + f));
    }

    // ═════════ S237：去冗余（菜单合并、调参分组）+ 开始页「上次做到哪」═════════
    [Test]
    public void S237_TuningGrouped_MenusMerged_LastWork()
    {
        var fields = TuningGroups.Fields(typeof(MarioMindTuningSO)).Where(x => x.field.Name != "dataVersion").ToList();
        foreach (var (f, h) in fields)
        {
            Assert.IsNotNull(TuningGroups.GroupOf(h), "调参没分组：" + f.Name + "（" + h + "）");
            Assert.IsNotEmpty(TuningGroups.Tip(f).Trim(), "调参没有中文说明：" + f.Name);
        }
        foreach (var n in TuningGroups.Common) Assert.IsTrue(fields.Any(x => x.field.Name == n), "常用里找不到 " + n);
        // 菜单顶层合并
        var tops = new HashSet<string>();
        foreach (var cs in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(cs), "\\[MenuItem\\(\"MarioTrickster/([^\"/]+)"))
                tops.Add(FeatureMap.StripShortcut(m.Groups[1].Value));
        Assert.LessOrEqual(tops.Count, 12, "MarioTrickster 菜单顶层：" + string.Join(", ", tops));
        foreach (var old in new[] { "Step 1", "Run Tests", "Level Design", "Art Pipeline", "网页同步", "Overworld" }) Assert.IsFalse(tops.Contains(old), "旧根菜单还在：" + old);
        // 上次做到哪
        var now = new System.DateTime(2026, 10, 8, 15, 30, 0);
        string st = RecentWork.Push("", RecentWork.Room, "诱捕走廊", now.AddHours(-1));
        st = RecentWork.Push(st, RecentWork.Room, "诱捕走廊", now);
        Assert.AreEqual(1, RecentWork.Parse(st).Count, "同名只留最新");
        Assert.AreEqual("5 分钟前", RecentWork.Ago(now.AddMinutes(-5), now));
        bool bad; StringAssert.Contains("必须改", RecentWork.HealthLine("**2 个必须改，0 个提醒**\n✗ 某关：出不去\n", out bad)); Assert.IsTrue(bad);
        StringAssert.Contains("LastWork()", Read("Scripts/Editor/StartHereWindow.cs"));
        StringAssert.Contains("StartHereWindow.Touch(RecentWork.Town", Read("Scripts/Editor/OverworldBuilder.cs"));
        StringAssert.Contains("[CustomEditor(typeof(MarioMindTuningSO))]", Read("Scripts/Editor/MarioMindTuningSOEditor.cs"));
        // 改任何数值 → ▶ 试玩自动重建场景（以前只认主题）
        var t1 = ScriptableObject.CreateInstance<MarioMindTuningSO>(); var t2 = ScriptableObject.CreateInstance<MarioMindTuningSO>(); t2.springLaunchSpeed += 1f;
        Assert.AreNotEqual(Step1PrankRoomBuilder.BuildKey(t1), Step1PrankRoomBuilder.BuildKey(t2));
        Assert.AreEqual(Step1PrankRoomBuilder.BuildKey(t1), Step1PrankRoomBuilder.BuildKey(ScriptableObject.CreateInstance<MarioMindTuningSO>()));
    }

    // ── S238：删旧工具 + 只留一个调参文件 + 性格一个下拉 ──────────
    [Test]
    public void S238_OldToolsGone_OneTuningFile_OnePersonalitySetting()
    {
        foreach (var gone in new[] { "Scripts/Editor/TestConsoleWindow.cs", "Scripts/Editor/StudioExplorationRunner.cs", "Scripts/Editor/TestSceneBuilder.cs",
            "Scripts/Editor/LevelStudioPlaySession.cs", "Scripts/Core/MemoryGuard.cs", "Scripts/LevelDesign/GameplayLoopConfigSO.cs", "Resources/GameplayLoopConfig.asset" })
            Assert.IsFalse(File.Exists(Path.Combine(Application.dataPath, gone)), gone + " 已删（S238）");
        // 性格：旧两个设置 → 一个下拉，换算不丢原意
        Assert.AreEqual(Step1PersonalityChoice.Random, MarioMindTuningSO.FromOld(true, -1));
        Assert.AreEqual(Step1PersonalityChoice.Rush, MarioMindTuningSO.FromOld(false, -1), "关了随机 = 永远冲冲型");
        Assert.AreEqual(Step1PersonalityChoice.Cautious, MarioMindTuningSO.FromOld(true, 1), "固定性格优先");
        Assert.AreEqual(Step1PersonalityChoice.Greedy, MarioMindTuningSO.FromOld(false, 2));
        var t = ScriptableObject.CreateInstance<MarioMindTuningSO>();
        try
        {
            Assert.AreEqual(-1, t.ForcedPersonalityIndex);
            t.marioPersonality = Step1PersonalityChoice.Cautious; Assert.AreEqual((int)MarioPersonalityKind.Cautious, t.ForcedPersonalityIndex);
            // 扫描 / 能量 / 附身：只从 RushMarioTuning 读；默认值 = 旧文件里的值
            Assert.AreEqual(5f, t.scanRadius); Assert.AreEqual(8f, t.scanCooldown); Assert.AreEqual(100f, t.energyMaxEnergy); Assert.AreEqual(15f, t.energyControlCost); Assert.AreEqual(0.8f, t.possessionRevealDuration);
            GameplayMetrics.SetTuning(t); t.scanRadius = 7f;
            Assert.AreEqual(7f, GameplayMetrics.ScanRadius(5f), "改 RushMarioTuning 的扫描半径 → 马里奥的 Q 扫描跟着变");
        }
        finally { GameplayMetrics.SetTuning(null); Object.DestroyImmediate(t); }
        StringAssert.Contains("GameManager.EditorRestartHandler = Retry;", Read("Scripts/Editor/PlayRetry.cs"), "F5 / R 在编辑器里仍然 = 停止再进 Play");
        StringAssert.DoesNotContain("personalitiesEnabled ?", Read("Scripts/Gameplay/Step1/RushMarioMind.cs"));
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 28);
    }

    static LevelPathPlanner.Cell CellOfIn(string[] g, char c)
    {
        for (int r = 0; r < g.Length; r++) { int x = g[r].IndexOf(c); if (x >= 0) return new LevelPathPlanner.Cell(x, g.Length - 1 - r); }
        return new LevelPathPlanner.Cell(-1, -1);
    }

    static AsciiElementRegistry reg() => AsciiElementRegistry.GetDefault();

    [Test]
    public void S226_ContextKeyBar_ShowsOnlyUsableKeys_CoreAlwaysThere()
    {
        string idle = Step1Text.ControlsBarFor(false, false, false, false, false, false, false);
        foreach (var core in new[] { "← →", "↑ 跳", "P 伪装", "L 触发", "H ", "Esc" }) StringAssert.Contains(core, idle);
        StringAssert.DoesNotContain("B 炸弹", idle, "炸弹用完/冷却 → 不显示");
        string all = Step1Text.ControlsBarFor(false, false, true, true, true, true, true);
        Assert.AreEqual(3, new[] { "↓ 钻", "B 炸弹", "G 诱饵", "T 挑衅", "Z 缩小" }.Count(x => all.Contains(x)), "能力键最多 3 个");
        StringAssert.Contains("P 变回", Step1Text.ControlsBarFor(true, false, false, false, false, false, false));
        var t = ScriptableObject.CreateInstance<MarioMindTuningSO>();
        Assert.IsTrue(t.contextKeyBar); Assert.AreEqual(1f, t.roomGameSpeed, "默认速度不改手感");
        Assert.AreEqual(0.5f, MarioMindTuningSO.ClampRoomSpeed(0.1f)); Assert.AreEqual(1f, MarioMindTuningSO.ClampRoomSpeed(2f));
    }

    [Test]
    public void S226_ReactionPunch_FrontLoaded()
    {
        Assert.AreEqual(0f, MarioReaction.Punch(0f), 1e-4f); Assert.AreEqual(1f, MarioReaction.Punch(MarioReaction.PunchPeak), 1e-4f); Assert.AreEqual(0f, MarioReaction.Punch(1f), 1e-4f);
        Assert.Greater(MarioReaction.Punch(0.2f), MarioReaction.Punch(0.6f), "前重后轻");
    }

    [Test]
    public void S227_ExitReport_ReadsOldAndNewCsv_WithWilson()
    {
        string oldCsv = "timestamp,round,winner,reason,seconds\n2026-09-26 22:37:39,1,Mario,Route cleared.,10.7,1,2,2,2,Escape:2,1,no,no,unfair:couldnt_read_him,1,\n";
        var rs = Step1ExitReport.ParseAll(new[] { oldCsv, oldCsv });
        Assert.AreEqual(1, rs.Count, "同一局两个文件里都有 → 只算一次");
        Assert.AreEqual("unfair:couldnt_read_him", rs[0].verdict); Assert.AreEqual(1, rs[0].wantAgain); Assert.AreEqual(0, rs[0].version);
        var w = Step1ExitReport.Wilson(5, 5); Assert.Less(w.lo, 0.6, "5/5 的下限只有 ~0.57");
        var r = Step1ExitReport.Analyze(rs);
        Assert.IsFalse(r.exitMet); StringAssert.Contains("只改呈现", string.Join(" ", r.next));
        CollectionAssert.AreEqual(Step1PlaytestLog.CsvHeader.Split(','), Step1ExitReport.Columns, "两边列名必须一致");
        Assert.AreEqual(0, Step1ExitReport.Analyze(rs, 24).rounds, "旧版本的局不混进结论");
        StringAssert.Contains("Step1ExitReport.Markdown", System.IO.File.ReadAllText("Assets/Scripts/Editor/TestHubWindow.cs"), "体检里有出口进度");
        StringAssert.Contains("旧版本", string.Join(" ", r.lines), "没有版本号的旧局要说明");
        Assert.AreEqual("Banana", Step1PlaytestLog.PrankKindOfCombo("slip")); Assert.AreEqual("Spring", Step1PlaytestLog.PrankKindOfCombo("launch"));
        Assert.AreEqual("", Step1PlaytestLog.PrankKindOfCombo("hurt"), "受伤由机关归因，不重复记");
        string log = System.IO.File.ReadAllText("Assets/Scripts/Gameplay/Step1/Step1PlaytestLog.cs");
        StringAssert.Contains("combo.ComboRegistered += HandleCombo", log);
        StringAssert.Contains("if (OverworldSession.Active || Step1QuickTest.On || Step1QuickTest.UsedThisRound) { roundMode", log, "小镇房间 / 快速测试也要记一行");
        var town = Step1ExitReport.Parse(Step1PlaytestLog.CsvHeader + "\n" + Step1PlaytestLog.CsvRow(new System.DateTime(2026, 10, 2), 1, "Mario", "x", 30f, 3, 0, 1, 0, new Dictionary<string, int> { { "Banana", 1 } }, null, 0, 0, 0, 0, "Rush", 24, "town"));
        Assert.AreEqual(1, town.Count); Assert.AreEqual("town", town[0].mode); Assert.AreEqual(24, town[0].version); Assert.IsNull(town[0].calculated, "没答问卷 = 空，不当成「否」");
    }

    // ── S240：坐进大炮 · 用过的机关 · 连击看得懂 · 卡住检测 ──
    [Test]
    public void S240_CannonSeat_FlyFarther_ReEnter()
    {
        var t = Tuning();
        Assert.AreEqual(29, MarioMindTuningSO.CurrentDataVersion);
        Assert.Greater(t.tricksterCannonSpeed, 18.5f, "捣蛋者坐炮比以前飞得远");
        Assert.Less(t.tricksterCannonCooldown, 5f, "落地很快就能再进 = 反复进");
        Assert.AreEqual(30f, t.cannonLaunchCooldown, 0.01f, "马里奥钻炮冷却不变");
        var v = PranksterCannon.LaunchVelocity(true, t.tricksterCannonSpeed, 40f);
        var arc = Step1Feel.Simulate(v, t.launchGravity, 40f, t.launchAirDrag, t.launchGroundFriction);
        var old = Step1Feel.Simulate(PranksterCannon.LaunchVelocity(true, 18.5f, 40f), t.launchGravity, 40f, t.launchAirDrag, t.launchGroundFriction);
        Assert.Greater(arc.range, old.range * 1.6f, "至少远 60%");
        Assert.IsTrue(PranksterCannon.CanSeat(false, false, 0f, false, false));
        Assert.IsFalse(PranksterCannon.CanSeat(true, false, 0f, false, false), "伪装中不能进");
        Assert.IsFalse(PranksterCannon.CanSeat(false, true, 0f, false, false), "缩小中不能进");
        Assert.IsFalse(PranksterCannon.CanSeat(false, false, 0.5f, false, false), "冷却中不能进");
        Assert.IsFalse(PranksterCannon.CanSeat(false, false, 0f, false, true), "里面有人不能进");
        var a = PranksterCannon.SeatAimStep(true, 40f, true, false, 1f, 90f, 0.5f, PranksterCannon.SeatAimMin, PranksterCannon.SeatAimMax);
        Assert.IsFalse(a.faceRight); Assert.AreEqual(80f, a.angle, 0.01f, "按住 ↑ 连续转，夹在上限");
        var pts = PranksterCannon.PreviewArc(Vector2.zero, v, t.launchGravity, 40f, t.launchAirDrag);
        Assert.Greater(pts.Count, 10); Assert.Greater(pts.Max(p => p.y), 2f, "轨迹预览有抛物线");
        string c = Read("Scripts/LevelElements/Traps/PranksterCannon.cs");
        StringAssert.Contains("Step1Keys.Down(KeyCode.Space)", c, "空格发射");
        StringAssert.Contains("Step1Keys.Down(KeyCode.DownArrow)", c, "↓ 进炮");
        StringAssert.Contains("f.EnterSeat(SeatPos())", c, "坐着身体锁住");
        StringAssert.Contains("PranksterCannon.TricksterSeated", Read("Scripts/Gameplay/Step1/TricksterKit.cs"), "坐着不能放炸弹");
        StringAssert.Contains("↓ 坐进大炮", Step1Text.ControlsBarFor(false, false, false, false, false, false, false, true));
        StringAssert.Contains("空格", Step1Text.ControlsBarSeated);
    }

    [Test]
    public void S240_SpentProps_GreyAndSkipped()
    {
        Assert.IsTrue(ControllablePropBase.IsSpent(PropControlState.Exhausted, 1, 0, true), "次数用完");
        Assert.IsTrue(ControllablePropBase.IsSpent(PropControlState.Idle, -1, -1, false), "裂缝已碎 / 铁笼已落");
        Assert.IsFalse(ControllablePropBase.IsSpent(PropControlState.Cooldown, -1, -1, true), "冷却中不算用光");
        Assert.IsFalse(ControllablePropBase.IsSpent(PropControlState.Idle, 3, 2, true), "还能用");
        Assert.AreEqual(-1, ControllablePropBase.SelectRank(true, false), "用光的不选");
        Assert.Less(ControllablePropBase.SelectRank(false, true), ControllablePropBase.SelectRank(false, false), "能用的优先于冷却中的");
        var grey = ControllablePropBase.SpentTint(new Color(1f, 0.2f, 0.1f, 1f));
        Assert.AreEqual(grey.r, grey.g, 0.001f); Assert.Less(grey.r, 0.5f, "变灰变暗");
        string sys = Read("Scripts/Ability/TricksterAbilitySystem.cs");
        StringAssert.Contains("bp.SpentThisRound", sys, "绑着的用光了就换");
        StringAssert.Contains("if (cachedProps[i].SpentThisRound) continue;", sys, "不连线");
        StringAssert.Contains("!p.SpentThisRound", Read("Scripts/Gameplay/Step1/ChainPlan.cs"), "不编号");
        StringAssert.Contains("GreyWhenSpent => false", Read("Scripts/LevelElements/Traps/PranksterCannon.cs"), "大炮打完还能坐，不变灰");
    }

    [Test]
    public void S240_ComboShowsCauses_ChainBadgesReadable()
    {
        Assert.AreEqual("炮弹", Step1ComboFeel.CauseName("hurt", "炮弹"));
        Assert.AreEqual("火", Step1ComboFeel.CauseName("hurt"));
        Assert.AreEqual("香蕉皮", Step1ComboFeel.CauseName("slip"));
        Assert.AreEqual("炮弹 → 香蕉皮", Step1ComboFeel.ChainText(new[] { "炮弹", "香蕉皮" }));
        Assert.AreEqual("… → c → d → e → f", Step1ComboFeel.ChainText(new[] { "a", "b", "c", "d", "e", "f" }));
        Assert.AreEqual(1f, Step1ComboFeel.WindowLeft01(10f, 10f, 4f), 0.001f);
        Assert.AreEqual(0.5f, Step1ComboFeel.WindowLeft01(12f, 10f, 4f), 0.001f);
        Assert.AreEqual(0f, Step1ComboFeel.WindowLeft01(15f, 10f, 4f), 0.001f);
        string combo = Read("Scripts/Gameplay/Step1/Step1Combo.cs");
        StringAssert.Contains("CannonBall.HitMario += HandleCannonHit", combo, "炮弹打中 → 显示成炮弹，不是火");
        StringAssert.Contains("BombEvents.MarioBlasted += HandleBlasted", combo);
        Assert.IsFalse(ChainPlan.BadgeVisible(true, true, 0f, 8f), "打完的不画");
        Assert.IsTrue(ChainPlan.BadgeVisible(false, true, 99f, 8f), "进行中全画");
        Assert.IsFalse(ChainPlan.BadgeVisible(false, false, 20f, 8f), "没开始时远的不画");
        var st = ChainPlan.Stagger(new List<Vector2> { new Vector2(100, 100), new Vector2(105, 100), new Vector2(110, 100) }, 30f);
        for (int i = 0; i < st.Count; i++) for (int j = 0; j < i; j++) Assert.GreaterOrEqual(Vector2.Distance(st[i], st[j]), 29.9f, "挨得近的错开");
        Assert.AreEqual(1, ChainPlan.NextIndex(new List<string> { "a", "b", "c" }, new HashSet<string> { "a" }));
        Assert.AreEqual(-1, ChainPlan.NextIndex(new List<string> { "a" }, new HashSet<string> { "a" }));
        StringAssert.Contains("②", ChainPlan.ListLine(new[] { "火", "香蕉皮" }, new[] { true, false }, 1));
    }

    [Test]
    public void S240_StuckRescue_PausesNotResets_HardCap_CriticalJumps()
    {
        // 晕 / 东张西望 = 暂停（以前清零 → 坑底有火永远凑不满 6 秒）
        float still = 0f, hard = 0f; bool rescued = false;
        for (int i = 0; i < 400 && !rescued; i++)
        {
            bool paused = (i / 10) % 2 == 0; // 一半时间在晕
            var r = Step1StuckRescue.Tick(still, hard, 0.05f, false, paused, false, 6f, 15f);
            still = r.still; hard = r.hard; rescued = r.rescue;
            if (rescued) Assert.LessOrEqual(i * 0.05f, 12.1f, "暂停不清零：一半时间在晕，约 12 秒内也会救");
        }
        Assert.IsTrue(rescued);
        var p = Step1StuckRescue.Tick(0f, 14.99f, 0.05f, false, true, false, 6f, 15f);
        Assert.IsTrue(p.rescue, "一直在晕也有总兜底");
        Assert.IsFalse(Step1StuckRescue.Tick(5f, 5f, 1f, true, false, false, 6f, 15f).rescue, "不在赶路 = 清零");
        Assert.IsFalse(Step1StuckRescue.Tick(5.9f, 5f, 1f, false, false, true, 6f, 15f).rescue, "有进展 = 清零");
        string line = Step1StuckRescue.StuckLine("abc", new Vector2(21.2f, 1f), "没进展");
        var parsed = Step1StuckRescue.ParseStuckLog(line + "\n" + line + "\n" + Step1StuckRescue.StuckLine("zzz", Vector2.zero, ""), "abc");
        Assert.AreEqual(1, parsed.Count); Assert.AreEqual(2, parsed[21 * 1000 + 1], "同一格卡两次，别的图不算");
        for (int v = 0; v < 3; v++) Assert.AreEqual(0, LevelRouteFollower.CriticalJumpCells(Step1PrankRoomBuilder.ResolvedRoom(v)).Count, "默认房间（3 种布局，塌后也算）不靠跳满 2 格出坑");
        Assert.Greater(LevelRouteFollower.CriticalJumpCells(new[] { "WWWWWWWWWWWW", "W..........W", "W.G.M....o.W", "W######..###", "W######..###", "WWWWWWWWWWWW" }).Count, 0, "2 格深的坑要标黄");
        StringAssert.Contains("HasGroundNow(c)", Read("Scripts/Gameplay/Step1/Step1StuckRescue.cs"), "放之前查脚下真有地面（塌桥不算）");
        StringAssert.Contains("Step1Feedback.CaptureNote", Read("Scripts/Gameplay/Step1/Step1StuckRescue.cs"), "自动截图");
        StringAssert.Contains("CriticalJumpCells", Read("Scripts/Editor/LevelWorkshopWindow.cs"), "工坊检查轨迹显示黄格");
    }

    // ── S241：机关不怕早 · 光影 · 遁地 · 蛛丝 · 图标图例 ─────────────────────
    [Test]
    public void S241_ArmRefund_ForgivingTiming()
    {
        Assert.AreEqual(Step1Stealth.Arm.Wait, Step1Stealth.ArmStep(false, 1f, 5.5f), "早按 = 等他走进来");
        Assert.AreEqual(Step1Stealth.Arm.FireNow, Step1Stealth.ArmStep(true, 1f, 5.5f));
        Assert.AreEqual(Step1Stealth.Arm.Expire, Step1Stealth.ArmStep(false, 6f, 5.5f), "等太久作废（退还）");
        Assert.IsTrue(Step1Stealth.Missed(10f, 5f, 11f)); Assert.IsFalse(Step1Stealth.Missed(10f, 10.4f, 11f));
        var r = ControllablePropBase.MissRefund(4f, 0, 1);
        Assert.AreEqual(2f, r.cooldown, 1e-4f, "没打中冷却减半"); Assert.AreEqual(1, r.remaining, "次数退回");
        Assert.AreEqual(1, ControllablePropBase.MissRefund(4f, 1, 1).remaining, "不超过上限");
        string sys = Read("Scripts/Ability/TricksterAbilitySystem.cs");
        StringAssert.Contains("private void ArmProp(IControllableProp prop)", sys);
        StringAssert.Contains("playerFired", sys, "只退还你亲手按的");
        StringAssert.Contains("public override bool ArmOnPress => false;", Read("Scripts/LevelElements/Traps/PranksterCannon.cs"), "坐炮 / 开炮一按就生效");
    }

    [Test]
    public void S241_Light_DayNightRain_FlashlightAndLamps()
    {
        var g = new List<string> { "WWWWWWWWWW", "W........W", "W....W...W", "W........W", "W########W" };
        Assert.IsTrue(Step1Stealth.Lit(false, new Vector2(3, 1), null, false, Vector2.zero, true, 6, 28, g), "白天处处亮");
        var lamps = new List<Step1Stealth.Light> { new Step1Stealth.Light(new Vector2(2, 1), 3f) };
        Assert.IsTrue(Step1Stealth.Lit(true, new Vector2(4, 1), lamps, false, Vector2.zero, true, 6, 28, g));
        Assert.IsFalse(Step1Stealth.Lit(true, new Vector2(8, 1), lamps, false, Vector2.zero, true, 6, 28, g), "灯外暗");
        Assert.IsTrue(Step1Stealth.Lit(true, new Vector2(5, 1), null, true, new Vector2(1, 1), true, 6, 28, g), "手电筒前方亮");
        Assert.IsFalse(Step1Stealth.Lit(true, new Vector2(5, 1), null, true, new Vector2(8, 1), true, 6, 28, g), "手电筒背后暗");
        Assert.IsFalse(Step1Stealth.Lit(true, new Vector2(7, 2), null, true, new Vector2(2, 2), true, 8, 28, g), "墙挡光");
        Assert.IsFalse(Step1Stealth.OverheadNoticed(new Vector2(3, 1), new Vector2(3, 3), 2.5f, false), "暗处从头顶荡过不察觉");
        Assert.IsTrue(Step1Stealth.OverheadNoticed(new Vector2(3, 1), new Vector2(3, 3), 2.5f, true));
        Assert.AreEqual(Step1LightMode.Day, Step1Stealth.Resolve(Step1LightMode.Auto, 1), "第 1 局白天");
        var t = Tuning();
        Assert.Less(Step1Stealth.FootstepRadius(t, true), Step1Stealth.FootstepRadius(t, false), "雨盖住脚步");
        Assert.IsFalse(Step1Stealth.MakesFootstep(true, 3f, true), "伪装 / 遁地 / 摆荡没脚步声");
        string eyes = Read("Scripts/Gameplay/Step1/MarioEyes.cs");
        StringAssert.Contains("Step1Lighting.Visible", eyes, "看见 = 视锥 + 亮");
        StringAssert.Contains("TricksterFootsteps.Stepped += eyes.NoteFootstep", Read("Scripts/Gameplay/Step1/MarioMindDriver.cs"), "暗处只能听");
        foreach (var token in new[] { "MarioEyes", "Meter." }) StringAssert.DoesNotContain(token, Read("Scripts/Gameplay/Step1/Step1Lighting.cs"), "光影层只画画面 + 判亮暗，不读马里奥心智");
    }

    [Test]
    public void S241_Burrow_And_Silk()
    {
        Assert.IsTrue(Step1Stealth.MoundVisible('#', false, true, false, true), "白天裸地移动 = 露土包");
        Assert.IsFalse(Step1Stealth.MoundVisible('v', false, true, false, true), "草地看不见");
        Assert.IsFalse(Step1Stealth.MoundVisible('#', false, false, false, true), "夜里看不见");
        Assert.IsFalse(Step1Stealth.MoundVisible('#', false, true, true, true), "雨天看不见");
        Assert.IsFalse(Step1Stealth.MoundVisible('#', true, true, false, true), "草丛下看不见");
        Assert.IsTrue(Step1Stealth.Trampled(new Vector2(3, 1), new Vector2(3.3f, 1), '#'), "反制：踩出来");
        Assert.IsTrue(Step1Stealth.ScanFlushes(Vector2.zero, new Vector2(2, 0), 3f), "反制：扫描");
        Assert.IsFalse(Step1Stealth.CanBurrow(true, true, false, false, false, '#', 0f), "伪装中不能遁地");
        var a = new Vector2(5, 6); var s = Step1Stealth.FromBody(a, new Vector2(5, 2), new Vector2(2, 0));
        double w0 = System.Math.Abs(s.omega); var s2 = Step1Stealth.Step(s, 2.0, 0, 9.8, 0.0001, 0, 1.2, 8);
        Assert.Greater(System.Math.Abs(s2.omega), w0 * 3.9, "收线一半 → 角速度约 ×4（角动量守恒）");
        var g = new List<string> { "WWWWWWWWWW", "W........W", "W........W", "W........W", "W########W" };
        Assert.IsNotNull(Step1Stealth.FindAnchor(g, new Vector2(3, 1), true, 8), "能挂天花板");
        Assert.IsFalse(Step1Stealth.CanSilk(false, false, true, 0f), "地下不能射丝");
        foreach (var f in new[] { "Scripts/Gameplay/Step1/TricksterKit.cs", "Scripts/Gameplay/Step1/DecoyAbility.cs", "Scripts/Gameplay/Step1/TauntAbility.cs", "Scripts/Gameplay/Step1/ChainPlan.cs", "Scripts/LevelElements/Pranks/Vent.cs", "Scripts/LevelElements/Traps/PranksterCannon.cs" })
            StringAssert.Contains("TricksterBurrow.BodyBusy", Read(f), f + "：遁地 / 摆荡中别的技能键不生效");
        StringAssert.Contains("Step1Keys.Down(KeyCode.U)", Read("Scripts/Gameplay/Step1/TricksterBurrow.cs"));
        StringAssert.Contains("Step1Keys.Down(KeyCode.K)", Read("Scripts/Gameplay/Step1/TricksterSilk.cs"));
        StringAssert.Contains("U 遁地", Step1Text.ControlsBarFor(false, false, false, false, false, false, false, false, true, false));
    }

    [Test]
    public void S241_LampGrass_IconsAndLegendOnlyThisRoom()
    {
        Assert.IsNotNull(ElementCatalog.Get('i')); Assert.IsNotNull(ElementCatalog.Get('v'));
        string room = string.Join("\n", Step1PrankRoomBuilder.Room);
        StringAssert.Contains("i", room, "默认房间有灯"); StringAssert.Contains("v", room, "默认房间有草地");
        Assert.IsTrue(LevelWorkshopModel.HakoniwaSample.Any(r => r.Contains('i')) && LevelWorkshopModel.HakoniwaSample.Any(r => r.Contains('v')));
        var only = Step1MapLegend.ForRoom(new[] { "WWWW", "W~.W", "W##W" });
        Assert.IsTrue(only.Any(e => e.ch == '~')); Assert.IsFalse(only.Any(e => e.ch == 'J'), "图例只列这个房间有的");
        Assert.AreEqual(Step1MapLegend.Group.Prank, Step1MapLegend.GroupOf('~'));
        Assert.IsTrue(Step1MapLegend.TagNear(Vector2.zero, new Vector2(3, 0), 5.5f)); Assert.IsFalse(Step1MapLegend.TagNear(Vector2.zero, new Vector2(9, 0), 5.5f), "只标你身边的");
        Assert.IsTrue(Step1PropIcons.IsWhiteBox(4, 4)); Assert.IsFalse(Step1PropIcons.IsWhiteBox(32, 32), "有美术贴图就不盖图标");
        Assert.AreEqual(new Color(0.45f, 0.45f, 0.45f, 0.85f), Step1PropIcons.IconTint(false, true, Color.red), "用光了变灰");
        Assert.AreNotEqual(Color.white, Step1PropIcons.IconTint(true, false, Color.red), "预警时跟着闪");
        Assert.IsTrue(Step1Icons.Has("FireTrap") && Step1Icons.Has("RoomLamp"));
    }

    [Test]
    public void S241_WallSlideFix_NoStickOnWalls()
    {
        StringAssert.Contains("AirWallSlide", Read("Scripts/Player/MarioController.cs"), "空中顶墙不粘墙");
        StringAssert.Contains("SqueezeDirection", Read("Scripts/LevelElements/Traps/ControllableBlocker.cs"), "封路墙只横向挤");
    }

    // ── S242：黑匣子 · 伪装装备栏 · 道具诱饵 · 一目了然 ─────────────────
    [Test]
    public void S242_BlackBox_DetectsStuckAndKeepsSizeUnderBudget()
    {
        var ring = new Step1BlackBox.Ring<int>(3); for (int i = 0; i < 5; i++) ring.Add(i);
        Assert.AreEqual(3, ring.Count); Assert.AreEqual(2, ring[0], "满了丢最旧的");
        var still = new List<Vector2> { new Vector2(5, 1), new Vector2(5.1f, 1), new Vector2(5.05f, 1) };
        Assert.IsTrue(Step1BlackBox.Stuck(still, true, 4f, 3.5f), "4 秒没挪窝 = 卡住");
        Assert.IsFalse(Step1BlackBox.Stuck(still, false, 4f, 3.5f), "被晕 / 等开局不算");
        Assert.IsTrue(Step1BlackBox.HeldNoMove(2f, 1.5f, still, true), "按住方向键你没动");
        Assert.IsTrue(Step1BlackBox.BadNumber(new Vector2(float.NaN, 0), 10, 5)); Assert.IsTrue(Step1BlackBox.BadNumber(new Vector2(30, 1), 10, 5)); Assert.IsFalse(Step1BlackBox.BadNumber(new Vector2(3, 1), 10, 5));
        Assert.IsTrue(Step1BlackBox.Mash(new List<float> { 1, 1.2f, 1.4f, 1.6f, 1.8f, 2f }, 2f, 2f, 6), "狂按");
        var th = new Step1BlackBox.Throttle(30f, 2);
        Assert.IsTrue(th.Allow(Step1BlackBox.Kind.Stuck, 0)); Assert.IsFalse(th.Allow(Step1BlackBox.Kind.Stuck, 10), "同一类 30 秒内只一次");
        Assert.IsTrue(th.Allow(Step1BlackBox.Kind.Hitch, 10)); Assert.IsFalse(th.Allow(Step1BlackBox.Kind.Mash, 60), "一次试玩上限");
        Assert.IsTrue(th.Allow(Step1BlackBox.Kind.Manual, 60), "你按的 F8 永远记");
        var files = new[] { new Step1BlackBox.FileItem { name = "feedback.md", bytes = 100, priority = 0 },
                            new Step1BlackBox.FileItem { name = "old.jpg", bytes = 600, priority = 2, order = 1 },
                            new Step1BlackBox.FileItem { name = "new.jpg", bytes = 600, priority = 2, order = 2 } };
        var b = Step1BlackBox.Budget(files, 1000);
        Assert.IsTrue(b.keep.Any(f => f.name == "new.jpg") && b.dropped.Any(f => f.name == "old.jpg"), "超预算先丢旧截图");
        Assert.AreEqual((960, 540), Step1BlackBox.Fit(1920, 1080, 960));
        var snap = Step1BlackBox.Snapshot(new[] { "WWWW", "W..W", "W##W" }, new Vector2(1, 1), new Vector2(2, 1));
        Assert.AreEqual("WMTW", snap[1], "房间快照标出 M 和 T");
        StringAssert.Contains("EncodeToJPG", Read("Scripts/Gameplay/Step1/Step1Feedback.cs"));
        StringAssert.Contains("Step1BlackBox.Budget", Read("Scripts/Editor/TestHubWindow.cs"));
        StringAssert.Contains("Shown?.Invoke", Read("Scripts/Gameplay/Step1/Step1Hint.cs"));
    }

    [Test]
    public void S242_Loadout_PicksRoomPropsAndPropDecoyWorksWhileDisguised()
    {
        var room = new[] { "WWWWWWWW", "Wc.cb.UW", "Wc..b.dW", "W######W" };
        var pick = Step1Loadout.DefaultPick(room, 3);
        Assert.AreEqual(3, pick.Count); Assert.AreEqual('c', pick[0], "房间里最多的排第一"); Assert.AreEqual('b', pick[1]);
        Assert.AreEqual(5, Step1Loadout.DefaultPick(new[] { "W..W" }, 9).Count, "最多 5 格；房间里没有也补齐");
        var sw = Step1Loadout.Sample(new List<char> { 'c', 'b', 'U' }, 0, 'U');
        CollectionAssert.AreEqual(new[] { 'U', 'b', 'c' }, sw, "取样已有的 = 两格交换，不重复");
        Assert.IsFalse(Step1Loadout.CanDisguiseAs('#')); Assert.IsFalse(Step1Loadout.CanDisguiseAs('o'), "宝物不能变");
        Assert.AreEqual(new Vector2(0.6f, 1.2f), Step1Loadout.BodySize(new Vector2(0.2f, 3f)), "碰撞体夹在 0.6–1.2");
        Assert.IsTrue(Step1Loadout.CanDecoy(true, true, false, 1, false), "道具诱饵伪装中也能丢");
        Assert.IsFalse(Step1Loadout.CanDecoy(false, true, false, 1, false), "假你诱饵仍要现形");
        Assert.IsTrue(Step1Loadout.ShapeShiftVisible(10.2f, 10f, 0.5f)); Assert.IsFalse(Step1Loadout.ShapeShiftVisible(11f, 10f, 0.5f));
        Assert.AreEqual(2.5f, Step1Loadout.ThrowArc(Vector2.zero, true, 2.5f, 1f).x, 0.01f, "落在 2.5 格外");
        Assert.Greater(Step1Loadout.ThrowArc(Vector2.zero, true, 2.5f, 0.22f).y, 0.8f, "空中有弧线");
        Assert.IsTrue(Step1Loadout.Wriggling(1.1f, 1.6f, 1f)); Assert.IsFalse(Step1Loadout.Wriggling(1.8f, 1.6f, 1f));
        Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 31);
        Assert.GreaterOrEqual(new MarioMindTuningSO().decoysPerRound, 2);
        StringAssert.Contains("AddComponent<TricksterLoadout>()", Read("Scripts/Gameplay/Step1/Step1Lighting.cs"));
        StringAssert.Contains("ShapeChangedAt", Read("Scripts/Gameplay/Step1/MarioEyes.cs"), "伪装中换形态被看见 = 动了");
        StringAssert.Contains("decoy.IsProp", Read("Scripts/Gameplay/Step1/MarioEyes.cs"));
        StringAssert.Contains("TricksterBurrow.BodyBusy", Read("Scripts/Gameplay/Step1/TricksterLoadout.cs"));
        StringAssert.Contains("PranksterCannon.TricksterSeated", Read("Scripts/Gameplay/Step1/TricksterLoadout.cs"));
        StringAssert.Contains("Step1Feedback.TagOpen", Read("Scripts/Gameplay/Step1/TricksterLoadout.cs"), "F8 打标签时数字键不换形态");
        StringAssert.Contains("G 丢假道具", Step1Text.ControlsBarFor(false, false, false, true, false, false, false, propDecoy: true));
    }

    [Test]
    public void S242_Glance_RouteAmbushIntentThreeColors()
    {
        var route = new List<Vector2> { new Vector2(1, 1), new Vector2(2, 1), new Vector2(3, 1), new Vector2(4, 1), new Vector2(5, 1) };
        var props = new List<(Vector2, char, bool)> { (new Vector2(4, 1), '~', true), (new Vector2(2, 1), 'J', true), (new Vector2(3, 5), 'n', true), (new Vector2(3, 1), 'Q', false) };
        var a = Step1Glance.AmbushesOnRoute(route, props, 2f, 1.2f, 3);
        Assert.AreEqual(2, a.Count, "远离路线的 / 用过的不算");
        Assert.AreEqual('J', a[0].ch, "按他先到的排"); Assert.AreEqual(1.5f, a[1].eta, 0.01f, "3 格 / 2 格每秒 = 1.5 秒");
        Assert.AreEqual(Step1Glance.Intent.Exit, Step1Glance.IntentOf("Running", true, false));
        Assert.AreEqual(Step1Glance.Intent.Chase, Step1Glance.IntentOf("Chasing", false, false));
        Assert.AreEqual(Step1Glance.Intent.Stunned, Step1Glance.IntentOf("Chasing", false, true));
        foreach (Step1Glance.Intent i in System.Enum.GetValues(typeof(Step1Glance.Intent))) Assert.IsTrue(Step1Icons.Has(Step1Glance.IntentIcon(i)), "每个意图都有图标");
        Assert.IsTrue(Step1Icons.Has("BadgeAmbush"));
        foreach (var (ch, _) in Step1MapLegend.Entries)
        {
            string v = Step1Glance.Verb(ch);
            Assert.LessOrEqual(v.Length, 4, ch + " 的动词最多 4 个字");
        }
        Assert.AreEqual(Step1Glance.Tint.Prank, Step1Glance.TintOf('~')); Assert.AreEqual(Step1Glance.Tint.Hide, Step1Glance.TintOf('b')); Assert.AreEqual(Step1Glance.Tint.Goal, Step1Glance.TintOf('o'));
        Assert.Greater(Step1Glance.Dashes(route, 0.6f).Count, route.Count, "路线变成小点");
        StringAssert.Contains("AddComponent<Step1GlanceView>()", Read("Scripts/Gameplay/Step1/Step1Combo.cs"));
        StringAssert.DoesNotContain("TricksterController", Read("Scripts/Gameplay/Step1/Step1Combo.cs"), "H4：连招层仍不碰捣蛋者");
    }

    // ── S243：AI 生成的像素美术（角色 / 机关 / 地形 / 背景），只换外观 ─────────────────────
    [Test]
    public void S243_ArtSkin_PixelArtPassesAuditAndKeepsPhysics()
    {
        foreach (var kv in Step1Art.Icons) CollectionAssert.IsEmpty(OverworldArt.Audit(Step1Art.Rgba(kv.Value), 16), kv.Key);
        foreach (var kv in Step1Art.Frames) CollectionAssert.IsEmpty(OverworldArt.Audit(Step1Art.Rgba(kv.Value), 16), kv.Key);
        for (int i = 0; i < 5; i++) { Assert.IsTrue(Step1Art.Frames.ContainsKey("Hero" + i)); Assert.IsTrue(Step1Art.Frames.ContainsKey("Imp" + i)); }
        foreach (var key in Step1Icons.ByKey.Keys) Assert.IsTrue(Step1Art.Icons.ContainsKey(key), "每个机关 / 徽章都有新图：" + key);
        Assert.AreEqual(Step1Art.Rgba(Step1Art.Icons["FireTrap"]), Step1Icons.Pixels("FireTrap"), "图例 / 作战图 / 快捷栏也用新图");
        Assert.AreEqual(Step1Art.Pose.Jump, Step1Art.PoseOf(false, 0f, false, 0f));
        Assert.AreEqual(Step1Art.Pose.Stunned, Step1Art.PoseOf(true, 3f, true, 0f));
        Assert.AreNotEqual(Step1Art.PoseOf(true, 3f, false, 0.01f), Step1Art.PoseOf(true, 3f, false, 0.13f), "跑步两帧交替");
        Assert.AreEqual(Step1Art.Pose.Jump, Step1Art.ImpPose(false, false, 0f, 0f)); Assert.AreEqual(Step1Art.Pose.Stunned, Step1Art.ImpPose(true, true, 0f, 0f));
        Assert.AreEqual("GroundTop", Step1Art.TileFor("Ground_0_0_w5", true)); Assert.AreEqual("GroundFill", Step1Art.TileFor("Ground_0_0_w5", false));
        Assert.AreEqual("Wall", Step1Art.TileFor("Wall_3_4", true)); Assert.AreEqual("Platform", Step1Art.TileFor("OneWayPlatform_2_3_w3", true)); Assert.IsNull(Step1Art.TileFor("FireTrap_1_1", true));
        Assert.AreEqual(0.6f, Step1Art.FitScale(0.5f, 0.5f), 1e-4); Assert.AreEqual(1f, Step1Art.FitScale(1.1f, 1.2f), 1e-4);
        Assert.AreEqual(0.35f, Step1Art.FitLift(0.3f, 1f), 1e-4, "扁机关（香蕉皮）图的底边 = 原色块底边");
        var bg = Step1Art.BackgroundRgba(); Assert.AreEqual(64 * 36 * 4, bg.Length);
        float lo = 1f, hi = 0f; for (int i = 0; i < bg.Length; i += 4) { float l = OverworldArt.Luma(bg[i], bg[i + 1], bg[i + 2]); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
        Assert.Greater(lo, OverworldArt.DarkLuma - 0.1f, "背景不能比主层描边还暗"); Assert.Less(hi - lo, 0.3f, "背景低对比");
        var skin = Read("Scripts/Gameplay/Step1/Step1ArtSkin.cs");
        StringAssert.Contains("[DefaultExecutionOrder(-1000)]", skin, "必须在机关 Awake 记原色之前换图");
        StringAssert.DoesNotContain("BoxCollider2D>().size =", skin, "H3：不改碰撞体");
        StringAssert.DoesNotContain("col.size =", skin);
        StringAssert.Contains("AddComponent<Step1ArtSkin>()", Read("Scripts/Editor/Step1PrankRoomBuilder.cs"));
        Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 24); Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 32);
        var t = ScriptableObject.CreateInstance<MarioMindTuningSO>(); Assert.IsTrue(t.artCharacters && t.artProps && t.artTiles && t.artBackground);
        foreach (var f in new[] { "RushMarioMind.cs", "MarioMindDriver.cs", "SuspicionMeter.cs", "Step1Combo.cs" }) StringAssert.DoesNotContain("Step1ArtSkin", Read("Scripts/Gameplay/Step1/" + f), "H4：心智 / 连招层不碰美术");
    }

    [Test]
    public void S244_RhythmPauseAndTownSave()
    {
        Assert.GreaterOrEqual(Step1PrankRoomBuilder.BuilderVersion, 25); Assert.GreaterOrEqual(MarioMindTuningSO.CurrentDataVersion, 33);
        var t = ScriptableObject.CreateInstance<MarioMindTuningSO>(); Assert.IsTrue(t.startCountdown && t.roundBanners && t.screenShake && t.townAutoSave && t.artJuice);
        Assert.AreEqual(6, Step1Flow.PauseItems(true).Count); Assert.AreEqual(5, Step1Flow.PauseItems(false).Count);
        Assert.AreEqual("3", Step1Flow.CountdownText(2.4f, -1f)); StringAssert.Contains("开始", Step1Flow.CountdownText(0f, 0.1f));
        foreach (var k in new[] { "GoalZone", "Collectible", "SimpleEnemy", "Checkpoint", "Decor" }) Assert.IsTrue(Step1Art.Icons.ContainsKey(k), "S244 补图：" + k);
        OverworldSession.NewDay("T", "Assets/Scenes/T.unity", 3); OverworldSession.Minute = 600; OverworldSession.Results[1] = OverworldSession.DoorResult.Defended;
        var back = Step1Flow.FromJson(Step1Flow.ToJson(Step1Flow.Capture()));
        OverworldSession.NewDay("T", "x", 1);
        Assert.IsTrue(Step1Flow.Restore(back, "T")); Assert.AreEqual(3, OverworldSession.Day); Assert.AreEqual(OverworldSession.DoorResult.Defended, OverworldSession.Results[1]);
        Assert.IsNull(Step1Flow.FromJson("{broken"));
        OverworldSession.NewDay("T", "x", 1); OverworldSession.Active = false;
        StringAssert.Contains("SaveKey", Read("Scripts/Overworld/Runtime/OverworldGame.cs"));
        foreach (var f in new[] { "RushMarioMind.cs", "SuspicionMeter.cs" }) StringAssert.DoesNotContain("Step1PauseMenu", Read("Scripts/Gameplay/Step1/" + f), "H4");
    }
}
