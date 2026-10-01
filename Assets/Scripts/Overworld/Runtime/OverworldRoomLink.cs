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
    private float dazedNote, carryNote;
    private int carriedMario = -1, carriedYou = -1;
    private GUIStyle noteStyle, overStyle;
    public const float AutoBackSeconds = 8f;

    private void Start()
    {
        if (!OverworldSession.Active) { enabled = false; return; }
        manager = GameManager.Instance;
        if (manager != null) { manager.OnGameOver += HandleOver; manager.OnRoundStart += HandleRoundStart; }
        GameManager.BlockRoundOverKeys = () => over; // 回合结束后 R/N 不重开，由这里送回小镇
        GameManager.RestartOverride = RestartRoom;   // S212：F5 = 平滑重开这个房间（以前会在编辑器里直接退出 Play，小镇进度全丢）
        StartCoroutine(ApplyNextFrame());
        StartCoroutine(RevealOnYou());
    }

    private void OnDestroy()
    {
        if (manager != null) { manager.OnGameOver -= HandleOver; manager.OnRoundStart -= HandleRoundStart; }
        if (GameManager.RestartOverride == (System.Func<bool>)RestartRoom) GameManager.RestartOverride = null;
    }

    /// <summary>S212：转场的圆在你（捣蛋者）身上展开——一进门眼睛就知道自己在哪。</summary>
    private System.Collections.IEnumerator RevealOnYou()
    {
        yield return null;
        var t = FindObjectOfType<TricksterController>();
        if (t != null) SceneTransit.RevealAt(t.transform.position);
    }

    /// <summary>结果已经记下 → 不许重开（防止输了就 F5 刷）；还在打 → 同一扇门重来一次（小镇时间、门的顺序都不变）。</summary>
    private bool RestartRoom()
    {
        if (over || SceneTransit.Busy) return true;
        string here = gameObject.scene.path;
        if (!SceneTransit.Go(here, Step1Text.OverworldTransitRetry(door))) return false;
        return true;
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
            // S218 小镇 → 房间：刚在镇上被大机关砸晕 → 开局多等几秒（迟到也照样晕着：晕是他身上的状态，不是"等你"）
            if (OverworldSession.CarriedDaze > 0f) { driver.AddStartDelay(OverworldSession.CarriedDaze); dazedNote = Time.unscaledTime + 3f; }
            // S220：小镇的心带进来（他至少 2 颗；你的心 = 命数）。少了才提示
            var hp = driver.GetComponent<PlayerHealth>();
            if (hp != null) { carriedMario = OverworldRoomCarry.MarioRoomHearts(MarioMindTuningSO.LoadOrDefault(), hp.MaxHealth); if (carriedMario < hp.MaxHealth) driver.SetCarriedHealth(carriedMario); }
        }
        var lives = FindObjectOfType<TricksterLives>();
        if (lives != null) { carriedYou = OverworldRoomCarry.TricksterRoomLives(lives.MaxLives); if (carriedYou < lives.MaxLives) lives.SetLives(carriedYou); }
        if ((carriedMario >= 0 && carriedMario < OverworldSession.MaxHearts) || (carriedYou >= 0 && carriedYou < OverworldSession.MaxHearts)) carryNote = Time.unscaledTime + 3.5f;
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
        GameManager.RestartOverride = null;
        Time.timeScale = 1f;
        string town = OverworldSession.TownScene;
        if (string.IsNullOrEmpty(town)) return;
        // S211：平滑回小镇；万一没登记到 Build Settings，退回直接加载（至少不会卡在房间里）
        var t = FindObjectOfType<TricksterController>(); // S212：圆在你身上收拢，回到小镇在门口展开
        if (!SceneTransit.Go(town, Step1Text.OverworldTransitToTown(OverworldMap.Clock(OverworldSession.Minute)), t != null ? t.transform.position : (Vector3?)null)) SceneManager.LoadScene(town);
    }

    private void OnGUI()
    {
        if (noteStyle == null) { noteStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true }; overStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true }; } // S220：样式只建一次（OnGUI 每帧好几次）
        if (!over && Time.unscaledTime < dazedNote && !SceneTransit.Busy) GUI.Box(new Rect(Screen.width / 2f - 240, 80, 480, 56), Step1Text.OverworldRoomDazed(OverworldSession.CarriedDaze), noteStyle);
        if (!over && Time.unscaledTime < carryNote && !SceneTransit.Busy) GUI.Box(new Rect(Screen.width / 2f - 240, 142, 480, 56), Step1Text.OverworldRoomCarry(carriedMario, carriedYou), noteStyle);
        if (!over || SceneTransit.Busy) return;
        GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height - 150, 520, 120), headline + "\n" + Step1Text.OverworldBackToTown, overStyle);
    }
}
