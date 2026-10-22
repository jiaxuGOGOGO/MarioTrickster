# S243: art.json (sliced AI sheets) -> Assets/Scripts/Gameplay/Step1/Step1Art.cs (pure data, compiled in sim).
import json
d=json.load(open('/home/user/workspace/art243/art.json'))
from slice import PAL
NEW={k:v for k,v in PAL.items() if k in 'fFde'}
L=[]
w=L.append
w('// 自动生成：tools_s243/gen_art.py（S243 AI 生成美术 → 16×16 字符画）。改图：改下面的字符画，或把同名 PNG 放进 Assets/Resources/Step1Art/<Key>.png 覆盖。')
w('using System.Collections.Generic;')
w('')
w('/// <summary>')
w('/// S243：用户「图示是不是都生成了直观美术？场景、马里奥、捣蛋者能不能更直观」——之前只有 16×16 程序画的图标，角色和地形还是红蓝方块。')
w('/// 这里是 GPT Image 2 生成的原创像素素材（洋红底图 → 切格 → 缩到 16×16 → 吸到项目调色板 → 自动补深色描边），全部过 OverworldArt.Audit。')
w('/// 角色：寻宝人（红帽，马里奥位）5 帧 = 站 / 跑 A / 跑 B / 跳 / 晕；小恶魔（蓝，捣蛋者）5 帧 = 站 / 踮脚 A / 踮脚 B / 跳 / 变身烟。')
w('/// 机关 22 个 + 徽章 8 个替换 Step1Icons 里程序画的旧图（Step1Icons.Pixels 先查这里）；地形 6 块；背景 64×36 低对比（主层黑描边、背景无描边、中间明度）。')
w('/// 参考：主层实心深描边 + 背景低对比无描边 https://www.sandromaglione.com/articles/pixel-art-platformer-level-design-full-guide ；')
w('/// 16×16 角色大头（头约占 45%）、先剪影 https://spritegen.io/guides/how-to-draw-a-pixel-art-character/ ；障碍和背景要拉开明度 https://www.reddit.com/r/gamedev/comments/w5w6xd/ 。')
w('/// S244 自检后：缩图改成先吸调色板再按格投票（颜色不再发灰发糊）；补出口 / 宝物 / 小怪 / 检查点 / 装饰；封路墙改铁闸门、弹簧改高蘑菇弹簧、香蕉皮重画；单向板 5 → 8 像素。')
w('/// 只换外观：碰撞体、判定框、玩法一律不动（H3）。')
w('/// </summary>')
w('public static class Step1Art')
w('{')
w('    public const int Size = 16;')
w('    /// <summary>S243 补的 4 个颜色（皮肤亮 / 皮肤暗 / 深蓝裤子 / 暗灰）——OverworldArt.Palette 没有的才放这里。</summary>')
w('    public static readonly Dictionary<char, float[]> ExtraPalette = new Dictionary<char, float[]>')
w('    {')
for k,v in NEW.items(): w(f"        {{ '{k}', new[] {{ {v[0]}f, {v[1]}f, {v[2]}f }} }},")
w('    };')
def block(name,dic):
    w(f'    public static readonly Dictionary<string, string[]> {name} = new Dictionary<string, string[]>')
    w('    {')
    for k,rows in dic.items():
        w(f'        {{ "{k}", new[] {{')
        for r in rows: w(f'            "{r}",')
        w('        } },')
    w('    };')
block('Icons',d['icons'])
w('    /// <summary>角色帧：Hero = 马里奥位（站 / 跑 A / 跑 B / 跳 / 晕），Imp = 捣蛋者（站 / 踮脚 A / 踮脚 B / 跳 / 变身烟）。</summary>')
frames={}
for i,r in enumerate(d['hero']): frames[f'Hero{i}']=r
for i,r in enumerate(d['imp']): frames[f'Imp{i}']=r
block('Frames',frames)
w('    /// <summary>地形块：GroundTop 地面最上层（有草）、GroundFill 地面里层、Wall 墙、Platform 单向板（16×8，S244 加粗）、StoneTop、MossWall。</summary>')
block('Tiles',d['tiles'])
w('    public static readonly float[][] BackgroundPalette =')
w('    {')
for p in d['bg']['pal']: w(f'        new[] {{ {p[0]}f, {p[1]}f, {p[2]}f }},')
w('    };')
w('    /// <summary>64×36 背景（每个字符 = BackgroundPalette 下标）。第一行在最上面。</summary>')
w('    public static readonly string[] Background =')
w('    {')
for r in d['bg']['rows']: w(f'        "{r}",')
w('    };')
w('''
    public enum Pose { Idle, RunA, RunB, Jump, Stunned }

    /// <summary>纯逻辑：根据状态挑一帧（空中 = 跳；晕 = 晕；跑步两帧交替，S244：跑得越快换得越快）。捣蛋者的"晕"帧是变身烟，只在变身那一下用。</summary>
    public static Pose PoseOf(bool grounded, float speedX, bool stunned, float time)
    {
        if (stunned) return Pose.Stunned;
        if (!grounded) return Pose.Jump;
        if (speedX < 0.15f) return Pose.Idle;
        return ((int)(time * RunFps(speedX))) % 2 == 0 ? Pose.RunA : Pose.RunB;
    }

    /// <summary>S244：跑步换帧速度（每秒几帧）= 4 + 速度 × 1.5，夹在 6–12。慢走 = 慢慢迈腿，冲刺 = 腿快速交替（脚步和移动对得上，不"滑步"）。</summary>
    public static float RunFps(float speedX)
    {
        float f = 4f + (speedX < 0f ? -speedX : speedX) * 1.5f;
        return f < 6f ? 6f : f > 12f ? 12f : f;
    }

    /// <summary>S244：换图后画多大、往上挪多少（相对物体中心，单位 = 格）。出口 = 1.5 格高的门、底边踩在地上；宝物 = 0.9 格、放在地上；
    /// 其余 = 原色块大小（0.6–1 格），底边对齐原色块底边。只动外观，碰撞体 / 触发框不变（H3）。</summary>
    public static float[] PlaceOf(string key, float sx, float sy)
    {
        if (key == "GoalZone") return new[] { 1.5f, 0.25f };
        if (key == "Collectible") return new[] { 0.9f, -0.05f };
        float s = FitScale(sx, sy); return new[] { s, FitLift(sy, s) };
    }

    /// <summary>S244：跑步扬尘间隔（秒）。0 = 不扬尘（站着 / 空中）。</summary>
    public static float DustEvery(bool grounded, float speedX) => grounded && (speedX < 0f ? -speedX : speedX) > 2.5f ? 0.22f : 0f;

    /// <summary>S244：落地扬尘有多大（0 = 不扬）：从空中落下、下落速度越大尘越大（0.4–1.4）。</summary>
    public static float LandDust(bool wasGrounded, bool grounded, float fallSpeed)
    {
        if (wasGrounded || !grounded) return 0f;
        float v = fallSpeed < 0f ? -fallSpeed : fallSpeed;
        if (v < 3f) return 0f;
        float s = v / 12f; return s < 0.4f ? 0.4f : s > 1.4f ? 1.4f : s;
    }

    /// <summary>纯逻辑：小恶魔的帧——第 5 帧是变身烟（只在变身 / 现形那一下），平时站 / 踮脚 / 跳。</summary>
    public static Pose ImpPose(bool poof, bool grounded, float speedX, float time) => poof ? Pose.Stunned : PoseOf(grounded, speedX, false, time);

    public static string FrameKey(bool hero, Pose p) => (hero ? "Hero" : "Imp") + (int)p;

    public static bool TryColor(char c, out float[] col)
    {
        if (OverworldArt.Palette.TryGetValue(c, out col)) return true;
        return ExtraPalette.TryGetValue(c, out col);
    }

    /// <summary>字符画 → RGBA（从左下角开始，和 Texture2D.SetPixels 一致）。宽 = 每行长度，高 = 行数。</summary>
    public static float[] Rgba(string[] rows)
    {
        if (rows == null || rows.Length == 0) return null;
        int h = rows.Length, w = rows[0].Length; var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            char c = rows[h - 1 - y][x]; int i = (y * w + x) * 4;
            if (c == '.' || !TryColor(c, out var col)) continue;
            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;
        }
        return px;
    }

    public static float[] BackgroundRgba()
    {
        int h = Background.Length, w = Background[0].Length; var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            var col = BackgroundPalette[Background[h - 1 - y][x] - '0']; int i = (y * w + x) * 4;
            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;
        }
        return px;
    }

    /// <summary>纯逻辑：白盒是 1 格 × (sx, sy) 的色块；像素图是正方形 → 取大的那边（夹在 0.6–1 格），扁的东西图里本来就画在底部。</summary>
    public static float FitScale(float sx, float sy) => FitScale(sx, sy, 0.6f);
    public static float FitScale(float sx, float sy, float min)
    {
        float m = sx > sy ? sx : sy; if (m < 0f) m = -m;
        return m < min ? min : m > 1f ? 1f : m;
    }

    /// <summary>S244：单向板换图后高 newH（像素图 8px = 0.5 格），原来色块高 oldH——往下挪多少，让板子的上表面不变（脚踩的位置 = 碰撞体顶）。</summary>
    public static float TopKeep(float oldH, float newH) => (oldH - newH) * 0.5f;

    /// <summary>纯逻辑：图往上挪多少，让图的底边 = 原来色块的底边（扁机关贴地，不会飘在半空或陷进地里）。</summary>
    public static float FitLift(float sy, float scale) => (scale - (sy < 0f ? -sy : sy)) * 0.5f;

    /// <summary>纯逻辑：地形块用哪张图（物体名前缀 → 图块）。墙 / 平台 / 地面最上层有草、下面是土。</summary>
    public static string TileFor(string objectName, bool hasAirAbove)
    {
        string n = objectName ?? "";
        if (n.StartsWith("Wall_")) return "Wall";
        if (n.StartsWith("OneWayPlatform_")) return "Platform";
        if (n.StartsWith("Ground_") || n.StartsWith("Platform_")) return hasAirAbove ? "GroundTop" : "GroundFill";
        return null;
    }

    /// <summary>S244：这个物体是不是地形（地面 / 墙 / 平台 / 单向板）——机关换图时跳过它们。</summary>
    public static bool IsTerrain(string objectName)
    {
        string n = objectName ?? "";
        return n.StartsWith("Ground_") || n.StartsWith("Wall_") || n.StartsWith("Platform_") || n.StartsWith("OneWayPlatform_");
    }
}''')
open('/home/user/workspace/repo/Assets/Scripts/Gameplay/Step1/Step1Art.cs','w',encoding='utf-8').write('\n'.join(L)+'\n')
print('ok',len(L))
