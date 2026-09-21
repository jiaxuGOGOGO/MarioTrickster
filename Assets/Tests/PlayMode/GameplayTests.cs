using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode 测试：验证核心玩法逻辑（需要运行时物理引擎）
/// 
/// 这些测试在 Play 模式下执行，可以验证：
///   1. 角色移动/跳跃的物理行为
///   2. 平台跟随（速度注入法）
///   3. 伪装系统的运行时行为
///   4. 道具操控状态机
///   5. 胜负判定流程
///   6. 碰撞触发（GoalZone / KillZone / DamageDealer）
/// 
/// 运行方式：Window → General → Test Runner → PlayMode → Run All
/// </summary>
public class GameplayTests
{
    [UnityTest]
    public IEnumerator NativeTunnelDirectionalInputArrivesAfterDelay() { yield return NativeTunnelLifecycle(false, false); }

    [UnityTest]
    public IEnumerator NativeTunnelRevealCancelsLateTeleport() { yield return NativeTunnelLifecycle(true, false); }

    [UnityTest]
    public IEnumerator NativeTunnelDisableCancelsCoroutineAndRestoresVisibility() { yield return NativeTunnelLifecycle(false, true); }

    private IEnumerator NativeTunnelLifecycle(bool reveal, bool disable)
    {
        float savedScale = Time.timeScale;
        var actor = CreateTestTrickster(new Vector3(35000, 1, 0));
        var origin = new GameObject("NativeTunnelOrigin"); var exit = new GameObject("NativeTunnelExit");
        try
        {
            Time.timeScale = 1;
            actor.GetComponent<TricksterController>().enabled = false;
            actor.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            origin.transform.position = actor.transform.position; exit.transform.position = actor.transform.position + Vector3.right * 6;
            var sourceProp = origin.AddComponent<FakeWall>(); exit.AddComponent<FakeWall>();
            var from = origin.AddComponent<PossessionAnchor>(); var to = exit.AddComponent<PossessionAnchor>();
            from.connectedUnderlineNodes.Add(to); to.connectedUnderlineNodes.Add(from); to.underlineTransitTime = 0.1f;
            var disguise = actor.GetComponent<DisguiseSystem>();
            var ability = actor.GetComponent<TricksterAbilitySystem>();
            var gate = actor.GetComponent<TricksterPossessionGate>();
            Assert.IsNotNull(gate);
            // Isolated lifecycle fixture: establish an already-blended actor, not AI/gameplay success evidence.
            disguise.enabled = false;
            SetPrivateField(disguise, "isDisguised", true); SetPrivateField(disguise, "isFullyBlended", true);
            SetPrivateField(ability, "isAbilityActive", true); SetPrivateField(ability, "boundProp", sourceProp);
            SetPrivateField(ability, "boundPropObject", origin);
            ability.OnPropBound?.Invoke(sourceProp);
            Assert.AreEqual(TricksterPossessionState.Possessing, gate.CurrentState);
            ability.SwitchTarget(Vector2.right); // Real public direction path, real delay coroutine.
            Assert.AreEqual(TricksterPossessionState.Underlining, gate.CurrentState);
            Assert.Less(Vector2.Distance(actor.transform.position, origin.transform.position), 0.01f);
            Assert.IsFalse(actor.GetComponent<SpriteRenderer>().enabled);
            if (reveal) gate.ForceReveal(1f, "test-cancel");
            if (disable) ability.enabled = false;
            yield return new WaitForSeconds(0.2f);
            if (reveal || disable)
                Assert.Less(Vector2.Distance(actor.transform.position, origin.transform.position), 0.01f, "Cancelled transit must never teleport later");
            else
            {
                Assert.Less(Vector2.Distance(actor.transform.position, exit.transform.position), 0.01f);
                Assert.AreSame(to, gate.CurrentAnchor);
            }
            Assert.IsTrue(actor.GetComponent<SpriteRenderer>().enabled);
            Assert.AreNotEqual(TricksterPossessionState.Underlining, gate.CurrentState);
            if (reveal) Assert.AreEqual(TricksterPossessionState.Revealed, gate.CurrentState);
        }
        finally
        {
            Time.timeScale = savedScale;
            Object.Destroy(actor); Object.Destroy(origin); Object.Destroy(exit);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 测试辅助：创建带完整组件的测试角色
    // ═══════════════════════════════════════════════════════

    private const string GROUND_LAYER = "Ground";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [UnityTest]
    public IEnumerator PendulumProbe_RealTriggerDistinguishesContactDamageAndDisabledRoot()
    {
        float originalScale = Time.timeScale;
        var pivot = new GameObject("ProbePivot");
        var actor = CreateTestMario(new Vector3(24010, 0, 0));
        var opponent = CreateTestTrickster(new Vector3(24020, 0, 0));
        ExplorationContactProbe root = null;
        int runnerContacts = 0, opponentContacts = 0;
        GameObject source = null;
        System.Action<string, GameObject, GameObject> contact = (id, from, who) => {
            if (id != "P" || from.GetComponent<ExplorationContactProbe>().Root != root) return;
            source = from;
            if (who == actor) runnerContacts++;
            if (who == opponent) opponentContacts++;
        };
        ExplorationContactProbe.Contact += contact;
        try
        {
            Time.timeScale = 1f;
            pivot.transform.position = new Vector3(24000, 3, 0);
            var trap = pivot.AddComponent<PendulumTrap>();
            root = pivot.AddComponent<ExplorationContactProbe>(); root.mechanism = "P";
            root.BindMovingPart(); root.BindMovingPart();
            var hammer = pivot.GetComponentInChildren<PendulumHammerTrigger>();
            Assert.AreEqual(1, hammer.GetComponents<ExplorationContactProbe>().Length, "Binding must be idempotent");
            Assert.AreEqual(2, pivot.GetComponentsInChildren<ExplorationContactProbe>().Length, "One root plus one relay");
            Assert.IsFalse(root.IsMovingPart);
            Assert.AreSame(root, hammer.GetComponent<ExplorationContactProbe>().Root);
            // Fixed fixture only: immobilize actors and the swing, retaining the real trigger and damage path.
            trap.enabled = false;
            actor.GetComponent<MarioController>().enabled = false;
            opponent.GetComponent<TricksterController>().enabled = false;
            actor.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            opponent.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            hammer.transform.position = new Vector3(24000, 0, 0);
            yield return new WaitForFixedUpdate();
            var health = actor.GetComponent<PlayerHealth>();
            int initialHealth = health.CurrentHealth;
            actor.transform.position = hammer.transform.position;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(runnerContacts, 0, "Must receive a real physics callback, not invoke Record directly");
            Assert.AreSame(hammer.gameObject, source, "Source must remain the actual moving part");
            Assert.AreEqual(initialHealth - 1, health.CurrentHealth);
            Assert.IsTrue(health.IsInvincible);
            int firstContacts = runnerContacts;
            actor.transform.position += Vector3.right * 10;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            actor.transform.position = hammer.transform.position;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(runnerContacts, firstContacts, "Invincible contact still counts as contact");
            Assert.AreEqual(initialHealth - 1, health.CurrentHealth, "Contact is not a damage event");
            actor.transform.position += Vector3.right * 10;
            opponent.transform.position = hammer.transform.position;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(opponentContacts, 0);
            int beforeDisable = runnerContacts;
            root.enabled = false;
            actor.transform.position = hammer.transform.position;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(beforeDisable, runnerContacts, "Disabled root must stop relay recording");
        }
        finally
        {
            ExplorationContactProbe.Contact -= contact;
            Object.DestroyImmediate(pivot); Object.DestroyImmediate(actor); Object.DestroyImmediate(opponent);
            Time.timeScale = originalScale;
        }
    }
#endif

    [UnityTest]
    public IEnumerator PublicQueueCueMatchesWorldLabelAndHonorsVisibility()
    {
        var go = new GameObject("QueueCueFixture");
        var wall = new GameObject("OpaqueFixture");
        try
        {
            go.transform.position = new Vector3(23000, 1, 0);
            var queue = go.AddComponent<StateQueueTrap>();
            var label = go.GetComponentInChildren<TextMesh>();
            var cue = queue.ReadPublicCue();
            Assert.IsFalse(cue.safe, "Generic Idle must not be interpreted as a safe queue");
            StringAssert.Contains(cue.current, label.text);
            StringAssert.Contains(cue.next, label.text);
            StringAssert.Contains("Reach:", label.text);
            var viewer = (Vector2)go.transform.position + Vector2.left * 3;
            Physics2D.SyncTransforms();
            Assert.IsTrue(queue.TryReadPublicCue(viewer, out var visible));
            Assert.AreEqual(cue.current, visible.current);
            Assert.IsFalse(queue.TryReadPublicCue(viewer + Vector2.left * 10, out _));
            Assert.IsFalse(queue.TryReadPublicCue(viewer + Vector2.up * 4, out _));
            label.GetComponent<Renderer>().enabled = false;
            Assert.IsFalse(queue.TryReadPublicCue(viewer, out _), "Hidden label cannot feed the AI");
            label.GetComponent<Renderer>().enabled = true;
            wall.transform.position = go.transform.position + Vector3.left * 1.5f;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.3f, 3f);
            Physics2D.SyncTransforms();
            Assert.IsFalse(queue.TryReadPublicCue(viewer, out _), "Opaque solid must occlude cue sampling");
            queue.enabled = false;
            Assert.IsFalse(queue.TryReadPublicCue(go.transform.position, out _));
            yield return null;
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(wall); }
    }

    [UnityTest]
    public IEnumerator QueueDamageEventReportsRealSourceAndNoInvincibleDamage()
    {
        var go = new GameObject("QueueDamageFixture");
        go.transform.position = new Vector3(23500, 1, 0);
        var actor = CreateTestMario(go.transform.position + Vector3.left * 0.85f);
        int events = 0, lost = 0;
        StateQueueTrap receivedSource = null;
        System.Action<StateQueueTrap, MarioController, int, StateQueueTrap.PublicCue> handler = (source, mario, amount, cue) => {
            if (mario.gameObject != actor) return;
            events++; lost += amount; receivedSource = source;
            Assert.AreEqual("Left Attack", cue.current);
        };
        StateQueueTrap.ActualDamage += handler;
        float scale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            actor.GetComponent<MarioController>().enabled = false;
            actor.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            var health = actor.GetComponent<PlayerHealth>();
            var queue = go.AddComponent<StateQueueTrap>();
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(0.15f);
            Assert.AreSame(queue, receivedSource); Assert.AreEqual(1, events);
            Assert.AreEqual(1, lost); Assert.AreEqual(health.MaxHealth - 1, health.CurrentHealth);
            yield return new WaitForSeconds(0.65f);
            Assert.IsTrue(health.IsInvincible);
            Assert.AreEqual(1, events, "Later attack tick during normal invulnerability must not emit damage");
        }
        finally
        {
            StateQueueTrap.ActualDamage -= handler;
            Object.DestroyImmediate(go); Object.DestroyImmediate(actor); Time.timeScale = scale;
        }
    }

    [UnityTest]
    public IEnumerator SpikeCycleKeepsAuthoredHeightAndRetractsRelativeToIt()
    {
        foreach (float height in new[] { -3f, 1f, 5f })
        {
            var go = new GameObject("AuthoredSpike");
            try
            {
                go.transform.position = new Vector3(25000, height, 0);
                var spike = go.AddComponent<SpikeTrap>();
                SetPrivateField(spike, "cycleTimer", 10f);
                yield return new WaitForSeconds(0.25f);
                Assert.AreEqual(height, go.transform.localPosition.y, 0.01f, "Extended spike must stay on its authored floor");
                spike.OnLevelReset(); // Periodic reset retracts; it must not travel toward global -0.8.
                SetPrivateField(spike, "cycleTimer", 10f);
                yield return new WaitForSeconds(0.25f);
                Assert.AreEqual(height - 0.8f, go.transform.localPosition.y, 0.02f);
                Assert.IsFalse(go.GetComponent<BoxCollider2D>().enabled, "Fully retracted collision must be disabled at any height");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

    [UnityTest]
    public IEnumerator OneWay_RepeatedDropRenewsOnlyRequestingPair()
    {
        var platform = new GameObject("DropPlatform");
        var runner = new GameObject("DropRunner");
        var opponent = new GameObject("OtherRider");
        try
        {
            var oneWay = platform.AddComponent<OneWayPlatform>();
            var deck = platform.GetComponent<BoxCollider2D>();
            var a = runner.AddComponent<BoxCollider2D>();
            var b = opponent.AddComponent<BoxCollider2D>();
            runner.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            opponent.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            SetPrivateField(oneWay, "dropThroughDuration", 0.8f);
            oneWay.AllowDropThrough(a);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(a, deck));
            Assert.IsFalse(Physics2D.GetIgnoreCollision(b, deck), "Other rider must retain collision");
            yield return new WaitForSeconds(0.5f);
            oneWay.AllowDropThrough(a);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(a, deck), "Earlier request must not expire renewed drop");
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(a, deck), "Renewed request must eventually restore collision");
        }
        finally { Object.DestroyImmediate(platform); Object.DestroyImmediate(runner); Object.DestroyImmediate(opponent); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OneWay_ResetOrDisableRestoresOwnedPairsButPreservesExternalIgnore(bool disable)
    {
        var platform = new GameObject("DropPlatform");
        var runner = new GameObject("DropRunner");
        var opponent = new GameObject("OtherRider");
        var external = new GameObject("ExternalIgnore");
        try
        {
            var oneWay = platform.AddComponent<OneWayPlatform>();
            var deck = platform.GetComponent<BoxCollider2D>();
            var a = runner.AddComponent<BoxCollider2D>();
            var b = opponent.AddComponent<BoxCollider2D>();
            var c = external.AddComponent<BoxCollider2D>();
            runner.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            opponent.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            external.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Physics2D.IgnoreCollision(c, deck, true);
            oneWay.AllowDropThrough(a); oneWay.AllowDropThrough(b); oneWay.AllowDropThrough(c);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(a, deck));
            Assert.IsTrue(Physics2D.GetIgnoreCollision(b, deck));
            if (disable) oneWay.enabled = false; else oneWay.OnLevelReset();
            Assert.IsFalse(Physics2D.GetIgnoreCollision(a, deck));
            Assert.IsFalse(Physics2D.GetIgnoreCollision(b, deck));
            Assert.IsTrue(Physics2D.GetIgnoreCollision(c, deck), "Pre-existing external ignore is not ours to release");
            Assert.IsTrue(deck.enabled, "Cancellation must not remove the shared platform");
            if (disable)
            {
                oneWay.AllowDropThrough(a);
                Assert.IsFalse(Physics2D.GetIgnoreCollision(a, deck), "Disabled component must reject new requests");
                oneWay.enabled = true;
            }
            oneWay.OnLevelReset(); // Idempotent, including after re-enable.
            Assert.IsTrue(Physics2D.GetIgnoreCollision(c, deck));
        }
        finally { Object.DestroyImmediate(platform); Object.DestroyImmediate(runner); Object.DestroyImmediate(opponent); Object.DestroyImmediate(external); }
    }

    /// <summary>创建测试用 Mario 对象</summary>
    private GameObject CreateTestMario(Vector3 position)
    {
        GameObject go = new GameObject("TestMario");
        go.tag = "Player";
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        MarioController mario = go.AddComponent<MarioController>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();

        // 设置 groundLayer
        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0)
        {
            SetPrivateField(mario, "groundLayer", (LayerMask)(1 << layerIndex));
        }

        return go;
    }

    /// <summary>创建测试用 Trickster 对象</summary>
    private GameObject CreateTestTrickster(Vector3 position)
    {
        GameObject go = new GameObject("TestTrickster");
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        TricksterController trickster = go.AddComponent<TricksterController>();
        DisguiseSystem disguise = go.AddComponent<DisguiseSystem>();
        TricksterAbilitySystem ability = go.AddComponent<TricksterAbilitySystem>();

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0)
        {
            SetPrivateField(trickster, "groundLayer", (LayerMask)(1 << layerIndex));
        }

        return go;
    }

    /// <summary>创建测试用地面</summary>
    private GameObject CreateTestGround(Vector3 position, Vector2 size)
    {
        GameObject go = new GameObject("TestGround");
        go.transform.position = position;

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0) go.layer = layerIndex;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = size;

        return go;
    }

    /// <summary>通过反射设置私有字段</summary>
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }

    /// <summary>通过反射获取私有字段</summary>
    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return (T)field.GetValue(obj);
        }
        return default;
    }

    // ═══════════════════════════════════════════════════════
    // 1. Mario 移动测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_MoveRight_IncreasesXPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        float startX = marioGO.transform.position.x;

        // 模拟向右输入
        mario.SetMoveInput(new Vector2(1f, 0f));

        // 等待物理模拟
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);

        float endX = marioGO.transform.position.x;
        Assert.Greater(endX, startX,
            $"向右移动后 X 坐标应该增加（起始: {startX}, 结束: {endX}）");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_MoveLeft_DecreasesXPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        float startX = marioGO.transform.position.x;

        mario.SetMoveInput(new Vector2(-1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);

        float endX = marioGO.transform.position.x;
        Assert.Less(endX, startX,
            $"向左移动后 X 坐标应该减少（起始: {startX}, 结束: {endX}）");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_StopInput_Decelerates()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        // 先移动
        mario.SetMoveInput(new Vector2(1f, 0f));
        yield return new WaitForSeconds(0.3f);

        // 停止输入
        mario.SetMoveInput(Vector2.zero);
        yield return new WaitForSeconds(0.5f);

        Rigidbody2D rb = marioGO.GetComponent<Rigidbody2D>();
        float speed = Mathf.Abs(rb.velocity.x);

        Assert.Less(speed, 1f,
            $"停止输入后角色应该减速（当前速度: {speed}）。groundDeceleration=200 应该很快停下。");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    // ═══════════════════════════════════════════════════════
    // 2. Mario 跳跃测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_Jump_IncreasesYPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 0.5f, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        // 等待角色落到地面
        yield return new WaitForSeconds(0.3f);

        float groundY = marioGO.transform.position.y;

        // 跳跃
        mario.OnJumpPressed();
        yield return new WaitForSeconds(0.15f);

        float jumpY = marioGO.transform.position.y;
        Assert.Greater(jumpY, groundY,
            $"跳跃后 Y 坐标应该增加（地面: {groundY}, 跳跃中: {jumpY}）");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_Gravity_PullsDown()
    {
        // 不创建地面，让 Mario 自由落体
        GameObject marioGO = CreateTestMario(new Vector3(0, 5, 0));

        float startY = marioGO.transform.position.y;

        yield return new WaitForSeconds(0.3f);

        float endY = marioGO.transform.position.y;
        Assert.Less(endY, startY,
            $"没有地面时角色应该因重力下落（起始: {startY}, 结束: {endY}）");

        Object.Destroy(marioGO);
    }

    // ═══════════════════════════════════════════════════════
    // 3. Trickster 移动测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Trickster_MoveRight_IncreasesXPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject tricksterGO = CreateTestTrickster(new Vector3(0, 1, 0));
        TricksterController trickster = tricksterGO.GetComponent<TricksterController>();

        float startX = tricksterGO.transform.position.x;

        trickster.SetMoveInput(new Vector2(1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);

        float endX = tricksterGO.transform.position.x;
        Assert.Greater(endX, startX,
            $"Trickster 向右移动后 X 应增加（起始: {startX}, 结束: {endX}）");

        Object.Destroy(tricksterGO);
        Object.Destroy(ground);
    }

    // ═══════════════════════════════════════════════════════
    // 4. 伪装系统运行时测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator DisguiseSystem_ToggleDisguise_WithoutConfig_StaysUndisuised()
    {
        GameObject tricksterGO = CreateTestTrickster(new Vector3(0, 1, 0));
        DisguiseSystem disguise = tricksterGO.GetComponent<DisguiseSystem>();

        disguise.ToggleDisguise();
        yield return null;

        Assert.IsFalse(disguise.IsDisguised,
            "没有配置伪装形态时，ToggleDisguise 不应进入伪装状态");

        Object.Destroy(tricksterGO);
    }

    [UnityTest]
    public IEnumerator DisguiseSystem_Cooldown_PreventsImmedateReDisguise()
    {
        GameObject tricksterGO = CreateTestTrickster(new Vector3(0, 1, 0));
        DisguiseSystem disguise = tricksterGO.GetComponent<DisguiseSystem>();

        // 手动添加一个伪装形态
        Texture2D tex = new Texture2D(4, 4);
        Sprite testSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        var disguises = GetPrivateField<System.Collections.Generic.List<DisguiseData>>(disguise, "availableDisguises");
        if (disguises != null)
        {
            disguises.Add(new DisguiseData
            {
                disguiseName = "TestBlock",
                disguiseSprite = testSprite
            });
        }

        // 变身
        disguise.Disguise();
        yield return null;
        Assert.IsTrue(disguise.IsDisguised, "配置伪装形态后应该能变身");

        // 解除变身
        disguise.Undisguise();
        yield return null;
        Assert.IsFalse(disguise.IsDisguised, "Undisguise 后应该解除伪装");

        // 冷却期间尝试再次变身
        Assert.Greater(disguise.CooldownRemaining, 0f, "解除伪装后应该有冷却时间");
        disguise.Disguise();
        yield return null;
        Assert.IsFalse(disguise.IsDisguised,
            "冷却期间不应该能再次变身");

        Object.Destroy(tricksterGO);
        Object.DestroyImmediate(tex);
    }

    // ═══════════════════════════════════════════════════════
    // 5. 道具操控状态机测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ControllableHazard_Activate_GoesToTelegraphThenActive()
    {
        GameObject hazardGO = new GameObject("TestHazard");
        hazardGO.AddComponent<SpriteRenderer>();
        BoxCollider2D col = hazardGO.AddComponent<BoxCollider2D>();
        ControllableHazard hazard = hazardGO.AddComponent<ControllableHazard>();

        try
        {
            yield return null;
            IControllableProp prop = hazard;
            Assert.AreEqual(PropControlState.Idle, prop.GetControlState());
            Assert.IsTrue(prop.CanBeControlled());
            prop.OnTricksterActivate(Vector2.right);
            Assert.AreEqual(PropControlState.Telegraph, prop.GetControlState());
            Assert.IsFalse(GetPrivateField<bool>(hazard, "isDamageActive"), "Telegraph must leave a safe reaction window");

            // Observe ordered transitions instead of guessing a fixed sleep from old durations.
            foreach (var expected in new[] { PropControlState.Active, PropControlState.Recovery, PropControlState.Cooldown, PropControlState.Idle })
            {
                var previous = prop.GetControlState();
                float deadline = Time.realtimeSinceStartup + 8f;
                while (prop.GetControlState() == previous && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.AreEqual(expected, prop.GetControlState(), "Missing or out-of-order hazard phase");
                Assert.AreEqual(expected == PropControlState.Active, GetPrivateField<bool>(hazard, "isDamageActive"),
                    "Damage is permitted only during Active, never during Recovery counterplay");
                Assert.AreEqual(expected == PropControlState.Idle, prop.CanBeControlled());
            }
        }
        finally { Object.Destroy(hazardGO); }
    }

    [UnityTest]
    public IEnumerator ControllableBlock_Activate_GoesToTelegraphThenActive()
    {
        GameObject blockGO = new GameObject("TestBlock");
        blockGO.AddComponent<SpriteRenderer>();
        BoxCollider2D col = blockGO.AddComponent<BoxCollider2D>();
        ControllableBlock block = blockGO.AddComponent<ControllableBlock>();

        yield return null;

        IControllableProp prop = block as IControllableProp;
        Assert.AreEqual(PropControlState.Idle, prop.GetControlState());

        prop.OnTricksterActivate(Vector2.right);
        yield return null;
        Assert.AreEqual(PropControlState.Telegraph, prop.GetControlState(),
            "触发后应该进入 Telegraph 状态");

        yield return new WaitForSeconds(1.0f);
        Assert.AreEqual(PropControlState.Active, prop.GetControlState(),
            "预警结束后应该进入 Active 状态");

        Object.Destroy(blockGO);
    }

    // ═══════════════════════════════════════════════════════
    // 6. 移动平台速度注入测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator MovingPlatform_Moves_BetweenPoints()
    {
        GameObject platformGO = new GameObject("TestMovingPlatform");
        platformGO.transform.position = new Vector3(0, 0, 0);

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0) platformGO.layer = layerIndex;

        SpriteRenderer sr = platformGO.AddComponent<SpriteRenderer>();
        BoxCollider2D col = platformGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3, 0.4f);

        MovingPlatform mp = platformGO.AddComponent<MovingPlatform>();
        SetPrivateField(mp, "pointB", new Vector3(5, 0, 0));
        SetPrivateField(mp, "moveSpeed", 10f); // 快速移动以便测试
        SetPrivateField(mp, "waitTime", 0.1f);

        float startX = platformGO.transform.position.x;

        yield return new WaitForSeconds(0.5f);

        float endX = platformGO.transform.position.x;
        Assert.AreNotEqual(startX, endX,
            $"移动平台应该在两点之间移动（起始: {startX}, 结束: {endX}）");

        Object.Destroy(platformGO);
    }

    // ═══════════════════════════════════════════════════════
    // 7. 胜负判定测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameManager_MarioReachesGoal_EndRoundMarioWins()
    {
        // 创建 GameManager
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        // 创建 Mario
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));

        yield return null; // 等待 Start（GameManager 自动查找引用并开始游戏）

        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "GameManager Start 后应该自动进入 Playing 状态");

        string winner = null;
        gm.OnGameOver += (w) => winner = w;

        // 模拟 Mario 到达终点
        gm.OnMarioReachedGoal();

        yield return null;

        Assert.AreEqual("Mario", winner,
            "Mario 到达终点后应该判定 Mario 胜利");
        Assert.AreEqual(GameState.RoundOver, gm.CurrentState,
            "回合结束后状态应该是 RoundOver");
        Assert.AreEqual(1, gm.MarioWins,
            "Mario 胜利次数应该为 1");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }

    [UnityTest]
    public IEnumerator GameManager_MarioDies_EndRoundTricksterWins()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        PlayerHealth health = marioGO.GetComponent<PlayerHealth>();

        yield return null;

        string winner = null;
        gm.OnGameOver += (w) => winner = w;

        // 让 Mario 死亡
        health.TakeDamage(999);

        yield return null;

        Assert.AreEqual("Trickster", winner,
            "Mario 死亡后应该判定 Trickster 胜利");
        Assert.AreEqual(1, gm.TricksterWins,
            "Trickster 胜利次数应该为 1");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }

    [UnityTest]
    public IEnumerator GameManager_ResetRound_RestoresHealth()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        PlayerHealth health = marioGO.GetComponent<PlayerHealth>();

        // 创建出生点
        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(-5, 1, 0);
        SetPrivateField(gm, "marioSpawnPoint", spawnPoint.transform);

        yield return null;

        // 受伤
        health.TakeDamage(1);
        Assert.Less(health.CurrentHealth, health.MaxHealth);

        // 结束回合然后重置
        gm.EndRound("Trickster");
        yield return null;

        gm.ResetRound();
        yield return null;

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth,
            "ResetRound 后 Mario 生命值应该恢复满血");
        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "ResetRound 后应该重新进入 Playing 状态");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
        Object.Destroy(spawnPoint);
    }

    [UnityTest]
    public IEnumerator GameManager_RoundFeedback_IsStableAndResetsForNextAttempt()
    {
        GameObject gmGO = new GameObject("FeedbackGM");
        GameObject marioGO = CreateTestMario(new Vector3(4, 2, 0));
        GameManager gm = gmGO.AddComponent<GameManager>();
        try
        {
            yield return null;
            yield return null;
            float elapsed = gm.RoundElapsed;
            Assert.GreaterOrEqual(elapsed, 0f);
            Vector3 endPosition = marioGO.transform.position;
            gm.EndRound("Mario", "Test route cleared");
            Assert.AreEqual("Test route cleared", gm.LastRoundReason);
            Assert.AreEqual(endPosition, gm.LastRoundPosition);
            Assert.AreEqual(1, gm.MarioWins);
            gm.EndRound("Trickster", "Must not overwrite");
            yield return null;
            Assert.AreEqual(elapsed, gm.RoundElapsed);
            Assert.AreEqual("Test route cleared", gm.LastRoundReason);
            Assert.AreEqual(0, gm.TricksterWins);
            gm.StartGame();
            Assert.AreEqual(0f, gm.RoundElapsed);
            Assert.IsEmpty(gm.LastRoundReason);
        }
        finally
        {
            Object.Destroy(gmGO);
            Object.Destroy(marioGO);
            Time.timeScale = 1f;
        }
    }

    [UnityTest]
    public IEnumerator GameManager_PausedTimeDoesNotCountAsAttemptTime()
    {
        GameObject gmGO = new GameObject("PauseFeedbackGM");
        GameManager gm = gmGO.AddComponent<GameManager>();
        try
        {
            yield return null;
            gm.TogglePause();
            float elapsed = gm.RoundElapsed;
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.AreEqual(elapsed, gm.RoundElapsed);
            gm.ResetRound();
            Assert.AreEqual(1f, Time.timeScale, "Restarting from pause must restore time");
            Assert.AreEqual(GameState.Playing, gm.CurrentState);
        }
        finally { Object.Destroy(gmGO); Time.timeScale = 1f; }
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator GameManager_EditorRetryUsesBridgeEvenForUnsavedScenes()
    {
        GameObject gmGO = new GameObject("RetryGM");
        GameManager gm = gmGO.AddComponent<GameManager>();
        var previous = GameManager.EditorRestartHandler;
        int requests = 0;
        try
        {
            yield return null;
            GameManager.EditorRestartHandler = () => { requests++; return true; };
            Time.timeScale = 0f;
            gm.RestartLevel();
            Assert.AreEqual(1, requests);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNotNull(gm, "Bridge must run before any scene load");
        }
        finally
        {
            GameManager.EditorRestartHandler = previous;
            Object.Destroy(gmGO);
            Time.timeScale = 1f;
        }
    }
#endif

    // ═══════════════════════════════════════════════════════
    // 8. 暂停/继续测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameManager_Pause_StopsTime()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));

        yield return null;

        gm.TogglePause();
        Assert.AreEqual(GameState.Paused, gm.CurrentState,
            "TogglePause 后应该进入 Paused 状态");
        Assert.AreEqual(0f, Time.timeScale,
            "暂停后 Time.timeScale 应该为 0");

        gm.TogglePause();
        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "再次 TogglePause 后应该恢复 Playing 状态");
        Assert.AreEqual(1f, Time.timeScale,
            "恢复后 Time.timeScale 应该为 1");

        // 确保 timeScale 恢复
        Time.timeScale = 1f;

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }

    // ═══════════════════════════════════════════════════════
    // 9. Mario 公共属性测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_IsMoving_ReflectsMovement()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return new WaitForSeconds(0.2f);

        // 静止时
        mario.SetMoveInput(Vector2.zero);
        yield return new WaitForSeconds(0.3f);
        // IsMoving 检查 _frameVelocity.x 的绝对值
        // 静止足够久后应该不在移动

        // 移动时
        mario.SetMoveInput(new Vector2(1f, 0f));
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(mario.IsMoving,
            "有移动输入且速度 > 0.1 时 IsMoving 应该为 true");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_Die_DisablesController()
    {
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return null;

        bool deathEventFired = false;
        mario.OnDeath += () => deathEventFired = true;

        mario.Die();

        Assert.IsTrue(deathEventFired, "Die() 应该触发 OnDeath 事件");
        Assert.IsFalse(mario.enabled, "Die() 后 MarioController 应该被禁用");

        Object.Destroy(marioGO);
    }

    [UnityTest]
    public IEnumerator Mario_Bounce_SetsUpwardVelocity()
    {
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return null;

        mario.Bounce(15f);

        Rigidbody2D rb = marioGO.GetComponent<Rigidbody2D>();
        Assert.Greater(rb.velocity.y, 0f,
            "Bounce 后 Y 速度应该为正（向上弹跳）");

        Object.Destroy(marioGO);
    }

    // ═══════════════════════════════════════════════════════
    // 10. InputManager 集成测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator InputManager_DisableInput_StopsPlayerMovement()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        GameObject imGO = new GameObject("TestIM");
        InputManager im = imGO.AddComponent<InputManager>();
        im.SetMarioController(mario);

        yield return null;

        // 禁用输入
        im.DisableAllInput();

        yield return null;

        // 验证 Mario 的移动输入被清零
        // （DisableAllInput 调用 SetMoveInput(Vector2.zero)）
        Assert.IsFalse(im.enabled, "DisableAllInput 后 InputManager 应该被禁用");

        Object.Destroy(marioGO);
        Object.Destroy(imGO);
        Object.Destroy(ground);
    }

    // ═══════════════════════════════════════════════════════
    // 11. Trickster 伪装状态下移动限制测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Trickster_IsDisguised_ReturnsFalse_WhenNoDisguiseSystem()
    {
        // 创建一个没有 DisguiseSystem 的 Trickster
        GameObject go = new GameObject("TestTricksterNoDisguise");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        TricksterController trickster = go.AddComponent<TricksterController>();

        yield return null;

                Assert.IsFalse(trickster.IsDisguised,
            "没有 DisguiseSystem 时 IsDisguised 应该返回 false");
        Object.Destroy(go);
    }

    // ═══════════════════════════════════════════════════════
    // 12. Mario 胜利表现测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_Win_DisablesControllerAndFiresEvent()
    {
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return null;

        bool winEventFired = false;
        mario.OnWin += () => winEventFired = true;

        mario.Win();

        Assert.IsTrue(winEventFired, "Win() 应该触发 OnWin 事件");
        Assert.IsFalse(mario.enabled, "Win() 后 MarioController 应该被禁用");

        Rigidbody2D rb = marioGO.GetComponent<Rigidbody2D>();
        Assert.AreEqual(0f, rb.velocity.x, 0.01f, "Win() 后 X 速度应为 0");
        Assert.AreEqual(0f, rb.velocity.y, 0.01f, "Win() 后 Y 速度应为 0");

        Object.Destroy(marioGO);
    }

    [UnityTest]
    public IEnumerator GlobalHUD_RuntimeLifecycle_FeedbackExpiresWhilePaused()
    {
        var go = new GameObject("RuntimeHUDTest");
        var existingSink = Object.FindObjectOfType<InteractionLogSink>();
        float originalScale = Time.timeScale;
        try
        {
            var hud = go.AddComponent<GlobalGameUICanvas>();
            yield return null; // Exercise real Awake, OnEnable, Start and Update, not reflection.
            var panel = go.transform.Find("HUDRoot/AbilityFailPanel");
            Assert.IsNotNull(panel);
            Assert.IsNotNull(go.transform.Find("HUDRoot/InteractionLogPanel"), "HUD must finish building");
            var text = panel.Find("AbilityFailText").GetComponent<UnityEngine.UI.Text>();
            Assert.IsFalse(panel.gameObject.activeSelf);
            Time.timeScale = 0f;
            hud.ShowAbilityFailFeedback("Not enough energy");
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf);
            Assert.AreEqual("Not enough energy", text.text);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (panel.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(panel.gameObject.activeSelf, "Feedback uses unscaled time and must not linger during pause");
            Assert.IsFalse(text.gameObject.activeInHierarchy);
            hud.ShowAbilityFailFeedback("Wait for cooldown");
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf);
            Assert.AreEqual("Wait for cooldown", text.text);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Time.timeScale = originalScale;
            Object.DestroyImmediate(go);
            if (existingSink == null)
            {
                var createdSink = Object.FindObjectOfType<InteractionLogSink>();
                if (createdSink != null) Object.DestroyImmediate(createdSink.gameObject);
            }
        }
    }

    [UnityTest]
    public IEnumerator GameManager_MarioReachesGoal_CallsWinAndShowsGameOver()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return null;

        string winner = null;
        gm.OnGameOver += (w) => winner = w;

        gm.OnMarioReachedGoal();

        yield return null;

        Assert.AreEqual("Mario", winner, "Mario 到达终点后应该判定 Mario 胜利");
        Assert.AreEqual(GameState.RoundOver, gm.CurrentState, "回合结束后状态应该是 RoundOver");
        Assert.IsFalse(mario.enabled, "Mario 胜利后控制器应被禁用");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }
}
