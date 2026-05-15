using UnityEngine;

/// <summary>
/// Commit 0：Trickster 附身/暴露状态。
///
/// 只描述 Trickster 身份与操控授权，不改物理参数、碰撞体、重力或 MotionState。
/// 状态含义：
///   Roaming    - 未伪装，可正常移动、跳跃、选择伪装。
///   Blending   - 已伪装但尚未完全融入，不能操控机关。
///   Possessing - 已完全融入并锁定至少一个附身点，可切换目标并出手。
///   Underlining - 暗线转移中，临时不可见、不可切换目标、不可出手操控。
///   Revealed   - 出手后短暂暴露，不能继续伪装/操控。
///   Escaping   - 暴露结束后的撤离窗口，不能立刻再次附身。
/// </summary>
public enum TricksterPossessionState
{
    Roaming,
    Blending,
    Possessing,
    Underlining,
    Revealed,
    Escaping
}
