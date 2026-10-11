"""Refresh only existing prefab/config inputs and snapshot them before final export."""
from pathlib import Path
import shutil,json,hashlib
ROOT=Path(__file__).resolve().parents[2];BUILD=ROOT/'Temp/WeaponProduction'
IDS=['weapon_002','weapon_003','weapon_004','EnemyWeapon_001','weapon_001','MossstoneBlade','ReinforcedBlade','FalconOath']
snapshot={}
for id in IDS:
    for suffix in ['.prefab','.asset']:
        p=ROOT/'Assets/Prefabs/Weapon'/id/(id+suffix)
        if p.exists():
            snapshot[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
            shutil.copy2(p,BUILD/p.relative_to(ROOT))
for p in (ROOT/'Tools/Weapons').glob('*.cs'):
    if p.name=='BuildPlayerWeaponMotion.cs':continue
    shutil.copy2(p,BUILD/'Assets/Editor'/p.name)
(BUILD/'export-input-hashes.json').write_text(json.dumps(snapshot,indent=2),encoding='utf-8')
print('Refreshed live inputs; Keqing generation excluded.')
