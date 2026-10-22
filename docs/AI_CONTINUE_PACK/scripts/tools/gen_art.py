# S220：生成 Assets/Scripts/Overworld/OverworldArt.cs（16×16 像素图标，纯数据；Unity / 编辑器 / 网页共用）
PAL = {'k':(0.10,0.09,0.12),'w':(1,1,1),'r':(0.90,0.20,0.22),'R':(0.60,0.10,0.12),'y':(1.0,0.86,0.25),'Y':(0.85,0.60,0.12),
       'b':(0.30,0.60,0.95),'B':(0.16,0.34,0.70),'g':(0.40,0.75,0.35),'G':(0.20,0.45,0.20),'n':(0.62,0.42,0.22),'N':(0.40,0.26,0.14),
       's':(0.70,0.69,0.66),'S':(0.42,0.41,0.40),'p':(0.72,0.45,1.0),'P':(0.45,0.22,0.70),'c':(0.55,0.90,1.0),'o':(0.95,0.60,0.18),'x':(0.05,0.04,0.04)}
def G(rows):
    rows=[r.ljust(16,'.')[:16] for r in rows]
    while len(rows)<16: rows=(['.'*16] if len(rows)%2 else [])+rows+([] if len(rows)%2 else ['.'*16])
    return rows[:16]
def blank(): return [list('.'*16) for _ in range(16)]
def box(fill, edge='k', y0=1, y1=14, x0=1, x1=14):
    g=blank()
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): g[y][x]=edge if y in (y0,y1) or x in (x0,x1) else fill
    return g
def over(g, glyph, ox, oy):
    for j,row in enumerate(glyph):
        for i,ch in enumerate(row):
            if ch!='.': g[oy+j][ox+i]=ch
    return g
S=lambda g:[''.join(r) for r in g]
I={}
I['Heart']=G(["","..kkk.....kkk...",".krrrk...krrrk..","krrwrrk.krrrrrk.","krwrrrrkrrrrrrk.","krrrrrrrrrrrrrk.","krrrrrrrrrrrrrk.",".krrrrrrrrrrrk..","..krrrrrrrrrk...","...krrrrrrrk....","....krrrrrk.....",".....krrrk......","......krk.......",".......k........"])
hb=[list(r) for r in I['Heart']]
for (x,y) in [(7,3),(7,4),(8,5),(8,6),(7,7),(6,8),(7,9),(8,10),(7,11)]: hb[y][x]='k'
I['HurtBadge']=S(hb)
I['Energy']=G(["",".......k",  "......kpk",".....kpwpk","....kppwppk","...kpppwpppk","..kppppppppppk",".kpppppppppppppk","..kPPPPPPPPPPk","...kPPPPPPPPk","....kPPPPPPk",".....kPPPPk","......kPPk",".......kk"])
I['Bolt']=G(["",".........kkkk","........kyyk",".......kyyk","......kyyk",".....kyykkkk","....kyyyyyyk","....kkkkyyk",".......kyyk","......kyyk",".....kyyk",".....kyk","....kyk","....kk"])
I['Cloud']=G(["",".....kkkk.......","....kssssk.kkk..","..kkssswsskssk..",".ksssssssssssk..","kssssssssssssssk","kSSSSSSSSSSSSSSk",".kSSSSSSSSSSSSk.","..kkkkkkkkkkkk..","....kyk...kyk...","...kyk...kyk....","..kyk...kyk.....","..kk....kk......"])
I['GiantCannon']=G(["",".........kkkkkk.",".......kkSSSSSSk",".....kkSSSSSSSwk","....kSSSSSSSSSSk","...kSSSSSSSSkkk.","..kSSSSSSSSk....","..kSSSSSSSk.....",".knnnkSSkknnk...","knNNNnkknNNNnk..","knNkNnk.knNkNnk.","knNNNnk.knNNNnk.",".knnnk...knnnk..","..kkk.....kkk..."])
I['CannonTarget']=G([".....kkkkkk.....","...kkrrrrrrkk...","..krrwwwwwwrrk..",".krrwrrrrrrwrrk.",".krwrrkkkkrrwrk.","krrwrkwwwwkrwrrk","krwrrkwrrwkrrwrk","krwrrkwrrwkrrwrk","krrwrkwwwwkrwrrk",".krwrrkkkkrrwrk.",".krrwrrrrrrwrrk.","..krrwwwwwwrrk..","...kkrrrrrrkk...",".....kkkkkk....."])
I['Boulder']=G(["",".....kkkkk......","...kksssssk.....","..ksssswsssk....",".ksssswwssssk...",".kssssssssSSsk..","kssSssssssSsssk.","ksSSssssssssssk.","kssssssSSsssssk.","kssssssSssssSsk.",".kSsssssssssSSk.",".kSSSsssssSSSk..","..kkSSSSSSSkk...","....kkkkkkk....."])
I['WaterTower']=G(["....kkkkkkkk....","...kbbbbbbbbk...","..kbbwbbbbbbbk..","..kbwbbbbbbbbk..","..kbbbbbbbbbbk..","..kBBBBBBBBBBk..","..kkkkkkkkkkkk..","...kn......nk...","...kn.k..k.nk...","...knk....knk...","...kn.k..k.nk...","...knk....knk...","...kn......nk...","..kNk......kNk..","..kk........kk.."])
I['Lamp']=G([".....kkkkk......","....kyyyyyk.....","....kywwyyk.....","....kyyyyyk.....",".....kkkkk......","......kSk.......","......kSk.......","......kSk.......","......kSk.......","......kSk.......","......kSk.......","......kSk.......","....kkSSSkk.....","...kSSSSSSSk....","...kkkkkkkkk...."])
I['BananaPeel']=G(["","..........kk....",".........kyk....","........kyYk....",".......kyyk.....","..kk..kyyk......",".kyyk.kyyk......","kyyyykyyyk......","kYyyyyyyyyk.....",".kYyyyyyyyyk....","..kYYyyyyyyyk...","...kkYYYYyyyyk..",".....kkkkYYYYk..",".........kkkk..."])
q=[".yyyy.","yykkyy","....yy","...yy.","..yy..","..yy..","......","..yy.."]
I['PickupBox']=S(over(box('o'),q,5,4))
cr=box('n','k',2,14,1,14)
for t in range(2,15):
    cr[t][1+((t-2)*13)//12 if False else 0]=cr[t][0]
for t in range(1,13): cr[2+t][1+t]='N'; cr[2+t][14-t]='N'
for x in range(1,15): cr[2][x]='k'; cr[14][x]='k'
I['Crate']=S(cr)
I['Cave']=G(["",".....kkkkkk.....","...kkSSSSSSkk...","..kSSSSSSSSSSk..",".kSSSkkkkkkSSSk.",".kSSkxxxxxxkSSk.","kSSkxxxxxxxxkSSk","kSSkxxxxxxxxkSSk","kSkxxxxxxxxxxkSk","kSkxxxxxxxxxxkSk","kSkxxxxxxxxxxkSk","kSkxxxxxxxxxxkSk","kkkkkkkkkkkkkkkk"])
I['MarioHome']=G([".......kk.......","......krrk......",".....krrrrk.....","....krrrrrrk....","...krrrrrrrrk...","..krrrrrrrrrrk..",".kkkkkkkkkkkkkk.","..kwwwwwwwwwwk..","..kwbbwwwwnnwk..","..kwbbwwwwnnwk..","..kwwwwwwwnnwk..","..kwwwwwwwnnwk..","..kkkkkkkkkkkk.."])
I['TricksterSpawn']=G(["...kk...........","...kbk..........","...kbbkk........","...kbbbbkk......","...kbbbbbbkk....","...kbbbbbbbbk...","...kbbbbbbkk....","...kbbbbkk......","...kbbkk........","...kSk..........","...kSk..........","...kSk..........","..kSSSk.........",".kkkkkkk........"])
I['StunBadge']=G([".......k........","......kyk.......","......kyk.......",".....kyyyk......","kkkkkyyyyykkkkk.",".kyyyyywyyyyyk..","..kyyyyyyyyyk...","...kyyyyyyyk....","...kyyyyyyyk....","..kyyykkkyyyk...","..kyyk...kyyk...",".kyk.......kyk..",".kk.........kk.."])
I['SlowBadge']=G([".......k........","......kbk.......","......kbk.......",".....kbbbk......","....kbbbbbk.....","...kbbwbbbbk....","..kbbwbbbbbbk...","..kbwbbbbbbbk...","..kbbbbbbbbbk...","..kbbbbbbbbbk...","...kBbbbbbBk....","....kBBBBBk.....",".....kkkkk......"])
TILE={'K':'GiantCannon','X':'CannonTarget','O':'Boulder','U':'WaterTower','i':'Lamp','n':'BananaPeel','?':'PickupBox','c':'Crate','h':'Cave','M':'MarioHome','T':'TricksterSpawn','+':'Heart','*':'Energy'}
for k,v in I.items():
    assert len(v)==16 and all(len(r)==16 for r in v),k
    for r in v:
        for ch in r: assert ch=='.' or ch in PAL,(k,ch)
out=['// 自动生成：tools_s220/gen_art.py（S220）。改图标：直接改下面的 16×16 字符画，或者用导出的 PNG 模板画好放进 Assets/Resources/OverworldArt/<Key>.png 覆盖。',
'using System.Collections.Generic;','',
'/// <summary>',
'/// S220：小镇格子 / 特效的 16×16 像素图标（纯数据，Unity 游戏、大地图工坊、网页设计台共用一份）。',
'/// 规则：图标只是"看得懂"的占位美术——轮廓一眼认出是什么东西（巨炮像炮、滚石像石头、补心像心），颜色沿用格子表；伤害角标形状 + 颜色双编码（红碎心 = 掉心、黄星 = 晕、蓝水滴 = 变慢），色弱也分得清。',
'/// 换美术：Unity 菜单 MarioTrickster/Overworld/导出像素图标模板 → 改 PNG → 放进 Assets/Resources/OverworldArt/（同名覆盖，自动设成像素风导入）。',
'/// 参考：Unity "placeholder asset problem"（占位美术要可替换、同名同尺寸）https://unity.com/blog/placeholder-asset-problem ；色弱友好的形状 + 颜色双编码 https://colorblindgames.com/2023/04/19/universal-colorblind-code/',
'/// </summary>',
'public static class OverworldArt','{','    public const int Size = 16;','',
'    /// <summary>调色板：字符 → 颜色（. = 透明）。</summary>',
'    public static readonly Dictionary<char, float[]> Palette = new Dictionary<char, float[]>','    {']
for ch,(r,g,b) in PAL.items(): out.append(f"        {{ '{ch}', new[] {{ {r}f, {g}f, {b}f }} }},")
out+=['    };','','    /// <summary>格子字符 → 图标名（没有的格子只用颜色画）。</summary>','    public static readonly Dictionary<char, string> TileIcon = new Dictionary<char, string>','    {']
for ch,k in TILE.items(): out.append(f"        {{ '{ch}', \"{k}\" }},")
out+=['    };','','    /// <summary>图标：从上到下 16 行，每行 16 个字符。</summary>','    public static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>','    {']
for k,v in I.items():
    out.append(f'        {{ "{k}", new[] {{'); out+= [f'            "{r}",' for r in v]; out.append('        } },')
out+=['    };','',
'    public static string IconOf(char tile) => TileIcon.TryGetValue(tile, out var k) ? k : null;','',
'    /// <summary>RGBA 像素（从左下角开始，和 Unity Texture2D.SetPixels 顺序一致）。没有这个图标 = null。</summary>',
'    public static float[] Pixels(string key)','    {',
'        if (key == null || !Icons.TryGetValue(key, out var rows)) return null;',
'        var px = new float[Size * Size * 4];',
'        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)','        {',
'            char c = rows[Size - 1 - y][x]; int i = (y * Size + x) * 4;',
'            if (c == \'.\' || !Palette.TryGetValue(c, out var col)) continue;',
'            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;','        }',
'        return px;','    }','}','']
open('/home/user/workspace/repo/Assets/Scripts/Overworld/OverworldArt.cs','w',encoding='utf-8').write('\n'.join(out))
# 预览图（给自己检查）
from PIL import Image
keys=list(I); sc=6; im=Image.new('RGBA',(len(keys)*17*sc, 17*sc),(60,90,60,255))
for n,k in enumerate(keys):
    for y,r in enumerate(I[k]):
        for x,ch in enumerate(r):
            if ch=='.': continue
            c=tuple(int(v*255) for v in PAL[ch])+(255,)
            for dy in range(sc):
                for dx in range(sc): im.putpixel((n*17*sc+x*sc+dx, y*sc+dy), c)
im.save('/home/user/workspace/tools_s220/art_preview.png'); print(len(keys),'icons')
