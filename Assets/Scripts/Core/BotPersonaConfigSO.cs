using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// BotPersonaConfigSO — 多维 AI 玩家画像配置
//
// 用途：
//   以 ScriptableObject 形式参数化不同类型的 AI 玩家行为偏好。
//   一个 SO 实例代表一种"人格"，可分别赋给 Mario 或 Trickster。
//   同一 SO 中包含两侧通用参数 + 各自专属参数。
//
// 使用方式：
//   1. 在 Project 面板右键 → Create → MarioTrickster → Bot Persona
//   2. 填写参数后拖拽到 AIArena 面板或通过代码赋值给 InputProvider
//
// [AI防坑警告]
//   - 本阶段仅搭建配置外壳，不修改深层寻路/决策逻辑
//   - 后续迭代中 Brain 方法将读取这些参数来调节行为
// ═══════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "NewBotPersona", menuName = "MarioTrickster/Bot Persona")]
public class BotPersonaConfigSO : ScriptableObject
{
    // ═══════════════════════════════════════════════════════════
    // 通用标识
    // ═══════════════════════════════════════════════════════════

    [Header("Identity")]

    [Tooltip("画像名称，如 Cautious / Speedrunner / Aggressive")]
    public string personaName = "Default";

    // ═══════════════════════════════════════════════════════════
    // Mario 侧参数
    // ═══════════════════════════════════════════════════════════

    [Header("Mario Parameters")]

    [Tooltip("危险反应延迟（秒）。越高反应越慢，模拟不同水平玩家。")]
    [Range(0f, 1.5f)]
    public float reactionDelay = 0.25f;

    [Tooltip("是否愿意顶着预警走高风险主路线。0=极度保守，1=全速冲刺。")]
    [Range(0f, 1f)]
    public float riskTolerance = 0.5f;

    [Tooltip("主动使用扫描的频率。0=从不扫描，1=有机会就扫。")]
    [Range(0f, 1f)]
    public float scanAggression = 0.5f;

    // ═══════════════════════════════════════════════════════════
    // Trickster 侧参数
    // ═══════════════════════════════════════════════════════════

    [Header("Trickster Parameters")]

    [Tooltip("主动出手的积极性。0=被动等待，1=见面就打。")]
    [Range(0f, 1f)]
    public float ambushAggression = 0.5f;

    [Tooltip("为了达成连击而冒高风险的倾向。0=稳妥单杀，1=追求华丽连击。")]
    [Range(0f, 1f)]
    public float comboPreference = 0.5f;
}
