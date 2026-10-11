"""Export only validated binding/event edits; reject concurrent changes to live inputs."""
from pathlib import Path
import hashlib, json, re, shutil

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'Temp/WeaponProduction'

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    for name in ['attack-data-binding-validation.txt', 'attack-data-play-validation.txt']:
        assert (FIXTURE/name).read_text().rstrip().splitlines()[-1].startswith('ALL PASSED'), name
    inputs = json.loads((FIXTURE/'attack-data-input-hashes.json').read_text())
    outputs = {}
    for path, original_hash in inputs.items():
        live, native = ROOT/path, FIXTURE/path
        assert digest(live) == original_hash, ('Live input changed; do not overwrite', path)
        if digest(native) == original_hash:
            continue
        if live.suffix == '.anim':
            # Animation curves/root motion must be byte-for-byte identical.
            assert live.read_text(encoding='utf-8').rsplit('  m_Events:', 1)[0] == native.read_text(encoding='utf-8').rsplit('  m_Events:', 1)[0], path
        if live.suffix == '.asset':
            # Unity reorders fields on save and may default legacy trailing fields.
            # Transfer only the two requested references into each current row.
            current = live.read_text(encoding='utf-8')
            native_text = native.read_text(encoding='utf-8')
            pattern = r'(?m)^  - damagePerHit:.*?(?=^  - damagePerHit:|^  [A-Za-z]|\Z)'
            new_rows = re.findall(pattern, native_text, re.S)
            index = 0
            def merge_row(match):
                nonlocal index
                source = new_rows[index]
                index += 1
                row = re.sub(r'^    (?:audioClip|eff):[^\n]*\n', '', match.group(0), flags=re.M)
                fields = ''.join(re.search(r'^    ' + name + r':[^\n]*\n', source, re.M).group(0) for name in ['audioClip', 'eff'])
                return re.sub(r'(^    time:[^\n]*\n)', lambda m: m.group(0) + fields, row, count=1, flags=re.M)
            candidate = re.sub(pattern, merge_row, current, flags=re.S)
            assert index == len(new_rows), ('Combo count changed', path)
            strip_resources = lambda s: re.sub(r'^    (?:audioClip|eff):[^\n]*\n', '', s, flags=re.M)
            assert strip_resources(current) == strip_resources(candidate), ('Combat/config fields changed', path)
            outputs[live] = candidate.replace('\n', '\r\n').encode('utf-8')
            continue
        outputs[live] = native.read_bytes()
    # All checks precede writes. Unrelated materials, controllers and scenes are excluded.
    for live, data in outputs.items():
        live.write_bytes(data)
    for name in ['attack-data-binding-validation.txt', 'attack-data-play-validation.txt']:
        shutil.copy2(FIXTURE/name, ROOT/'Tools/Weapons'/name)
    manifest = {str(p.relative_to(ROOT)): digest(p) for p in outputs}
    (ROOT/'Tools/Weapons/attack-data-export.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('Exported', len(outputs), 'validated files; animation curves and combat values preserved.')

if __name__ == '__main__':
    main()
