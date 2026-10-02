using UnityEngine;

/// <summary>
/// S224（总方案阶段 C：把已有的数字画出来）——纯逻辑，沙盒可测。只决定"画多大、画到哪"，不改任何规则（H4：不新增感知通道）。
///  · 视锥灌注（Shadow Tactics：黄色从眼睛往你这边灌，灌到你 = 被发现）：灌到的长度 = 视野 × 起疑进度，永远不超过墙挡住的地方。
///  · 声音圈（Mark of the Ninja：声音 = 画面上的圆，静音也能读，H6）：圈的大小 = 他真实的听力范围（和 MarioEyes / OverworldTown 用同一个数）。
/// </summary>
public static class Step1Readability
{
    /// <summary>视锥里灌注的长度（格）：0 = 平静，满 = 灌到被墙挡住的地方。normalized = SuspicionMeter.Normalized（0–1）。</summary>
    public static float FillReach(float normalized, float clearDistance, float range)
        => Mathf.Min(Mathf.Max(0f, clearDistance), Mathf.Max(0f, range) * Mathf.Clamp01(normalized));

    /// <summary>灌注的颜色（H6：黄 = 起疑，红 = 认出你；和头顶 ? / ! 同色）。</summary>
    public static Color FillColor(SuspicionLevel level) =>
        level == SuspicionLevel.Alert ? new Color(1f, 0.25f, 0.2f, 0.42f) : new Color(1f, 0.85f, 0.2f, 0.32f);

    /// <summary>房间里会被马里奥听见的声音（只有位置，H4）。</summary>
    public enum Sound { Taunt, Bomb, WallSmash, Vent, ChainClick }

    /// <summary>房间声音圈半径 = MarioEyes 的听力判定：NoteNoise / NoteTaunt 用 hearingRange，NoteNoiseNear（通风管咣当、连锁咔哒）用 1/3。</summary>
    public static float SoundRadius(Sound s, MarioMindTuningSO t) =>
        s == Sound.Vent || s == Sound.ChainClick ? t.hearingRange / 3f : t.hearingRange;

    /// <summary>小镇：挑衅多远听得见（OverworldTown 判定直接用这个函数 → 圈和规则永远一致）。</summary>
    public static float TownTauntRadius(MarioMindTuningSO t) => t.overworldVisionRange * 1.5f;

    /// <summary>小镇：大机关的动静多远听得见。</summary>
    public static float TownNoiseRadius(MarioMindTuningSO t) => t.overworldNoiseRange;

    public static readonly Color SoundColor = new Color(0.5f, 0.85f, 1f, 0.85f);
}
