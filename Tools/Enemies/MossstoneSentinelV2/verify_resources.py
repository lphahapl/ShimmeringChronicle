from pathlib import Path
import re,json,hashlib,subprocess
from PIL import Image

root=Path.cwd();tool=root/'Tools/Enemies/MossstoneSentinelV2'
folders=[root/'Assets/Prefabs/Enemies/ReinforcedSentinel',root/'Assets/Prefabs/Items/ReinforcedFists',root/'Assets/Prefabs/Items/ReinforcedBlade']
assets=[p for f in folders for p in f.rglob('*') if p.suffix in {'.prefab','.asset','.anim','.mat','.controller','.overrideController'}]
required=set();prefabs=0
for p in assets:
 s=p.read_text(encoding='utf-8-sig')
 required.update(g for g in re.findall(r'guid: ([a-f0-9]{32})',s) if not g.startswith('0000000000000000'))
 if p.suffix=='.prefab':
  ids=set(re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M));prefabs+=1
  local=set(re.findall(r'\{fileID: (-?\d+)\}',s))-{'0'}
  assert local.issubset(ids),(p,local-ids)
 if p.suffix=='.mat':
  assert 'guid: 933532a4fcc9baf4fa0491de14d08ed7' in s and '_BaseColor:' in s and '_BaseMap:' in s,p
 if p.suffix=='.asset' and p.parent in folders:
  assert 'MossstoneSentinelV2' not in s and 'enemy_mossstone_' not in s,p
pattern='^guid: ('+'|'.join(sorted(required))+')$'
result=subprocess.run(['rg','--no-ignore','-l','-g','*.meta',pattern,'Assets','Library/PackageCache'],cwd=root,capture_output=True,text=True,encoding='utf-8')
found={}
for line in result.stdout.splitlines():
 p=root/line;guid=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M).group(1);found.setdefault(guid,[]).append(line)
assert required.issubset(found),required-found.keys()
assert all(len(found[g])==1 for g in required),{g:found[g] for g in required if len(found[g])!=1}
group=(root/'Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset').read_text(encoding='utf-8-sig')
configs=[folders[0]/'ReinforcedSentinel.asset',folders[0]/'ReinforcedSentinel_Blade.asset',folders[1]/'ReinforcedFists.asset',folders[2]/'ReinforcedBlade.asset']
for p in configs:
 guid=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M).group(1)
 address=str(p.relative_to(root)).replace('\\','/')
 assert group.count('m_GUID: '+guid)==1 and f'm_GUID: {guid}\n    m_Address: {address}' in group,p
assert 'Assets/Prefabs/Enemies/MossstoneSentinelV2' not in group
historical=json.loads((tool/'original-hashes.json').read_text());external=json.loads((tool/'preserved-external-edits.json').read_text())
for path,old in historical.items():
 assert hashlib.sha256((root/path).read_bytes()).hexdigest()==external.get(path,old),path
for name,count in [('fists-two-hit',93),('blade-three-hit',159)]:
 with Image.open(tool/'Previews'/f'{name}.gif') as im:
  assert im.n_frames==count
  for i in [0,count//2,count-1]:im.seek(i);im.load()
report=f'PASS: {len(assets)} resources, {prefabs} prefabs; all {len(required)} external GUIDs resolve uniquely; all local prefab references resolve; URP materials; four Addressables entries at renamed paths; original enemy files preserved including the external controller edit; GIFs decoded.\n'
(tool/'reinforced-export-validation.txt').write_text(report,encoding='utf-8');print(report)
