"""Generate original, deterministic stone-fist swing sounds (no recorded samples)."""
from pathlib import Path
import array
import json
import math
import random
import wave

ROOT = Path(__file__).resolve().parents[2]
RATE = 44100


def generate(name, duration, seed, weight):
    random_source = random.Random(seed)
    samples = []
    low = slower = phase = 0.0
    count = round(duration * RATE)
    for i in range(count):
        t = i / RATE
        progress = i / (count - 1)
        noise = random_source.uniform(-1, 1)
        low += 0.14 * (noise - low)
        slower += 0.025 * (low - slower)
        air = low - slower
        phase += 2 * math.pi * (150 - 95 * progress) / RATE
        envelope = math.sin(math.pi * progress) ** 1.5
        grain = math.sin(2 * math.pi * 29 * t) * math.exp(-9 * progress)
        samples.append(envelope * (air * 1.1 + math.sin(phase) * weight + grain * 0.03))
    peak = max(abs(s) for s in samples)
    pcm = array.array('h', (round(s / peak * 0.55 * 32767) for s in samples))
    if __import__('sys').byteorder != 'little':
        pcm.byteswap()
    destination = ROOT / 'Assets/Prefabs/Weapon' / name / 'Audio/unarmed_swing.wav'
    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), 'wb') as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())
    with wave.open(str(destination), 'rb') as check:
        assert check.getnchannels() == 1 and check.getframerate() == RATE
        assert check.getnframes() == count
    assert pcm[0] == 0 and pcm[-1] == 0
    assert max(abs(x) for x in pcm) < 32767
    return dict(path=destination.relative_to(ROOT).as_posix(), duration=duration,
                sample_rate=RATE, channels=1, peak_dbfs=round(20 * math.log10(0.55), 2),
                rms_dbfs=round(20 * math.log10(math.sqrt(sum(s*s for s in samples)/count)/peak*0.55), 2),
                source='Original synthesized noise and oscillators; no external samples',
                status='Generated, validated; awaiting user AudioClip field and animation event binding')


if __name__ == '__main__':
    results = [generate('MossstoneFists', 0.28, 1101, 0.075),
               generate('ReinforcedFists', 0.34, 1102, 0.13)]
    manifest = ROOT / 'art_out/weapon_production/unarmed_audio.json'
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(results, indent=2))
