from pathlib import Path
import json,re,subprocess,shutil,hashlib
root=Path.cwd();fixture=root/'Temp/MossstoneV2Build';fixture.mkdir(parents=True,exist_ok=True)
old=['Assets/Prefabs/Enemies/MossstoneSentinel','Assets/Prefabs/Items/MossstoneFists','Assets/Prefabs/Items/MossstoneBlade']
hashes={str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for folder in old for p in (root/folder).rglob('*') if p.is_file()}
(root/'Tools/Enemies/MossstoneSentinelV2/original-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
for rel in ['Assets/Editor','Assets/Plugins','Assets/Scripts','Packages','ProjectSettings']:(fixture/rel).mkdir(parents=True,exist_ok=True)
for p in (root/'Assets/Scripts').rglob('*'):
 if not p.is_file():continue
 dst=fixture/p.relative_to(root);dst.parent.mkdir(parents=True,exist_ok=True)
 if p.suffix=='.cs':
  b=p.read_bytes()
  try:s=b.decode('utf-8-sig')
  except UnicodeDecodeError:s=b.decode('gbk')
  s=s.replace("\r\n","\n");s=re.sub(r"^using (?:static UnityEngine.Rendering.STP|UnityEditor.ShaderGraph.Internal);\s*$", "", s, flags=re.M)
  dst.write_text(s,encoding='utf-8')
 else:shutil.copy2(p,dst)
for name in ['Unity.Addressables','Unity.ResourceManager','Unity.AI.Navigation','Unity.Burst','Unity.Collections','Unity.InputSystem','Unity.Mathematics','Unity.TextMeshPro','UnityEngine.UI','Unity.VisualScripting.Core','Unity.VisualScripting.Flow','Unity.VisualScripting.State','Unity.InternalAPIEngineBridge.004','UnityEditor.UI','UnityEditor.UI.Analytics','Unity.Profiling.Core']:
 shutil.copy2(root/f'Library/ScriptAssemblies/{name}.dll',fixture/f'Assets/Plugins/{name}.dll')
for name in ['Unity.Burst.Unsafe.dll','Unity.VisualScripting.Antlr3.Runtime.dll','Unity.Collections.LowLevel.ILSupport.dll']:
 src=next((root/'Library/PackageCache').rglob(name));shutil.copy2(src,fixture/'Assets/Plugins'/name)
nunit=next((root/'Library/PackageCache').glob('com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll'));shutil.copy2(nunit,fixture/'Assets/Plugins/nunit.framework.dll')
manifest=json.loads((root/'Packages/manifest.json').read_text(encoding='utf-8-sig'));(fixture/'Packages/manifest.json').write_text(json.dumps({'dependencies':{k:v for k,v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}},indent=2),encoding='utf-8')
for name in ['ProjectVersion.txt','TagManager.asset','InputManager.asset','TimeManager.asset','PhysicsManager.asset']:
 src=root/'ProjectSettings'/name
 if src.exists():shutil.copy2(src,fixture/'ProjectSettings'/name)
queue=[]
for rel in old+['Assets/Prefabs/TestEnemy','Assets/TextMesh Pro','Assets/Resources']:
 src=root/rel;dst=fixture/rel
 if src.is_dir():
  shutil.copytree(src,dst,dirs_exist_ok=True)
  queue.extend(p for p in src.rglob('*') if p.is_file() and p.suffix in {'.asset','.mat','.prefab','.controller','.overrideController','.anim'})
  if Path(str(src)+'.meta').exists():shutil.copy2(str(src)+'.meta',str(dst)+'.meta')
# Resolve serialized dependencies by their actual GUID; do not copy unrelated art.
seen=set();scriptmap={}
while queue:
 needed=set()
 for p in queue:
  if p in seen:continue
  seen.add(p)
  try:s=p.read_text(encoding='utf-8-sig')
  except UnicodeDecodeError:continue
  needed.update(g for g in re.findall(r'guid: ([a-f0-9]{32})',s) if not g.startswith('0000000000000000'))
 if not needed:break
 pattern='^guid: ('+'|'.join(sorted(needed))+')$'
 result=subprocess.run(['rg','--no-ignore','-l','-g','*.meta',pattern,'Assets','Library/PackageCache'],cwd=root,capture_output=True,text=True,encoding='utf-8')
 queue=[]
 for line in result.stdout.splitlines():
  meta=root/line;src=Path(str(meta)[:-5]);guid=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M).group(1)
  if src.suffix=='.cs':
   if 'com.unity.ugui' in str(src):scriptmap[guid]=src.stem
   continue
  if not src.is_file():continue
  if 'com.unity.render-pipelines.' in str(src):continue
  if src.is_relative_to(root/'Assets'):dst=fixture/src.relative_to(root)
  else:
   rel=src.relative_to(root/'Library/PackageCache');dst=fixture/'Assets/PackageResources'/rel
  if dst.exists():continue
  dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst);shutil.copy2(meta,str(dst)+'.meta')
  if src.suffix in {'.asset','.mat','.prefab','.controller','.overrideController','.anim'}:queue.append(src)
# Imported TMP shaders use relative include files, copy the whole shader directory.
for src in (root/'Library/PackageCache').glob('com.unity.ugui*/Shaders'):
 shutil.copytree(src,fixture/'Assets/PackageResources'/src.relative_to(root/'Library/PackageCache'),dirs_exist_ok=True)
(fixture/'source-script-map.tsv').write_text('\n'.join(g+'\t'+n for g,n in scriptmap.items()),encoding='utf-8')
# Old model materials are preview-only in this isolated built-in rendering project.
for rel in old:
 for p in (fixture/rel).rglob('*.mat'):
  s=p.read_text(encoding='utf-8-sig');s=re.sub(r'  m_Shader: .*','  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}',s);p.write_text(s,encoding='utf-8')
for src in (root/'Tools/Enemies/MossstoneSentinelV2').glob('*.cs'):shutil.copy2(src,fixture/'Assets/Editor'/src.name)
print('Prepared isolated project with actual scripts, DLL dependencies, old comparison model, UI/font dependencies and a hash manifest preserving V1.')
