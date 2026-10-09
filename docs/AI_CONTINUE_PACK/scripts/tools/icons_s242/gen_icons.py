# S242 (from S241): 16x16 room-prop icons + glance badges. Shapes drawn with primitives, then a dark outline is added automatically.
import math
N=16
def blank(): return [['.']*N for _ in range(N)]
def px(g,x,y,c):
    if 0<=x<N and 0<=y<N: g[y][x]=c
def rect(g,x0,y0,x1,y1,c):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): px(g,x,y,c)
def circ(g,cx,cy,r,c):
    for y in range(N):
        for x in range(N):
            if (x-cx)**2+(y-cy)**2<=r*r: px(g,x,y,c)
def poly(g,pts,c):
    for y in range(N):
        for x in range(N):
            X,Y=x+0.5,y+0.5; inside=False; j=len(pts)-1
            for i in range(len(pts)):
                xi,yi=pts[i]; xj,yj=pts[j]
                if (yi>Y)!=(yj>Y) and X<(xj-xi)*(Y-yi)/(yj-yi+1e-9)+xi: inside=not inside
                j=i
            if inside: px(g,x,y,c)
def line(g,x0,y0,x1,y1,c):
    n=max(abs(x1-x0),abs(y1-y0))*2+1
    for i in range(n+1):
        t=i/n; px(g,round(x0+(x1-x0)*t),round(y0+(y1-y0)*t),c)
def outline(g):
    o=[r[:] for r in g]
    for y in range(N):
        for x in range(N):
            if g[y][x]!='.': continue
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                X,Y=x+dx,y+dy
                if 0<=X<N and 0<=Y<N and g[Y][X] not in '.k': o[y][x]='k'; break
    # edge pixels at the canvas border must be dark too
    for y in range(N):
        for x in range(N):
            if o[y][x] not in '.k' and (x in (0,N-1) or y in (0,N-1)): o[y][x]='k'
    return o
icons={}
def fire():
    g=blank(); poly(g,[(8,1),(13,8),(12.5,13),(8,15),(3.5,13),(3,8),(5,5),(6,8)],'r')
    poly(g,[(8,6),(11,10),(10.5,13),(8,14),(5.5,13),(5,10)],'o'); poly(g,[(8,10),(9.5,12),(8,13.5),(6.5,12)],'y'); return g
def blocker():
    g=blank(); rect(g,1,4,14,9,'w')
    for x0 in (1,5,9,13): poly(g,[(x0,9),(x0+2,9),(x0+5,4),(x0+3,4)],'r')
    rect(g,3,10,4,14,'S'); rect(g,11,10,12,14,'S'); rect(g,1,14,14,14,'S'); return g
def bridge():
    g=blank(); rect(g,1,3,5,5,'n'); rect(g,10,3,14,5,'n'); rect(g,1,5,5,5,'N'); rect(g,10,5,14,5,'N')
    poly(g,[(6,6),(9,7),(8.5,9),(5.5,8)],'n'); rect(g,7,10,8,12,'w'); poly(g,[(5.5,12),(9.5,12),(7.5,15)],'w'); return g
def spring():
    g=blank(); rect(g,2,12,13,14,'G'); rect(g,3,2,12,4,'g')
    for i,y in enumerate(range(5,12)): x=5 if i%2==0 else 9; rect(g,x,y,x+2,y,'s')
    poly(g,[(8,0.2),(10,2),(6,2)],'w'); return g
def crackfloor():
    g=blank(); rect(g,1,6,14,13,'n'); rect(g,1,6,14,7,'N')
    line(g,4,7,7,10,'k'); line(g,7,10,6,13,'k'); line(g,7,10,11,9,'k'); line(g,11,9,13,12,'k'); return g
def cage():
    g=blank(); rect(g,2,3,13,4,'S'); rect(g,2,14,13,14,'S')
    for x in (3,6,9,12): rect(g,x,5,x,13,'s')
    rect(g,7,1,8,2,'S'); return g
def snare():
    g=blank(); circ(g,8,10,4.6,'n'); circ(g,8,10,2.6,'.'); rect(g,7,1,8,6,'n'); return g
def barrel():
    g=blank(); poly(g,[(4,2),(12,2),(13,8),(12,14),(4,14),(3,8)],'r')
    rect(g,3,5,13,5,'R'); rect(g,3,11,13,11,'R'); poly(g,[(8,6.5),(10,9),(8,10.5),(6,9)],'y'); return g
def tripwire():
    g=blank(); rect(g,2,4,3,14,'n'); rect(g,12,4,13,14,'n'); line(g,3,10,12,10,'w'); rect(g,6,9,9,9,'w'); rect(g,1,14,14,14,'N'); return g
def vent():
    g=blank(); rect(g,2,2,13,13,'S'); rect(g,3,3,12,12,'k')
    for y in (4,7,10): rect(g,3,y,12,y+1,'s')
    poly(g,[(6,14),(10,14),(8,15.9)],'c'); return g
def crackwall():
    g=blank(); rect(g,3,2,12,13,'S')
    for y in (5,9): rect(g,3,y,12,y,'s')
    for (x,y) in ((7,2),(5,6),(10,6),(7,10)): rect(g,x,y,x,y+2,'s')
    line(g,6,3,9,7,'k'); line(g,9,7,7,10,'k'); line(g,7,10,10,13,'k'); return g
def bush():
    g=blank(); circ(g,5,10,3.8,'G'); circ(g,11,10,3.8,'G'); circ(g,8,7,4.3,'g'); circ(g,7,6,1.3,'y'); circ(g,10,9,1,'y'); rect(g,2,13,13,14,'G'); return g
def door():
    g=blank(); rect(g,3,1,12,14,'n'); rect(g,4,2,11,13,'N'); rect(g,9,7,10,8,'y')
    poly(g,[(0.5,8),(4,5),(4,11)],'y'); return g
def poison():
    g=blank(); poly(g,[(1,10),(4,8),(12,8),(15,10),(13,14),(3,14)],'p'); circ(g,6,10,1,'w'); circ(g,10,11,1.2,'w'); circ(g,8,5,1.5,'p'); circ(g,11,3,1,'p'); return g
def glue():
    g=blank(); poly(g,[(1,9),(4,7),(12,7),(15,9),(13,13),(3,13)],'y'); rect(g,5,13,6,15,'Y'); rect(g,10,13,10,14,'Y'); circ(g,7,9,1,'w'); return g
def lamp():
    g=blank(); poly(g,[(4,2),(12,2),(10,7),(6,7)],'y'); rect(g,7,8,8,13,'S'); rect(g,4,14,11,14,'S'); rect(g,6,3,9,4,'w'); return g
def spike():
    g=blank()
    for x0 in (1,6,11): poly(g,[(x0,13),(x0+2,3),(x0+4,13)],'s')
    rect(g,1,13,14,14,'S'); return g
def cannon():
    g=blank(); poly(g,[(3,6),(14,3),(15,8),(4,11)],'S'); circ(g,5,11,3.2,'N'); circ(g,5,11,1.2,'n'); rect(g,1,14,10,14,'N'); return g
def pickup():
    g=blank(); rect(g,2,2,13,13,'y'); rect(g,3,3,12,12,'Y'); 
    for (x,y) in ((6,4),(7,4),(8,4),(9,4),(10,5),(10,6),(9,7),(8,8),(8,9),(8,11),(8,12)): px(g,x,y,'w')
    return g
def crate():
    g=blank(); rect(g,2,2,13,13,'n'); rect(g,2,2,13,3,'N'); rect(g,2,12,13,13,'N'); rect(g,2,2,3,13,'N'); rect(g,12,2,13,13,'N'); line(g,4,4,11,11,'N'); return g
def grass():
    g=blank(); rect(g,0,8,15,15,'n'); rect(g,0,6,15,9,'g')
    for x in range(0,16,3): rect(g,x,4,x,6,'G')
    return g
for k,f in [("FireTrap",fire),("ControllableBlocker",blocker),("CollapsingPlatform",bridge),("SpringPad",spring),("CrackFloor",crackfloor),
            ("IronCage",cage),("SnareTrap",snare),("OilBarrel",barrel),("Tripwire",tripwire),("Vent",vent),("CrackedWall",crackwall),("Bush",bush),
            ("OneWayDoor",door),("PoisonPool",poison),("Glue",glue),("RoomLamp",lamp),("SpikeTrap",spike),("Cannon",cannon),("PickupSpot",pickup),
            ("Crate",crate),("GrassGround",grass)]:
    icons[k]=[''.join(r) for r in outline(f())]

# S242 glance badges (Mario intent bubble + ambush marker + key cap)
def badge_bg(g,c):
    circ(g,7.5,7.5,7.2,c)
def b_loot():
    g=blank(); badge_bg(g,'Y'); poly(g,[(3,6),(12,6),(12,12),(3,12)],'n'); rect(g,3,6,12,7,'N'); rect(g,7,8,8,10,'y'); poly(g,[(4,6),(7.5,3),(11,6)],'y'); return g
def b_exit():
    g=blank(); badge_bg(g,'G'); rect(g,5,3,10,12,'N'); rect(g,6,4,9,12,'k'); poly(g,[(2,8),(6,5),(6,11)],'w'); return g
def b_q():
    g=blank(); badge_bg(g,'y')
    for (x,y) in ((6,3),(7,3),(8,3),(9,3),(10,4),(10,5),(9,6),(8,7),(7,8),(7,9),(7,12),(8,12),(6,4),(10,6)): px(g,x,y,'k')
    rect(g,7,11,8,12,'k'); return g
def b_alert():
    g=blank(); badge_bg(g,'r'); rect(g,7,3,8,9,'w'); rect(g,7,11,8,12,'w'); return g
def b_eye():
    g=blank(); badge_bg(g,'b'); poly(g,[(2,7.5),(7.5,4),(13,7.5),(7.5,11)],'w'); circ(g,7.5,7.5,2,'B'); circ(g,7.5,7.5,0.8,'k'); return g
def b_star():
    g=blank(); badge_bg(g,'p')
    poly(g,[(7.5,2),(9,6),(13,6),(10,9),(11,13),(7.5,10.5),(4,13),(5,9),(2,6),(6,6)],'y'); return g
def b_ambush():
    g=blank(); badge_bg(g,'r'); circ(g,7.5,7.5,4.6,'w'); circ(g,7.5,7.5,3.2,'r'); circ(g,7.5,7.5,1.4,'w'); return g
def b_hide():
    g=blank(); badge_bg(g,'b'); circ(g,4.5,9,2.8,'G'); circ(g,10.5,9,2.8,'G'); circ(g,7.5,6,3.4,'g'); circ(g,6.5,5,1,'y'); circ(g,9.5,8,0.8,'y'); rect(g,2,11,13,12,'G'); px(g,4,4,'g'); px(g,11,5,'g'); return g
for k,f in [("BadgeLoot",b_loot),("BadgeExit",b_exit),("BadgeQuestion",b_q),("BadgeAlert",b_alert),("BadgeEye",b_eye),("BadgeStar",b_star),("BadgeAmbush",b_ambush),("BadgeHide",b_hide)]:
    icons[k]=[''.join(r) for r in outline(f())]
pal={'k':(0.1,0.09,0.12),'w':(1,1,1),'r':(0.9,0.2,0.22),'R':(0.6,0.1,0.12),'y':(1,0.86,0.25),'Y':(0.85,0.6,0.12),'b':(0.3,0.6,0.95),'B':(0.16,0.34,0.7),'g':(0.4,0.75,0.35),'G':(0.2,0.45,0.2),'n':(0.62,0.42,0.22),'N':(0.4,0.26,0.14),'s':(0.7,0.69,0.66),'S':(0.42,0.41,0.4),'p':(0.72,0.45,1),'P':(0.45,0.22,0.7),'c':(0.55,0.9,1),'o':(0.95,0.6,0.18),'x':(0.05,0.04,0.04)}
def luma(c): return 0.299*c[0]+0.587*c[1]+0.114*c[2]
bad=0
for k,rows in icons.items():
    on=lambda x,y: 0<=x<N and 0<=y<N and rows[y][x]!='.'
    filled=sum(1 for y in range(N) for x in range(N) if on(x,y)); edge=dark=0
    for y in range(N):
        for x in range(N):
            if not on(x,y): continue
            if not(on(x-1,y) and on(x+1,y) and on(x,y-1) and on(x,y+1)):
                edge+=1; dark+= luma(pal[rows[y][x]])<0.3
    fill=filled/256; ok=0.15<=fill<=0.85 and dark/edge>=0.75
    if not ok: bad+=1; print("BAD",k,fill,dark/edge)
print("icons",len(icons),"bad",bad)
out=['// 自动生成：tools_s242/gen_icons.py（S241 机关图标 + S242 一目了然徽章）。改图标：直接改下面的 16×16 字符画，或者把同名 PNG 放进 Assets/Resources/Step1Icons/<Key>.png 覆盖。',
'using System.Collections.Generic;','',
'/// <summary>',
'/// S241：恶作剧房间机关的 16×16 像素图标（用户："图例太密集、看不过来、大脑过载——有没有更形象的图例"）。',
'/// 第一性原理：认东西靠<b>形状</b>，不靠读字——火苗像火、弹簧像弹簧、香蕉像香蕉；颜色沿用元素原色（形状 + 颜色双编码，色弱也分得清）。',
'/// 调色板 = OverworldArt.Palette（小镇和房间同一套颜色）；每张图都过 OverworldArt.Audit（≤16 色、深色描边、剪影不太空不太满）。',
'/// 运行时 Step1PropIcons 只给"没换美术"的白盒机关贴图标（换了主题图的不动）。',
'/// 参考：Kenney 1-Bit Pack（CC0，16×16 一眼可读的图标做法）https://kenney.nl/assets/1-bit-pack ；色弱友好的形状 + 颜色双编码 https://colorblindgames.com/2023/04/19/universal-colorblind-code/',
'/// </summary>',
'public static class Step1Icons','{',
'    public const int Size = OverworldArt.Size;','',
'    /// <summary>元素主题键（= 物体名前缀）→ 图标名。香蕉皮 / 道具箱 / 箱子 / 大炮和小镇共用图标。</summary>',
'    public static readonly Dictionary<string, string> ByKey = new Dictionary<string, string>','    {']
for k in icons: out.append(f'        {{ "{k}", "{k}" }},')
out.append('        { "BananaPeel", "BananaPeel" },')
out+=['    };','',
'    public static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>','    {']
for k,rows in icons.items():
    out.append(f'        {{ "{k}", new[] {{')
    for r in rows: out.append(f'            "{r}",')
    out.append('        } },')
out+=['    };','',
'    /// <summary>这个元素有没有图标（房间自己的 → 小镇的）。</summary>',
'    public static bool Has(string key) => key != null && ByKey.TryGetValue(key, out var n) && (Icons.ContainsKey(n) || OverworldArt.Icons.ContainsKey(n));','',
'    /// <summary>RGBA 像素（从左下角开始，和 Texture2D.SetPixels 顺序一致）。没有 = null。</summary>',
'    public static float[] Pixels(string key)',
'    {',
'        if (key == null || !ByKey.TryGetValue(key, out var name)) return null;',
'        if (Step1Art.Icons.TryGetValue(name, out var art)) return Step1Art.Rgba(art); // S243：AI 生成的新图优先，旧的程序画留作备份',
'        if (!Icons.TryGetValue(name, out var rows)) return OverworldArt.Pixels(name);',
'        var px = new float[Size * Size * 4];',
'        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)',
'        {',
'            char c = rows[Size - 1 - y][x]; int i = (y * Size + x) * 4;',
"            if (c == '.' || !OverworldArt.Palette.TryGetValue(c, out var col)) continue;",
'            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;',
'        }',
'        return px;',
'    }','}','']
open('/home/user/workspace/repo/Assets/Scripts/Gameplay/Step1/Step1Icons.cs','w',encoding='utf-8').write('\n'.join(out))
# preview
from PIL import Image
im=Image.new('RGBA',(N*len(icons)*4+len(icons)*8,N*4+8),(60,60,70,255))
for i,(k,rows) in enumerate(icons.items()):
    for y in range(N):
        for x in range(N):
            c=rows[y][x]
            if c=='.': continue
            r,g,b=pal[c]
            for dy in range(4):
                for dx in range(4): im.putpixel((i*(N*4+8)+4+x*4+dx,4+y*4+dy),(int(r*255),int(g*255),int(b*255),255))
im.save('/home/user/workspace/tools_s242/icons_preview.png')
