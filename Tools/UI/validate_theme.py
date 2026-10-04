from pathlib import Path
import re,yaml
from PIL import Image
files=['Assets/Scenes/SampleScene.unity','Assets/Prefabs/slot.prefab','Assets/Configs/ItemSO/weapon_001.asset']
for f in files:
 s=Path(f).read_text()
 ids=re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M)
 assert len(ids)==len(set(ids)),f+' duplicate IDs'
 clean=re.sub(r'^%.*\n','',s,flags=re.M)
 clean=re.sub(r'^--- !u!\d+ &-?\d+(?: stripped)?','---',clean,flags=re.M)
 docs=list(yaml.safe_load_all(clean))
 local=[x for x in re.findall(r'\{fileID: (-?\d+)\}',s) if x!='0' and x not in ids]
 assert not local,(f,local)
 print(f, 'YAML OK;',len(docs),'objects; local references OK')
for p in Path('Assets/Art/UI/Astral').glob('*.png'):
 meta=yaml.safe_load(Path(str(p)+'.meta').read_text()); im=Image.open(p)
 assert meta['TextureImporter']['spriteMode']==1
 assert meta['TextureImporter']['textureType']==8
 assert im.mode=='RGBA'
 print(p.name,im.size,'RGBA; Sprite OK')
scene=Path(files[0]).read_text(); prefab=Path(files[1]).read_text();weapon=Path(files[2]).read_text()
for name,target in [('panel',scene),('slot',prefab),('hp_track',scene),('bar_fill',scene),('weapon_001_icon',weapon)]:
 guid=yaml.safe_load(Path('Assets/Art/UI/Astral/'+name+'.png.meta').read_text())['guid']
 assert 'fileID: 21300000, guid: '+guid in target
print('All five sprite bindings verified.')
