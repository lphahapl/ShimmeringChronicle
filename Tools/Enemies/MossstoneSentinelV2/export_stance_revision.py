from pathlib import Path
import re,shutil,json,hashlib
from PIL import Image

root=Path.cwd();fixture=root/'Temp/MossstoneV2Build';tool=root/'Tools/Enemies/MossstoneSentinelV2'
for name in ['reinforced-revision-result.txt','v2-play-result.txt','v2-preview-result.txt']:
    assert (fixture/name).read_text().startswith('PASS'),name
stance=(fixture/'reinforced-stance-result.txt').read_text()
assert stance.count('zero root displacement and fixed foot goals')==4
timing=(fixture/'v2-play-timing.txt').read_text()
assert 'Fists complete combo displacement=' in timing and 'Blade complete combo displacement=' in timing
clips=[
    'Assets/Prefabs/Enemies/ReinforcedSentinel/Animations/Reinforced_Idle.anim',
    'Assets/Prefabs/Items/ReinforcedFists/Animations/ReinforcedFists_Attack01_Punch.anim',
    'Assets/Prefabs/Items/ReinforcedFists/Animations/ReinforcedFists_Attack02_GroundSlap.anim',
    'Assets/Prefabs/Items/ReinforcedBlade/Animations/ReinforcedBlade_Attack01_Charge.anim',
    'Assets/Prefabs/Items/ReinforcedBlade/Animations/ReinforcedBlade_Attack02_Sweep.anim',
    'Assets/Prefabs/Items/ReinforcedBlade/Animations/ReinforcedBlade_Attack03_GroundSlap.anim',
]
snapshot=json.loads((tool/'stance-preserved-hashes.json').read_text())
assert all(hashlib.sha256((root/p).read_bytes()).hexdigest()==h for p,h in snapshot.items()),'Original enemy changed'
for rel in clips:
    src=fixture/rel;dst=root/rel
    assert dst.resolve().is_relative_to(root.resolve())
    a=re.search(r'^guid: (\w+)',Path(str(src)+'.meta').read_text(),re.M).group(1)
    b=re.search(r'^guid: (\w+)',Path(str(dst)+'.meta').read_text(),re.M).group(1)
    assert a==b,'Animation GUID changed'
    shutil.copy2(src,dst)
for name in ['reinforced-revision-result.txt','reinforced-stance-result.txt','reinforced-contact-result.txt','v2-clearance-result.txt','v2-play-result.txt','v2-play-timing.txt','v2-preview-result.txt']:
    shutil.copy2(fixture/name,tool/name)
previews=tool/'Previews'
for p in (fixture/'PreviewsV2').glob('*.png'):shutil.copy2(p,previews/p.name)
for name in ['fists-two-hit','blade-three-hit']:
    files=sorted((fixture/'PreviewsV2'/f'{name}-frames').glob('*.png'))
    palette_source=Image.new('RGB',(32*32,((len(files)+31)//32)*32))
    for i,p in enumerate(files):
        with Image.open(p) as f:palette_source.paste(f.convert('RGB').resize((32,32)),((i%32)*32,(i//32)*32))
    palette=palette_source.quantize(colors=192);frames=[]
    for p in files:
        with Image.open(p) as f:frames.append(f.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE))
    durations=[(round((i+1)*100/18)-round(i*100/18))*10 for i in range(len(frames))]
    frames[0].save(previews/f'{name}.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2,optimize=True)
    print(name,len(frames),'native frames')
assert all(hashlib.sha256((root/p).read_bytes()).hexdigest()==h for p,h in snapshot.items())
(tool/'stance-preserved-result.txt').write_text(f'PASS: all {len(snapshot)} original enemy asset/meta files match the snapshot at the start of this stance revision. Only five Reinforced Sentinel attack clips and its idle clip exported; existing GUIDs, prefabs, weapon configs, other animations and main gameplay scripts preserved.\n',encoding='utf-8')
shutil.copy2(tool/'ReinforcedSentinelGuide.md',root/'Assets/Prefabs/Enemies/ReinforcedSentinel/ResourceGuide.md')
print('Exported six animation clips with existing GUIDs; original enemy resources preserved.')
