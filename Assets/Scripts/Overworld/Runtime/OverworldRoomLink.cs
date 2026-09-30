using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// S210：挂在"从大地图进来的房间"场景里（构建器自动加）。只有今天是从小镇进来时才生效；单独试玩这个房间时什么都不做。
/// 开局：迟到 → 马里奥不等你；埋伏 / 捡到的道具 → 多几枚炸弹；在镇上追你时进门 → 起疑带进来（上限 '?'，H2）。
/// 结束：你赢/他赢写回小镇，按 Enter（或 8 秒后）回到小镇。房间里不弹问卷。
/// </summary>
public sealed class OverworldRoomLink : MonoBehaviour
{
    public int door;
    private GameManager manager;
    private bool over, applied;
    private float overTime;
    private string headline = "";
    public const float AutoBackSeconds = 8f;

    private void Start()
    {
        if (!OverworldSession.Active) { enabled = false; return; }
        manager = GameManager.Instance;
        if (manager != null) { manager.OnGameOver += HandleOver; manager.OnRoundStart += HandleRoundStart; }
        GameManager.BlockRoundOverKeys = () => over; // 回合结束后 R/N 不重开，由这里送回小镇
        StartCoroutine(ApplyNextFrame());
    }

    private void OnDestroy()
    {
        if (manager != null) { manager.OnGameOver -= HandleOver; manager.OnRoundStart -= HandleRoundStart; }
    }

    private void HandleRoundStart() { if (!over) StartCoroutine(ApplyNextFrame()); }

    // 晚一帧：TricksterKit.ResetRound / MarioMindDriver.ResetForRound 先跑完，再叠加小镇带来的效果
    private System.Collections.IEnumerator ApplyNextFrame()
    {
        yield return null;
        if (applied) yield break;
        applied = true;
        var kit = TricksterKit.Instance;
        if (kit != null && OverworldSession.BonusBombs > 0) kit.AddBombs(OverworldSession.BonusBombs);
        var driver = FindObjectOfType<MarioMindDriver>();
        if (driver != null)
        {
            if (OverworldSession.PendingOutcome == OverworldMind.DoorOutcome.Late) driver.SkipStartDelay();
            if (OverworldSession.CarriedSuspicion > 0f && driver.Mind != null) driver.Mind.Meter.Set(OverworldSession.CarriedSuspicion);
        }
    }

    private void HandleOver(string winner)
    {
        if (over) return;
        var o = Step1Text.Classify(winner, manager != null ? manager.LastRoundReason : "");
        bool won = Step1Text.PlayerWon(o);
        OverworldSession.RecordRoom(door, won);
        headline = Step1Text.Headline(o);
        over = true; overTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (!over || SceneTransit.Busy) return;
        if (Step1Keys.Down(KeyCode.Return) || Time.unscaledTime - overTime > AutoBackSeconds) Back();
    }

    private void Back()
    {
        enabled = false;
        GameManager.BlockRoundOverKeys = null;
        Time.timeScale = 1f;
        string town = OverworldSession.TownScene;
        if (string.IsNullOrEmpty(town)) return;
        // S211：平滑回小镇；万一没登记到 Build Settings，退回直接加载（至少不会卡在房间里）
        if (!SceneTransit.Go(town, Step1Text.OverworldTransitToTown(OverworldMap.Clock(OverworldSession.Minute)))) SceneManager.LoadScene(town);
    }

    private void OnGUI()
    {
        if (!over || SceneTransit.Busy) return;
        var st = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height - 150, 520, 120), headline + "\n" + Step1Text.OverworldBackToTown, st);
    }
}
