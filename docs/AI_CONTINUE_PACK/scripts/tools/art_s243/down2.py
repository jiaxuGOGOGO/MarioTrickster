# S244: 更好的缩小：先在原图上把每个像素吸到调色板，再按"格子里出现最多的颜色"取（保留饱和色，不会被平均成泥色）；描边只补外圈
import numpy as np, sys
sys.path.insert(0,'/home/user/workspace/tools_s243')
exec(open('/home/user/workspace/tools_s243/slice.py').read().split("out={'icons'")[0])
def quant_hsv(rgb):
    # 加权距离 + 饱和度惩罚：避免鲜艳色被吸到灰/棕
    d=((rgb[...,None,:]-P[None,None])**2*np.array([0.35,0.45,0.2])).sum(-1)
    sat=lambda c:c.max(-1)-c.min(-1)
    d=d+0.08*np.abs(sat(rgb)[...,None]-sat(P)[None,None])
    return d.argmin(-1)
def downs2(a,m,box,side,anchor='bottom',S=16,fill=None):
    x0,y0,x1,y1=box; w,h=x1-x0,y1-y0
    sc=side/max(w,h) if fill is None else fill
    tw,th=max(1,round(w*sc)),max(1,round(h*sc))
    idx=quant_hsv(a[y0:y1,x0:x1]); msk=m[y0:y1,x0:x1]
    grid=[['.']*S for _ in range(S)]
    ox=(S-tw)//2; oy=S-th if anchor=='bottom' else (S-th)//2
    for ty in range(th):
        for tx in range(tw):
            ya,yb=int(ty*h/th),max(int(ty*h/th)+1,int((ty+1)*h/th)); xa,xb=int(tx*w/tw),max(int(tx*w/tw)+1,int((tx+1)*w/tw))
            mm=msk[ya:yb,xa:xb]
            if mm.mean()<0.45: continue
            v=idx[ya:yb,xa:xb][mm]
            # 暗色线条在小图里最重要：暗像素占 >=30% 就取暗色
            cnt=np.bincount(v,minlength=len(keys))
            dark=[i for i,k in enumerate(keys) if k in 'kx']
            if cnt[dark].sum()>=0.3*len(v): c=keys[dark[int(np.argmax(cnt[dark]))]]
            else: c=keys[int(np.argmax(cnt))]
            if 0<=oy+ty<S and 0<=ox+tx<S: grid[oy+ty][ox+tx]=c
    return grid
