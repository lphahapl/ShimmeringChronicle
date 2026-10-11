"""Check Unity GUID references and the read-only animation/model sources."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

tools = Path(__file__).resolve().parent
project = tools.parents[2]
folder = project / 'Assets/Prefabs/Player/KeqingRootMotion'
guids = set()
for base in (project / 'Assets',):
    for path in base.rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})', path.read_text(encoding='utf-8-sig'), re.M)
        if match:
            guids.add(match[1])
references = []
for path in folder.rglob('*'):
    if path.suffix not in ('.prefab', '.unity', '.controller', '.asset', '.mat', '.anim'):
        continue
    for guid in set(re.findall(r'guid: ([0-9a-f]{32})', path.read_text(encoding='utf-8-sig'))):
        if guid.startswith('0000000000000000'):
            continue
        references.append((str(path.relative_to(project)), guid))
unresolved = {guid for _, guid in references if guid not in guids}
if unresolved:
    cache = project / 'Library/PackageCache'
    result = subprocess.run(['rg', '-l', '-g', '*.meta', '|'.join(sorted(unresolved)), str(cache)],
                            capture_output=True, text=True, encoding='utf-8')
    assert result.returncode in (0, 1), result.stderr
    for name in result.stdout.splitlines():
        match = re.search(r'^guid: ([0-9a-f]{32})', Path(name).read_text(encoding='utf-8-sig'), re.M)
        if match:
            guids.add(match[1])
missing = [(name, guid) for name, guid in references if guid not in guids]
assert not missing, f'Missing references: {missing}'
baseline = json.loads((tools / 'source-hashes.json').read_text(encoding='utf-8-sig'))
for name, digest in baseline.items():
    assert hashlib.sha256((project / name).read_bytes()).hexdigest() == digest, f'Source changed: {name}'
assert len(list((folder / 'Animations').glob('*.anim'))) == 14
print(f'PASS: 14 clips; all exported asset GUID references resolve; {len(baseline)} original source files unchanged.')
