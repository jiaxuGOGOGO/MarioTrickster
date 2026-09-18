using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ═══════════════════════════════════════════════════════════════════
// S50_AutoRunE2ETests — 首个端到端自动化跑图测试
//
// [AI防坑警告] 此测试文件是 TAS 录播系统的验证端。
// 它使用 AsciiLevelGenerator 生成微型考场，通过 AutomatedInputProvider
// 注入预录制的 InputFrame 序列，在 10x 加速物理下验证 Mario 能否
// 成功跑完关卡并触发胜利判定。
//
// 关键设计决策：
//   1. timeScale=10 加速测试，但 fixedDeltaTime 保持 0.02 不变
//      （Edy/Unity Legacy Contributor 的建议：让 Unity 自动增加物理步数）
//   2. 不修改 fixedDeltaTime 确保物理行为完全一致，抛物线不失真
//   3. Teardown 严格恢复 timeScale 和 fixedDeltaTime，避免污染后续测试
//   4. 所有等待循环都有 Timeout 防死锁
//
// 依赖：
//   - AsciiLevelGenerator（生成微型关卡）
//   - AutomatedInputProvider（注入虚拟输入）
//   - InputManager（输入源热替换）
//   - GameManager（胜利判定）
//   - GoalZone（终点触发）
//
// 架构参考：
//   - Celeste TAS: 逐帧输入回放 + 断言角色状态
//   - Unity PlayMode 测试最佳实践: Setup → Act → Assert → Teardown
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// S50: 端到端自动化跑图测试。
/// 
/// 使用 ASCII 微型关卡 + AutomatedInputProvider 虚拟输入 + 10x 物理加速，
/// 验证 Mario 能否成功跑完关卡并触发胜利判定。
/// </summary>
public class S50_AutoRunE2ETests
{
    // ── 测试常量 ──────────────────────────────────────────
    private const float TEST_TIMESCALE = 10f;
    private const float TEST_TIMEOUT_SECONDS = 15f; // 真实时间超时（10x 加速下等效 150 秒游戏时间）
    private const string GROUND_LAYER = "Ground";

    // ── 测试状态 ──────────────────────────────────────────
    private float _originalTimeScale;
    private float _originalFixedDeltaTime;
    private List<GameObject> _testObjects;

    // ═══════════════════════════════════════════════════════
    // Setup / Teardown
    // ═══════════════════════════════════════════════════════

    [SetUp]
    public void SetUp()
    {
        _testObjects = new List<GameObject>();

        // 保存原始时间设置
        _originalTimeScale = Time.timeScale;
        _originalFixedDeltaTime = Time.fixedDeltaTime;
    }

    [TearDown]
    public void TearDown()
    {
        // [AI防坑警告] 必须严格恢复时间设置，否则会污染后续测试！
        Time.timeScale = _originalTimeScale;
        Time.fixedDeltaTime = _originalFixedDeltaTime;

        // 清理所有测试对象
        foreach (var go in _testObjects)
        {
            if (go != null) Object.Destroy(go);
        }
        _testObjects.Clear();

        // 清理 ASCII 关卡残留
        AsciiLevelGenerator.ClearGeneratedLevel();

        // 清理 GameManager 单例（防止残留影响后续测试）
        GameManager gm = Object.FindObjectOfType<GameManager>();
        if (gm != null) Object.Destroy(gm.gameObject);
    }

    // ═══════════════════════════════════════════════════════
    // E2E 测试：Mario 自动跑图到达终点
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// E2E 测试：Mario 在平坦地形上向右跑到终点。
    /// 
    /// ASCII 关卡布局（5行 x 15列）：
    ///   Row 0: ...............   (空气)
    ///   Row 1: ...............   (空气)
    ///   Row 2: ...............   (空气)
    ///   Row 3: M.............G   (Mario出生 → 终点)
    ///   Row 4: ###############   (地面)
    /// 
    /// 输入序列：向右走 ~200 帧（10x 加速下约 0.4 秒真实时间）
    /// 断言：Mario 存活 + GameState == RoundOver（胜利）
    /// </summary>
    [TestCase(typeof(MarioController))]
    [TestCase(typeof(TricksterController))]
    public void JumpBuffer_RequiresRealPressAtStartupAndAfterConsumption(System.Type type)
    {
        var go = new GameObject("JumpBufferContract");
        _testObjects.Add(go);
        var controller = go.AddComponent(type);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        System.Action<string, object> set = (name, value) => type.GetField(name, flags).SetValue(controller, value);
        System.Func<Vector2> velocity = () => (Vector2)type.GetField("_frameVelocity", flags).GetValue(controller);
        var handle = type.GetMethod("HandleJump", flags);
        set("_time", 0f); set("_grounded", true); set("_bufferedJumpUsable", true);
        handle.Invoke(controller, null);
        Assert.AreEqual(0f, velocity().y, "Landing at startup is not a buffered button press");
        set("_timeJumpWasPressed", 0f); set("_jumpToConsume", true);
        handle.Invoke(controller, null);
        Assert.Greater(velocity().y, 0f, "A real press at time zero must work");
        set("_frameVelocity", Vector2.zero); set("_bufferedJumpUsable", true);
        handle.Invoke(controller, null);
        Assert.AreEqual(0f, velocity().y, "A consumed press cannot reappear on early re-landing");
        set("_timeJumpWasPressed", 0f); set("_jumpToConsume", true); set("jumpPressedThisFrame", true);
        type.GetMethod("ResetForNewRound").Invoke(controller, null);
        set("_grounded", true); set("_bufferedJumpUsable", true);
        handle.Invoke(controller, null);
        Assert.AreEqual(0f, velocity().y, "New rounds must clear buffered and unconsumed presses");
    }

    [UnityTest]
    public IEnumerator NeutralInput_DoesNotJumpDuringInitialLanding()
    {
        var root = AsciiLevelGenerator.GenerateFromTemplate("...............\n...............\n...............\nM.............G\n###############", true);
        _testObjects.Add(root);
        SetupPlayableEnvironment(root);
        var mario = Object.FindObjectOfType<MarioController>();
        int jumps = 0;
        mario.OnJump += () => jumps++;
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(0, jumps, "Neutral warmup must not consume a phantom startup jump");
        Assert.IsTrue(mario.IsGrounded);
    }

    [UnityTest]
    public IEnumerator DirectTas_UsesPhysicsClockAndDeliversOpeningJumpAt10x()
    {
        var root = AsciiLevelGenerator.GenerateFromTemplate("...............\n...............\n...............\nM.............G\n###############", true);
        Assert.IsNotNull(root);
        _testObjects.Add(root);
        SetupPlayableEnvironment(root);
        yield return null;
        var mario = Object.FindObjectOfType<MarioController>();
        var input = Object.FindObjectOfType<InputManager>();
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!mario.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(mario.IsGrounded);
        int jumps = 0;
        mario.OnJump += () => jumps++;
        var frames = new List<InputFrame>();
        for (int i = 0; i < 1000; i++)
            frames.Add(new InputFrame { duration = 1, p1JumpDown = i == 0, p1JumpHeld = i < 40 });
        var replay = new AutomatedInputProvider(frames);
        input.SetInputProvider(replay);
        float firstFixedTime = Time.fixedTime;
        Time.timeScale = TEST_TIMESCALE;
        yield return new WaitForSecondsRealtime(0.1f);
        int physicsSteps = Mathf.RoundToInt((Time.fixedTime - firstFixedTime) / Time.fixedDeltaTime);
        Assert.Greater(physicsSteps, 1);
        Assert.AreEqual(physicsSteps, replay.CurrentSegmentIndex, "One segment per physics step, not per rendered frame");
        Assert.AreEqual(1, jumps, "The opening one-step jump must be delivered once before physics");
    }

    [UnityTest]
    public IEnumerator Bot_LowStepUnderOneWayRoute_ReachesGoalWithOrdinaryInput()
    {
        Time.timeScale = 1f;
        var root = AsciiLevelGenerator.GenerateFromTemplate(
            "...............\n....-------....\n...............\nM....=........G\n###############", true);
        _testObjects.Add(root);
        SetupPlayableEnvironment(root);
        yield return null;
        var mario = Object.FindObjectOfType<MarioController>();
        var input = Object.FindObjectOfType<InputManager>();
        var manager = GameManager.Instance;
        bool won = false;
        System.Action<string> onEnd = winner => won = winner == "Mario";
        manager.OnGameOver += onEnd;
        try
        {
            var bot = new HeuristicBotInputProvider();
            bot.SetDecisionSeed(153);
            input.SetInputProvider(bot);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!won && manager.CurrentState == GameState.Playing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(won, $"Low-step navigation failed at {mario.transform.position}; intent={bot.MarioIntent}; recovery attempts={bot.RecoveryAttempts}");
        }
        finally { if (manager != null) manager.OnGameOver -= onEnd; }
    }

    [UnityTest]
    public IEnumerator Mario_OrdinaryJumpPassesOneWayLandsAndDropsThrough() => VerifyVerticalPlatform(false, true);
    [UnityTest]
    public IEnumerator Trickster_OrdinaryJumpPassesOneWayAndLands() => VerifyVerticalPlatform(true, true);
    [UnityTest]
    public IEnumerator Mario_SolidCeilingStillBlocksOrdinaryJump() => VerifyVerticalPlatform(false, false);
    [UnityTest]
    public IEnumerator Trickster_SolidCeilingStillBlocksOrdinaryJump() => VerifyVerticalPlatform(true, false);

    private IEnumerator VerifyVerticalPlatform(bool opponent, bool oneWay)
    {
        Time.timeScale = 1f;
        string shelf = oneWay ? ".------........" : ".######........";
        var root = AsciiLevelGenerator.GenerateFromTemplate("...............\n...............\n" + shelf + "\n..M...........G\n###############", true);
        // The low-ceiling fixture has only 0.025 units of standing headroom.
        // Its initial placement must not use the normal +0.5 airborne spawn lift.
        _testObjects.Add(root); SetupPlayableEnvironment(root, 0f);
        var mario = Object.FindObjectOfType<MarioController>();
        var input = Object.FindObjectOfType<InputManager>();
        GameObject actor = mario.gameObject;
        TricksterController trickster = null;
        if (opponent)
        {
            actor = new GameObject("TestTrickster"); _testObjects.Add(actor);
            actor.transform.position = new Vector3(5, 1f, 0);
            var visual = new GameObject("Visual"); visual.transform.SetParent(actor.transform, false);
            visual.AddComponent<SpriteRenderer>();
            var col = actor.AddComponent<BoxCollider2D>();
            col.size = new Vector2(PhysicsMetrics.MARIO_COLLIDER_WIDTH, PhysicsMetrics.MARIO_COLLIDER_HEIGHT);
            col.offset = new Vector2(0, PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y);
            actor.AddComponent<Rigidbody2D>();
            trickster = actor.AddComponent<TricksterController>();
            int layer = LayerMask.NameToLayer(GROUND_LAYER);
            SetPrivateField(trickster, "groundLayer", (LayerMask)(1 << (layer < 0 ? 0 : layer)));
            input.SetTricksterController(trickster);
        }
        Physics2D.SyncTransforms();
        var body = actor.GetComponent<Collider2D>();
        var ceiling = System.Array.Find(root.GetComponentsInChildren<BoxCollider2D>(), c =>
            Mathf.Abs(c.bounds.center.y - 2f) < 0.01f && c.bounds.min.x <= body.bounds.min.x && c.bounds.max.x >= body.bounds.max.x);
        Assert.IsNotNull(ceiling, "Generated ceiling must cover the whole actor");
        Assert.Less(body.bounds.max.y, ceiling.bounds.min.y, "Fixture must START below the ceiling, not overlap or stand on it");
        float settleDeadline = Time.realtimeSinceStartup + 3f;
        do { yield return new WaitForFixedUpdate(); }
        while (!(opponent ? trickster.IsGrounded : mario.IsGrounded) && Time.realtimeSinceStartup < settleDeadline);
        Assert.IsTrue(opponent ? trickster.IsGrounded : mario.IsGrounded);
        Assert.That(body.bounds.min.y, Is.EqualTo(0.5f).Within(0.04f), "Neutral settle must land on the floor, not the ceiling");
        Assert.Less(body.bounds.max.y, ceiling.bounds.min.y, "Actor must still be below the ceiling before jump input");
        var frames = new List<InputFrame> {
            new InputFrame { duration = 25, p1JumpHeld = !opponent, p2JumpHeld = opponent },
            new InputFrame { duration = 150 }
        };
        input.SetInputProvider(new AutomatedInputProvider(frames));
        float maxFeet = body.bounds.min.y, maxHead = body.bounds.max.y;
        bool landedOnTop = false;
        int jumps = 0;
        System.Action onJump = () => jumps++;
        if (!opponent) mario.OnJump += onJump;
        try
        {
            float end = Time.realtimeSinceStartup + 3.5f;
            while (Time.realtimeSinceStartup < end)
            {
                maxFeet = Mathf.Max(maxFeet, body.bounds.min.y);
                maxHead = Mathf.Max(maxHead, body.bounds.max.y);
                if ((opponent ? trickster.IsGrounded : mario.IsGrounded) && body.bounds.min.y > 2.08f)
                    landedOnTop = true;
                yield return new WaitForFixedUpdate();
            }
            if (!opponent) Assert.AreEqual(1, jumps, "A real jump press must be delivered and consumed exactly once");
            if (oneWay)
            {
                Assert.Greater(maxFeet, 2.2f, "A jump must actually cross the platform top");
                Assert.IsTrue(landedOnTop, "Passing upward is not sufficient: land on the deck");
                if (!opponent)
                {
                    input.SetInputProvider(new AutomatedInputProvider(new List<InputFrame> {
                        new InputFrame { duration = 1, p1SHeld = true, p1JumpHeld = true },
                        new InputFrame { duration = 100 }
                    }));
                    yield return new WaitForSeconds(1f);
                    Assert.IsTrue(mario.IsGrounded);
                    Assert.Less(body.bounds.min.y, 0.6f, "S+Jump must fall through without phantom re-grounding");
                }
            }
            else
            {
                Assert.LessOrEqual(maxHead, 1.58f, "Solid cell at y=2 must still block the jump from below");
                Assert.IsFalse(landedOnTop);
            }
        }
        finally { if (!opponent && mario != null) mario.OnJump -= onJump; }
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator Observer_RealFatalHammerKeepsContactAndHealthEvidence()
    {
        Time.timeScale = 1f;
        yield return null;
        var plan = System.Type.GetType("MechanismExplorationPlan, MarioTrickster.Editor", true);
        var scenario = plan.GetMethod("Build").Invoke(null, new object[] { 153, "P", 0 });
        var root = AsciiLevelGenerator.GenerateFromTemplate((string)scenario.GetType().GetField("ascii").GetValue(scenario), true);
        _testObjects.Add(root); SetupPlayableEnvironment(root);
        var trap = root.GetComponentInChildren<PendulumTrap>();
        var probe = trap.gameObject.AddComponent<ExplorationContactProbe>(); probe.mechanism = "P";
        probe.BindMovingPart(); probe.BindMovingPart();
        var mario = Object.FindObjectOfType<MarioController>();
        mario.enabled = false; mario.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        yield return null;
        var trialType = plan.GetNestedType("Trial");
        var trial = System.Activator.CreateInstance(trialType);
        trialType.GetField("profile").SetValue(trial, "Explorer");
        var observerType = System.Type.GetType("ExplorationTrialObserver, MarioTrickster.Editor", true);
        var observer = (System.IDisposable)System.Activator.CreateInstance(observerType, new object[] { scenario, trial, 30f });
        try
        {
            var health = mario.GetComponent<PlayerHealth>();
            health.TakeDamage(1); health.Heal(1); health.ResetHealth();
            Assert.AreEqual(1, trialType.GetField("runnerDamageEvents").GetValue(trial), "Heal/reset must not count as damage");
            health.TakeDamage(2);
            yield return new WaitForSeconds(1.6f); // Natural invulnerability expiry, never force god mode off.
            Assert.IsFalse(health.IsInvincible);
            trap.enabled = false;
            var hammer = trap.GetComponentInChildren<PendulumHammerTrigger>();
            hammer.transform.position = mario.GetComponent<Collider2D>().bounds.center;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual("RunnerStopped", trialType.GetField("outcome").GetValue(trial));
            Assert.AreEqual(3, trialType.GetField("runnerDamageEvents").GetValue(trial));
            Assert.AreEqual(4, trialType.GetField("runnerHealthLost").GetValue(trial), "Cumulative loss includes damage before healing");
            var coverage = (IList)trialType.GetField("coverage").GetValue(trial);
            Assert.AreEqual(1, coverage.Count);
            var evidence = coverage[0];
            Assert.AreEqual(1, evidence.GetType().GetField("built").GetValue(evidence), "Relay must not double built count");
            Assert.Greater((int)evidence.GetType().GetField("runnerMovingPartContacts").GetValue(evidence), 0,
                "Fatal contact must survive either trigger callback order");
            observer.Dispose();
            health.ResetHealth(); health.TakeDamage(1);
            Assert.AreEqual(3, trialType.GetField("runnerDamageEvents").GetValue(trial), "Disposed observer must not collect later events");
        }
        finally { observer.Dispose(); }
    }

    [UnityTest]
    public IEnumerator Explorer_CheckpointDecks_ReservesTimeForActualGoal() => VerifyBoundedExplorer(0x45a35, "S-", 36);
    [UnityTest]
    public IEnumerator Explorer_DecksBreakable_ReservesTimeForActualGoal() => VerifyBoundedExplorer(0x47924, "-X", 37);

    private IEnumerator VerifyBoundedExplorer(int seed, string mechanisms, int index)
    {
        Time.timeScale = 1f;
        yield return null; // Allow prior fixtures' deferred destruction before singleton lookup.
        var plan = System.Type.GetType("MechanismExplorationPlan, MarioTrickster.Editor", true);
        var scenario = plan.GetMethod("Build").Invoke(null, new object[] { seed, mechanisms, index });
        var root = AsciiLevelGenerator.GenerateFromTemplate((string)scenario.GetType().GetField("ascii").GetValue(scenario), true);
        Assert.IsNotNull(root); _testObjects.Add(root); SetupPlayableEnvironment(root);
        // Navigation isolation: retain every authored mechanism; no opponent is spawned by this fixture.
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>())
        {
            string id = component is OneWayPlatform ? "-" : component is Checkpoint ? "S" : component is BreakableBlock ? "X" : null;
            if (id != null) (component.GetComponent<ExplorationContactProbe>() ?? component.gameObject.AddComponent<ExplorationContactProbe>()).mechanism = id;
        }
        yield return null;
        var trialType = plan.GetNestedType("Trial");
        var trial = System.Activator.CreateInstance(trialType);
        trialType.GetField("profile").SetValue(trial, "Explorer");
        var observerType = System.Type.GetType("ExplorationTrialObserver, MarioTrickster.Editor", true);
        var observer = (System.IDisposable)System.Activator.CreateInstance(observerType, new object[] { scenario, trial, 30f });
        try
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            var tick = observerType.GetMethod("Tick");
            while (!(bool)observerType.GetProperty("Finished").GetValue(observer) && Time.realtimeSinceStartup < deadline)
            {
                tick.Invoke(observer, new object[] { Time.deltaTime, 30f });
                yield return null;
            }
            Assert.AreEqual("Cleared", trialType.GetField("outcome").GetValue(trial), "Actual goal event required; a bounded visit is not a clear");
            Assert.AreEqual(2, trialType.GetField("probeTargets").GetValue(trial), "Eight auxiliary decks must not become eight visits");
            Assert.LessOrEqual((float)trialType.GetField("probeElapsedSeconds").GetValue(trial), 8f);
            Assert.Less((float)trialType.GetField("seconds").GetValue(trial), 30f);
        }
        finally { observer.Dispose(); }
    }

    [UnityTest]
    public IEnumerator AuthoredUpperRoute_OrdinaryInputCompletesAllLandings() => VerifyAuthoredUpper(false);
    [UnityTest]
    public IEnumerator AuthoredUpperRoute_LootReturnCompletesBothDirections() => VerifyAuthoredUpper(true);

    private IEnumerator VerifyAuthoredUpper(bool lootReturn)
    {
        Time.timeScale = 1f;
        // PlayModeTests must remain player-buildable: resolve the editor-only authoring plan
        // at runtime in the editor, rather than adding an Editor dependency to its asmdef.
        var planType = System.Type.GetType("MechanismExplorationPlan, MarioTrickster.Editor", true);
        var scenario = planType.GetMethod("BuildExperience").Invoke(null, new object[] { 153, lootReturn ? 2 : 0 });
        var scenarioType = scenario.GetType();
        string ascii = (string)scenarioType.GetField("ascii").GetValue(scenario);
        // Navigation isolation: remove opponent props, keep the exact authored platforms/targets.
        ascii = ascii.Replace('[', '.').Replace('F', '.');
        var root = AsciiLevelGenerator.GenerateFromTemplate(ascii, true);
        _testObjects.Add(root); SetupPlayableEnvironment(root);
        if (lootReturn)
        {
            foreach (var old in root.GetComponentsInChildren<Collectible>())
            { old.enabled = false; old.gameObject.AddComponent<LootObjective>(); Object.Destroy(old); }
            foreach (var old in root.GetComponentsInChildren<GoalZone>())
            { old.enabled = false; old.gameObject.AddComponent<EscapeGate>(); Object.Destroy(old); }
        }
        yield return null;
        var mario = Object.FindObjectOfType<MarioController>();
        var input = Object.FindObjectOfType<InputManager>();
        var botType = System.Type.GetType("ExplorationTrialObserver+GuidedBot, MarioTrickster.Editor", true);
        var bot = (HeuristicBotInputProvider)System.Activator.CreateInstance(botType, new object[] {
            mario, new Dictionary<string, Transform[]>(), false, scenarioType.GetField("routes").GetValue(scenario), true
        });
        bot.RunnerStrategy = HeuristicBotInputProvider.RunnerPolicy.SafeRoute;
        bot.SetDecisionSeed(153); input.SetInputProvider(bot);
        var manager = GameManager.Instance;
        bool won = false;
        System.Action<string> onEnd = winner => won = winner == "Mario";
        manager.OnGameOver += onEnd;
        try
        {
            float deadline = Time.realtimeSinceStartup + 40f;
            while (manager.CurrentState == GameState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            var completed = (IReadOnlyList<string>)botType.GetProperty("CompletedRoutes").GetValue(bot);
            CollectionAssert.Contains(completed, "Out:upper", "A fallback lower-lane clear cannot pass upper acceptance");
            if (lootReturn) CollectionAssert.Contains(completed, "Return:upper");
            Assert.AreEqual(0, botType.GetProperty("RouteSwitchRequests").GetValue(bot), "The authored route must work without fallback");
            Assert.IsTrue(won, $"Actual Mario goal/escape required; end={mario.transform.position}, intent={bot.MarioIntent}");
        }
        finally
        {
            if (manager != null) manager.OnGameOver -= onEnd;
            input.SetInputProvider(new AutomatedInputProvider(new List<InputFrame>()));
        }
    }
#endif

    private const string BounceApproachRoom =
        "........................................\n........................................\n" +
        "........................................\n........................................\n" +
        "........................................\n...........----------...--..............\n" +
        "..........-...............-.............\n.........-.................-............\n" +
        "..M....T-.............B.....-........G..\n########################################";

    [UnityTest]
    public IEnumerator Bot_BounceApproach_Cautious_ActuallyLaunchesAndClears() => VerifyBounceApproach("Cautious");
    [UnityTest]
    public IEnumerator Bot_BounceApproach_Runner_ActuallyLaunchesAndClears() => VerifyBounceApproach("Runner");
    [UnityTest]
    public IEnumerator Bot_BounceApproach_Explorer_ActuallyLaunchesAndClears() => VerifyBounceApproach("Explorer");

    private IEnumerator VerifyBounceApproach(string profile)
    {
        Time.timeScale = 1f;
        var root = AsciiLevelGenerator.GenerateFromTemplate(BounceApproachRoom, true);
        Assert.IsNotNull(root); _testObjects.Add(root); SetupPlayableEnvironment(root);
        yield return null;
        var mario = Object.FindObjectOfType<MarioController>();
        var input = Object.FindObjectOfType<InputManager>();
        var platform = root.GetComponentInChildren<BouncyPlatform>();
        var manager = GameManager.Instance;
        var persona = ScriptableObject.CreateInstance<BotPersonaConfigSO>();
        persona.reactionDelay = profile == "Cautious" ? 0.4f : 0.1f;
        persona.riskTolerance = profile == "Runner" ? 0.9f : 0.25f;
        var bot = new HeuristicBotInputProvider { marioPersona = persona };
        bot.SetDecisionSeed(153);
        int launches = 0;
        bool won = false;
        System.Action<string> onEnd = winner => won = winner == "Mario";
        System.Action<GameplayEventBus.BouncyPlatformLaunchedPayload> onLaunch = payload => {
            if (payload.platform == platform.gameObject && payload.target == mario.gameObject && payload.launchVelocity.y > 0) launches++;
        };
        manager.OnGameOver += onEnd; GameplayEventBus.OnBouncyPlatformLaunched += onLaunch;
        try
        {
            input.SetInputProvider(bot);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!won && manager.CurrentState == GameState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(launches, 0, $"{profile}: passing around B is not a bounce test; end={mario.transform.position}, attempts={bot.BounceLandingAttempts}");
            Assert.IsTrue(won, $"{profile}: a bounce alone is not a clear; end={mario.transform.position}, intent={bot.MarioIntent}");
        }
        finally
        {
            if (manager != null) manager.OnGameOver -= onEnd;
            GameplayEventBus.OnBouncyPlatformLaunched -= onLaunch;
            input.SetInputProvider(new AutomatedInputProvider(new List<InputFrame>()));
            Object.Destroy(persona);
        }
    }

    [UnityTest]
    public IEnumerator BouncePlatform_SideContactAloneDoesNotLaunch()
    {
        Time.timeScale = 1f;
        var root = AsciiLevelGenerator.GenerateFromTemplate(BounceApproachRoom, true);
        Assert.IsNotNull(root); _testObjects.Add(root); SetupPlayableEnvironment(root);
        yield return null;
        var mario = Object.FindObjectOfType<MarioController>();
        var input = Object.FindObjectOfType<InputManager>();
        var platform = root.GetComponentInChildren<BouncyPlatform>();
        int launches = 0;
        System.Action<GameplayEventBus.BouncyPlatformLaunchedPayload> onLaunch = payload => {
            if (payload.target == mario.gameObject && payload.platform == platform.gameObject) launches++;
        };
        GameplayEventBus.OnBouncyPlatformLaunched += onLaunch;
        try
        {
            input.SetInputProvider(new AutomatedInputProvider(new List<InputFrame> { new InputFrame { duration = 1000, p1Horizontal = 1f } }));
            yield return new WaitForSecondsRealtime(3.5f);
            float gap = platform.GetComponent<Collider2D>().bounds.min.x - mario.GetComponent<Collider2D>().bounds.max.x;
            Assert.Less(Mathf.Abs(gap), 0.1f, "Fixture must actually reach the side, not stop far away");
            Assert.AreEqual(0, launches, "Do not weaken the top-landing rule to make AI tests pass");
        }
        finally { GameplayEventBus.OnBouncyPlatformLaunched -= onLaunch; }
    }

    [UnityTest]
    public IEnumerator E2E_FlatRun_MarioReachesGoal()
    {
        // ── Step 1: 生成微型考场 ──
        string asciiLevel =
            "...............\n" +
            "...............\n" +
            "...............\n" +
            "M.............G\n" +
            "###############";

        GameObject levelRoot = AsciiLevelGenerator.GenerateFromTemplate(asciiLevel, true);
        Assert.IsNotNull(levelRoot, "ASCII 关卡生成失败");
        _testObjects.Add(levelRoot);

        // ── Step 2: 创建可玩环境（Mario + Managers + KillZone）──
        SetupPlayableEnvironment(levelRoot);

        // 等待一帧让所有组件初始化
        yield return null;

        // ── Step 3: 验证环境就绪 ──
        GameManager gm = GameManager.Instance;
        Assert.IsNotNull(gm, "GameManager 未初始化");
        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "GameManager 应该自动进入 Playing 状态");

        InputManager im = Object.FindObjectOfType<InputManager>();
        Assert.IsNotNull(im, "InputManager 未找到");

        MarioController mario = Object.FindObjectOfType<MarioController>();
        Assert.IsNotNull(mario, "Mario 未找到");

        PlayerHealth health = mario.GetComponent<PlayerHealth>();
        Assert.IsNotNull(health, "PlayerHealth 未找到");

        // ── Step 4: 注入 TAS 输入序列 ──
        // 向右走 250 帧（足够走完 14 格距离 + 余量）
        var sequence = new List<InputFrame>
        {
            new InputFrame { duration = 250, p1Horizontal = 1f }
        };

        var autoProvider = new AutomatedInputProvider(sequence);
        im.SetInputProvider(autoProvider);

        // ── Step 5: 加速物理（10x）──
        // [AI防坑警告] 只修改 timeScale，不修改 fixedDeltaTime！
        // Unity 自动将物理步数提升到 500Hz（10 × 50Hz），物理行为完全一致。
        Time.timeScale = TEST_TIMESCALE;

        // ── Step 6: 等待序列执行完毕（带 Timeout 防死锁）──
        float startTime = Time.realtimeSinceStartup;
        bool won = false;

        gm.OnGameOver += (winner) =>
        {
            if (winner == "Mario") won = true;
        };

        while (!autoProvider.IsFinished && !won && gm.CurrentState == GameState.Playing)
        {
            // Timeout 防死锁
            if (Time.realtimeSinceStartup - startTime > TEST_TIMEOUT_SECONDS)
            {
                Assert.Fail($"E2E 测试超时（{TEST_TIMEOUT_SECONDS}s 真实时间）！" +
                    $"Mario 位置: {mario.transform.position}, " +
                    $"序列进度: {autoProvider.CurrentSegmentIndex}/{sequence.Count}, " +
                    $"GameState: {gm.CurrentState}");
            }
            yield return null;
        }

        // 额外等待几帧让 GoalZone 触发和 GameManager 处理
        for (int i = 0; i < 10; i++)
        {
            if (won || gm.CurrentState == GameState.RoundOver) break;
            yield return null;
        }

        // ── Step 7: 断言 ──
        Assert.IsTrue(health.CurrentHealth > 0,
            $"Mario 应该存活（当前血量: {health.CurrentHealth}）");
        Assert.IsTrue(won,
            $"Mario 应该触发胜利判定（GameState: {gm.CurrentState}, won: {won}）");
    }

    /// <summary>
    /// E2E 测试：Mario 跳跃跨坑到达终点。
    /// 
    /// ASCII 关卡布局（6行 x 18列）：
    ///   Row 0: ..................   (空气)
    ///   Row 1: ..................   (空气)
    ///   Row 2: ..................   (空气)
    ///   Row 3: ..................   (空气)
    ///   Row 4: M.....   .........G   (Mario出生 → 坑 → 终点)
    ///   Row 5: ######...#########   (地面 + 3格坑)
    /// 
    /// 输入序列：向右走 → 跳跃跨坑 → 继续向右 → 到达终点
    /// 断言：Mario 存活 + GameState == RoundOver（胜利）
    /// </summary>
    [UnityTest]
    public IEnumerator E2E_JumpOverPit_MarioReachesGoal()
    {
        // ── Step 1: 生成微型考场（带坑）──
        string asciiLevel =
            "..................\n" +
            "..................\n" +
            "..................\n" +
            "..................\n" +
            "M................G\n" +
            "######...#########";

        GameObject levelRoot = AsciiLevelGenerator.GenerateFromTemplate(asciiLevel, true);
        Assert.IsNotNull(levelRoot, "ASCII 关卡生成失败");
        _testObjects.Add(levelRoot);

        // ── Step 2: 创建可玩环境 ──
        SetupPlayableEnvironment(levelRoot);

        yield return null;

        // ── Step 3: 验证环境就绪 ──
        GameManager gm = GameManager.Instance;
        Assert.IsNotNull(gm, "GameManager 未初始化");

        InputManager im = Object.FindObjectOfType<InputManager>();
        Assert.IsNotNull(im, "InputManager 未找到");

        MarioController mario = Object.FindObjectOfType<MarioController>();
        Assert.IsNotNull(mario, "Mario 未找到");

        PlayerHealth health = mario.GetComponent<PlayerHealth>();
        Assert.IsNotNull(health, "PlayerHealth 未找到");

        float settleDeadline = Time.realtimeSinceStartup + 3f;
        while (!mario.IsGrounded && Time.realtimeSinceStartup < settleDeadline) yield return null;
        Assert.IsTrue(mario.IsGrounded, "TAS fixture must be grounded before playback");

        // ── Step 4: 注入 TAS 输入序列（跳跃跨坑）──
        // 策略：向右走 → 接近坑边缘时跳跃 → 空中保持向右 → 落地后继续向右
        var sequence = new List<InputFrame>
        {
            // 27 physics steps at default 9 units/s: x ~= 4.69, before the pit edge x=5.5.
            // The old 80 steps reached x>14 before the first jump, even at a correct clock.
            new InputFrame { duration = 27, p1Horizontal = 1f },
            // 2. 起跳（JumpDown + JumpHeld + 继续向右）
            new InputFrame { duration = 1, p1Horizontal = 1f, p1JumpDown = true, p1JumpHeld = true },
            // 3. 空中保持向右 + 按住跳跃（延长跳跃高度）
            new InputFrame { duration = 40, p1Horizontal = 1f, p1JumpHeld = true },
            // 4. 释放跳跃，继续向右（下落阶段）
            new InputFrame { duration = 30, p1Horizontal = 1f },
            // 5. 落地后继续向右走到终点
            new InputFrame { duration = 200, p1Horizontal = 1f }
        };

        var autoProvider = new AutomatedInputProvider(sequence);
        im.SetInputProvider(autoProvider);

        // ── Step 5: 加速物理 ──
        Time.timeScale = TEST_TIMESCALE;

        // ── Step 6: 等待（带 Timeout）──
        float startTime = Time.realtimeSinceStartup;
        bool won = false;

        gm.OnGameOver += (winner) =>
        {
            if (winner == "Mario") won = true;
        };

        while (!autoProvider.IsFinished && !won && gm.CurrentState == GameState.Playing)
        {
            if (Time.realtimeSinceStartup - startTime > TEST_TIMEOUT_SECONDS)
            {
                Assert.Fail($"E2E 跳坑测试超时！" +
                    $"Mario 位置: {mario.transform.position}, " +
                    $"血量: {health.CurrentHealth}, " +
                    $"序列进度: {autoProvider.CurrentSegmentIndex}/{sequence.Count}, " +
                    $"GameState: {gm.CurrentState}");
            }
            yield return null;
        }

        // 额外等待
        for (int i = 0; i < 10; i++)
        {
            if (won || gm.CurrentState == GameState.RoundOver) break;
            yield return null;
        }

        // ── Step 7: 断言 ──
        Assert.IsTrue(health.CurrentHealth > 0,
            $"Mario 应该存活（当前血量: {health.CurrentHealth}）");
        Assert.IsTrue(won,
            $"Mario 应该触发胜利判定（GameState: {gm.CurrentState}, won: {won}）");
    }

    /// <summary>
    /// E2E 测试：验证 InputRecorder 的 RLE 压缩功能。
    /// 
    /// 创建一个 InputRecorder，模拟录制一段输入，
    /// 验证 RLE 压缩确实将连续相同帧合并为单条记录。
    /// </summary>
    [UnityTest]
    public IEnumerator InputRecorder_RLE_CompressesRepeatedFrames()
    {
        // 创建一个带 InputRecorder 的 GameObject
        GameObject recorderGO = new GameObject("TestRecorder");
        _testObjects.Add(recorderGO);

        // 需要 InputManager 和 Mario 才能录制
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        _testObjects.Add(marioGO);

        GameObject imGO = new GameObject("TestIM");
        InputManager im = imGO.AddComponent<InputManager>();
        im.SetMarioController(marioGO.GetComponent<MarioController>());
        _testObjects.Add(imGO);

        // 注入一段已知的自动化输入（模拟"录制"场景）
        var inputSequence = new List<InputFrame>
        {
            new InputFrame { duration = 50, p1Horizontal = 1f },     // 向右走 50 帧
            new InputFrame { duration = 1, p1JumpDown = true, p1JumpHeld = true }, // 跳
            new InputFrame { duration = 30, p1Horizontal = 1f, p1JumpHeld = true }, // 空中向右
            new InputFrame { duration = 20, p1Horizontal = 0f }       // 停止
        };
        var autoProvider = new AutomatedInputProvider(inputSequence);
        im.SetInputProvider(autoProvider);

        yield return null;

        InputRecorder recorder = recorderGO.AddComponent<InputRecorder>();

        yield return null; // 等待 Start

        // 开始录制
        recorder.StartRecording();

        // 运行足够帧让序列播放完
        int totalFrames = 50 + 1 + 30 + 20 + 10; // 加余量
        for (int i = 0; i < totalFrames; i++)
        {
            yield return null;
        }

        recorder.StopRecording();

        // 断言：RLE 压缩后的段数应该远少于总逻辑帧数
        Assert.Greater(recorder.TotalLogicalFrames, 0,
            "应该录制到帧数据");
        Assert.Less(recorder.CompressedSegments, recorder.TotalLogicalFrames,
            $"RLE 压缩后段数({recorder.CompressedSegments})应该少于总帧数({recorder.TotalLogicalFrames})");

        // 验证 JSON 导出不为空
        string json = recorder.GetJsonExport();
        Assert.IsNotEmpty(json, "JSON 导出不应为空");
        Assert.IsTrue(json.Contains("duration"),
            "JSON 应该包含 duration 字段");

        Debug.Log($"[S50 Test] RLE 压缩结果: {recorder.CompressedSegments} segments / {recorder.TotalLogicalFrames} frames " +
            $"(ratio: {(float)recorder.TotalLogicalFrames / recorder.CompressedSegments:F1}:1)");
    }

    // ═══════════════════════════════════════════════════════
    // 测试辅助方法
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 为 ASCII 关卡创建完整的可玩环境（PlayMode 版本）。
    /// 
    /// 与 TestConsoleWindow.EnsurePlayableEnvironment 类似，
    /// 但适配 PlayMode 测试环境（不使用 Undo、不依赖 Editor API）。
    /// </summary>
    [UnityTest]
    public IEnumerator EnvironmentUsesGroundSpawnAndWiresRoundReset() => VerifyAuthoredSpawn(2, 1);
    [UnityTest]
    public IEnumerator EnvironmentUsesElevatedSpawnAndWiresRoundReset() => VerifyAuthoredSpawn(8, 3);

    private IEnumerator VerifyAuthoredSpawn(int x, int y)
    {
        // Let deferred destruction from the preceding PlayMode fixture finish first.
        yield return null;
        const int width = 15, height = 5;
        var rows = new string[height];
        for (int row = 0; row < height; row++) rows[row] = new string('.', width);
        rows[height - 1] = new string('#', width);
        rows[height - 2] = new string('.', width - 1) + "G";
        var spawnRow = rows[height - 1 - y].ToCharArray(); spawnRow[x] = 'M';
        rows[height - 1 - y] = new string(spawnRow);
        var root = AsciiLevelGenerator.GenerateFromTemplate(string.Join("\n", rows), true);
        _testObjects.Add(root); SetupPlayableEnvironment(root);
        var actor = Object.FindObjectOfType<MarioController>();
        Assert.AreEqual(new Vector3(x, y + 0.5f, 0), actor.transform.position);
        var field = typeof(GameManager).GetField("marioSpawnPoint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.AreEqual(new Vector3(x, y, 0), ((Transform)field.GetValue(GameManager.Instance)).position);
    }

    [Test]
    public void EnvironmentRejectsMissingSpawnInsteadOfTestingAtFallbackCoordinates()
    {
        var root = new GameObject("MissingSpawnFixture"); _testObjects.Add(root);
        Assert.Throws<AssertionException>(() => SetupPlayableEnvironment(root));
        Assert.IsNull(root.transform.Find("MarioSpawnPoint"), "Do not fabricate a replacement marker");
    }

    private void SetupPlayableEnvironment(GameObject levelRoot, float spawnLift = 0.5f)
    {
        // ── 查找 SpawnPoint ──
        Transform marioSpawnT = null;
        float levelWidth = 0f;
        float levelHeight = 0f;

        foreach (Transform child in levelRoot.transform)
        {
            // Generator emits MarioSpawn_x_y, not MarioSpawnPoint. Never silently
            // substitute a different position: that can put the runner ON a test ceiling.
            if (child.name.StartsWith("MarioSpawn_", System.StringComparison.Ordinal) || child.name == "MarioSpawnPoint")
            {
                Assert.IsNull(marioSpawnT, "Fixture requires exactly one authored Mario spawn");
                marioSpawnT = child;
            }

            float x = child.position.x;
            float y = child.position.y;
            if (x > levelWidth) levelWidth = x;
            if (y > levelHeight) levelHeight = y;
        }

        Assert.IsNotNull(marioSpawnT, "Missing authored MarioSpawn_x_y marker; refusing fallback spawn");
        Vector3 marioSpawnPos = marioSpawnT.position;

        // ── Ground Layer ──
        int groundLayerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (groundLayerIndex == -1) groundLayerIndex = 0;
        LayerMask groundLayerMask = 1 << groundLayerIndex;

        // ── 创建 Mario ──
        GameObject mario = new GameObject("Mario");
        mario.tag = "Player";
        mario.transform.position = marioSpawnPos + Vector3.up * spawnLift;
        _testObjects.Add(mario);

        // S37: 视碰分离
        GameObject marioVisual = new GameObject("Visual");
        marioVisual.transform.SetParent(mario.transform, false);
        SpriteRenderer marioSR = marioVisual.AddComponent<SpriteRenderer>();
        marioSR.color = Color.red;

        // 创建白色方块 Sprite（与 AsciiLevelGenerator 一致）
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        marioSR.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);

        BoxCollider2D marioCol = mario.AddComponent<BoxCollider2D>();
        marioCol.size = new Vector2(PhysicsMetrics.MARIO_COLLIDER_WIDTH, PhysicsMetrics.MARIO_COLLIDER_HEIGHT);
        marioCol.offset = new Vector2(0f, PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y);

        Rigidbody2D marioRb = mario.AddComponent<Rigidbody2D>();
        marioRb.gravityScale = 0f;
        marioRb.freezeRotation = true;
        marioRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        MarioController marioCtrl = mario.AddComponent<MarioController>();
        marioCtrl.visualTransform = marioVisual.transform;
        PlayerHealth marioHealth = mario.AddComponent<PlayerHealth>();

        SetPrivateField(marioCtrl, "groundLayer", groundLayerMask);

        // ── 创建 Managers ──
        GameObject managers = new GameObject("Managers");
        _testObjects.Add(managers);

        GameManager gameManager = managers.AddComponent<GameManager>();
        InputManager inputManager = managers.AddComponent<InputManager>();
        LevelManager levelManager = managers.AddComponent<LevelManager>();

        // 连线 InputManager
        inputManager.SetMarioController(marioCtrl);
        // No live keyboard or default bot may move the runner during fixture warmup.
        inputManager.SetInputProvider(new AutomatedInputProvider(new List<InputFrame>()));

        // 连线 GameManager（通过反射设置 SerializeField）
        SetPrivateField(gameManager, "mario", marioCtrl);
        SetPrivateField(gameManager, "marioHealth", marioHealth);
        SetPrivateField(gameManager, "inputManager", inputManager);

        // SpawnPoint
        GameObject marioSP = marioSpawnT.gameObject;
        SetPrivateField(gameManager, "marioSpawnPoint", marioSP.transform);
        SetPrivateField(levelManager, "marioSpawnPoint", marioSP.transform);

        // 关卡边界
        SetPrivateField(levelManager, "levelMinX", -3f);
        SetPrivateField(levelManager, "levelMaxX", levelWidth + 5f);
        SetPrivateField(levelManager, "levelMinY", -10f);
        SetPrivateField(levelManager, "levelMaxY", levelHeight + 10f);

        // ── KillZone ──
        GameObject killZone = new GameObject("KillZone");
        killZone.transform.position = new Vector3(levelWidth / 2f, -8f, 0);
        BoxCollider2D killCol = killZone.AddComponent<BoxCollider2D>();
        killCol.size = new Vector2(levelWidth + 30f, 2f);
        killCol.isTrigger = true;
        KillZone kz = killZone.AddComponent<KillZone>();
        kz.SetFallbackY(-13f);
        _testObjects.Add(killZone);
    }

    /// <summary>创建测试用 Mario（简化版，用于 InputRecorder 测试）</summary>
    private GameObject CreateTestMario(Vector3 position)
    {
        GameObject go = new GameObject("TestMario");
        go.tag = "Player";
        go.transform.position = position;
        go.AddComponent<SpriteRenderer>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1f);
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        go.AddComponent<MarioController>();
        go.AddComponent<PlayerHealth>();

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0)
        {
            SetPrivateField(go.GetComponent<MarioController>(), "groundLayer",
                (LayerMask)(1 << layerIndex));
        }

        return go;
    }

    /// <summary>通过反射设置私有/SerializeField 字段</summary>
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            Debug.LogWarning($"[S50 Test] Field '{fieldName}' not found on {obj.GetType().Name}");
        }
    }
}
