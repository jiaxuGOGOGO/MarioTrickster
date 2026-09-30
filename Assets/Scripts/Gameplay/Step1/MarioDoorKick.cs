using UnityEngine;

/// <summary>
/// S199：马里奥踢门——他在**捷径门的错误一侧**（打不开的那一侧）赶路受阻时，会原地踢门 doorKickSeconds 秒（默认 2），把门踢开。
/// 意义：捷径门不再是"只属于你的"单向优势；他绕不过去时也有反制（宪法 A2：每种优势都有代价/反制）。
/// 踢门有声音（14 格内你能在字幕看到"咚咚"）、看得见（门在抖），你可以趁这 2 秒坑他。
/// H4：只用马里奥自己的位置与门的位置；不读捣蛋者信息。
/// </summary>
public class MarioDoorKick : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private MarioMindDriver driver;
    private OneWayDoor target;
    private float timer;
    public bool Kicking => target != null;
    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        driver = GetComponent<MarioMindDriver>();
    }

    /// <summary>纯逻辑：马里奥贴着门、在不能开的那一侧、并且他的目标在门的另一边 → 该踢了。</summary>
    public static bool ShouldKick(Vector2 mario, Vector2 door, bool opensFromLeft, Vector2 goal, float reach)
    {
        if (Mathf.Abs(mario.y - door.y) > 0.8f) return false;
        float dx = mario.x - door.x;
        if (Mathf.Abs(dx) > 0.5f + reach) return false;
        bool onLeft = dx < 0f;
        bool canOpenFromHere = opensFromLeft == onLeft;
        if (canOpenFromHere) return false;
        return onLeft ? goal.x > door.x : goal.x < door.x;
    }

    private void Update()
    {
        if (tuning == null || driver == null || tuning.doorKickSeconds <= 0f || Step1HandsOffCheck.IsRunning && !tuning.doorKickInHandsOff) return;
        if (target != null)
        {
            if (target.IsOpen) { target = null; return; }
            timer -= Time.deltaTime;
            var rb = GetComponent<Rigidbody2D>(); if (rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
            GetComponent<MarioController>()?.ApplyKnockbackStun(0.1f);
            target.Shake();
            if (timer <= 0f) { target.Open(); Step1Hint.Show(Step1Text.DoorKicked, 1.5f); target = null; }
            return;
        }
        if (driver.Mind == null || driver.Mind.State != MarioMindState.Running) return;
        var goal = driver.CurrentGoal();
        if (!goal.HasValue) return;
        foreach (var door in OneWayDoor.AllDoors)
        {
            if (door == null || door.IsOpen) continue;
            if (!ShouldKick(transform.position, door.transform.position, door.OpenFromLeft, goal.Value, 0.9f)) continue;
            target = door; timer = tuning.doorKickSeconds;
            Step1Hint.Show(Step1Text.DoorKicking, tuning.doorKickSeconds);
            break;
        }
    }
}
