"""Encode the native Unity render frames; does not generate or alter animation poses."""
from pathlib import Path
import math
from PIL import Image, ImageDraw

project = Path(__file__).resolve().parents[3]
source = project / 'Temp/KeqingRootMotionBuild/KeqingPreviews'
output = Path(__file__).resolve().parent / 'Previews'
output.mkdir(exist_ok=True)
sequence = [('WalkStart', .32), ('Walk', .96), ('Walk', .96), ('WalkStop', .32),
            ('Idle', .45), ('RunStart', .38), ('Run', .68), ('Run', .68),
            ('RunStop', .38), ('Idle', .45), ('Attack1', .82), ('Attack2', .90),
            ('Attack3', 1.08), ('Idle', .45), ('TurnRight', .46), ('Idle', .45)]
labels = [name for name, duration in sequence for _ in range(math.ceil(duration * 24))]
frames = []
for index, path in enumerate(sorted((source / 'Frames').glob('frame_*.png'))):
    if index % 2:
        continue
    frame = Image.open(path).convert('RGB').resize((360, 396), Image.Resampling.LANCZOS)
    draw = ImageDraw.Draw(frame)
    draw.rounded_rectangle((12, 12, 218, 40), radius=6, fill=(18, 24, 34))
    draw.text((22, 21), 'Keqing / ' + labels[index], fill=(236, 228, 255))
    frames.append(frame)
frames[0].save(output / 'KeqingMotion.gif', save_all=True, append_images=frames[1:],
               duration=84, loop=0, optimize=True, disposal=2)
Image.open(source / 'KeqingActionSheet.png').save(output / 'KeqingActionSheet.png')
print(f'{len(frames)} native-rendered frames: {output}')
