import json,sys,numpy as np
from PIL import Image, ImageDraw
src=open('/home/user/workspace/tools_s243/slice.py').read(); ns={}; exec(src[:src.index('def load')],ns); PAL=ns['PAL']
d=json.load(open('/home/user/workspace/art245/art.json'))
def img(rows,sc=6):
    h,w=len(rows),len(rows[0]); a=np.zeros((h,w,4),np.uint8)
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c!='.': a[y,x]=[*(int(v*255) for v in PAL[c]),255]
    return Image.fromarray(a).resize((w*sc,h*sc),Image.NEAREST)
items=[(k,v) for g in ('town','decor','fx','elems','fixes') for k,v in d[g].items()]
cols=8; S=16*6+8; rows=(len(items)+cols-1)//cols
sheet=Image.new('RGBA',(cols*S,rows*(S+14)),(60,60,80,255)); dr=ImageDraw.Draw(sheet)
for i,(k,v) in enumerate(items):
    x,y=(i%cols)*S,(i//cols)*(S+14); sheet.alpha_composite(img(v),(x+4,y+4)); dr.text((x+4,y+S-2),f'{i+1}',fill=(255,255,255,255))
sheet.save('/home/user/workspace/art245/contact.png')
for n,s in d['strips'].items():
    pal=s['pal']; rows_=s['rows']; h,w=len(rows_),len(rows_[0]); a=np.zeros((h,w,4),np.uint8)
    for y,r in enumerate(rows_):
        for x,c in enumerate(r):
            if c!='.': a[y,x]=[*(int(v*255) for v in pal[int(c)]),255]
    Image.fromarray(a).resize((w*5,h*5),Image.NEAREST).save(f'/home/user/workspace/art245/{n}_strip.png')
print([f'{i+1}:{k}' for i,(k,_) in enumerate(items)])
