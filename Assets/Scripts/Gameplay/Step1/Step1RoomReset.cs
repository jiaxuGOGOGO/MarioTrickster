using UnityEngine;

/// <summary>
/// S181：每回合开始时把房间里所有机关恢复初始状态（火焰、封路墙、崩塌桥）。
/// 修复：按 N 开下一局时 GameManager.ResetRound 只调 ResetUses()，正在喷火的火焰会跳过 OnActiveEnd，
/// tricksterOverride 留在 true → 下一局火焰一直烧。LevelElementRegistry.ResetAll 走每个机关自己的 OnLevelReset。
/// 同时记录生成本场景的构建器版本，菜单"Play Prank Room"发现版本旧了会自动重建场景（用户不用手动重建）。
/// </summary>
public class Step1RoomReset : MonoBehaviour
{
    [SerializeField] private int builtVersion;
    private GameManager subscribed;

    public int BuiltVersion => builtVersion;
    public void SetBuiltVersion(int version) => builtVersion = version;

    private void Start()
    {
        subscribed = GameManager.Instance;
        if (subscribed != null) subscribed.OnRoundStart += ResetRoom;
    }

    private void OnDestroy()
    {
        if (subscribed != null) subscribed.OnRoundStart -= ResetRoom;
    }

    private void ResetRoom()
    {
        // [AI防坑警告] 必须走 OnLevelReset（每个机关自己关掉效果），不能只 ResetUses。
        LevelElementRegistry.ResetAll();
    }
}
