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

        while (!autoProvider.IsFinished && !won)
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
            if (won) break;
            yield return null;
        }

        // ── Step 7: 断言 ──
        Assert.IsTrue(health.CurrentHealth > 0,
            $"Mario 应该存活（当前血量: {health.CurrentHealth}）");
        Assert.IsTrue(won || gm.CurrentState == GameState.RoundOver,
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

        // 等待 Mario 落地稳定
        yield return null;
        yield return null;

        // ── Step 4: 注入 TAS 输入序列（跳跃跨坑）──
        // 策略：向右走 → 接近坑边缘时跳跃 → 空中保持向右 → 落地后继续向右
        var sequence = new List<InputFrame>
        {
            // 1. 向右走接近坑边缘（约 80 帧 = 1.6 秒游戏时间）
            new InputFrame { duration = 80, p1Horizontal = 1f },
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

        while (!autoProvider.IsFinished && !won)
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
            if (won) break;
            yield return null;
        }

        // ── Step 7: 断言 ──
        Assert.IsTrue(health.CurrentHealth > 0,
            $"Mario 应该存活（当前血量: {health.CurrentHealth}）");
        Assert.IsTrue(won || gm.CurrentState == GameState.RoundOver,
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
    private void SetupPlayableEnvironment(GameObject levelRoot)
    {
        // ── 查找 SpawnPoint ──
        Transform marioSpawnT = null;
        float levelWidth = 0f;
        float levelHeight = 0f;

        foreach (Transform child in levelRoot.transform)
        {
            if (child.name.StartsWith("MarioSpawnPoint"))
                marioSpawnT = child;

            float x = child.position.x;
            float y = child.position.y;
            if (x > levelWidth) levelWidth = x;
            if (y > levelHeight) levelHeight = y;
        }

        Vector3 marioSpawnPos = marioSpawnT != null
            ? marioSpawnT.position
            : new Vector3(1f, 2f, 0f);

        // ── Ground Layer ──
        int groundLayerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (groundLayerIndex == -1) groundLayerIndex = 0;
        LayerMask groundLayerMask = 1 << groundLayerIndex;

        // ── 创建 Mario ──
        GameObject mario = new GameObject("Mario");
        mario.tag = "Player";
        mario.transform.position = marioSpawnPos + Vector3.up * 0.5f;
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

        // 连线 GameManager（通过反射设置 SerializeField）
        SetPrivateField(gameManager, "mario", marioCtrl);
        SetPrivateField(gameManager, "marioHealth", marioHealth);
        SetPrivateField(gameManager, "inputManager", inputManager);

        // SpawnPoint
        GameObject marioSP = marioSpawnT != null
            ? marioSpawnT.gameObject
            : new GameObject("MarioSpawnPoint");
        if (marioSpawnT == null)
        {
            marioSP.transform.position = marioSpawnPos;
            _testObjects.Add(marioSP);
        }
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
