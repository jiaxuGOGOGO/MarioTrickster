using UnityEngine;

/// <summary>
/// 设计宪法 H4：全项目唯一允许"读捣蛋者"的 Mario 感知类，而且只读"眼睛能看到的东西"：
///   1. 先判 CanSee（视锥+距离+遮挡），看不见就什么都不知道；
///   2. 看得见之后，才读"外观"：它长得像道具吗（伪装外观本身是公开信息）、在动吗（位移）；
///   3. 渲染器全隐藏（例如暗线移动）= 看不见。
/// 绝不读附身门禁 / 当前锚点 / 伪装系统内部状态。
/// 机关触发：只有触发位置在触发后 activationWitnessWindow 秒内进入视野，才算"亲眼看见"。
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

    /// <summary>S197：小声音（通风管咣当）：只有很近才听得见（听力范围的 1/3）。</summary>
    public void NoteNoiseNear(Vector2 where)
    {
        if (mario == null || t == null) return;
        if (Vector2.Distance(mario.position, where) > t.hearingRange / 3f) return;
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
            if (MarioVision.CanSee(eye, facingRight, pos, figure.transform, t))
            {
                p.seesFigure = true;
                p.figurePos = pos;
                p.figureLooksLikeProp = figure.IsDisguised;
                Vector2 velocity = hasLastFigurePos && dt > 0f ? (pos - lastFigurePos) / dt : Vector2.zero;
                p.figureVelocity = velocity;
                p.figureMoving = velocity.magnitude > t.disguisedMoveThreshold;
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
        var decoy = DecoyAbility.Active;
        if (!p.seesFigure && decoy != null && !decoy.Revealed)
        {
            Vector2 dp = decoy.transform.position;
            if (MarioVision.CanSee(eye, facingRight, dp, decoy.transform, t))
            {
                if (Decoy.SeenThrough(mario.position, dp, decoy.RevealDistance, Time.time < RandomPickups.MarioXRayUntil))
                {
                    decoy.Reveal();                       // 识破：看见的是"一个假人"，并在附近起疑（只有诱饵的位置）
                    p.sawRustle = true; p.rustlePos = dp;
                }
                else
                {
                    p.seesFigure = true; p.figurePos = dp; p.figureLooksLikeProp = false;
                    p.figureVelocity = Vector2.zero; p.figureMoving = true; hasLastFigurePos = false;
                }
            }
        }

        p.facingRight = facingRight;
        if (pendingRustle != null)
        {
            pendingRustleAge += dt;
            Vector2 where = pendingRustle.position;
            if (MarioVision.CanSee(eye, facingRight, where, pendingRustle, t))
            {
                p.sawRustle = true;
                p.rustlePos = where;
                pendingRustle = null;
            }
            else if (pendingRustleAge > t.activationWitnessWindow) pendingRustle = null;
        }

        if (heardNoise) { p.sawRustle = true; p.rustlePos = heardAt; heardNoise = false; }

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
