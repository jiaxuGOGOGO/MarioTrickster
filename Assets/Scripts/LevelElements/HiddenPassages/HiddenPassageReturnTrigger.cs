using UnityEngine;

/// <summary>
/// 隐藏通道返回触发区 - 在出口位置动态创建，允许玩家传回入口
/// 
/// Session 19 新增:
///   当 HiddenPassage 的 exitPoint 不是另一个 HiddenPassage 时，
///   在出口位置动态创建此触发区，使玩家可以按 S 键传回入口。
///   
///   此脚本由 HiddenPassage.CreateReturnTrigger() 自动创建和初始化，
///   不需要手动挂载。
/// 
/// 扩展/删除指南: 删除此文件需同时修改 HiddenPassage.cs 中的 CreateReturnTrigger()
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class HiddenPassageReturnTrigger : MonoBehaviour
{
    private HiddenPassage ownerPassage;
    private GameObject playerInZone;

    /// <summary>获取拥有此返回触发区的 HiddenPassage（供 MarioInteractionHelper 使用）</summary>
    public HiddenPassage OwnerPassage => ownerPassage;

    /// <summary>由 HiddenPassage 调用初始化</summary>
    public void Initialize(HiddenPassage owner)
    {
        ownerPassage = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ownerPassage == null) return;
        if (other.GetComponent<MarioController>() == null) return;

        playerInZone = other.gameObject;
        ownerPassage.OnPlayerEnterReturnZone(other.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (ownerPassage == null) return;
        if (other.gameObject != playerInZone) return;

        ownerPassage.OnPlayerExitReturnZone(other.gameObject);
        playerInZone = null;
    }
}
