using UnityEngine;

/// <summary>
/// S241：你跑动的脚步声（"阴影处只能听声音"）。只在<b>夜里</b>响（白天他直接看得见你，脚步不该再白送他信息——试玩数据马里奥已经太强）。
/// 伪装 / 遁地 / 挂在丝上 / 站着 = 没声音（Step1Stealth.MakesFootstep）。下雨打折（rainHearingScale）。
/// 声音只带位置（H4），圈的大小 = 他耳朵的判定（Step1Stealth.FootstepRadius，S224 规则：圈 = 判定同一个数）。
/// 由 Step1Combo 运行时自动挂上（旧场景不用重建）。
/// </summary>
public class TricksterFootsteps : MonoBehaviour
{
    /// <summary>一声脚步（只有位置）。MarioEyes.NoteFootstep 听、Step1SoundRings 画圈。</summary>
    public static event System.Action<Vector2> Stepped;
    private TricksterController self;
    private Rigidbody2D rb;
    private float next;
    private const float Interval = 0.45f;

    private void Start() { self = GetComponent<TricksterController>(); rb = GetComponent<Rigidbody2D>(); }

    private void Update()
    {
        if (self == null || rb == null || Step1HandsOffCheck.IsRunning) return;
        var light = Step1Lighting.Current;
        if (light == null || !light.Dark) return;
        bool quiet = self.IsDisguised || self.BusyMoving || self.ExternalDrive;
        if (!Step1Stealth.MakesFootstep(self.IsGrounded, rb.velocity.x, quiet)) { next = 0f; return; }
        next -= Time.deltaTime;
        if (next > 0f) return;
        next = Interval;
        Stepped?.Invoke(transform.position);
    }
}
