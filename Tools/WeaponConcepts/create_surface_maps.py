from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

OUT=Path(__file__).resolve().parents[2]/'Assets/Art/Weapons/FalconOath/Textures'
OUT.mkdir(parents=True,exist_ok=True)
N=1024
rng=np.random.default_rng(82941)
y,x=np.mgrid[0:N,0:N]/N
def noise(scale):
    a=Image.fromarray(rng.integers(0,256,(scale,scale),dtype=np.uint8))
    return np.asarray(a.resize((N,N),Image.Resampling.BICUBIC),dtype=float)/255-.5
def save(name,a): Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(OUT/name)
def maps(name,color,metal,smooth,kind):
    fine=rng.normal(0,.015,(N,N));cloud=noise(32); h=np.zeros((N,N))
    if kind=='metal':
        grain=np.broadcast_to(rng.normal(0,.016,(1,N)),(N,N))
        scratch=np.zeros((N,N))
        for _ in range(170):
            xx=int(rng.integers(N)); yy=int(rng.integers(N)); length=int(rng.integers(8,100))
            scratch[yy:min(N,yy+length),xx]=rng.uniform(.12,.40)
        variation=1+grain+cloud*.07+fine*.3-scratch*.25
        h=grain*.08+fine*.03-scratch*.008
        rough=smooth+cloud*.09-grain*.3-scratch*.24
    elif kind=='leather':
        pores=noise(256);seam=np.exp(-((np.mod(y*12+x,1)-.5)/.035)**2)
        variation=.95+pores*.23+cloud*.2-seam*.24
        h=pores*.012-seam*.018
        rough=smooth+pores*.09-seam*.08
    else:
        weave=np.sin(x*N*np.pi/2)*np.sin(y*N*np.pi/2)
        variation=1+weave*.06+cloud*.12
        h=weave*.002
        rough=smooth+weave*.03
    save(name+'_BaseColor.png',np.array(color)[None,None,:]*variation[:,:,None])
    mask=np.ones((N,N,4));mask[:,:,0]=metal;mask[:,:,1:3]=0;mask[:,:,3]=rough
    save(name+'_MetallicSmoothness.png',mask)
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*6;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*6
    normal=np.stack([-dx,dy,np.ones_like(dx)],2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
    save(name+'_Normal.png',normal*.5+.5)
for args in [
    ('Steel',(.65,.70,.75),.98,.79,'metal'),
    ('DarkSteel',(.19,.22,.25),.88,.54,'metal'),
    ('Brass',(.68,.47,.22),.92,.64,'metal'),
    ('Leather',(.27,.055,.041),0,.31,'leather'),
    ('Cloth',(.38,.042,.038),0,.20,'cloth')]:maps(*args)
print('Created 15 PBR textures (1024 x 1024).')
