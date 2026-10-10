using UnityEngine;

/// <summary>
/// S244：房间里的暂停菜单（Esc 打开）。以前暂停只有一行"已暂停 Esc 继续"——想重开、看说明、改速度都得记快捷键。
/// ↑↓ 选、Enter / 空格 确认、数字键直接选、Esc 继续。设置子页 ←→ 调值（改的是调参文件里同名字段，S238：只有一个调参文件）。
/// 暂停还是 GameManager.TogglePause 在管（timeScale = 0、输入关掉），这里只画菜单、读菜单键（两套输入都读，Step1Keys）。
/// 纯逻辑在 Step1Flow（菜单项 / 绕回 / 速度档），sim 编译验证。
/// </summary>
public class Step1PauseMenu : MonoBehaviour
{
    private GameManager manager;
    private MarioMindTuningSO tuning;
    private int index, settingIndex;
    private bool inSettings, wasPaused, helpWasOpen;
    private System.Collections.Generic.List<Step1Flow.PauseItem> items;

    public static bool Open { get; private set; }
    /// <summary>场景里有暂停菜单（旧场景没重建 = 没有 → Step1Screen 退回画一行"已暂停"）。</summary>
    public static bool Exists { get; private set; }

    private void Awake() { Exists = true; }

    private void Start()
    {
        manager = GameManager.Instance;
        tuning = MarioMindTuningSO.LoadOrDefault();
        items = Step1Flow.PauseItems(OverworldSession.Active);
    }

    private void OnDestroy() { Open = false; Exists = false; }

    private bool Paused => manager != null && manager.CurrentState == GameState.Paused;

    private void Update()
    {
        bool p = Paused && !Step1HandsOffCheck.IsRunning;
        if (p && !wasPaused) { index = 0; inSettings = false; } // 每次打开都从"继续"开始（防止手快连按 Enter 误点重开）
        bool helpJustClosed = helpWasOpen && !Step1Screen.HelpOpen; helpWasOpen = Step1Screen.HelpOpen;
        wasPaused = p; Open = p && !Step1Screen.HelpOpen;
        if (!Open || helpJustClosed) return; // 关说明的那一下按键不算菜单操作（不然按 Enter 关说明会顺手再点一次）
        if (inSettings) { SettingsKeys(); return; }
        if (Step1Keys.Down(KeyCode.UpArrow) || Step1Keys.Down(KeyCode.W)) index = Step1Flow.Move(index, -1, items.Count);
        if (Step1Keys.Down(KeyCode.DownArrow) || Step1Keys.Down(KeyCode.S)) index = Step1Flow.Move(index, 1, items.Count);
        int pick = Step1Flow.DigitPick(Step1Keys.Digit1to5(), items.Count);
        if (pick >= 0) { index = pick; Choose(items[index]); return; }
        if (Step1Keys.Down(KeyCode.Return) || Step1Keys.Down(KeyCode.Space)) Choose(items[index]);
    }

    private void Choose(Step1Flow.PauseItem it)
    {
        switch (it)
        {
            case Step1Flow.PauseItem.Resume: manager.TogglePause(); break;
            case Step1Flow.PauseItem.Restart: manager.TogglePause(); manager.RestartLevel(); break; // 从小镇来的房间：RestartOverride = 同一扇门平滑重来
            case Step1Flow.PauseItem.Help: Step1Screen.OpenHelp(); break; // 说明关掉后还在暂停菜单
            case Step1Flow.PauseItem.Settings: inSettings = true; settingIndex = 0; break;
            case Step1Flow.PauseItem.BackToTown:
                manager.TogglePause();
                manager.EndRound("Mario", "Left the room from the pause menu."); // 算他赢 = 这扇门被偷（不能靠暂停刷结果）；OverworldRoomLink 接着送回小镇
                break;
            default:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    private void SettingsKeys()
    {
        int n = Step1Flow.Settings.Length;
        if (Step1Keys.Down(KeyCode.Escape) || Step1Keys.Down(KeyCode.Backspace)) { inSettings = false; return; } // Esc = 和别处一样直接继续游戏（GameManager 读 Esc）；Backspace / 最后一行"返回" = 回到暂停菜单
        if (Step1Keys.Down(KeyCode.UpArrow) || Step1Keys.Down(KeyCode.W)) settingIndex = Step1Flow.Move(settingIndex, -1, n + 1);
        if (Step1Keys.Down(KeyCode.DownArrow) || Step1Keys.Down(KeyCode.S)) settingIndex = Step1Flow.Move(settingIndex, 1, n + 1);
        int dir = Step1Keys.Down(KeyCode.RightArrow) || Step1Keys.Down(KeyCode.D) ? 1 : Step1Keys.Down(KeyCode.LeftArrow) || Step1Keys.Down(KeyCode.A) ? -1 : 0;
        bool ok = Step1Keys.Down(KeyCode.Return) || Step1Keys.Down(KeyCode.Space);
        if (settingIndex == n) { if (ok) inSettings = false; return; } // 最后一行 = 返回
        if (dir == 0 && ok) dir = 1;
        if (dir != 0) Change(Step1Flow.Settings[settingIndex], dir);
    }

    private void Change(Step1Flow.Setting s, int dir)
    {
        switch (s)
        {
            case Step1Flow.Setting.GameSpeed: tuning.roomGameSpeed = Step1Flow.StepSpeed(tuning.roomGameSpeed, dir); break; // Step1Screen 恢复游戏时按这个速度
            case Step1Flow.Setting.ScreenShake: tuning.screenShake = !tuning.screenShake; break;
            case Step1Flow.Setting.SoundRings: tuning.soundRings = !tuning.soundRings; break;
            case Step1Flow.Setting.ArtSkin: tuning.artCharacters = tuning.artProps = !tuning.artCharacters; Step1Hint.Show(Step1Text.PauseArtNextRound, 2f); break; // 换图要重开一局才生效（白盒是开局时换的）
            case Step1Flow.Setting.HelpOnStart: tuning.showHelpOnStart = !tuning.showHelpOnStart; break;
            default: tuning.startCountdown = !tuning.startCountdown; break;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tuning); // 编辑器里改的值写回调参文件（下次进 Play 还在）
#endif
    }

    private string ValueOf(Step1Flow.Setting s)
    {
        switch (s)
        {
            case Step1Flow.Setting.GameSpeed: return Step1Flow.SpeedText(MarioMindTuningSO.ClampRoomSpeed(tuning.roomGameSpeed));
            case Step1Flow.Setting.ScreenShake: return Step1Flow.OnOff(tuning.screenShake);
            case Step1Flow.Setting.SoundRings: return Step1Flow.OnOff(tuning.soundRings);
            case Step1Flow.Setting.ArtSkin: return Step1Flow.OnOff(tuning.artCharacters);
            case Step1Flow.Setting.HelpOnStart: return Step1Flow.OnOff(tuning.showHelpOnStart);
            default: return Step1Flow.OnOff(tuning.startCountdown);
        }
    }

    private void OnGUI()
    {
        if (!Open) return;
        GUI.depth = -12;
        float w = Step1Gui.Begin(), h = Step1Gui.VirtualHeight;
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); GUI.color = old; // 整个画面压暗 = 一眼知道停了
        int rows = inSettings ? Step1Flow.Settings.Length + 1 : items.Count;
        var r = new Rect(w * 0.5f - 360f, h * 0.5f - (150f + rows * 30f), 720f, 190f + rows * 60f);
        Step1Gui.Panel(r, 0.93f);
        GUI.Label(new Rect(r.x, r.y + 20f, r.width, 60f), inSettings ? "<b>设置  Settings</b>" : "<b>已暂停  Paused</b>", Step1Gui.Text(40, TextAnchor.MiddleCenter));
        float y = r.y + 100f;
        for (int i = 0; i < rows; i++)
        {
            bool cur = inSettings ? i == settingIndex : i == index;
            var row = new Rect(r.x + 60f, y + i * 60f, r.width - 120f, 52f);
            if (cur) { GUI.color = new Color(0.45f, 0.7f, 1f, 0.35f); GUI.DrawTexture(row, Texture2D.whiteTexture); GUI.color = old; }
            string label;
            if (!inSettings) label = $"<color=#9AA4B8>{i + 1}</color>   " + Step1Flow.PauseLabel(items[i]);
            else if (i == Step1Flow.Settings.Length) label = "← 返回  Back";
            else label = Step1Flow.SettingLabel(Step1Flow.Settings[i]) + $"   <b>{(cur ? "◀ " : "")}{ValueOf(Step1Flow.Settings[i])}{(cur ? " ▶" : "")}</b>";
            GUI.Label(row, (cur ? "▶ " : "   ") + label, Step1Gui.Text(28, TextAnchor.MiddleLeft, false));
        }
        GUI.Label(new Rect(r.x, r.yMax - 52f, r.width, 40f), inSettings ? Step1Text.PauseSettingsKeys : Step1Text.PauseKeys, Step1Gui.Text(20, TextAnchor.MiddleCenter));
    }
}
