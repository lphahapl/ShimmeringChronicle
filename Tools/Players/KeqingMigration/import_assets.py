from pathlib import Path
import hashlib,json,re,uuid
source=Path(r'D:\unity_training\Runnner&&Lucy')
target=Path(r'D:\unity_training\Practice')
output=target/'Assets/Prefabs/Player/Keqing/Imported'
pattern=re.compile(rb'guid: ([0-9a-f]{32})')
def index(root):
    result={}
    for meta in root.rglob('*.meta'):
        m=pattern.search(meta.read_bytes())
        if m: result[m[1].decode()]=Path(str(meta)[:-5])
    return result
sources=index(source/'Assets'); existing=index(target/'Assets')
pending=[source/'Assets/Arts/Models/53 刻晴/Keqing_Humanoid.prefab',source/'Assets/Arts/Models/53 刻晴/Keqing_CLazyIdle.controller']
pending += [source/'Assets/Girl_GreatSword_AnimSet/Animation/Humanoid/hit_head_front_inplace.fbx', source/'Assets/Girl_GreatSword_AnimSet/Animation/Humanoid/dead_01_inplace.fbx']
dependencies=set();missing=set()
while pending:
    path=pending.pop()
    if path in dependencies: continue
    dependencies.add(path)
    data=Path(str(path)+'.meta').read_bytes()
    if path.suffix.lower() in ['.prefab','.controller','.anim','.mat','.asset','.overridecontroller']: data+=path.read_bytes()
    for guid in set(g.decode() for g in pattern.findall(data)):
        if guid in sources:
            d=sources[guid]
            if d.is_file(): pending.append(d)
        elif not guid.startswith('0000000000000000'): missing.add(guid)
report_path=target/'Tools/Players/KeqingMigration/import-manifest.json'
remap={}
if report_path.exists(): remap=json.loads(report_path.read_text(encoding='utf-8'))['remapped_guids']
for path in dependencies:
    guid=pattern.search(Path(str(path)+'.meta').read_bytes())[1].decode()
    if guid in existing: remap[guid]=uuid.uuid5(uuid.NAMESPACE_URL,'Practice/Keqing/'+guid).hex
manifest=[]
for path in sorted(dependencies):
    dst=output/path.relative_to(source/'Assets');dst.parent.mkdir(parents=True,exist_ok=True)
    for a,b in [(path,dst),(Path(str(path)+'.meta'),Path(str(dst)+'.meta'))]:
        data=a.read_bytes()
        if a.suffix.lower() in ['.meta','.prefab','.controller','.anim','.mat','.asset','.cs']:
            for old,new in remap.items(): data=data.replace(old.encode(),new.encode())
            if a.suffix.lower()=='.cs': data=data.replace(b'TestPlayerController',b'PlayerController')
        b.write_bytes(data)
    manifest.append({'source':str(path.relative_to(source)),'target':str(dst.relative_to(target)),'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
report={'files':manifest,'remapped_guids':remap,'external_guids':sorted(missing)}
(target/'Tools/Players/KeqingMigration/import-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'copied':len(manifest),'mb':round(sum(p.stat().st_size for p in dependencies)/1024**2,2),'external_guids':sorted(missing),'scripts':[str(p) for p in dependencies if p.suffix=='.cs']},ensure_ascii=False))
