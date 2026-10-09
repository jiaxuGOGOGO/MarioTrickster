using UnityEngine;

/// <summary>
/// S244：一局的节奏提示（纯画面，不改任何判定 / 计时 / 马里奥决策——H4）。
/// ① 开局倒计时：马里奥出发前最后 3 秒屏幕中间大字 3 · 2 · 1，出发那一下"开始！"（以前只有左上角一行小字"x 秒后出发"，没人看）。
/// ② 横幅：他拿到宝 / 最后 10 秒——屏幕上方大字 1.6 秒（以前只能读左上角的状态行）。
/// 每项可在调参 startCountdown / roundBanners 关掉；暂停菜单"设置"里也能开关倒计时。纯逻辑在 Step1Flow，sim 验证。
/// </summary>
public class Step1Rhythm : MonoBehaviour
{
    private MarioMindTuningSO tuning;
    private GameManager manager;
    private MarioMindDriver driver;
    private float goAt = -99f, lastWait = -1f, prevTimer = 999f;
    private string lastNum = ""; private float numAt;
    private Step1Flow.Banner banner; private float bannerAt = -99f;
    private const float BannerSeconds = 1.6f;
    private bool lootSeen;

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        driver = FindObjectOfType<MarioMindDriver>();
        LootObjective.OnLootCollected += OnLoot;
        if (manager != null) manager.OnRoundStart += OnRound;
    }

    private void OnDestroy()
    {
        LootObjective.OnLootCollected -= OnLoot;
        if (manager != null) manager.OnRoundStart -= OnRound;
    }

    private void OnRound() { goAt = -99f; lastWait = -1f; prevTimer = 999f; lootSeen = false; banner = Step1Flow.Banner.None; }
    private void OnLoot() { if (lootSeen) return; lootSeen = true; Show(Step1Flow.Banner.LootTaken); }

    private void Show(Step1Flow.Banner b)
    {
        if (tuning == null || !tuning.roundBanners) return;
        banner = b; bannerAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (driver == null) driver = FindObjectOfType<MarioMindDriver>();
        if (driver != null)
        {
            float wait = driver.IsWaitingToStart ? driver.StartDelayRemaining : 0f;
            if (lastWait > 0f && wait <= 0f) goAt = Time.unscaledTime; // 刚出发
            lastWait = wait;
        }
        if (manager != null && manager.CurrentState == GameState.Playing)
        {
            float t = manager.GameTimer;
            if (Step1Flow.CrossedLastTen(prevTimer, t)) Show(Step1Flow.Banner.LastTen);
            prevTimer = t;
        }
    }

    private void OnGUI()
    {
        if (tuning == null || Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen || Step1PauseMenu.Open) return;
        if (manager != null && manager.CurrentState == GameState.RoundOver) return;
        float w = Step1Gui.Begin(), h = Step1Gui.VirtualHeight;
        var old = GUI.color;
        if (tuning.startCountdown && driver != null)
        {
            string s = Step1Flow.CountdownText(lastWait, goAt > 0f ? Time.unscaledTime - goAt : -1f);
            if (s.Length > 0)
            {
                if (s != lastNum) { lastNum = s; numAt = Time.unscaledTime; }
                float k = Step1Flow.PunchScale(Time.unscaledTime - numAt);
                int size = Mathf.RoundToInt((s.Length > 2 ? 90 : 150) * k);
                var r = new Rect(0f, h * 0.30f - size * 0.6f, w, size * 1.2f);
                GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.Label(new Rect(r.x + 5, r.y + 5, r.width, r.height), $"<b>{s}</b>", Step1Gui.Text(size, TextAnchor.MiddleCenter, false)); // 投影，亮背景上也看得清
                GUI.color = s.Length > 2 ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.92f, 0.4f);
                GUI.Label(r, $"<b>{s}</b>", Step1Gui.Text(size, TextAnchor.MiddleCenter, false));
                GUI.color = old;
            }
            else lastNum = "";
        }
        if (banner != Step1Flow.Banner.None)
        {
            float a = Step1Flow.BannerAlpha(Time.unscaledTime - bannerAt, BannerSeconds);
            if (a <= 0f) { banner = Step1Flow.Banner.None; return; }
            var r = new Rect(w * 0.5f - 560f, 150f, 1120f, 84f);
            GUI.color = new Color(1f, 1f, 1f, a); Step1Gui.Panel(r, 0.8f * a);
            GUI.Label(r, "<b>" + Step1Flow.BannerText(banner) + "</b>", Step1Gui.Text(40, TextAnchor.MiddleCenter, false));
            GUI.color = old;
        }
    }
}
