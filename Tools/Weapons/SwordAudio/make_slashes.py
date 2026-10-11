"""Derive three short sword swishes from the supplied reference, with a common strike peak."""
from pathlib import Path
import json, wave, hashlib
import numpy as np
from scipy import signal

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Prefabs/Weapon/weapon_001/Audio'
SR = 44100
PEAK_TIME = .010  # Damage detection starts playback; peak follows within 10 ms.

def stretch(x, length):
    nfft, hop = 1024, 128
    _, _, spectrum = signal.stft(x, nperseg=nfft, noverlap=nfft-hop)
    rate = len(x) / length
    steps = np.arange(0, spectrum.shape[1]-1, rate)
    phase = np.angle(spectrum[:, 0])
    advance = 2*np.pi*hop*np.arange(spectrum.shape[0])/nfft
    result = np.empty((spectrum.shape[0], len(steps)), dtype=complex)
    for i, step in enumerate(steps):
        frame = int(step); frac = step-frame
        magnitude = (1-frac)*np.abs(spectrum[:, frame])+frac*np.abs(spectrum[:, frame+1])
        result[:, i] = magnitude*np.exp(1j*phase)
        delta = np.angle(spectrum[:, frame+1])-np.angle(spectrum[:, frame])-advance
        delta -= 2*np.pi*np.round(delta/(2*np.pi))
        phase += advance+delta
    _, audio = signal.istft(result, nperseg=nfft, noverlap=nfft-hop)
    return np.pad(audio, (0, max(0, length-len(audio))))[:length]

def write(path, x):
    path.parent.mkdir(parents=True, exist_ok=True)
    x = np.clip(x, -1, 1)
    pcm = np.round(x*32767).astype('<i2')
    pcm[0] = pcm[-1] = 0
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(SR); f.writeframes(pcm.tobytes())

def main():
    decoded = ROOT/'Temp/WeaponProduction/sword-reference.wav'
    with wave.open(str(decoded)) as f:
        rate = f.getframerate()
        original = np.frombuffer(f.readframes(f.getnframes()), '<i2').astype(float)/32768
    # Remove the reference's long lead-in; keep its actual 0.4-second swish.
    source = original[round(3.135*rate):round(3.555*rate)]
    source = signal.resample_poly(source, 147, 160) if rate == 48000 else signal.resample(source, round(len(source)*SR/rate))
    source -= source.mean()
    high = signal.butter(2, 100, 'highpass', fs=SR, output='sos')
    source = signal.sosfilt(high, source)
    records, clips = [], []
    for index, (duration, semitones, body) in enumerate([(0.160, .4, .06), (0.200, -.35, .16), (0.180, -1.35, .30)], 1):
        pitch = 2**(semitones/12)
        pitched = signal.resample(source, round(len(source)/pitch))
        x = stretch(pitched, round(duration*SR))
        bass = signal.sosfilt(signal.butter(2, 950, 'lowpass', fs=SR, output='sos'), x)
        x = x + body*bass
        width = round(.006*SR)
        rms = np.sqrt(np.convolve(x*x, np.ones(width)/width, 'same'))
        shift = round(PEAK_TIME*SR)-int(rms.argmax())
        aligned = np.zeros_like(x)
        if shift >= 0:
            aligned[shift:] = x[:len(x)-shift]
        else:
            aligned[:len(x)+shift] = x[-shift:]
        t = np.arange(len(x))/SR
        envelope = np.exp(-.5*((t-PEAK_TIME)/np.where(t<PEAK_TIME, .026, .035+index*.004))**2)
        x = aligned*envelope
        fade_in, fade_out = round(.004*SR), round(.012*SR)
        x[:fade_in] *= np.linspace(0, 1, fade_in)**2
        x[-fade_out:] *= np.linspace(1, 0, fade_out)**2
        x -= x.mean()
        x[:fade_in] *= np.linspace(0, 1, fade_in)
        x[-fade_out:] *= np.linspace(1, 0, fade_out)
        x *= 10**(-3/20)/np.max(np.abs(x))
        x[0] = x[-1] = 0
        name = f'SwordSlash_0{index}.wav'
        write(OUT/name, x); clips.append(x)
        rms = np.sqrt(np.convolve(x*x, np.ones(width)/width, 'same'))
        rms_peak = float(rms.argmax()/SR)
        assert abs(rms_peak-PEAK_TIME) < .01, (name, rms_peak)
        records.append(dict(file=name, duration=duration, strike_peak=rms_peak, peak_dbfs=-3, pitch_semitones=semitones, sample_rate=SR, channels=1, sha256=hashlib.sha256((OUT/name).read_bytes()).hexdigest()))
    write(ROOT/'Tools/Weapons/SwordAudio/ThreeSlashPreview.wav', np.concatenate([np.concatenate([x, np.zeros(round(SR*.3))]) for x in clips]))
    hit_times = [[.4], [.53333336, .8666667], [.4, .73333335, 1.4]]
    durations = [1.4666668, 1.8000001, 3.3000002]
    timeline = []
    for clip, hits, duration in zip(clips, hit_times, durations):
        track = np.zeros(round(SR*duration))
        for hit in hits:
            start = round(hit*SR)
            track[start:start+len(clip)] += clip
        timeline.extend([track, np.zeros(round(.3*SR))])
    write(ROOT/'Tools/Weapons/SwordAudio/SwordComboTimingPreview.wav', np.concatenate(timeline))
    manifest = dict(reference='poppop.ai - sword slash sound.mp3', source_region=[3.135, 3.555], shared_strike_peak=PEAK_TIME, clips=records, hit_times=hit_times, trigger='damage detection', audio_event_times=hit_times)
    (ROOT/'Tools/Weapons/SwordAudio/slashes.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(records, indent=2))

if __name__ == '__main__':
    main()
