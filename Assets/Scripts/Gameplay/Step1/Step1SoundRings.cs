using UnityEngine;

/// <summary>
/// S224（总方案阶段 C · Mark of the Ninja）：房间里马里奥听得见的声音画成圈。纯画面。
/// 订阅的就是 MarioMindDriver 喂给马里奥耳朵的同一组事件（挑衅 / 炸弹 / 砸墙 / 通风管 / 连锁咔哒），
/// 圈的大小 = 耳朵判定用的同一个数（Step1Readability.SoundRadius）→ "他在圈里 = 他听见了"。
/// 不新增感知（H4）：马里奥那边一行没改；这里只画。草晃不画圈——那是"看见"，不是"听见"。
/// 由 Step1Combo 运行时自动挂上（旧场景不用重建）。
/// </summary>
public class Step1SoundRings : MonoBehaviour
{
    private MarioMindTuningSO tuning;

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        TauntAbility.Taunted += OnTaunt;
        TricksterBomb.Exploded += OnBomb;
        CrackedWall.Smashed += OnWall;
        Vent.Clanged += OnVent;
        ChainPlan.Clicked += OnClick;
        TricksterFootsteps.Stepped += OnStep; // S241
    }

    private void OnDestroy()
    {
        TauntAbility.Taunted -= OnTaunt;
        TricksterBomb.Exploded -= OnBomb;
        CrackedWall.Smashed -= OnWall;
        Vent.Clanged -= OnVent;
        ChainPlan.Clicked -= OnClick;
        TricksterFootsteps.Stepped -= OnStep;
    }

    private void Ring(Vector2 at, Step1Readability.Sound s)
    {
        if (tuning == null || !tuning.soundRings) return;
        Step1Fx.SoundRing(at, Step1Readability.SoundRadius(s, tuning));
    }

    private void OnTaunt(Vector2 at) => Ring(at, Step1Readability.Sound.Taunt);
    private void OnBomb(Vector2 at) => Ring(at, Step1Readability.Sound.Bomb);
    private void OnWall(Vector2 at) => Ring(at, Step1Readability.Sound.WallSmash);
    private void OnVent(Vector2 at) => Ring(at, Step1Readability.Sound.Vent);
    private void OnClick(Vector2 at) => Ring(at, Step1Readability.Sound.ChainClick);
    // S241：脚步圈 = 耳朵判定同一个函数（下雨打折）
    private void OnStep(Vector2 at) { if (tuning != null && tuning.soundRings) Step1Fx.SoundRing(at, Step1Stealth.FootstepRadius(tuning, Step1Lighting.Raining)); }
}
