"""Create quiet fox gesture auditions from filtered noise without tonal oscillators."""
import argparse
import hashlib
import io
import json
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
ROOT = Path(__file__).resolve().parents[2]
REFERENCE = ROOT / 'Dice-Unity/Assets/Dice/Presentation/Resources/SpiritAudio/spirit_air.wav'


def reference_rms(path):
    with wave.open(str(path), "rb") as reader:
        assert reader.getsampwidth() == 2 and reader.getnchannels() == 1
        samples = struct.unpack("<" + "h" * reader.getnframes(), reader.readframes(reader.getnframes()))
    return math.sqrt(sum((value / 32768) ** 2 for value in samples) / len(samples))


def whoosh(seed, duration, cutoff, attack):
    rng = random.Random(seed)
    length = round(duration * RATE)
    rumble = first = second = movement = 0.0
    highpass_feed = 1 - math.exp(-math.tau * 120 / RATE)
    movement_feed = 1 - math.exp(-math.tau * 9 / RATE)
    result = []
    for i in range(length):
        position = i / (length - 1)
        noise = rng.uniform(-1, 1)
        rumble += highpass_feed * (noise - rumble)
        # Two smooth low-pass stages suppress hiss; cutoff follows the gesture's broad swell.
        sweep = 320 + cutoff * math.sin(math.pi * position) ** 2
        feed = 1 - math.exp(-math.tau * sweep / RATE)
        first += feed * (noise - rumble - first)
        second += feed * (first - second)
        movement += movement_feed * (rng.uniform(-1, 1) - movement)
        if position < attack:
            envelope = .5 - .5 * math.cos(math.pi * position / attack)
        else:
            envelope = (.5 + .5 * math.cos(math.pi * (position - attack) / (1 - attack))) ** 1.4
        result.append(second * envelope * (1 + .4 * movement))
    return result


def encode(samples):
    assert all(abs(value) < 1 for value in samples)
    pcm = [round(value * 32767) for value in samples]
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(struct.pack("<" + "h" * len(pcm), *pcm))
    return buffer.getvalue()


def write_new(path, payload):
    if path.exists():
        if path.read_bytes() != payload:
            raise RuntimeError("Refusing to replace a differing file: " + str(path))
    else:
        with path.open("xb") as output:
            output.write(payload)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", type=Path, default=REFERENCE,
                        help="RMS reference WAV; defaults to the repository's original spirit cue.")
    parser.add_argument("--output", type=Path, default=ROOT / "Docs/work/audio-generated/soft-whooshes",
                        help="Destination for WAV files, audition, and recipe manifest; defaults to ignored working output.")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    original_rms = reference_rms(args.reference)
    target_rms = original_rms * 10 ** (-12 / 20)
    recipes = [(901, .72, 470, .32), (902, .86, 580, .38), (903, .65, 390, .29)]
    manifest = {
        "authoring": "Project-authored filtered noise; no third-party samples or tonal oscillators.",
        "generator": "Tools/Audio/generate_soft_whooshes.py",
        "reference": (args.reference.resolve().relative_to(ROOT).as_posix()
                      if args.reference.resolve().is_relative_to(ROOT) else str(args.reference.resolve())),
        "reference_sha256": hashlib.sha256(args.reference.read_bytes()).hexdigest(),
        "relative_rms_db": -12,
        "audition_playback_gain": .65,
        "sample_rate": RATE,
        "clips": [],
    }
    audition = [0.0] * round(.3 * RATE)
    for number, (seed, duration, cutoff, attack) in enumerate(recipes, 1):
        samples = whoosh(seed, duration, cutoff, attack)
        rms = math.sqrt(sum(value * value for value in samples) / len(samples))
        samples = [value * target_rms / rms for value in samples]
        payload = encode(samples)
        filename = "fox_whoosh_" + str(number).zfill(2) + ".wav"
        write_new(args.output / filename, payload)
        manifest["clips"].append({
            "file": filename, "seed": seed, "duration_seconds": duration,
            "maximum_cutoff_hz": 320 + cutoff, "attack_fraction": attack,
            "peak_dbfs": round(20 * math.log10(max(abs(value) for value in samples)), 2),
            "rms_dbfs": round(20 * math.log10(target_rms), 2),
            "sha256": hashlib.sha256(payload).hexdigest(),
        })
        audition.extend(value * .65 for value in samples)
        audition.extend([0.0] * round(.75 * RATE))
    write_new(args.output / "fox_whoosh_audition.wav", encode(audition))
    write_new(args.output / "provenance.json", (json.dumps(manifest, indent=2) + "\n").encode("utf-8"))
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
