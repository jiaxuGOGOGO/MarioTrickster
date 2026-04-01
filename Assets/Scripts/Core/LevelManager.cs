using UnityEngine;

/// <summary>
/// 关卡管理器 - MVP辅助脚本
/// 功能: 管理关卡中的可交互对象、出生点、关卡边界
/// 使用方式: 挂载到场景中的空GameObject上
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("=== 出生点 ===")]
    [SerializeField] private Transform marioSpawnPoint;
    [SerializeField] private Transform tricksterSpawnPoint;

    [Header("=== 关卡边界 ===")]
    [SerializeField] private float levelMinX = -2f;
    [SerializeField] private float levelMaxX = 50f;
    [SerializeField] private float levelMinY = -10f;
    [SerializeField] private float levelMaxY = 20f;

    [Header("=== 可伪装对象列表 ===")]
    [Tooltip("场景中所有可被Trickster伪装的对象")]
    [SerializeField] private GameObject[] disguisableObjects;

    // 公共属性
    public Transform MarioSpawn => marioSpawnPoint;
    public Transform TricksterSpawn => tricksterSpawnPoint;

    private void Start()
    {
        // 设置相机边界
        CameraController cam = FindObjectOfType<CameraController>();
        if (cam != null)
        {
            cam.SetBounds(levelMinX, levelMaxX, levelMinY, levelMaxY);
        }
    }

    /// <summary>获取关卡中所有可伪装对象的位置</summary>
    public Vector3[] GetDisguisablePositions()
    {
        if (disguisableObjects == null) return new Vector3[0];

        Vector3[] positions = new Vector3[disguisableObjects.Length];
        for (int i = 0; i < disguisableObjects.Length; i++)
        {
            if (disguisableObjects[i] != null)
                positions[i] = disguisableObjects[i].transform.position;
        }
        return positions;
    }

    #region 调试可视化

    private void OnDrawGizmos()
    {
        // 绘制出生点
        if (marioSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(marioSpawnPoint.position, 0.5f);
            Gizmos.DrawIcon(marioSpawnPoint.position, "Mario Spawn", true);
        }

        if (tricksterSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(tricksterSpawnPoint.position, 0.5f);
            Gizmos.DrawIcon(tricksterSpawnPoint.position, "Trickster Spawn", true);
        }

        // 绘制关卡边界
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Vector3 center = new Vector3((levelMinX + levelMaxX) / 2f, (levelMinY + levelMaxY) / 2f, 0);
        Vector3 size = new Vector3(levelMaxX - levelMinX, levelMaxY - levelMinY, 0);
        Gizmos.DrawWireCube(center, size);
    }

    #endregion
}
