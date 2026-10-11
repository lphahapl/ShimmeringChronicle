"""Authored closed meshes, PBR maps and original swing sounds for approved concepts."""
from pathlib import Path
import math, json, wave, array, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Temp/WeaponProduction/Assets/Prefabs/Weapon'
SPEC={
 'weapon_002':dict(length=.95,tip=.755,color=(1,.81,.39),life=.16,width=.12,sound=.22,volume=.60),
 'weapon_003':dict(length=1.10,tip=.875,color=(.20,.79,1),life=.20,width=.16,sound=.25,volume=.55),
 'weapon_004':dict(length=1.10,tip=.870,color=(.68,.22,1),life=.18,width=.14,sound=.27,volume=.65),
 'EnemyWeapon_001':dict(length=1.35,tip=1.10,color=(1,.70,.30),life=.22,width=.20,sound=.25,volume=.65),
 'weapon_001':dict(color=(1,.90,.63),life=.16,width=.12,sound=.055,volume=.60),
 'MossstoneBlade':dict(color=(.82,.72,.40),life=.20,width=.18,sound=.24,volume=.65),
 'ReinforcedBlade':dict(color=(1,.66,.25),life=.22,width=.22,sound=.28,volume=.65),
 'FalconOath':dict(color=(1,.82,.42),life=.16,width=.10,sound=.23,volume=.55)}

class Model:
    def __init__(self):self.faces=[]
    def polygon(self,points,material):
        # Convex polygons used for each surface strip and profile.
        for i in range(1,len(points)-1):self.faces.append((points[0],points[i],points[i+1],material))
    def relief(self,poly,depth,mat,z=0,bevel=.003):
        if sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly)))<0:poly=list(reversed(poly))
        n=len(poly); cx=sum(x for x,y in poly)/n;cy=sum(y for x,y in poly)/n
        inner=[(x+(cx-x)*.10,y+(cy-y)*.06) for x,y in poly]
        for side in [-1,1]:
            edge=[(x,y,z+side*(depth/2-bevel)) for x,y in poly]
            cap=[(x,y,z+side*depth/2) for x,y in inner]
            for i in range(n):
                j=(i+1)%n; self.polygon([edge[i],edge[j],cap[j],cap[i]][::side],mat)
                self.polygon([(cx,cy,z+side*depth/2),cap[i],cap[j]][::side],mat)
        for i in range(n):
            j=(i+1)%n;self.polygon([(poly[i][0],poly[i][1],z-depth/2+bevel),(poly[j][0],poly[j][1],z-depth/2+bevel),(poly[j][0],poly[j][1],z+depth/2-bevel),(poly[i][0],poly[i][1],z+depth/2-bevel)],mat)
    def lathe(self,profile,mat,n=24,z=0):
        rings=[[(r*math.cos(2*math.pi*j/n),y,z+r*math.sin(2*math.pi*j/n)) for j in range(n)] for y,r in profile]
        for a,b in zip(rings,rings[1:]):
            for j in range(n):k=(j+1)%n;self.polygon([a[j],b[j],b[k],a[k]],mat)
        self.polygon(rings[0],mat);self.polygon(list(reversed(rings[-1])),mat)
    def blade(self,profile,mat):
        rings=[]
        for y,w,d in profile:
            rings.append([(x,y,z) for x,z in [(-w,0),(-w*.8,d*.6),(-w*.12,d),(w*.12,d),(w*.8,d*.6),(w,0),(w*.8,-d*.6),(w*.12,-d),(-w*.12,-d),(-w*.8,-d*.6)]])
        for a,b in zip(rings,rings[1:]):
            for j in range(10):k=(j+1)%10;self.polygon([a[j],a[k],b[k],b[j]],mat)
        self.polygon(list(reversed(rings[0])),mat);self.polygon(rings[-1],mat)
    def wrap(self,a,b,r,mat,turns=8):
        strips=[]
        for k in range(turns*32+1):
            ang=k/32*2*math.pi;y=a+(b-a)*k/(turns*32)
            strips.append([(r*math.cos(ang),y+dy,r*math.sin(ang)) for dy in [-.0015,.0015]])
        for a,b in zip(strips,strips[1:]):self.polygon([a[0],b[0],b[1],a[1]],mat)
    def save(self,path):
        lines=['# Authored in metres; +Y blade axis, grip-centre origin.', 'mtllib '+path.stem+'.mtl']
        count=0;prev=None
        for a,b,c,mat in self.faces:
            if mat!=prev:lines+=['g '+mat,'usemtl '+mat];prev=mat
            norm=np.cross(np.array(b)-a,np.array(c)-a);size=np.linalg.norm(norm)
            if size<1e-10:continue
            norm/=size
            for p in [a,b,c]:
                lines+=['v %.7f %.7f %.7f'%tuple(p),'vt %.7f %.7f'%(p[0]*5+.5,p[1]*2),'vn %.7f %.7f %.7f'%tuple(norm)]
            lines+=['f '+' '.join(f'{count+j}/{count+j}/{count+j}' for j in [1,2,3])];count+=3
        path.parent.mkdir(parents=True,exist_ok=True);path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
        used=sorted({face[3] for face in self.faces})
        path.with_suffix('.mtl').write_text('\n'.join('newmtl '+name+'\nKd '+' '.join(str(v) for v in PALETTES[name])+'\nKs 0.6 0.6 0.6\nNs 80\n' for name in used),encoding='utf-8')
        return count//3

def sword(id):
    m=Model(); tip=SPEC[id]['tip']; heavy=id=='weapon_004';enemy=id=='EnemyWeapon_001'
    bottom=tip-SPEC[id]['length']; guard=.10 if not enemy else .12
    if enemy:
        m.relief([(-.14,.15),(.14,.15),(.14,1.07),(.10,1.10),(-.10,1.10),(-.14,1.07)],.11,'Steel',bevel=.008)
        m.relief([(-.20,.095),(.20,.095),(.20,.165),(-.20,.165)],.13,'Brass',bevel=.009)
        for x in [-.045,.045]:
            for y in [.115,.147]:
                for z in [-.067,.067]:m.relief([(x-.009,y),(x,y-.009),(x+.009,y),(x,y+.009)],.006,'Brass',z)
        m.relief([(-.043,bottom),(.043,bottom),(.052,bottom+.075),(-.052,bottom+.075)],.10,'DarkSteel',bevel=.006)
        m.lathe([(bottom+.073,.033),(bottom+.09,.033),(.085,.033),(.095,.033)],'Leather',n=8)
        m.wrap(bottom+.084,.083,.034,'DarkLeather',6)
    else:
        if id=='weapon_002':
            m.blade([(.12,.026,.007),(.18,.024,.007),(.68,.023,.006),(.755,.0001,.0001)],'Steel')
            # Recessed dark fuller on both sides, bracketed by raised steel edges.
            for z in [-.0071,.0071]:m.relief([(-.003,.17),(.003,.17),(.003,.66),(0,.69),(-.003,.66)],.0003,'DarkSteel',z,bevel=.00005)
            m.relief([(-.115,.103),(-.115,.124),(-.04,.108),(.04,.108),(.115,.124),(.115,.103),(.04,.090),(-.04,.090)],.022,'Brass')
            for s in [-1,1]:m.lathe([(.103,.009),(.109,.011),(.121,.007)],'Brass',n=16,z=0) if False else m.relief([(s*.119,.110),(s*.113,.126),(s*.105,.112),(s*.114,.102)],.019,'Brass')
            # Disc pommel, extruded along Z.
            y=bottom+.037;poly=[(.036*math.cos(a),y+.036*math.sin(a)) for a in np.linspace(0,2*math.pi,32,endpoint=False)]
            m.relief(poly,.022,'Brass',bevel=.003)
            grip_a=bottom+.08;grip_b=.082;grip_r=.021; grip='Leather';wrap='DarkLeather'
        else:
            frost=id=='weapon_003'; frame='Steel' if frost else 'DarkSteel';crystal='FrostCrystal' if frost else 'VioletCrystal'; gold='Steel' if frost else 'Brass'
            m.blade([(.11,.043,.013),(.23,.033,.013),(.32,.036,.012),(.74,.032,.010),(tip,.0001,.0001)],frame)
            m.blade([(.145,.032,.023),(.26,.025,.023),(.32,.028,.021),(.74,.025,.018),(tip-.027,.0001,.0001)],crystal)
            for z in [-.026,.026]:
                m.relief([(-.003,.12),(.003,.12),(.003,.73),(0,tip-.012),(-.003,.73)],.003,gold,z,bevel=.0002)
            for s in [-1,1]:
                m.relief([(s*.015,.12),(s*.045,.11),(s*.12,.16),(s*.145,.22),(s*.142,.15),(s*.12,.11),(s*.05,.080),(s*.012,.083)],.028,gold)
                for k in range(3):m.relief([(s*.03,.105-k*.012),(s*.08,.123-k*.012),(s*(.125-k*.014),.117-k*.012),(s*.075,.085-k*.014)],.014,gold,z=.01)
                m.relief([(s*.035,.123),(s*.095,.143),(s*.116,.137),(s*.074,.105)],.012,crystal,z=.015)
            m.relief([(0,.052),(-.038,.105),(0,.167),(.038,.105)],.040,gold)
            for z in [-.023,.023]:m.relief([(0,.068),(-.025,.107),(0,.145),(.025,.107)],.012,crystal,z,bevel=.002)
            m.relief([(0,bottom),(-.032,bottom+.065),(0,bottom+.098),(.032,bottom+.065)],.035,gold)
            for z in [-.02,.02]:m.relief([(0,bottom+.018),(-.019,bottom+.062),(0,bottom+.081),(.019,bottom+.062)],.008,crystal,z,bevel=.001)
            grip_a=bottom+.102;grip_b=.064;grip_r=.0185;grip='VioletLeather' if heavy else 'BlueLeather';wrap=gold
        m.lathe([(grip_a,grip_r*.90),(grip_a+.008,grip_r),(grip_b-.008,grip_r),(grip_b,grip_r*.9)],grip)
        m.wrap(grip_a+.006,grip_b-.006,grip_r+.0009,wrap,7)
        for y in [grip_a,grip_b]:m.lathe([(y-.005,grip_r*.95),(y-.002,grip_r*1.14),(y+.002,grip_r*1.14),(y+.005,grip_r*.95)],'Brass' if id!='weapon_003' else 'Steel')
    tris=m.save(OUT/id/'Models'/f'{id}.obj')
    return dict(id=id,triangles=tris,length=SPEC[id]['length'],tip=tip)

PALETTES={'Steel':(.66,.72,.78),'DarkSteel':(.12,.15,.19),'Brass':(.65,.43,.17),'Leather':(.25,.095,.055),'DarkLeather':(.10,.042,.025),'BlueLeather':(.025,.07,.13),'VioletLeather':(.14,.045,.24),'FrostCrystal':(.075,.56,.77),'VioletCrystal':(.33,.07,.63)}
def maps(id):
    path=OUT/id/'Textures';path.mkdir(parents=True,exist_ok=True)
    rng=np.random.default_rng(1244+list(SPEC).index(id)); n=512;y,x=np.mgrid[:n,:n]/n
    for name,color in PALETTES.items():
        crystal='Crystal' in name;leather='Leather' in name;noise=rng.normal(0,.012,(n,n))
        h=noise*.1
        if leather:
            variation=1+noise*4+np.sin((x+y*.3)*128)*.04;smooth=.25;metal=0
        elif crystal:
            cracks=np.maximum(0,np.sin(x*32+y*17+np.sin(y*18)*2)-.988)*45
            variation=.86+np.sin(x*10+y*3)*.08+noise+cracks*.2;h+=cracks*.012;smooth=.85;metal=.35
        else:
            grain=np.broadcast_to(rng.normal(0,.014,(1,n)),(n,n));variation=1+grain+noise;smooth=.68;metal=.9
        def save(s,a):Image.fromarray(np.uint8(np.clip(a,0,1)*255)).save(path/(name+s+'.png'))
        save('_BaseColor',np.asarray(color)[None,None,:]*variation[:,:,None])
        packed=np.zeros((n,n,4));packed[:,:,0]=metal;packed[:,:,3]=smooth+noise
        save('_MetallicSmoothness',packed)
        dx=(np.roll(h,-1,1)-np.roll(h,1,1))*4;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*4
        normal=np.stack([-dx,dy,np.ones_like(dx)],2);normal/=np.linalg.norm(normal,axis=2)[:,:,None];save('_Normal',normal*.5+.5)

def glow(id):
    path=OUT/id/'Textures';path.mkdir(parents=True,exist_ok=True)
    y,x=np.mgrid[:128,:128]/127*2-1;r=x*x+y*y
    rgba=np.ones((128,128,4));rgba[:,:,3]=np.maximum(0,np.exp(-r*5)-math.exp(-5))/(1-math.exp(-5))
    Image.fromarray(np.uint8(rgba*255)).save(path/'AttackGlow.png')

def audio(id,duration,impact=False):
    path=OUT/id/'Audio';path.mkdir(parents=True,exist_ok=True);rng=random.Random(3001+list(SPEC).index(id));rate=44100;count=round(duration*rate);a=[];lo=slow=phase=0.
    for i in range(count):
        p=i/(count-1);t=i/rate;v=rng.uniform(-1,1);lo+=.22*(v-lo);slow+=.027*(lo-slow)
        env=math.sin(math.pi*p)**(1.6 if not impact else .7)
        heavy=id in ['weapon_004','EnemyWeapon_001','MossstoneBlade','ReinforcedBlade'];phase+=2*math.pi*(180-105*p if heavy else 340-210*p)/rate
        sound=(lo-slow)+math.sin(phase)*(.075 if heavy else .02)
        if id=='weapon_003':sound+=math.sin(2*math.pi*1640*t)*.04*math.exp(-5*p)
        if id=='weapon_004':sound+=v*.10*max(0,math.sin(t*340))*math.exp(-3*p)
        if impact:sound=slow*.9+math.sin(2*math.pi*(85-25*p)*t)*.25*math.exp(-5*p)+v*.03
        a.append(env*sound)
    peak=max(abs(s) for s in a);pcm=array.array('h',[round(s/peak*.55*32767) for s in a]);file=path/('ground_impact.wav' if impact else 'attack_swing.wav')
    with wave.open(str(file),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(pcm.tobytes())
    return dict(path=str(file.relative_to(OUT)),duration=duration,peak_dbfs=-5.19,source='Original deterministic synthesis',start=pcm[0],end=pcm[-1])

def main():
    model_records=[]
    for id in list(SPEC)[:4]:model_records.append(sword(id));maps(id)
    for id in SPEC:glow(id)
    audio_records=[audio(id,s['sound']) for id,s in SPEC.items()]
    records=ROOT/'art_out/weapon_production/source-assets.json';records.write_text(json.dumps(dict(models=model_records,audio=audio_records,spec=SPEC),indent=2),encoding='utf-8')
    (ROOT/'Temp/WeaponProduction/weapon-spec.json').write_text(json.dumps(SPEC,indent=2),encoding='utf-8')
    print(json.dumps(dict(models=model_records,audio_count=len(audio_records))))

if __name__=='__main__':main()
