"""Validate the exported OBJ normal data consumed by model importers."""
from pathlib import Path
import math
path=Path(__file__).resolve().parents[2]/'Assets/Art/Weapons/FalconOath/FalconOath.obj'
lines=path.read_text().splitlines()
normals=[tuple(map(float,line.split()[1:])) for line in lines if line.startswith('vn ')]
faces=[line.split()[1:] for line in lines if line.startswith('f ')]
assert normals, 'OBJ contains no normal records'
for normal in normals:
    assert all(math.isfinite(x) for x in normal)
    assert abs(sum(x*x for x in normal)-1)<1e-6, 'Non-unit normal'
for face in faces:
    for vertex in face:
        fields=vertex.split('/')
        assert len(fields)==3 and fields[2], 'Face missing normal reference'
        assert 1<=int(fields[2])<=len(normals), 'Invalid normal index'
print(f'PASS: {len(faces)} faces reference valid unit normals ({len(normals)} records).')
