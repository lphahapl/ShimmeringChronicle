"""Build the selected Falcon Oath as a UV-mapped, material-separated game mesh."""
from pathlib import Path
import math, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Art/Weapons/FalconOath'
OUT.mkdir(parents=True,exist_ok=True)
V=[]; UV=[]; F=[]
COLORS={'Steel':(.48,.55,.61),'DarkSteel':(.10,.125,.145),'Brass':(.62,.40,.15),'Leather':(.23,.047,.038),'Garnet':(.48,.016,.03),'Cloth':(.38,.045,.04)}
def mesh(verts,faces,mat,uv=None):
    start=len(V); V.extend(verts)
    UV.extend(uv or [(p[0]*4+.5,p[1]/2.25) for p in verts])
    for f in faces:
        for i in range(1,len(f)-1): F.append((tuple(start+j for j in (f[0],f[i],f[i+1])),mat))
def lathe(profile,mat,n=24):
    vs=[(r*math.cos(2*math.pi*j/n),y,r*math.sin(2*math.pi*j/n)) for y,r in profile for j in range(n+1)]
    uv=[(j/n,i/(len(profile)-1)) for i in range(len(profile)) for j in range(n+1)]
    fs=[]
    for i in range(len(profile)-1):
        for j in range(n):
            a=i*(n+1)+j; fs.append((a,a+n+1,a+n+2,a+1))
    fs.extend([tuple(range(n)),tuple((len(profile)-1)*(n+1)+j for j in reversed(range(n)))])
    mesh(vs,fs,mat,uv)
def ring(y,r=.018,h=.009):
    lathe([(y-h/2,r*.90),(y-h*.30,r),(y+h*.30,r),(y+h/2,r*.90)],'Brass')
def relief(poly,depth,mat,z=0):
    # Convex contour with raised ridge, mirrored reverse, closed edge walls.
    if sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly)))<0: poly=list(reversed(poly))
    n=len(poly); cx=sum(x for x,y in poly)/n; cy=sum(y for x,y in poly)/n
    vs=[(x,y,z+depth*.25) for x,y in poly]+[(cx,cy,z+depth)]
    vs += [(x,y,z-depth*.25) for x,y in poly]+[(cx,cy,z-depth)]
    fs=[]
    for i in range(n):
        j=(i+1)%n
        fs += [(i,j,n),(n+1+j,n+1+i,2*n+1),(i,n+1+i,n+1+j,j)]
    mesh(vs,fs,mat)
def ribbon(y0,y1,r,mat,turns):
    vs=[]; count=int(turns*32)
    for i in range(count+1):
        a=i/count*turns*2*math.pi; y=y0+(y1-y0)*i/count
        for dy in [-.0011,.0011]: vs.append((r*math.cos(a),y+dy,r*math.sin(a)))
    mesh(vs,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(count)],mat)

# One continuous structural shaft; the pivot is the middle of the lower grip.
lathe([(.20,.0105),(.23,.013),(1.70,.013),(1.78,.016)],'DarkSteel')
for a,b in [(.62,.91),(1.36,1.57)]:
    lathe([(a,.014),(a+.009,.0153),(b-.009,.0153),(b,.014)],'Leather')
    ribbon(a+.004,b-.004,.0157,'Leather',10 if a<1 else 7)
    for y in [a,b]: ring(y,.0185,.019);ring(y+.014,.016,.004)
for y in [.23,.27,1.62,1.65,1.70,1.735]:ring(y,.019 if y<1.7 else .023)
lathe([(1.69,.017),(1.73,.025),(1.78,.022),(1.835,.019),(1.85,.014)],'DarkSteel')
ring(1.745,.026,.018)
# Continuous central ridge and narrow sharpened bevels, with no fan-shaped dent.
blade_profile=[(1.815,.014,.007),(1.86,.018,.009),(1.94,.048,.012),(2.235,.0025,.0015),(2.25,.0001,.0001)]
blade=[]
for yy,ww,dd in blade_profile:
    blade += [(xx,yy,zz) for xx,zz in [(-ww,0),(-ww*.82,dd*.35),(0,dd),(ww*.82,dd*.35),(ww,0),(ww*.82,-dd*.35),(0,-dd),(-ww*.82,-dd*.35)]]
blade_faces=[]
for row in range(len(blade_profile)-1):
    for j in range(8):
        a=row*8+j;b=row*8+(j+1)%8
        blade_faces.append((a,b,b+8,a+8))
blade_faces += [tuple(reversed(range(8))),tuple(range(len(blade)-8,len(blade)))]
mesh(blade,blade_faces,'Steel')
# Narrow brass socket framing with garnet on both sides.
relief([(-.023,1.795),(0,1.857),(.023,1.795),(0,1.756)],.020,'Brass')
for z in [-.020,.020]:
    relief([(-.012,1.798),(0,1.829),(.012,1.798),(0,1.775)],.007,'Garnet',z)
# Layered falcon wings: each feather is a separate closed faceted solid.
for s in [-1,1]:
    def wing(points,d=.008):relief([(s*x,y) for x,y in points],d,'Brass')
    wing([(.017,1.822),(.039,1.886),(.063,1.843),(.077,1.798),(.050,1.767),(.029,1.784)],.013)
    for k in range(6):
        x=.030+k*.0055; y=1.853-k*.014
        tipx=.060+k*.017; tipy=1.761-k*.008
        wing([(x,y),(x+.013,y+.005),(tipx+.014,tipy),(tipx,tipy+.007),(x+.004,y-.037)],.008-k*.00045)
    for k in range(3):
        x=.033+k*.013; y=1.838-k*.026
        wing([(x-.009,y+.012),(x+.005,y+.025),(x+.021,y-.023),(x+.008,y-.014)],.015)
# Butt socket and tapered steel counterweight.
lathe([(.18,.011),(.205,.024),(.222,.017),(.25,.015)],'Brass')
relief([(0,0),(-.022,.185),(0,.209),(.022,.185)],.014,'Steel')
ring(.205,.024,.009)
# Two folded, solid red pennant tails under the upper collar.
for sign,offset in [(1,0),(-1,.025)]:
    pts=[(.015,1.59-offset),(.029,1.585-offset),(.037,1.50-offset),(.050,1.39-offset),(.039,1.405-offset),(.033,1.38-offset),(.021,1.48-offset)]
    relief([(x,y) for x,y in pts],.002,'Cloth',sign*.018)
ring(1.59,.020,.007)

# Shift model origin to a useful hand attachment point, retain meter scale.
V=np.asarray(V,dtype=float);V[:,1]-=.765
lines=['# Falcon Oath B | meters | Y up | origin: lower grip','mtllib FalconOath.mtl']
lines += ['v %.7f %.7f %.7f'%tuple(v) for v in V]
lines += ['vt %.6f %.6f'%tuple(uv) for uv in UV]
normals=[]
for (a,b,c),mat in F:
    normal=np.cross(V[b]-V[a],V[c]-V[a])
    length=np.linalg.norm(normal)
    assert length>1e-12, 'Cannot export a normal for a degenerate triangle'
    normals.append(normal/length)
lines += ['vn %.8f %.8f %.8f'%tuple(n) for n in normals]
for mat in COLORS:
    lines+=['g '+mat,'usemtl '+mat,'s off']
    for face_index,(ids,m) in enumerate(F):
        if m==mat:lines.append('f '+' '.join(f'{i+1}/{i+1}/{face_index+1}' for i in ids))
(OUT/'FalconOath.obj').write_text('\n'.join(lines),encoding='utf8')
(OUT/'FalconOath.mtl').write_text('\n'.join(f'newmtl {m}\nKd {c[0]} {c[1]} {c[2]}\nKs 0.4 0.4 0.4\nNs 80\n' for m,c in COLORS.items()))
areas=[np.linalg.norm(np.cross(V[b]-V[a],V[c]-V[a]))*.5 for (a,b,c),m in F]
assert min(areas)>1e-12 and np.isfinite(V).all()
report={'vertices':len(V),'triangles':len(F),'materials':len(COLORS),'height_m':round(float(np.ptp(V[:,1])),5),'min_triangle_area':float(min(areas)),'pivot':'lower grip center','axis':'Y up'}
(OUT/'mesh-report.json').write_text(json.dumps(report,indent=2))

# Actual mesh orthographic renders, CPU z-buffer, no generated concept imagery.
W,H=1800,1500
canvas=Image.new('RGB',(W,H),(23,28,36));draw=ImageDraw.Draw(canvas)
fontpath='C:/Windows/Fonts/arial.ttf'
font=ImageFont.truetype(fontpath,22); big=ImageFont.truetype(fontpath,40)
draw.text((65,35),'FALCON OATH  /  B',font=big,fill=(233,211,159))
draw.text((65,91),'ACTUAL 3D MESH   |   2.25 m   |   Steel / Brass / Oxblood leather',font=font,fill=(166,176,190))
def render(angle,cx,cy,scale,targety):
    t=math.radians(angle); rot=np.array([[math.cos(t),0,math.sin(t)],[0,1,0],[-math.sin(t),0,math.cos(t)]])
    p=V@rot.T; screen=np.column_stack([cx+p[:,0]*scale,cy-(p[:,1]-targety)*scale])
    buf=np.full((H,W),-np.inf); rgb=np.asarray(canvas).copy()
    light=np.array([-.4,.65,1.0]);light/=np.linalg.norm(light)
    for ids,mat in F:
        q=screen[list(ids)]; xyz=p[list(ids)]
        lo=np.maximum(np.floor(q.min(0)).astype(int),[0,0]); hi=np.minimum(np.ceil(q.max(0)).astype(int),[W-1,H-1])
        if np.any(hi<lo):continue
        a,b,c=q; den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
        u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den; w=1-u-v
        depth=u*xyz[0,2]+v*xyz[1,2]+w*xyz[2,2]
        region=buf[lo[1]:hi[1]+1,lo[0]:hi[0]+1];mask=(u>=0)&(v>=0)&(w>=0)&(depth>region)
        normal=np.cross(xyz[1]-xyz[0],xyz[2]-xyz[0]);normal/=np.linalg.norm(normal)
        if normal[2]<0:normal=-normal
        diffuse=max(0,float(normal@light)); spec=max(0,float(normal@np.array([-.15,.3,.942])))**24
        color=np.clip(np.array(COLORS[mat])*(.42+.85*diffuse)+spec*.24,0,1)
        color=(color**(1/1.7)*255).astype(np.uint8)
        rgb[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=color;region[mask]=depth[mask]
    canvas.paste(Image.fromarray(rgb))
for a,x,label in [(0,260,'FRONT'),(90,600,'SIDE'),(180,940,'BACK')]:
    render(a,x,790,525,.36)
    ImageDraw.Draw(canvas).text((x-40,1430),label,font=font,fill=(207,213,222))
render(-24,1440,630,1250,1.15)
ImageDraw.Draw(canvas).text((1230,1430),'THREE-QUARTER DETAIL',font=font,fill=(207,213,222))
canvas.save(ROOT/'Tools/WeaponConcepts/B-Falcon-Oath-model.png')
print(json.dumps(report))
