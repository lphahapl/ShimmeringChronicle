"""Prepare an isolated native Unity build without touching the open project scene."""
from pathlib import Path
import shutil, json, hashlib

ROOT=Path(__file__).resolve().parents[2]
BUILD=ROOT/'Temp/WeaponProduction'

def copy_file(path):
    source=ROOT/path; target=BUILD/path
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,target)

def tree(path, skip=()):
    source=ROOT/path
    for p in source.rglob('*'):
        if p.is_file() and not any(s in p.parts for s in skip):
            copy_file(p.relative_to(ROOT))

def run():
    BUILD.mkdir(parents=True,exist_ok=True)
    (BUILD/'Packages').mkdir(exist_ok=True)
    (BUILD/'Packages/manifest.json').write_text('{"dependencies":{}}',encoding='utf-8')
    for p in ['ProjectSettings/ProjectVersion.txt','ProjectSettings/TagManager.asset']:
        copy_file(p)
    tree('Assets/Scripts')
    # Compiled package dependencies already used by the native player validation.
    deps=ROOT/'Temp/PlayerPoiseValidation/Assets/Dependencies'
    shutil.copytree(deps,BUILD/'Assets/Dependencies',dirs_exist_ok=True)
    tree('Assets/Prefabs/Weapon')
    tree('Assets/Prefabs/Enemies')
    tree('Assets/Prefabs/Player/Warrior')
    tree('Assets/Prefabs/Player/KeqingRootMotion',skip=('Source',))
    source=ROOT/'Assets/Prefabs/Player/KeqingRootMotion/Models/Source'
    for p in source.rglob('*'):
        if p.is_file() and p.suffix.lower() not in ('.blend','.pmx') and not p.name.endswith(('.blend.meta','.pmx.meta')):
            copy_file(p.relative_to(ROOT))
    (BUILD/'Assets/Editor').mkdir(exist_ok=True)
    protected=[ROOT/'Assets/Prefabs/Player/Warrior/Character Warrior.prefab',ROOT/'Assets/Prefabs/Player/KeqingRootMotion/KeqingRootMotionPlayer.prefab']
    snapshot={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    (BUILD/'protected-hashes.json').write_text(json.dumps(snapshot,indent=2),encoding='utf-8')
    print(json.dumps({'build':str(BUILD),'protected':snapshot},ensure_ascii=True))

if __name__=='__main__':run()
