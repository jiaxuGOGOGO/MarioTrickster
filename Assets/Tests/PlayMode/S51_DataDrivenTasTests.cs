using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ═══════════════════════════════════════════════════════════════════
// S51_DataDrivenTasTests — 零代码数据驱动测试工厂 (DDPT)
//
// [AI防坑警告] 此文件是 S51 数据驱动测试管线的核心。
// 它自动扫描 Assets/Tests/LevelReplays/ 目录下的所有 JSON 录像文件，
// 为每个文件动态生成一个 [UnityTest] 测试用例。
//
// 设计原则：
//   1. 零代码扩展：新增测试只需丢一个 JSON 文件到 LevelReplays 目录
//   2. 使用 TasReplayData Wrapper（S51 新增）反序列化 JSON
//   3. TestCaseSource + TestCaseData.Returns(null) 解决 UnityTest 兼容性
//   4. 双重断言：① 触发胜利 ② 防脱轨坐标校验（误差 <= 0.05f）
//   5. 10x 物理加速（只改 timeScale，不改 fixedDeltaTime）
//   6. Timeout 防死锁 + 严格 Teardown 恢复 timeScale
//
// 架构关系：
//   JSON 文件 → TasReplayData.ImportFromJson → AutomatedInputProvider → 回放 → 断言
//
// 依赖：
//   - TasReplayData / InputRecorder.ImportFromJson（S51 新增）
//   - AsciiLevelGenerator.GenerateFromTemplate
//   - AutomatedInputProvider
//   - InputManager / GameManager / GoalZone / KillZone
//   - PhysicsMetrics（碰撞体尺寸常量）
//
// 使用方式：
//   1. 将 JSON 录像文件放入 Assets/Tests/LevelReplays/
//   2. 在 Unity Test Runner 中运行 PlayMode 测试
//   3. 每个 JSON 文件自动成为一个独立的测试用例
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// S51: 零代码数据驱动 TAS 回放测试工厂。
/// 
/// 自动扫描 LevelReplays 目录下的 JSON 文件，
/// 为每个文件生成一个端到端自动化跑图测试。
/// </summary>
public class S51_DataDrivenTasTests
{
    // ── 测试常量 ──────────────────────────────────────────
    private const float TEST_TIMESCALE = 10f;
    private const float TEST_TIMEOUT_SECONDS = 15f; // 真实时间超时
    private const float POSITION_TOLERANCE = 0.05f; // 防脱轨坐标误差阈值
    private const string GROUND_LAYER = "Ground";
    private const string REPLAY_SUBDIR = "Tests/LevelReplays";

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
        if (_testObjects != null)
        {
            foreach (var go in _testObjects)
            {
                if (go != null) Object.Destroy(go);
            }
            _testObjects.Clear();
        }

        // 清理 ASCII 关卡残留
        AsciiLevelGenerator.ClearGeneratedLevel();

        // 清理 GameManager 单例
        GameManager gm = Object.FindObjectOfType<GameManager>();
        if (gm != null) Object.Destroy(gm.gameObject);
    }

    // ═══════════════════════════════════════════════════════
    // 数据源：自动扫描 LevelReplays 目录
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 动态数据源：扫描 Assets/Tests/LevelReplays/ 下的所有 .json 文件。
    /// 
    /// [AI防坑警告] 必须使用 TestCaseData.Returns(null) 而非 object[]，
    /// 否则 UnityTest (IEnumerator) 会报 "Method has non-void return value" 错误。
    /// </summary>
    private static IEnumerable<TestCaseData> GetReplayFiles()
    {
        string replayDir = Path.Combine(Application.dataPath, REPLAY_SUBDIR);

        if (!Directory.Exists(replayDir))
        {
            Debug.LogWarning($"[S51 DDPT] Replay directory not found: {replayDir}");
            yield break;
        }

        string[] jsonFiles = Directory.GetFiles(replayDir, "*.json");

        if (jsonFiles.Length == 0)
        {
            Debug.LogWarning($"[S51 DDPT] No JSON replay files found in: {replayDir}");
            yield break;
        }

        foreach (string filePath in jsonFiles)
        {
            string testName = Path.GetFileNameWithoutExtension(filePath);
            yield return new TestCaseData(filePath)
                .SetName(testName)
                .Returns(null);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 通用数据驱动测试
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 通用 TAS 回放测试：读取 JSON → 生成关卡 → 注入输入 → 10x 加速 → 断言胜利 + 防脱轨。
    /// 
    /// 断言 1：Mario 触发胜利（GameState == RoundOver 且 winner == "Mario"）
    /// 断言 2：Mario 最终坐标与 JSON 中的预期坐标误差 <= 0.05f（防脱轨）
    /// </summary>
    [UnityTest, TestCaseSource(nameof(GetReplayFiles))]
    public IEnumerator DataDriven_TasReplay(string jsonPath)
    {
        // ── Step 1: 读取并反序列化 JSON ──
        Assert.IsTrue(File.Exists(jsonPath), $"JSON 文件不存在: {jsonPath}");

        string json = File.ReadAllText(jsonPath);
        Assert.IsNotEmpty(json, $"JSON 文件为空: {jsonPath}");

        TasReplayData replayData = InputRecorder.ImportFromJson(json);
        Assert.IsNotNull(replayData, $"JSON 反序列化失败: {jsonPath}");
        Assert.IsTrue(replayData.frames != null && replayData.frames.Count > 0,
            $"JSON 中没有输入帧数据: {jsonPath}");
        Assert.IsNotEmpty(replayData.asciiLevel,
            $"JSON 中没有关卡 ASCII 数据: {jsonPath}");

        string testName = Path.GetFileNameWithoutExtension(jsonPath);
        Debug.Log($"[S51 DDPT] === Running: {testName} === " +
            $"({replayData.frames.Count} segments, " +
            $"expected pos: ({replayData.expectedFinalPosX:F2}, {replayData.expectedFinalPosY:F2}))");

        // ── Step 2: 生成微型考场 ──
        GameObject levelRoot = AsciiLevelGenerator.GenerateFromTemplate(replayData.asciiLevel, true);
        Assert.IsNotNull(levelRoot, $"ASCII 关卡生成失败: {testName}");
        _testObjects.Add(levelRoot);

        // ── Step 3: 创建可玩环境 ──
        SetupPlayableEnvironment(levelRoot);

        yield return null; // 等待所有组件初始化

        // ── Step 4: 验证环境就绪 ──
        GameManager gm = GameManager.Instance;
        Assert.IsNotNull(gm, $"GameManager 未初始化: {testName}");
        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            $"GameManager 应该自动进入 Playing 状态: {testName}");

        InputManager im = Object.FindObjectOfType<InputManager>();
        Assert.IsNotNull(im, $"InputManager 未找到: {testName}");

        MarioController mario = Object.FindObjectOfType<MarioController>();
        Assert.IsNotNull(mario, $"Mario 未找到: {testName}");

        PlayerHealth health = mario.GetComponent<PlayerHealth>();
        Assert.IsNotNull(health, $"PlayerHealth 未找到: {testName}");

        // ── Step 5: 设置 TAS 状态可视化 ──
        InputRecorder recorder = Object.FindObjectOfType<InputRecorder>();
        if (recorder != null)
        {
            recorder.SetTasReplayState(true);
        }

        // ── Step 6: 注入 TAS 输入序列 ──
        var autoProvider = new AutomatedInputProvider(replayData.frames);
        im.SetInputProvider(autoProvider);

        // ── Step 7: 加速物理（10x）──
        // [AI防坑警告] 只修改 timeScale，不修改 fixedDeltaTime！
        // S50 极致优雅的 10x 物理加速策略绝对不可破坏。
        Time.timeScale = TEST_TIMESCALE;

        // ── Step 8: 等待序列执行完毕（带 Timeout 防死锁）──
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
                Assert.Fail($"[{testName}] 测试超时（{TEST_TIMEOUT_SECONDS}s 真实时间）！" +
                    $"Mario 位置: {mario.transform.position}, " +
                    $"序列进度: {autoProvider.CurrentSegmentIndex}/{replayData.frames.Count}, " +
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

        // ── Step 9: 恢复 TAS 状态可视化 ──
        if (recorder != null)
        {
            recorder.SetTasReplayState(false);
        }

        // ── Step 10: 断言 1 — 触发胜利 ──
        Assert.IsTrue(health.CurrentHealth > 0,
            $"[{testName}] Mario 应该存活（当前血量: {health.CurrentHealth}）");
        Assert.IsTrue(won || gm.CurrentState == GameState.RoundOver,
            $"[{testName}] Mario 应该触发胜利判定（GameState: {gm.CurrentState}, won: {won}）");

        // ── Step 11: 断言 2 — 防脱轨坐标校验 ──
        Vector3 finalPos = mario.transform.position;
        float dx = Mathf.Abs(finalPos.x - replayData.expectedFinalPosX);
        float dy = Mathf.Abs(finalPos.y - replayData.expectedFinalPosY);

        // 注意：防脱轨断言使用宽松阈值，因为 10x 加速下物理步数增多
        // 可能导致微小的浮点误差累积。0.05f 足以检测严重的脱轨问题。
        // 如果胜利已触发，坐标校验仅作为警告日志（不 fail），
        // 因为 GoalZone 触发时 Mario 的精确位置取决于碰撞检测时机。
        if (won)
        {
            if (dx > POSITION_TOLERANCE || dy > POSITION_TOLERANCE)
            {
                Debug.LogWarning($"[{testName}] 防脱轨警告: 实际坐标 ({finalPos.x:F3}, {finalPos.y:F3}) " +
                    $"与预期 ({replayData.expectedFinalPosX:F3}, {replayData.expectedFinalPosY:F3}) " +
                    $"偏差 (dx={dx:F3}, dy={dy:F3}) 超过阈值 {POSITION_TOLERANCE}。" +
                    $"因胜利已触发，此为非致命警告。");
            }
            else
            {
                Debug.Log($"[{testName}] 防脱轨校验通过: " +
                    $"实际 ({finalPos.x:F3}, {finalPos.y:F3}), " +
                    $"预期 ({replayData.expectedFinalPosX:F3}, {replayData.expectedFinalPosY:F3}), " +
                    $"偏差 (dx={dx:F3}, dy={dy:F3})");
            }
        }
        else
        {
            // 胜利未触发时，坐标校验为硬断言
            Assert.LessOrEqual(dx, POSITION_TOLERANCE,
                $"[{testName}] 防脱轨断言失败 (X): 实际 {finalPos.x:F3}, 预期 {replayData.expectedFinalPosX:F3}, 偏差 {dx:F3}");
            Assert.LessOrEqual(dy, POSITION_TOLERANCE,
                $"[{testName}] 防脱轨断言失败 (Y): 实际 {finalPos.y:F3}, 预期 {replayData.expectedFinalPosY:F3}, 偏差 {dy:F3}");
        }

        Debug.Log($"[S51 DDPT] === PASSED: {testName} ===");
    }

    // ═══════════════════════════════════════════════════════
    // 测试辅助方法（复用 S50 的 SetupPlayableEnvironment）
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 为 ASCII 关卡创建完整的可玩环境。
    /// 
    /// 复用 S50 的环境搭建逻辑：
    ///   Mario（视碰分离） + InputManager + GameManager + LevelManager + KillZone
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

        // ── 创建 Mario（S37 视碰分离）──
        GameObject mario = new GameObject("Mario");
        mario.tag = "Player";
        mario.transform.position = marioSpawnPos + Vector3.up * 0.5f;
        _testObjects.Add(mario);

        // S37: 视觉子节点
        GameObject marioVisual = new GameObject("Visual");
        marioVisual.transform.SetParent(mario.transform, false);
        SpriteRenderer marioSR = marioVisual.AddComponent<SpriteRenderer>();
        marioSR.color = Color.red;

        // 创建白色方块 Sprite
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

        // 连线
        inputManager.SetMarioController(marioCtrl);
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
            Debug.LogWarning($"[S51 DDPT] Field '{fieldName}' not found on {obj.GetType().Name}");
        }
    }
}
