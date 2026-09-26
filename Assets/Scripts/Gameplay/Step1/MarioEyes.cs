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

    public void Forget() { hasLastFigurePos = false; pendingActivation = null; pendingActivationAge = float.PositiveInfinity; }

    public void Look(float dt, ref MarioPercept p)
    {
        if (mario == null) return;
        Vector2 eye = MarioVision.EyeOf(mario, t);
        bool facingRight = marioController.IsFacingRight;
        p.marioPos = mario.position;

        p.seesFigure = false;
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
            }
            else hasLastFigurePos = false;
        }
        else hasLastFigurePos = false;

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
