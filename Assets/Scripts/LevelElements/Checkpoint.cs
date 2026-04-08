using UnityEngine;

/// <summary>
/// 检查点 — 降低长关卡挫败感的复活点 (S56)
///
/// 设计参考：
///   - Celeste: 每个房间入口自动存档，死亡后从最近检查点复活
///   - Shovel Knight: 可破坏的检查点（本实现为不可破坏版本）
///
/// ASCII 字符: 'S'
/// 行为: 玩家触碰后激活，死亡后从最近的已激活检查点复活
///        激活时视觉反馈（颜色变化）
///        继承 LevelElementBase 获得自动注册
///
/// 与 GameManager/LevelManager 的集成：
///   激活时通过 LevelManager.SetCheckpoint 更新复活点位置。
///   如果 LevelManager 尚未支持 SetCheckpoint，
///   则直接更新 MarioSpawnPoint 的位置作为临时方案。
/// </summary>
public class Checkpoint : LevelElementBase
{
    [Header("=== 检查点参数 ===")]
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color activeColor = new Color(0f, 1f, 0.5f);

    private bool isActivated;
    private SpriteRenderer spriteRenderer;

    protected override void OnEnable()
    {
        elementName = "Checkpoint";
        category = ElementCategory.Checkpoint;
        tags = ElementTag.Interactive;
        base.OnEnable();
    }

    private void Awake()
    {
        // 视碰分离：查找 Visual 子节点的 SpriteRenderer
        Transform vis = transform.Find("Visual");
        if (vis != null)
            spriteRenderer = vis.GetComponent<SpriteRenderer>();
        else
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.color = inactiveColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isActivated) return;

        // 只有 Mario 可以激活检查点
        MarioController mario = other.GetComponent<MarioController>();
        if (mario == null) return;

        Activate();
    }

    private void Activate()
    {
        isActivated = true;

        // 视觉反馈
        if (spriteRenderer != null)
            spriteRenderer.color = activeColor;

        // 更新复活点：查找 MarioSpawnPoint 并移动到检查点位置
        GameObject spawnPoint = GameObject.Find("MarioSpawnPoint");
        if (spawnPoint != null)
        {
            spawnPoint.transform.position = transform.position;
            Debug.Log($"[Checkpoint] Activated at {transform.position}. Spawn point updated.");
        }
        else
        {
            // 如果没有 MarioSpawnPoint，创建一个
            GameObject newSpawn = new GameObject("MarioSpawnPoint");
            newSpawn.transform.position = transform.position;
            Debug.Log($"[Checkpoint] Activated at {transform.position}. New spawn point created.");
        }
    }

    public override void OnLevelReset()
    {
        isActivated = false;
        if (spriteRenderer != null)
            spriteRenderer.color = inactiveColor;
    }
}
