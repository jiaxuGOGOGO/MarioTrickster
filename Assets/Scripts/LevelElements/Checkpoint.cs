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

        // [BugFix] 更新复活点：优先通过 LevelManager 的引用更新，
        // 避免 GameObject.Find 与 Generator 命名不匹配的问题。
        // Fallback: 按 "MarioSpawnPoint" 和 "MarioSpawn" 两种命名查找。
        Transform spawnTransform = null;

        // 方式 1: 通过 LevelManager 获取引用（最可靠，不依赖对象名）
        LevelManager levelManager = Object.FindObjectOfType<LevelManager>();
        if (levelManager != null && levelManager.MarioSpawn != null)
        {
            spawnTransform = levelManager.MarioSpawn;
        }

        // 方式 2: Fallback — 在场景中查找 SpawnPoint 对象（兼容两种命名）
        if (spawnTransform == null)
        {
            GameObject spawnPoint = GameObject.Find("MarioSpawnPoint");
            if (spawnPoint == null)
            {
                // Generator 创建的命名格式: MarioSpawn_x_y
                GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.StartsWith("MarioSpawn"))
                    {
                        spawnPoint = obj;
                        break;
                    }
                }
            }
            if (spawnPoint != null)
                spawnTransform = spawnPoint.transform;
        }

        // 更新位置
        if (spawnTransform != null)
        {
            spawnTransform.position = transform.position;
            Debug.Log($"[Checkpoint] Activated at {transform.position}. Spawn point updated.");
        }
        else
        {
            // 最终 fallback: 创建一个 MarioSpawnPoint
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
