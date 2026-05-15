using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VisualFeedbackBridge — GameplayEventBus 到视听表现层的唯一桥接点。
///
/// 核心逻辑脚本只发布事件，本类负责把事件翻译为：
///   - SpriteEffectController(SEF) 预设效果：HitFlash / Dissolve / Outline / HSV
///   - CameraController.Shake 相机震动
///
/// [AI防坑警告]
///   1. 禁止在陷阱、平台、热度、危机等核心逻辑脚本中直接调用 SEF 或 Camera Shake。
///   2. 本类不得修改 Rigidbody2D、Collider2D、MarioController、TricksterController 等核心物理 / 输入状态。
///   3. 若未来更换美术表现，只修改本桥接层或 SEF 预设，不回写机制脚本。
/// </summary>
public sealed class VisualFeedbackBridge : MonoBehaviour
{
    private sealed class ResidueEffectState
    {
        public bool prevOutline;
        public Color prevOutlineColor;
        public float prevThickness;
        public float prevGlow;
        public bool prevHsv;
        public float prevBrightness;
        public float prevSaturation;
        public Coroutine timeoutRoutine;
    }

    private static VisualFeedbackBridge _instance;

    [Header("=== SEF 自动补齐 ===")]
    [Tooltip("目标物体没有 SpriteEffectController 但有 SpriteRenderer 时，自动补齐 SEF 组件以保持旧表现不丢失。")]
    [SerializeField] private bool autoAddSpriteEffectController = true;

    [Header("=== 残留 / 破绽表现 ===")]
    [SerializeField] private Color residueOutlineColor = new Color(1f, 0.3f, 0.9f, 1f);
    [SerializeField] private Color highHeatOutlineColor = new Color(1f, 0.75f, 0.15f, 1f);
    [SerializeField] private float residuePulseDuration = 0.35f;
    [SerializeField] private float residueOutlineThickness = 3f;
    [SerializeField] private float residueOutlineGlow = 1.25f;

    [Header("=== 揭露 / 危机表现 ===")]
    [SerializeField] private Color revealFlashColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float revealFlashDuration = 0.18f;
    [SerializeField] private Color crisisFlashColor = new Color(1f, 0.35f, 0.05f, 1f);
    [SerializeField] private float crisisFlashDuration = 0.12f;

    private readonly Dictionary<SpriteEffectController, ResidueEffectState> _residueStates = new Dictionary<SpriteEffectController, ResidueEffectState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        GameObject go = new GameObject("[VisualFeedbackBridge]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<VisualFeedbackBridge>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        GameplayEventBus.OnTrapTriggered += HandleTrapTriggered;
        GameplayEventBus.OnBouncyPlatformLaunched += HandleBouncyPlatformLaunched;
        GameplayEventBus.OnTricksterRevealed += HandleTricksterRevealed;
        GameplayEventBus.OnHeatTierChanged += HandleHeatTierChanged;
        GameplayEventBus.OnCrisisWarning += HandleCrisisWarning;
        GameplayEventBus.OnResidueSpotted += HandleResidueSpotted;
    }

    private void OnDisable()
    {
        GameplayEventBus.OnTrapTriggered -= HandleTrapTriggered;
        GameplayEventBus.OnBouncyPlatformLaunched -= HandleBouncyPlatformLaunched;
        GameplayEventBus.OnTricksterRevealed -= HandleTricksterRevealed;
        GameplayEventBus.OnHeatTierChanged -= HandleHeatTierChanged;
        GameplayEventBus.OnCrisisWarning -= HandleCrisisWarning;
        GameplayEventBus.OnResidueSpotted -= HandleResidueSpotted;
    }

    private void HandleTrapTriggered(GameplayEventBus.TrapTriggeredPayload payload)
    {
        if (payload == null) return;

        PlayHitFlash(payload.source, payload.hitFlashDuration, payload.hitFlashColor);
        if (payload.playDissolve)
        {
            PlayDissolve(payload.source, payload.dissolveDuration);
        }

        ShakeCamera(payload.shakeDuration, payload.shakeMagnitude);
    }

    private void HandleBouncyPlatformLaunched(GameplayEventBus.BouncyPlatformLaunchedPayload payload)
    {
        if (payload == null) return;

        PlayHitFlash(payload.platform, payload.flashDuration, payload.flashColor);
        ShakeCamera(payload.shakeDuration, payload.shakeMagnitude);
    }

    private void HandleTricksterRevealed(GameplayEventBus.TricksterRevealedPayload payload)
    {
        if (payload == null || payload.trickster == null) return;

        PlayHitFlash(payload.trickster.gameObject, revealFlashDuration, revealFlashColor);
        ShakeCamera(0.16f, 0.08f);
    }

    private void HandleHeatTierChanged(GameplayEventBus.HeatTierChangedPayload payload)
    {
        if (payload == null || payload.heatMeter == null) return;

        // 热度变为高压档位时给热度拥有者一个短促 SEF 提示；只做表现，不影响热度模型。
        if (payload.newTier == TricksterHeatMeter.HeatTier.Alert || payload.newTier == TricksterHeatMeter.HeatTier.Lockdown)
        {
            Color color = payload.newTier == TricksterHeatMeter.HeatTier.Lockdown ? crisisFlashColor : highHeatOutlineColor;
            PlayHitFlash(payload.heatMeter.gameObject, crisisFlashDuration, color);
        }
    }

    private void HandleCrisisWarning(GameplayEventBus.CrisisWarningPayload payload)
    {
        if (payload == null) return;

        ShakeCamera(payload.shakeDuration, payload.shakeMagnitude);
        if (payload.director != null)
        {
            PlayHitFlash(payload.director.gameObject, crisisFlashDuration, crisisFlashColor);
        }
    }

    private void HandleResidueSpotted(GameplayEventBus.ResidueSpottedPayload payload)
    {
        if (payload == null) return;

        GameObject target = payload.target;
        if (target == null && payload.anchor != null)
        {
            target = payload.anchor.gameObject;
        }
        if (target == null) return;

        if (payload.intensity <= 0.01f)
        {
            ClearResiduePulse(target);
            return;
        }

        Color color = payload.heatTier == TricksterHeatMeter.HeatTier.Lockdown ? crisisFlashColor : residueOutlineColor;
        float duration = Mathf.Max(0.05f, residuePulseDuration * Mathf.Lerp(0.75f, 1.5f, payload.intensity));
        float thickness = Mathf.Lerp(1f, residueOutlineThickness, payload.intensity);
        float glow = Mathf.Lerp(0.25f, residueOutlineGlow, payload.intensity);

        PlayHitFlash(target, Mathf.Min(duration, 0.2f), color);
        StartResiduePulse(target, color, duration, thickness, glow);
    }

    private void PlayHitFlash(GameObject target, float duration, Color color)
    {
        if (target == null || duration <= 0f) return;

        SpriteEffectController effect = ResolveSpriteEffect(target);
        if (effect == null) return;

        effect.PlayHitFlash(duration, color);
    }

    private void PlayDissolve(GameObject target, float duration)
    {
        if (target == null || duration <= 0f) return;

        SpriteEffectController effect = ResolveSpriteEffect(target);
        if (effect == null) return;

        effect.PlayDissolve(duration);
    }

    private void StartResiduePulse(GameObject target, Color color, float duration, float thickness, float glow)
    {
        SpriteEffectController effect = ResolveSpriteEffect(target);
        if (effect == null) return;

        if (!_residueStates.TryGetValue(effect, out ResidueEffectState state))
        {
            state = CaptureResidueState(effect);
            _residueStates.Add(effect, state);
        }
        else if (state.timeoutRoutine != null)
        {
            StopCoroutine(state.timeoutRoutine);
        }

        effect.enableOutline = true;
        effect.outlineColor = color;
        effect.outlineThickness = thickness;
        effect.outlineGlow = glow;
        effect.enableHSV = true;
        effect.brightness = Mathf.Max(state.prevBrightness, 1.15f);
        effect.saturation = Mathf.Max(state.prevSaturation, 1.15f);
        effect.EditorSyncProperties();

        state.timeoutRoutine = StartCoroutine(ClearResidueAfterDelay(effect, duration));
    }

    private void ClearResiduePulse(GameObject target)
    {
        SpriteEffectController effect = ResolveSpriteEffect(target);
        if (effect == null) return;
        RestoreResidueState(effect);
    }

    private ResidueEffectState CaptureResidueState(SpriteEffectController effect)
    {
        return new ResidueEffectState
        {
            prevOutline = effect.enableOutline,
            prevOutlineColor = effect.outlineColor,
            prevThickness = effect.outlineThickness,
            prevGlow = effect.outlineGlow,
            prevHsv = effect.enableHSV,
            prevBrightness = effect.brightness,
            prevSaturation = effect.saturation
        };
    }

    private IEnumerator ClearResidueAfterDelay(SpriteEffectController effect, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && effect != null)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (effect != null)
        {
            if (_residueStates.TryGetValue(effect, out ResidueEffectState state))
            {
                state.timeoutRoutine = null;
            }
            RestoreResidueState(effect);
        }
    }

    private void RestoreResidueState(SpriteEffectController effect)
    {
        if (effect == null) return;
        if (!_residueStates.TryGetValue(effect, out ResidueEffectState state)) return;

        if (state.timeoutRoutine != null)
        {
            StopCoroutine(state.timeoutRoutine);
            state.timeoutRoutine = null;
        }

        effect.enableOutline = state.prevOutline;
        effect.outlineColor = state.prevOutlineColor;
        effect.outlineThickness = state.prevThickness;
        effect.outlineGlow = state.prevGlow;
        effect.enableHSV = state.prevHsv;
        effect.brightness = state.prevBrightness;
        effect.saturation = state.prevSaturation;
        effect.EditorSyncProperties();

        _residueStates.Remove(effect);
    }

    private SpriteEffectController ResolveSpriteEffect(GameObject root)
    {
        if (root == null) return null;

        SpriteEffectController effect = root.GetComponent<SpriteEffectController>();
        if (effect != null) return effect;

        effect = root.GetComponentInChildren<SpriteEffectController>(true);
        if (effect != null) return effect;

        if (!autoAddSpriteEffectController) return null;

        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = root.GetComponentInChildren<SpriteRenderer>(true);
        }
        if (renderer == null) return null;

        return renderer.GetComponent<SpriteEffectController>() ?? renderer.gameObject.AddComponent<SpriteEffectController>();
    }

    private void ShakeCamera(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        CameraController controller = mainCamera.GetComponent<CameraController>();
        if (controller == null) return;

        controller.Shake(duration, magnitude);
    }
}
