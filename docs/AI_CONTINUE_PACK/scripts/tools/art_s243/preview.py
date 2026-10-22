import json
from PIL import Image, ImageDraw
from slice import PAL
d=json.load(open('/home/user/workspace/art243/art.json'))
Z=6
def draw(img,rows,ox,oy,z=Z):
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c=='.' : continue
            col=tuple(int(v*255) for v in PAL[c]); ImageDraw.Draw(img).rectangle([ox+x*z,oy+y*z,ox+x*z+z-1,oy+y*z+z-1],fill=col)
items=[('hero%d'%i,r) for i,r in enumerate(d['hero'])]+[('imp%d'%i,r) for i,r in enumerate(d['imp'])]+list(d['icons'].items())+list(d['tiles'].items())
cols=10; cw=16*Z+14
H=((len(items)+cols-1)//cols)*(cw+14)+10
img=Image.new('RGB',(cols*cw+10,H),(70,74,92))
for i,(k,r) in enumerate(items):
    x=10+(i%cols)*cw; y=10+(i//cols)*(cw+14); draw(img,r,x,y); ImageDraw.Draw(img).text((x,y+16*Z+2),k[:14],fill=(230,230,230))
img.save('/home/user/workspace/art243/preview16.png'); print(img.size)
