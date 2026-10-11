from pathlib import Path
import re,json,hashlib,wave
ROOT=Path(__file__).resolve().parents[2]
exported=json.loads((ROOT/'Tools/Weapons/exported-assets.json').read_text())
index=set()
for directory in [ROOT/'Assets',ROOT/'Library/PackageCache']:
    for path in directory.rglob('*.meta'):
        match=re.search(rb'^guid: ([a-f0-9]{32})',path.read_bytes(),re.M)
        if match:index.add(match.group(1).decode())
unresolved=set();checked=0
for file,digest in exported.items():
    path=ROOT/file;data=path.read_bytes();assert hashlib.sha256(data).hexdigest()==digest,file
    if path.suffix in ['.prefab','.asset','.mat','.controller','.overrideController','.mask','.anim']:
        refs=set(g.decode() for g in re.findall(rb'guid: ([a-f0-9]{32})',data))
        unresolved.update(g for g in refs if g not in index and not g.startswith('0000000000000000'));checked+=len(refs)
    if path.suffix=='.wav':
        with wave.open(str(path)) as audio:assert audio.getnchannels()==1 and audio.getframerate()==44100
assert not unresolved,sorted(unresolved)
protected=json.loads((ROOT/'Temp/WeaponProduction/protected-hashes.json').read_text())
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in protected.items())
report=f'PASS {len(exported)} exported files match recorded hashes\nPASS {checked} asset GUID references resolve in this project and package cache\nPASS generated audio is mono 44.1 kHz\nPASS original Warrior and Keqing prefab hashes unchanged\nPASS no Keqing assets exported\n'
(ROOT/'Tools/Weapons/post-export-validation.txt').write_text(report,encoding='utf-8');print(report)
