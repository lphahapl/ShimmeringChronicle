"""Export the new Warrior root-motion folder only, keeping all source assets intact."""
from pathlib import Path
import hashlib, json, re, shutil

ROOT = Path(__file__).resolve().parents[3]
FIXTURE = ROOT / 'Temp/WeaponProduction'
OUTPUT = Path('Assets/Prefabs/Player/Warrior/RootMotion')
TOOLS = ROOT / 'Tools/Players/WarriorRootMotion'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    original = json.loads((TOOLS/'original-hashes.json').read_text())
    for path, expected in original.items():
        assert digest(ROOT/path) == expected, ('An original changed; stop before exporting', path)
    for name in ['warrior-root-motion-validation.txt', 'warrior-root-motion-play-validation.txt']:
        assert (FIXTURE/name).read_text().rstrip().splitlines()[-1].startswith('ALL PASSED'), name
    script = Path('Assets/Scripts/Logic/WarriorRootMotionController.cs.meta')
    guid = re.search(rb'guid: ([a-f0-9]{32})', (ROOT/script).read_bytes()).group(1)
    assert guid in (FIXTURE/OUTPUT/'WarriorRootMotionPlayer.prefab').read_bytes()
    assert (ROOT/script).read_bytes() == (FIXTURE/script).read_bytes()
    assert not (ROOT/OUTPUT).exists(), 'Output exists; do not replace any live edits'
    shutil.copytree(FIXTURE/OUTPUT, ROOT/OUTPUT)
    shutil.copy2(Path(str(FIXTURE/OUTPUT)+'.meta'), Path(str(ROOT/OUTPUT)+'.meta'))
    for name in ['warrior-motion-inspection.txt', 'warrior-root-motion-build.txt', 'warrior-jump-root-motion.txt', 'warrior-root-motion-validation.txt', 'warrior-root-motion-play-validation.txt']:
        shutil.copy2(FIXTURE/name, TOOLS/name)
    files = {str(p.relative_to(ROOT)): digest(p) for p in (ROOT/OUTPUT).rglob('*') if p.is_file()}
    (TOOLS/'exported-files.json').write_text(json.dumps(files, indent=2), encoding='utf-8')
    for path, expected in original.items():
        assert digest(ROOT/path) == expected, path
    (TOOLS/'original-integrity.txt').write_text(f'PASS: all {len(original)} original Warrior/weapon files retain their pre-task hashes. Export writes only the new RootMotion folder.\n')
    print(f'Exported {len(files)} new files; verified {len(original)} originals unchanged.')

if __name__ == '__main__':
    main()
