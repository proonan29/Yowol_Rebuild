import cv2,numpy as np,glob,os,sys,json
from PIL import Image
def load(p,bg=(70,60,60)):
    im=np.array(Image.open(p).convert('RGBA')).astype(np.float32)
    a=im[...,3:4]/255.
    rgb=im[...,:3]*a+np.array(bg[::-1],np.float32)*(1-a)
    return cv2.cvtColor(rgb.astype(np.uint8),cv2.COLOR_RGB2BGR), (im[...,3]>20)
ref,_=load(sys.argv[1]); x0,y0,x1,y1=map(int,sys.argv[2].split(','))
T=ref[y0:y1,x0:x1]
res={}
for p in sys.argv[3:]:
    img,mask=load(p); best=(-1,)
    for s in np.arange(0.55,1.45,0.02):
        t=cv2.resize(T,None,fx=s,fy=s,interpolation=cv2.INTER_AREA)
        if t.shape[0]>=img.shape[0] or t.shape[1]>=img.shape[1]: continue
        for flip in (0,1):
            tt=t[:,::-1] if flip else t
            r=cv2.matchTemplate(img,tt,cv2.TM_CCOEFF_NORMED); _,mv,_,ml=cv2.minMaxLoc(r)
            if mv>best[0]: best=(mv,round(float(s),2),ml[0]+tt.shape[1]/2,ml[1]+tt.shape[0]/2,flip)
    ys,xs=np.where(mask)
    res[p]=dict(score=round(best[0],3),scale=best[1],cx=round(best[2]),cy=round(best[3]),flip=best[4],w=img.shape[1],h=img.shape[0],bb=[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())])
    print(p,res[p],flush=True)
json.dump(res,open('/home/claude/rev/face.json','w'),indent=1)
