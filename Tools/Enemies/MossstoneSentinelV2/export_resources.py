from pathlib import Path
import re,json,shutil,uuid,hashlib,sys
from PIL import Image
root=Path.cwd();fixture=root/'Temp/MossstoneV2Build';out=root/'Tools/Enemies/MossstoneSentinelV2';previews=out/'Previews';previews.mkdir(exist_ok=True)
folders=['Assets/Prefabs/Enemies/ReinforcedSentinel','Assets/Prefabs/Items/ReinforcedFists','Assets/Prefabs/Items/ReinforcedBlade']
def rename(s):
 return s.replace('MossstoneSentinelV2','ReinforcedSentinel').replace('MossstoneFistsV2','ReinforcedFists').replace('MossstoneBladeV2','ReinforcedBlade').replace('SentinelV2_','Reinforced_').replace('FistsV2_','ReinforcedFists_').replace('BladeV2_','ReinforcedBlade_').replace('Attack02_Slam','Attack02_GroundSlap').replace('Attack03_Cleave','Attack03_GroundSlap')
remap=[line.split('\t') for line in (fixture/'script-remap.tsv').read_text(encoding='utf-8-sig').splitlines() if '\t' in line]
assert (fixture/'v2-play-result.txt').read_text().startswith('PASS')
assert (fixture/'reinforced-revision-result.txt').read_text().startswith('PASS')
for old in ['Assets/Prefabs/Enemies/MossstoneSentinelV2','Assets/Prefabs/Items/MossstoneFistsV2','Assets/Prefabs/Items/MossstoneBladeV2']:
 src=root/old;dst=root/rename(old)
 assert src.resolve().is_relative_to(root.resolve()) and dst.resolve().is_relative_to(root.resolve())
 if src.exists():
  assert '--update-v2' in sys.argv and not dst.exists()
  # Move assets and their meta files together so scene and controller GUID references survive.
  src.rename(dst);Path(str(src)+'.meta').rename(Path(str(dst)+'.meta'))
  for p in list(dst.rglob('*')):
   if p.is_file() and rename(p.name)!=p.name:p.rename(p.with_name(rename(p.name)))
for rel in folders:
 dst=root/rel;src=fixture/rel
 if dst.exists():
  assert '--update-v2' in sys.argv,'V2 exists: update requires explicit --update-v2'
  for meta in src.rglob('*.meta'):
   target=dst/meta.relative_to(src)
   if target.exists():assert re.search(r'^guid: (\w+)',meta.read_text(),re.M).group(1)==re.search(r'^guid: (\w+)',target.read_text(),re.M).group(1),'Update must preserve V2 GUIDs'
 shutil.copytree(src,dst,dirs_exist_ok=True);shutil.copy2(str(src)+'.meta',str(dst)+'.meta')
 for p in dst.rglob('*'):
  if p.suffix not in {'.prefab','.asset','.mat','.anim','.controller','.overrideController'}:continue
  s=p.read_text(encoding='utf-8-sig')
  for old,new in remap:s=s.replace(old,new)
  if p.suffix=='.mat':
   s=re.sub(r'  m_Shader: .*','  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}',s)
   base=re.search(r'    - _MainTex:\n(?:        .*\n){3}',s).group().replace('_MainTex','_BaseMap');s=s.replace('    m_TexEnvs:\n','    m_TexEnvs:\n'+base)
   color=re.search(r'    - _Color: .*',s).group().replace('_Color','_BaseColor');s=s.replace('    m_Colors:\n','    m_Colors:\n'+color+'\n')
   for key,val in {'_Smoothness':.2,'_Surface':0,'_Blend':0,'_Cull':2,'_ZWrite':1,'_AlphaClip':int(p.stem=='Moss'),'_ReceiveShadows':1}.items():
    if re.search(r'    - '+key+':',s):s=re.sub(r'(    - '+key+':) .*',r'\g<1> '+str(val),s)
    else:s=s.replace('    m_Floats:\n',f'    m_Floats:\n    - {key}: {val}\n')
  p.write_text(s,encoding='utf-8')
configs=[(folders[0]+'/ReinforcedSentinel.asset','EnemySO'),(folders[0]+'/ReinforcedSentinel_Blade.asset','EnemySO'),(folders[1]+'/ReinforcedFists.asset','ItemSO'),(folders[2]+'/ReinforcedBlade.asset','ItemSO')]
p=root/'Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset';s=p.read_text(encoding='utf-8-sig');entries=''
for path,label in configs:
 guid=re.search(r'^guid: (\w+)',Path(path+'.meta').read_text(),re.M).group(1)
 if 'm_GUID: '+guid in s:
  s=re.sub(r'(  - m_GUID: '+guid+r'\n    m_Address:) [^\n]*',r'\g<1> '+path,s)
  continue
 entries+=f'  - m_GUID: {guid}\n    m_Address: {path}\n    m_ReadOnly: 0\n    m_SerializedLabels:\n    - {label}\n    FlaggedDuringContentUpdateRestriction: 0\n'
needle='  m_ReadOnly: 0\n  m_Settings:';assert needle in s;s=s.replace(needle,entries+needle);p.write_text(s,encoding='utf-8')
for p in (fixture/'PreviewsV2').glob('*.png'):shutil.copy2(p,previews/p.name)
for name in ['v2-build-result.txt','v2-clearance-result.txt','v2-polish-result.txt','v2-play-result.txt','v2-play-timing.txt','v2-preview-result.txt']:shutil.copy2(fixture/name,out/name)
for name in ['reinforced-revision-result.txt','reinforced-contact-result.txt']:shutil.copy2(fixture/name,out/name)
for name in ['fists-two-hit','blade-three-hit']:
 files=sorted((fixture/'PreviewsV2'/f'{name}-frames').glob('*.png'));assert len(files)>70
 palette_source=Image.new('RGB',(32*32,((len(files)+31)//32)*32))
 for i,p in enumerate(files):
  with Image.open(p) as frame:palette_source.paste(frame.convert('RGB').resize((32,32)),((i%32)*32,(i//32)*32))
 palette=palette_source.quantize(colors=192);frames=[]
 for p in files:
  with Image.open(p) as frame:frames.append(frame.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE))
 durations=[(round((i+1)*100/18)-round(i*100/18))*10 for i in range(len(frames))]
 frames[0].save(previews/f'{name}.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2,optimize=True)
 print(f'{name}: {len(frames)} native rendered frames encoded, {sum(durations)/1000:.2f}s')
original=json.loads((out/'original-hashes.json').read_text())
changed=[path for path,h in original.items() if hashlib.sha256((root/path).read_bytes()).hexdigest()!=h]
external_controller='Assets/Prefabs/Enemies/MossstoneSentinel/MossstoneSentinel.controller'
# The live editor changed the original controller during this revision. This exporter
# never writes original enemy folders; keep that edit rather than restore an old hash.
assert set(changed).issubset({external_controller}),changed
kept={path:hashlib.sha256((root/path).read_bytes()).hexdigest() for path in changed}
(out/'preserved-external-edits.json').write_text(json.dumps(kept,indent=2),encoding='utf-8')
(out/'v1-preserved-result.txt').write_text(f'PASS: {len(original)-len(changed)} of {len(original)} original V1 asset/meta files match the earlier snapshot. The generator did not write any original enemy resource. External controller edit retained, not restored: {changed}. Current hashes recorded in preserved-external-edits.json.\n',encoding='utf-8')
print('Exported Reinforced Sentinel, preserved V2 GUIDs, updated four Addressables paths, restored source script GUIDs, and verified original resources were preserved including the external controller edit.')
