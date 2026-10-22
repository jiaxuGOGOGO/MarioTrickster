import re
from PIL import Image, ImageDraw, ImageFont
src=open('/home/user/workspace/repo/Assets/Scripts/Gameplay/Step1/Step1Icons.cs',encoding='utf-8').read()
icons={}
for m in re.finditer(r'\{ "(\w+)", new\[\] \{\n((?:\s+"[^"]+",\n)+)\s+\} \}',src):
    icons[m.group(1)]=re.findall(r'"([^"]+)"',m.group(2))
pal={'k':(0.1,0.09,0.12),'w':(1,1,1),'r':(0.9,0.2,0.22),'R':(0.6,0.1,0.12),'y':(1,0.86,0.25),'Y':(0.85,0.6,0.12),'b':(0.3,0.6,0.95),'B':(0.16,0.34,0.7),'g':(0.4,0.75,0.35),'G':(0.2,0.45,0.2),'n':(0.62,0.42,0.22),'N':(0.4,0.26,0.14),'s':(0.7,0.69,0.66),'S':(0.42,0.41,0.4),'p':(0.72,0.45,1),'P':(0.45,0.22,0.7),'c':(0.55,0.9,1),'o':(0.95,0.6,0.18),'x':(0.05,0.04,0.04)}
def icon(name,sz):
    im=Image.new('RGBA',(16,16),(0,0,0,0))
    for y,row in enumerate(icons[name]):
        for x,c in enumerate(row):
            if c!='.': r,g,b=pal[c]; im.putpixel((x,y),(int(r*255),int(g*255),int(b*255),255))
    return im.resize((sz,sz),Image.NEAREST)
F='/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
f1=ImageFont.truetype(F,44,index=2); f2=ImageFont.truetype(F,26,index=2); f3=ImageFont.truetype(F,22,index=2)
RED=(255,82,77); BLUE=(89,179,255); YEL=(255,217,64); BG=(28,30,38); CARD=(42,45,56); TXT=(235,235,240); DIM=(160,163,175)
W,H=1600,1000; im=Image.new('RGB',(W,H),BG); d=ImageDraw.Draw(im)
d.text((50,32),'一眼看懂：恶作剧房间速查卡（S242）',font=f1,fill=TXT)
d.text((50,92),'颜色只有三种：',font=f2,fill=DIM)
x=250
for col,t in [(RED,'红 = 坑他（按 L）'),(BLUE,'蓝 = 躲 · 钻'),(YEL,'黄 = 目标 / 他要走的路')]:
    d.rounded_rectangle((x,96,x+28,124),6,fill=col); d.text((x+38,92),t,font=f2,fill=TXT); x+=330
# section 1: Mario intent
def card(x,y,w,h,title,col):
    d.rounded_rectangle((x,y,x+w,y+h),16,fill=CARD); d.rectangle((x,y+16,x+6,y+h-16),fill=col); d.text((x+22,y+12),title,font=f2,fill=col)
card(50,150,740,370,'他头顶的图标 = 他现在想干什么',YEL)
for i,(n,t) in enumerate([('BadgeLoot','去拿宝'),('BadgeExit','拿到宝，要逃了'),('BadgeQuestion','起疑，过来看看'),('BadgeAlert','看见你，追你！'),('BadgeEye','跟丢了，在找你'),('BadgeStar','被坑晕了 = 接连招')]):
    cx=80+(i%2)*350; cy=205+(i//2)*100
    im.paste(icon(n,72),(cx,cy),icon(n,72)); d.text((cx+90,cy+20),t,font=f2,fill=TXT)
card(810,150,740,370,'按 M = 作战图（看一眼就关）',RED)
d.text((835,205),'• • • • • 小点 = 他接下来要走的路',font=f2,fill=YEL)
im.paste(icon('BadgeAmbush',64),(835,250),icon('BadgeAmbush',64)); d.text((915,255),'靶心 = 埋伏点：他会经过的机关',font=f2,fill=TXT); d.text((915,290),'下面写「喷火 3 秒」= 3 秒后他走到',font=f3,fill=DIM)
d.text((835,345),'身边那个东西会出一张小卡：',font=f2,fill=TXT); d.text((835,382),'图标 + 名字 + 一句话（别的不写字）',font=f3,fill=DIM)
d.text((835,425),'V = 每个东西头上一行「名字 · 动词」',font=f2,fill=TXT); d.text((835,462),'平时不开 M，路线也淡淡地显示',font=f3,fill=DIM)
card(50,540,1500,420,'伪装装备栏 + 假道具诱饵',BLUE)
for i,(n,k) in enumerate([('OilBarrel','1'),('Bush','2'),('RoomLamp','3')]):
    bx=90+i*110; d.rounded_rectangle((bx,600,bx+92,692),10,fill=(60,64,78),outline=BLUE if i==0 else None,width=5)
    im.paste(icon(n,64),(bx+14,614),icon(n,64)); d.text((bx+8,600),k,font=f3,fill=TXT)
d.text((90,705),'底部这一排 = 你能变的形态',font=f2,fill=TXT); d.text((90,742),'默认 = 这个房间里',font=f3,fill=DIM); d.text((90,772),'最多的 3 种东西（藏木于林）',font=f3,fill=DIM)
lines=[('1 2 3','换形态（伪装中也能换；他正看着你时换 = 露馅）'),('P','变身 / 变回'),('E','站在东西旁边按 = 把它放进当前格'),('G','丢一个当前形态的假道具（伪装中也能丢，每局 2 个）'),('Shift+G','丢以前那种假的你'),]
for i,(k,t) in enumerate(lines):
    y=600+i*62; d.rounded_rectangle((520,y,650,y+46),8,fill=(70,74,90)); d.text((585-d.textlength(k,font=f2)/2,y+6),k,font=f2,fill=TXT); d.text((670,y+8),t,font=f2,fill=TXT)
d.text((520,915),'假道具落地后时不时扭一下 → 他当成怪东西过来查看 → 走近 2.5 格识破；趁这时从另一边溜',font=f3,fill=DIM)
im.save('/home/user/workspace/repo/docs/step1/S242_速查卡.png')
print('ok')
