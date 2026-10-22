using UnityEngine;

/// <summary>
/// 设计宪法 H4：全项目唯一允许"读捣蛋者"的 Mario 感知类，而且只读"眼睛能看到的东西"：
///   1. 先判 CanSee（视锥+距离+遮挡），看不见就什么都不知道；
///   2. 看得见之后，才读"外观"：它长得像道具吗（伪装外观本身是公开信息）、在动吗（位移）；
///   3. 渲染器全隐藏（例如暗线移动）= 看不见。
/// 绝不读附身门禁 / 当前锚点 / 伪装系统内部状态。
/// 机关触发：只有触发位置在触发后 activationWitnessWindow 秒内进入视野，才算"亲眼看见"。
/// S241：夜里只看得见亮处（Step1Lighting：灯 / 火 / 他的手电筒），暗处只有贴身才看得见；预警中的机关、炸弹、油桶自己会发光，不受影响。
/// </summary>
public sealed class MarioEyes
{
    private readonly MarioMindTuningSO t;
    private readonly Transform mario;
    private readonly MarioController marioController;
    private TricksterController figure;
    private Renderer[] figureRenderers;
    private Vector2 lastFigurePos;
    private bool hasLastFigurePos;
    private Transform pendingActivation;
    private float pendingActivationAge = float.PositiveInfinity;
    private Transform pendingRustle;
    private float pendingRustleAge = float.PositiveInfinity;

    public MarioEyes(MarioMindTuningSO tuning, MarioController marioController, TricksterController figure)
    {
        t = tuning;
        this.marioController = marioController;
        mario = marioController != null ? marioController.transform : null;
        SetFigure(figure);
    }

    public void SetFigure(TricksterController value)
    {
        figure = value;
        figureRenderers = value != null ? value.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        hasLastFigurePos = false;
    }

    /// <summary>由 TricksterAbilitySystem.OnPropActivated 转发：只记录"哪里动了"，是否被看见由 Look 判定。</summary>
    public void NoteChainLink(IControllableProp prop) => NotePropActivated(prop);

    public void NotePropActivated(IControllableProp prop)
    {
        Transform where = prop != null ? prop.GetTransform() : null;
        if (where == null) return;
        pendingActivation = where;
        pendingActivationAge = 0f;
    }

    /// <summary>S187：草丛晃了（RustleOnPass.Rustled 转发）。只记录位置，看没看见由 Look 判定。</summary>
    public void NoteRustle(Transform where)
    {
        if (where == null) return;
        pendingRustle = where;
        pendingRustleAge = 0f;
    }

    /// <summary>S196：听见响声（砸墙）。声音不需要视线：在听力范围内就当作"看见草丛晃了"（同一起疑通道，只有位置）。</summary>
    public void NoteNoise(Vector2 where)
    {
        if (mario == null || t == null) return;
        if (Vector2.Distance(mario.position, where) > t.hearingRange) return;
        heardNoise = true; heardAt = where;
    }
    private bool heardNoise; private Vector2 heardAt;

    /// <summary>S200：听见挑衅（"来抓我呀"）。只收一个位置；听力范围与响声相同。</summary>
    public void NoteTaunt(Vector2 where)
    {
        if (mario == null || t == null) return;
        if (Vector2.Distance(mario.position, where) > t.hearingRange) return;
        heardTauntFlag = true; tauntAt = where;
    }
    private bool heardTauntFlag; private Vector2 tauntAt;

    /// <summary>S197：小声音（通风管咣当）：只有很近才听得见（听力范围的 1/3）。</summary>
    public void NoteNoiseNear(Vector2 where)
    {
        if (mario == null || t == null) return;
        if (Vector2.Distance(mario.position, where) > t.hearingRange / 3f) return;
        heardNoise = true; heardAt = where;
    }

    /// <summary>S241：听见脚步（夜里你跑动）。只收一个位置；距离 = 脚步声圈（Step1Stealth.FootstepRadius，下雨打折）。</summary>
    public void NoteFootstep(Vector2 where)
    {
        if (mario == null || t == null) return;
        if (Vector2.Distance(mario.position, where) > Step1Stealth.FootstepRadius(t, Step1Lighting.Raining)) return;
        heardNoise = true; heardAt = where;
    }

    public void Forget() { hasLastFigurePos = false; pendingActivation = null; pendingActivationAge = float.PositiveInfinity; pendingRustle = null; }

    public void Look(float dt, ref MarioPercept p)
    {
        if (mario == null) return;
        Vector2 eye = MarioVision.EyeOf(mario, t);
        bool facingRight = marioController.IsFacingRight;
        p.marioPos = mario.position;

        p.seesFigure = false;
        p.sawRustle = false;
        if (figure != null && figure.isActiveAndEnabled && AnyRendererVisible())
        {
            Vector2 pos = figure.transform.position;
            // H4 顺序契约：必须先 CanSee，再看外观。
            // S241：看见 = 视锥 + 无遮挡 + 被照亮（暗处只有贴身才看得见）；头顶：你在他头顶附近而且那里亮 → 他抬头察觉。
            bool inSight = MarioVision.CanSee(eye, facingRight, pos, figure.transform, t) && Step1Lighting.Visible(eye, pos);
            bool overhead = !inSight && Step1Stealth.OverheadNoticed(mario.position, pos, t.overheadNoticeRadius, Step1Lighting.IsLit(pos)) && SightLine.CanWitness(eye, pos, t.overheadNoticeRadius + 1f, figure.transform);
            if (inSight || overhead)
            {
                p.seesFigure = true;
                p.figurePos = pos;
                p.figureLooksLikeProp = figure.IsDisguised;
                Vector2 velocity = hasLastFigurePos && dt > 0f ? (pos - lastFigurePos) / dt : Vector2.zero;
                p.figureVelocity = velocity;
                p.figureMoving = velocity.magnitude > t.disguisedMoveThreshold
                                 || (p.figureLooksLikeProp && Step1Loadout.ShapeShiftVisible(Time.time, figure.ShapeChangedAt, t.shapeShiftTellSeconds)); // S242：他看着时你换了形态 = 你动了
                lastFigurePos = pos;
                hasLastFigurePos = true;
                // S198 道具：透视 = 看穿伪装；隐身 = 站着不动时看不见（动起来照样看得见）
                if (Time.time < RandomPickups.MarioXRayUntil) p.figureLooksLikeProp = false;
                if (Time.time < RandomPickups.TricksterInvisibleUntil && !p.figureMoving) { p.seesFigure = false; hasLastFigurePos = false; }
            }
            else hasLastFigurePos = false;
        }
        else hasLastFigurePos = false;

        // S199：诱饵——和看你同一个视锥/遮挡规则。看得见你真身时以真身为准；只看得见诱饵时把诱饵当成你。
        // S242：道具诱饵看起来是"一个道具"——扭的时候是"会动的怪东西"（他会过来查看），走近识破。
        //       看得见你本人但你正老实装道具（没在动）时，扭动的诱饵更显眼 → 他先看诱饵。
        var decoy = DecoyAbility.Active;
        bool decoyWins = decoy != null && decoy.IsProp && p.seesFigure && p.figureLooksLikeProp && !p.figureMoving;
        if ((!p.seesFigure || decoyWins) && decoy != null && !decoy.Revealed)
        {
            Vector2 dp = decoy.transform.position;
            if (MarioVision.CanSee(eye, facingRight, dp, decoy.transform, t) && Step1Lighting.Visible(eye, dp))
            {
                if (Decoy.SeenThrough(mario.position, dp, decoy.RevealDistance, Time.time < RandomPickups.MarioXRayUntil))
                {
                    decoy.Reveal();                       // 识破：看见的是"一个假人"，并在附近起疑（只有诱饵的位置）
                    p.sawRustle = true; p.rustlePos = dp;
                }
                else if (decoy.IsProp)
                {
                    if (decoy.Wriggling) { p.seesFigure = true; p.figurePos = dp; p.figureLooksLikeProp = true; p.figureVelocity = Vector2.zero; p.figureMoving = true; hasLastFigurePos = false; }
                }
                else
                {
                    p.seesFigure = true; p.figurePos = dp; p.figureLooksLikeProp = false;
                    p.figureVelocity = Vector2.zero; p.figureMoving = true; hasLastFigurePos = false;
                }
            }
        }

        p.facingRight = facingRight;
        // S202：看得见的危险 / 道具箱（场景里公开可见的物体；同一视锥+遮挡规则）
        p.dangerRadius = 0f; float bestD = float.MaxValue;
        foreach (var b in TricksterBomb.Live)
        {
            if (b == null) continue;
            Vector2 bp = b.transform.position; float d = (bp - (Vector2)mario.position).sqrMagnitude;
            if (d < bestD && MarioVision.CanSee(eye, facingRight, bp, b.transform, t)) { bestD = d; p.dangerPos = bp; p.dangerRadius = b.Radius; }
        }
        foreach (var o in OilBarrel.All)
        {
            if (o == null || !o.Lit || o.Exploded) continue;
            Vector2 op = o.transform.position; float d = (op - (Vector2)mario.position).sqrMagnitude;
            if (d < bestD && MarioVision.CanSee(eye, facingRight, op, o.transform, t)) { bestD = d; p.dangerPos = op; p.dangerRadius = o.Radius; }
        }
        p.seesPickup = false; float bestP = float.MaxValue;
        foreach (var s in PickupSpot.All)
        {
            if (s == null || !s.Live) continue;
            Vector2 sp = s.transform.position; float d = (sp - (Vector2)mario.position).sqrMagnitude;
            if (d < bestP && MarioVision.CanSee(eye, facingRight, sp, s.transform, t) && Step1Lighting.Visible(eye, sp)) { bestP = d; p.seesPickup = true; p.pickupPos = sp; }
        }
        if (pendingRustle != null)
        {
            pendingRustleAge += dt;
            Vector2 where = pendingRustle.position;
            if (MarioVision.CanSee(eye, facingRight, where, pendingRustle, t) && Step1Lighting.Visible(eye, where))
            {
                p.sawRustle = true;
                p.rustlePos = where;
                pendingRustle = null;
            }
            else if (pendingRustleAge > t.activationWitnessWindow) pendingRustle = null;
        }

        if (heardNoise) { p.sawRustle = true; p.rustlePos = heardAt; heardNoise = false; }
        p.heardTaunt = heardTauntFlag; p.tauntPos = tauntAt; heardTauntFlag = false;

        p.witnessedActivation = false;
        if (pendingActivation != null)
        {
            pendingActivationAge += dt;
            Vector2 where = pendingActivation.position;
            if (MarioVision.CanSee(eye, facingRight, where, pendingActivation, t))
            {
                p.witnessedActivation = true;
                p.activationPos = where;
                pendingActivation = null;
            }
            else if (pendingActivationAge > t.activationWitnessWindow) pendingActivation = null;
        }
    }

    private bool AnyRendererVisible()
    {
        foreach (var r in figureRenderers)
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
        return false;
    }
}
