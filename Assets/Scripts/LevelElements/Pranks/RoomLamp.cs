using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S241：灯（ASCII 'i'）—— 夜里的光源。平时亮着（半径 lampRadius 内的东西他看得见）；
/// 捣蛋者伪装在旁按 L → 闪一下后熄灭 lampOffSeconds 秒（这一片变暗：你能从这里溜过去 / 荡过去），之后自己亮回来。
/// 白天开不开灯没区别（处处亮）。
/// 规则：H3 有预警（闪）；H4 灯灭是公开的画面事件，马里奥看见灯灭会起疑（OnPropActivated → 看见机关动了，同一视锥规则）；
///       H9 熄灭有时限。代价：灭灯这一下他看得见（亮处在动）→ 别在他眼皮底下关。
/// 不预约（按下就灭）、不按"打没打中"退还（它本来就不是用来打人的）。
/// 参考：Mark of the Ninja 打灭灯制造暗区（只借规则）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class RoomLamp : ControllableLevelElement
{
    public override bool ArmOnPress => false;
    protected override bool RefundOnMiss => false;

    private static readonly List<RoomLamp> all = new List<RoomLamp>();
    public static IReadOnlyList<RoomLamp> All => all;
    private float offFor;
    private SpriteRenderer glow;

    /// <summary>亮着吗（熄灭期间 = false）。</summary>
    public bool On => offFor <= 0f;

    /// <summary>纯逻辑：熄灭倒计时走一步。</summary>
    public static float TickOff(float offFor, float dt) => Mathf.Max(0f, offFor - dt);

    protected override void Awake()
    {
        propName = "灯";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Interactive | ElementTag.Resettable;
        elementDescription = "夜里的光源；按 L 灭 8 秒";
        telegraphDuration = 0.3f; activeDuration = 0.1f; recoveryDuration = 0f; cooldownDuration = 1f;
        base.Awake();
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
        var g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        glow = g.AddComponent<SpriteRenderer>();
        glow.sprite = Step1Sprites.Square; glow.sortingOrder = 1;
        g.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
    }

    protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); all.Remove(this); }

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }
    protected override void OnActiveEnd() { }
    protected override void OnActivate(Vector2 direction)
    {
        var t = MarioMindTuningSO.LoadOrDefault();
        offFor = t != null ? t.lampOffSeconds : 8f;
        Step1Fx.Burst(transform.position, 6, new Color(1f, 0.95f, 0.6f, 1f), 3f, Vector2.up, 360f, 0f, 0.1f, 0.3f);
        Step1Hint.Show(string.Format(Step1Text.LampOff, offFor), 1.4f);
    }

    protected override void Update()
    {
        base.Update();
        offFor = TickOff(offFor, Time.deltaTime);
        if (glow != null)
        {
            bool night = Step1Lighting.Current != null && Step1Lighting.Current.Dark;
            glow.enabled = On && night;
            glow.color = new Color(1f, 0.92f, 0.55f, 0.18f + Mathf.Sin(Time.time * 3f) * 0.03f);
        }
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        offFor = 0f;
    }
}
