using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// S237：调参文件 RushMarioTuning 有 250 个数值，散在 36 个按 Session 起名的小标题里（S193 / S198 / S200…）——
/// 用户找一个数要从头翻到尾，改了哪些也看不出来。这里把它们按「你想调什么」重新分成 9 组 + 一组「常用 15 个」。
/// 只是换一种看法：不改任何字段名、默认值、数据版本（旧资产原样能读，sim 检查每个字段都恰好在一组里）。
/// Inspector（Editor/MarioMindTuningSOEditor）和网页「数值关系」都可以按这个分组显示。
/// </summary>
public static class TuningGroups
{
    public sealed class Group { public string title, why; public string[] headers; }

    /// <summary>每组 = 原来的几个小标题（Header）。新加字段放进已有的 Header 里，或者在这里给新 Header 找一个组。</summary>
    public static readonly Group[] All =
    {
        new Group { title = "马里奥：走路 · 眼睛 · 起疑", why = "他多快、看多远、多容易起疑（? → !）、追多久放弃",
            headers = new[] { "Identity", "Movement persona (feeds the existing HeuristicBot)", "Vision (H4: cone + range + occlusion only)", "Suspicion sources", "Thresholds (H2: '?' must show before '!')", "Behaviour", "Randomness for emergence (S187)" } },
        new Group { title = "马里奥：性格 · 学习 · 反制", why = "三种性格、记住被坑的地方、停时间、躲炸弹、踢门、钻炮",
            headers = new[] { "S203: Mario personalities (Rush / Cautious / Greedy)", "Mario time stop (S197)" } },
        new Group { title = "你的技能：炸弹 · 缩小 · 通风管 · 诱饵 · 挑衅 · 连锁 · 能量", why = "每局几次、冷却多久、炸多大；能量和马里奥的 Q 扫描也在这",
            headers = new[] { "Trickster kit (S197)", "S200: chain plan (F), taunt (T), tripwire, Mario learning", "S238: 扫描 · 能量 · 附身（以前在第二个调参文件 GameplayLoopConfig）", "S241: 机关预约 · 光影 · 遁地 · 蛛丝 · 图标", "S242: 伪装装备栏 · 道具诱饵 · 一目了然 · 黑匣子" } },
        new Group { title = "机关：火 · 墙 · 弹簧 · 香蕉皮 · 油桶 · 铁笼 · 绳套 · 大炮 · 地形", why = "每种机关预警多久、晕多久、范围多大",
            headers = new[] { "Prank props (S183)", "New pranks (S193)", "S198: bombs hurt, cannon aim, snare, pickups", "S199: oil barrel, cage, decoy, alarm, door kick", "Movement-limiting terrain (S197)", "Cannon (S187)", "S240: 坐进大炮 · 用过的机关 · 连击看得懂 · 卡住记录", "Collapse bridge (S183)", "Hakoniwa (S196: Souls-style interconnected floors)" } },
        new Group { title = "一局的规则：命 · 时间 · 连招 · 回放", why = "几条命、一局多长、连招窗口、顿帧和震屏、慢动作回放",
            headers = new[] { "Rules", "Smoothness & combos (S185)", "Combo feel (S193: fighting-game rules)", "S202: replay, strategy sim, trap probe, Mario dodge/grab", "S228: 按调研定的数值 + 钟楼 + 房间大炮轰出窗户" } },
        new Group { title = "手感：被弹飞 · 受伤 · 特效", why = "抛物线重力、受伤小跳、红白闪、冲击环",
            headers = new[] { "S216: 手感 / 被弹飞的抛物线 / 特效" } },
        new Group { title = "镜头 · 屏幕 · 好不好读", why = "镜头怎么跟、小地图、视锥灌黄、声音圈、按键条、游戏速度",
            headers = new[] { "Camera", "Big rooms (S207: Dead Cells-style follow camera)", "Playtest screen (S182)", "S224: 少等待 + 可读性（总方案阶段 C）", "Theme (S187)", "S243: 美术皮肤（AI 生成的像素角色 · 机关 · 地形 · 背景）", "S244: 动作画面 · 节奏 · 暂停 · 存档" } },
        new Group { title = "小镇大地图", why = "走路速度、一天多长、视野、大机关、天气、心和雷区",
            headers = new[] { "Overworld (S210: Stardew-style top-down town)", "S218 小镇大机关 / 天气 / 小镇↔房间联动", "S220: 小镇的心 / 雷区 / 雷云" } },
        new Group { title = "自动检查 · 防卡死（一般不用动）", why = "马里奥自己跑几局、卡住多久救出来",
            headers = new[] { "Playtest tools (S181)", "Anti-stuck (S189, H9)" } },
    };

    /// <summary>「常用」——试玩反馈里最常想改的 15 个，Inspector 最上面单独列出。</summary>
    public static readonly string[] Common =
    {
        "marioSpeedScale", "chaseSpeedScale", "visionRange", "startDelaySeconds", "hurtStunSeconds",
        "startingLives", "roundTimeLimit", "bombsPerRound", "decoysPerRound", "tauntUses",
        "timeStopUsesPerRound", "marioPersonality", "roomGameSpeed", "overworldMarioSpeed", "overworldMinutesPerSecond",
    };

    /// <summary>字段 → 它原来的小标题（按源码顺序，没写 Header 的跟着上一个）。</summary>
    public static List<(FieldInfo field, string header)> Fields(Type t)
    {
        var o = new List<(FieldInfo, string)>(); string h = "";
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken))
        {
            bool hidden = f.GetCustomAttributes(true).Any(a => a.GetType().Name == "HideInInspectorAttribute");
            foreach (var a in f.GetCustomAttributes(true)) { var p = a.GetType().GetField("header"); if (a.GetType().Name == "HeaderAttribute" && p != null) h = (string)p.GetValue(a) ?? h; }
            if (f.IsPublic && !hidden) o.Add((f, h));   // S238：旧字段（HideInInspector、只为换算）不算
        }
        return o;
    }

    public static Group GroupOf(string header) => All.FirstOrDefault(g => g.headers.Contains(header));

    /// <summary>某个字段的说明（Tooltip），没有就空。</summary>
    public static string Tip(FieldInfo f)
    {
        foreach (var a in f.GetCustomAttributes(true)) if (a.GetType().Name == "TooltipAttribute") { var p = a.GetType().GetField("tooltip"); if (p != null) return (string)p.GetValue(a) ?? ""; }
        return "";
    }
}
