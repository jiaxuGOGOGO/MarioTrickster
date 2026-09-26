using System;
using UnityEngine;

/// <summary>
/// 设计宪法第 1 步规则：捣蛋者 3 条命；被马里奥抓到 = 掉 1 条命并回出生点；3 条命用完 = 马里奥赢。
/// 这是"裁判规则"，不是马里奥的感知：可以读双方真实位置，但马里奥心智只有在"看见本体且贴身"时才会请求抓捕。
/// </summary>
public class TricksterLives : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    [SerializeField] private TricksterController trickster;
    [SerializeField] private Transform respawnPoint;

    private float invulnerable;
    private GameManager subscribedManager;

    public int Lives { get; private set; }
    public int MaxLives => tuning != null ? tuning.startingLives : 3;
    public int TimesCaught { get; private set; }
    public bool IsInvulnerable => invulnerable > 0f;
    public event Action<int> LivesChanged;

    private void Awake()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        Lives = MaxLives;
    }

    private void Start()
    {
        if (trickster == null) trickster = FindObjectOfType<TricksterController>();
        if (respawnPoint == null)
        {
            var level = FindObjectOfType<LevelManager>();
            if (level != null) respawnPoint = level.TricksterSpawn;
        }
        subscribedManager = GameManager.Instance;
        if (subscribedManager != null) subscribedManager.OnRoundStart += ResetLives;
        ResetLives();
    }

    private void OnDestroy()
    {
        if (subscribedManager != null) subscribedManager.OnRoundStart -= ResetLives;
    }

    private void Update()
    {
        if (invulnerable > 0f) invulnerable -= Time.deltaTime;
    }

    public void Configure(MarioMindTuningSO t, TricksterController figure, Transform spawn)
    {
        tuning = t; trickster = figure; respawnPoint = spawn; Lives = MaxLives;
    }

    public void ResetLives()
    {
        Lives = MaxLives; TimesCaught = 0; invulnerable = 0f;
        LivesChanged?.Invoke(Lives);
    }

    /// <summary>裁判：马里奥在 catchRadius 内且捣蛋者不在无敌期 → 抓到。</summary>
    public bool TryCatch(Vector2 marioPosition)
    {
        if (trickster == null || invulnerable > 0f || Lives <= 0) return false;
        var gm = GameManager.Instance;
        if (gm != null && gm.CurrentState != GameState.Playing) return false;
        if (Vector2.Distance(marioPosition, trickster.transform.position) > tuning.catchRadius) return false;

        Lives--; TimesCaught++;
        LivesChanged?.Invoke(Lives);
        if (Lives <= 0)
        {
            if (gm != null) gm.EndRound("Mario", $"Trickster caught {MaxLives} times.");
            return true;
        }
        if (respawnPoint != null) trickster.transform.position = respawnPoint.position;
        // 被抓 = 现形 + 回出生点（ResetForNewRound 会解除伪装）；编辑器测试中控制器未初始化，跳过。
        if (Application.isPlaying) trickster.ResetForNewRound();
        invulnerable = tuning.respawnInvulnerableSeconds;
        return true;
    }
}
