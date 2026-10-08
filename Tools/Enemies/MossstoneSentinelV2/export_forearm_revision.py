from pathlib import Path
import re,shutil,json,hashlib
from PIL import Image

root=Path.cwd();fixture=root/'Temp/MossstoneV2Build';tool=root/'Tools/Enemies/MossstoneSentinelV2'
assert (fixture/'reinforced-revision-result.txt').read_text().startswith('PASS')
assert (fixture/'v2-play-result.txt').read_text().startswith('PASS')
assert 'First blade charge actual root displacement=' in (fixture/'v2-play-timing.txt').read_text()
clips=['Assets/Prefabs/Items/ReinforcedFists/Animations/ReinforcedFists_Attack02_GroundSlap.anim','Assets/Prefabs/Items/ReinforcedBlade/Animations/ReinforcedBlade_Attack03_GroundSlap.anim']
for rel in clips:
 src=fixture/rel;dst=root/rel
 assert dst.resolve().is_relative_to(root.resolve())
 a=re.search(r'^guid: (\w+)',Path(str(src)+'.meta').read_text(),re.M).group(1)
 b=re.search(r'^guid: (\w+)',Path(str(dst)+'.meta').read_text(),re.M).group(1)
 assert a==b,'Animation GUID changed'
 shutil.copy2(src,dst)
for name in ['reinforced-revision-result.txt','reinforced-contact-result.txt','v2-clearance-result.txt','v2-play-result.txt','v2-play-timing.txt','v2-preview-result.txt']:
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
snapshot=json.loads((tool/'forearm-revision-preserved-hashes.json').read_text())
changed=[p for p,h in snapshot.items() if hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
assert not changed,changed
(tool/'forearm-preserved-result.txt').write_text(f'PASS: all {len(snapshot)} original enemy asset/meta files match the snapshot at the start of the forearm revision. Only the two Reinforced Sentinel ground-impact animation clips were exported; prefab, weapon configs and charge/punch clips were preserved.\n',encoding='utf-8')
shutil.copy2(tool/'ReinforcedSentinelGuide.md',root/'Assets/Prefabs/Enemies/ReinforcedSentinel/ResourceGuide.md')
print('Exported two ground impact clips with the existing GUIDs; original enemy resources preserved.')
