"""Deterministic authored prototype audio. Standard library only; no recorded or fetched material."""
import argparse
import hashlib
import json
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
SEED = 731082
TAU = math.tau
ROOT = Path(__file__).resolve().parents[2]


def lowpass(samples, frequency):
    """Circular one-pole filter, including its periodic initial state."""
    retention = math.exp(-TAU * frequency / RATE)
    feed = 1 - retention
    state = 0.0
    for value in samples:
        state = retention * state + feed * value
    state /= 1 - retention ** len(samples)
    result = []
    for value in samples:
        state = retention * state + feed * value
        result.append(state)
    return result


def add_impact(destination, start, amplitude, rng, bone=False, wrap=False):
    frequencies = (570, 883, 1379, 2131) if bone else (181, 267, 421, 683)
    length = min(len(destination), int(RATE * .22))
    variation = rng.uniform(.89, 1.12)
    contact = 0.0
    for i in range(length):
        t = i / RATE
        contact += .32 * (rng.uniform(-1, 1) - contact)
        attack = 1 - math.exp(-t / .0008)
        modes = sum((.53 ** mode) * math.sin(TAU * frequency * variation * t)
                    * math.exp(-t / (.027 + .013 * mode))
                    for mode, frequency in enumerate(frequencies))
        value = amplitude * attack * (modes + .32 * contact * math.exp(-t / .006))
        index = start + i
        if wrap:
            destination[index % len(destination)] += value
        elif index < len(destination):
            destination[index] += value


def roll(variant):
    rng = random.Random(SEED + variant)
    result = [0.] * int(RATE * .54)
    for i in range(12):
        start = int(RATE * (i * .032 + rng.uniform(.002, .014)))
        add_impact(result, start, rng.uniform(.24, .46) * (1 - i * .035), rng, bone=i % 3 != 2)
    add_impact(result, int(RATE * .39), .58, rng)
    add_impact(result, int(RATE * .425), .21, rng, bone=True)
    return [result]


def ambience():
    length = RATE * 12
    channels = []
    for side in range(2):
        rng = random.Random(SEED + 100 + side)
        noise = [rng.uniform(-1, 1) for _ in range(length)]
        rain = lowpass(noise, 1350)
        room = lowpass(noise, 180)
        channels.append([.65 * rain[i] + .30 * room[i]
                         + .014 * math.sin(TAU * 96 * i / RATE)
                         * (1 + .18 * math.sin(TAU * i / length)) for i in range(length)])
        # Quiet window droplets supply intermittent texture over the softened rain bed.
        for _ in range(115):
            add_impact(channels[-1], rng.randrange(length), rng.uniform(.014, .030), rng, wrap=True)
    return channels


def spirit():
    rng = random.Random(SEED + 200)
    length = int(RATE * .66)
    airy = lowpass([rng.uniform(-1, 1) for _ in range(length)], 900)
    lower = lowpass(airy, 240)
    result = []
    for i in range(length):
        t = i / RATE
        envelope = math.sin(math.pi * i / (length - 1)) ** 2
        result.append(envelope * (.7 * (airy[i] - lower[i])
                      + .12 * math.sin(TAU * 326 * t) * math.exp(-t * 4)
                      + .07 * math.sin(TAU * 491 * t) * math.exp(-t * 5)))
    return [result]


def score():
    result = [0.] * int(RATE * .12)
    add_impact(result, 0, .35, random.Random(SEED + 300), bone=True)
    return [result]


def stage_clear():
    rng = random.Random(SEED + 400)
    result = [0.] * int(RATE * .88)
    for start, frequency, amplitude in ((.005, 196, .36), (.14, 294, .23), (.29, 392, .19)):
        add_impact(result, int(RATE * start), amplitude * .25, rng)
        for i in range(int(RATE * .57)):
            t = i / RATE
            index = int(RATE * start) + i
            if index >= len(result):
                break
            envelope = (1 - math.exp(-t / .005)) * math.exp(-t / .14)
            result[index] += amplitude * envelope * (math.sin(TAU * frequency * t)
                             + .22 * math.sin(TAU * frequency * 2.01 * t))
    return [result]


def creak():
    rng = random.Random(SEED + 500)
    length = int(RATE * 1.1)
    friction = lowpass([rng.uniform(-1, 1) for _ in range(length)], 700)
    result = []
    phase = 0.
    for i in range(length):
        t = i / RATE
        phase += TAU * (148 + 23 * math.sin(t * 4) + 2 * math.sin(t * 47)) / RATE
        envelope = math.sin(math.pi * i / (length - 1)) ** 2
        strain = .60 + .40 * math.sin(t * 31) ** 6
        result.append(envelope * strain * (.33 * math.sin(phase)
                      + .09 * math.sin(phase * 2.03) + .16 * friction[i]))
    return [result]


def pcm(channels, target_peak, loop=False):
    length = len(channels[0])
    if not loop:
        fade = int(RATE * .006)
        for channel in channels:
            for i in range(fade):
                channel[i] *= i / fade
                channel[-i - 1] *= i / fade
    peak = max(abs(value) for channel in channels for value in channel)
    scale = target_peak / peak
    samples = [round(channels[c][i] * scale * 32767)
               for i in range(length) for c in range(len(channels))]
    data = struct.pack('<' + 'h' * len(samples), *samples)
    rms = math.sqrt(sum((sample / 32767) ** 2 for sample in samples) / len(samples))
    seam = max(abs(channels[c][0] - channels[c][-1]) * scale for c in range(len(channels)))
    return data, {"duration_seconds": length / RATE, "channels": len(channels),
                  "peak_dbfs": round(20 * math.log10(target_peak), 3),
                  "rms_dbfs": round(20 * math.log10(rms), 3),
                  "boundary_step": round(seam, 7), "clipped_samples": sum(abs(s) >= 32767 for s in samples)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'Docs/work/audio-generated/prototype',
                        help='Destination for generated WAV files; defaults to ignored working output.')
    parser.add_argument('--provenance', type=Path,
                        help='Recipe manifest path; defaults to provenance.json in the output directory.')
    parser.add_argument('--verify', action='store_true', help='Compare existing files without writing.')
    args = parser.parse_args()
    if not args.verify:
        args.output.mkdir(parents=True, exist_ok=True)
    recipes = [(f'bone_wood_roll_{i + 1:02d}', roll(i), .46, False) for i in range(3)]
    recipes += [('rain_room_lantern_loop', ambience(), .09, True),
                ('spirit_air', spirit(), .17, False), ('score_bone_tick', score(), .23, False),
                ('stage_clear_warm', stage_clear(), .30, False), ('room_creak', creak(), .19, False)]
    provenance = {"authoring": "Original mathematical synthesis authored for the Spirit House prototype.",
                  "license": "Project-authored material; no third-party samples or external media.",
                  "generator": "Tools/Audio/generate_spirit_audio.py", "seed": SEED,
                  "sample_rate": RATE, "encoding": "signed 16-bit PCM WAV", "clips": {}}
    for name, channels, peak, loop in recipes:
        import io
        data, stats = pcm(channels, peak, loop)
        buffer = io.BytesIO()
        with wave.open(buffer, 'wb') as writer:
            writer.setnchannels(len(channels))
            writer.setsampwidth(2)
            writer.setframerate(RATE)
            writer.writeframes(data)
        payload = buffer.getvalue()
        path = args.output / (name + '.wav')
        if path.exists():
            if path.read_bytes() != payload:
                raise RuntimeError(f'Existing authored file differs; refusing replacement: {path}')
        elif args.verify:
            raise RuntimeError(f'Missing authored file: {path}')
        else:
            path.write_bytes(payload)
        stats.update(loop=loop, sha256=hashlib.sha256(payload).hexdigest())
        provenance['clips'][path.name] = stats
        assert stats['clipped_samples'] == 0
        print(name, json.dumps(stats))
    path = args.provenance or args.output / 'provenance.json'
    payload = json.dumps(provenance, indent=2) + '\n'
    if path.exists():
        if path.read_text(encoding='utf-8') != payload:
            raise RuntimeError('Existing provenance differs; refusing replacement.')
    elif args.verify:
        raise RuntimeError(f'Missing provenance file: {path}')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(payload, encoding='utf-8')


if __name__ == '__main__':
    main()
