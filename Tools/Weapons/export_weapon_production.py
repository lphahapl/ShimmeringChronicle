"""Export approved native assets only; merge current SO values and existing clip events."""
from pathlib import Path
import re,json,hashlib,shutil
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2];BUILD=ROOT/'Temp/WeaponProduction';SRC=BUILD/'Assets/Prefabs/Weapon';DST=ROOT/'Assets/Prefabs/Weapon'
IDS=['weapon_002','weapon_003','weapon_004','EnemyWeapon_001','weapon_001','MossstoneBlade','ReinforcedBlade','FalconOath']
LIT='933532a4fcc9baf4fa0491de14d08ed7';PARTICLES='0406db5a14f94604a8c57ccfbc9f3b46'
pending={}

def put(path,data):
    assert path.is_relative_to(ROOT) and 'Keqing' not in str(path)
    pending[path]=data

def copy(p):
    target=DST/p.relative_to(SRC)
    if p.suffix=='.mat':
        s=p.read_text(encoding='utf-8');fx=p.stem=='AttackGlow'
        s=re.sub(r'm_Shader: .*',f'm_Shader: {{fileID: 4800000, guid: {PARTICLES if fx else LIT}, type: 3}}',s)
        if fx:
            s=s.replace('_ALPHABLEND_ON','_SURFACE_TYPE_TRANSPARENT')
            for key,value in {'_Surface':1,'_Blend':2,'_Cull':0,'_AlphaClip':0,'_SrcBlendAlpha':1,'_DstBlendAlpha':1}.items():
                if re.search(r'^    - '+key+':',s,re.M):s=re.sub(r'^    - '+key+r': .*$',f'    - {key}: {value}',s,flags=re.M)
                else:s=s.replace('    m_Floats:\n','    m_Floats:\n'+f'    - {key}: {value}\n')
        put(target,s.encode('utf-8'))
    elif p.name.endswith('_MetallicSmoothness.png.meta'):put(target,p.read_bytes().replace(b'sRGBTexture: 1',b'sRGBTexture: 0'))
    else:put(target,p.read_bytes())

def pair(p):
    if p.exists():copy(p)
    meta=Path(str(p)+'.meta')
    if meta.exists():copy(meta)

def directory(p,allowed=None):
    if not p.exists():return
    for f in p.rglob('*'):
        if f.is_file() and (allowed is None or allowed(f)):copy(f)
    pair(Path(str(p)+'.meta'))

def field(s,name):
    found=re.search(r'^  '+name+r': .*$',s,re.M)
    assert found,name
    return found.group(0)

def merge_so(id):
    main=DST/id/(id+'.asset')
    if not main.exists():return
    native=(SRC/id/(id+'.asset')).read_text(encoding='utf-8');s=main.read_text(encoding='utf-8')
    names=[]
    if id in IDS[:4]:names+=['stopDistance','attackBreak','overrideController']
    for name in names:
        line=field(native,name)
        if re.search(r'^  '+name+r':',s,re.M):s=re.sub(r'^  '+name+r': .*$',lambda _:line,s,flags=re.M)
        else:s+='\n'+line+'\n'
    if id in IDS[:4]:
        native_block=re.search(r'  combo:\n(.*?)(?=^  \w|\Z)',native,re.M|re.S).group(1)
        old=re.search(r'  combo:\n(.*?)(?=^  \w|\Z)',s,re.M|re.S)
        new_hits=re.split(r'(?=^  - damagePerHit:)',native_block,flags=re.M)[1:]
        old_hits=re.split(r'(?=^  - damagePerHit:)',old.group(1),flags=re.M)[1:]
        assert len(new_hits)==len(old_hits),(id,'Combo count changed while producing assets')
        for n,(old_hit,new_hit) in enumerate(zip(old_hits,new_hits)):
            for name in ['radius','angle','judgeOffset']:
                line=re.search(r'^    '+name+r': .*$',new_hit,re.M).group(0)
                if re.search(r'^    '+name+r':',old_hit,re.M):old_hit=re.sub(r'^    '+name+r': .*$',lambda _:line,old_hit,flags=re.M)
                else:old_hit+=line+'\n'
            old_hits[n]=old_hit
        s=s[:old.start(1)]+''.join(old_hits)+s[old.end(1):]
    put(main,s.encode('utf-8'))

def events(p):
    main=DST/p.relative_to(SRC);current=main.read_text(encoding='utf-8');native=p.read_text(encoding='utf-8')
    prefix,tail=current.rsplit('  m_Events:',1);native_tail=native.rsplit('  m_Events:',1)[1]
    def chunks(t):return re.findall(r'  - time:.*?(?=  - time:|\Z)',t,re.S)
    changed=['PlayHandingEffORAudioClip','EndEff']
    old=[e for e in chunks(tail) if not any('functionName: '+name+'\n' in e for name in changed)]
    new=[e for e in chunks(native_tail) if any('functionName: '+name+'\n' in e for name in changed)]
    result=sorted(old+new,key=lambda e:float(re.search(r'  - time: ([^\n]+)',e).group(1)))
    put(main,(prefix+'  m_Events:\n'+''.join(result)).encode('utf-8'))

def previews():
    out=ROOT/'Tools/Weapons/Previews';out.mkdir(parents=True,exist_ok=True)
    for p in (BUILD/'ProductionPreviews').glob('*.png'):
        if 'Keqing' not in p.name:shutil.copy2(p,out/p.name)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
    frames=[]
    for name in ['Walk','Run','Attack1','Attack2','Attack3']:
        for p in sorted((BUILD/'ProductionPreviews/HeavyFrames').glob(name+'_*.png')):
            im=Image.open(p).convert('RGB');ImageDraw.Draw(im).text((16,14),'紫电 · '+{'Walk':'双手持剑行走','Run':'双手持剑跑步','Attack1':'第一段横斩','Attack2':'第二段反手斩','Attack3':'第三段重劈'}[name],font=font,fill='white');frames.append(im)
    if frames:frames[0].save(out/'VioletWarrior.gif',save_all=True,append_images=frames[1:],duration=50,loop=0,optimize=False)
    return out

def main():
    assert (BUILD/'production-validation.txt').read_text().rstrip().endswith('ALL PASSED'),'Native validation failed'
    assert 'Keqing deferred' in (BUILD/'preview-result.txt').read_text(),'Preview failed'
    assert 'ALL PASSED' in (BUILD/'audio-play-validation.txt').read_text(),'Actual Play Mode audio validation failed'
    inputs=json.loads((BUILD/'export-input-hashes.json').read_text())
    for path,digest in inputs.items():assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest,('Live input changed, refresh before export',path)
    for id in IDS:
        src=SRC/id;pair(src/(id+'.prefab'));directory(src/'Effects');directory(src/'Audio')
        used=set()
        if id in IDS[:4]:used=set(re.findall(r'^usemtl (.+)$',(src/'Models'/(id+'.obj')).read_text(),re.M));directory(src/'Models')
        directory(src/'Materials',lambda p:p.stem.replace('.mat','') in used|{'AttackGlow'})
        directory(src/'Textures',lambda p:p.name.startswith(tuple(used|{'AttackGlow'})))
        if id in IDS[:3]:directory(src/'Animations/Warrior');pair(src/'Animations.meta')
        if id=='EnemyWeapon_001':directory(src/'Animations')
        merge_so(id)
    for id in ['MossstoneBlade','ReinforcedBlade','MossstoneFists','ReinforcedFists']:
        for p in (SRC/id/'Animations').glob('*.anim'):events(p)
    # No mutation above; all input checks and merges must succeed before writing.
    for path,data in pending.items():path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    out=previews();shutil.copy2(BUILD/'production-validation.txt',ROOT/'Tools/Weapons/production-validation.txt');shutil.copy2(BUILD/'audio-play-validation.txt',ROOT/'Tools/Weapons/audio-play-validation.txt')
    exported={str(p.relative_to(ROOT)):hashlib.sha256(data).hexdigest() for p,data in pending.items()}
    (ROOT/'Tools/Weapons/exported-assets.json').write_text(json.dumps(exported,indent=2),encoding='utf-8')
    print(json.dumps({'exported_files':len(exported),'previews':str(out),'keqing':'excluded'},ensure_ascii=True))

if __name__=='__main__':main()
