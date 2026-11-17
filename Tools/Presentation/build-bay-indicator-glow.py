"""Writes the bay indicator's glow sprite: a white rounded square whose alpha falls smoothly to nothing at its rim.

The bay indicator prefab (Content/Presentation/Shared/BayIndicator.prefab) lays it flat on a dice bay's floor and tints
it with the item's glow colour. Run from the repository root with Python 3.10+; it needs no packages.

    python Tools/Presentation/build-bay-indicator-glow.py [--size 128] [--output PATH]
"""

import argparse
import struct
import zlib
from pathlib import Path

DEFAULT_OUTPUT = Path("Dice-Unity/Assets/Dice/Presentation/Shared/BayIndicatorGlow.png")
# Fraction of the half width that keeps the peak alpha before the falloff begins.
CORE = 0.6
# Alpha at the centre: below 1 so the floor shows through and two items' glows on one bay mix rather than cover.
PEAK_ALPHA = 0.55
# Exponent of the superellipse that shapes the glow: 2 is a disc, higher is squarer, matching a bay's floor.
SHAPE_EXPONENT = 4.0


def alpha_at(distance: float) -> float:
    """Alpha at a distance from the centre, measured from 0 at the centre to 1 at the rim."""
    if distance >= 1.0:
        return 0.0
    if distance <= CORE:
        return PEAK_ALPHA
    t = (distance - CORE) / (1.0 - CORE)
    # Smoothstep falloff, squared so the rim fades out without a visible edge.
    fall = 1.0 - t * t * (3.0 - 2.0 * t)
    return PEAK_ALPHA * fall * fall


def png(size: int) -> bytes:
    rows = bytearray()
    centre = (size - 1) / 2.0
    for y in range(size):
        rows.append(0)
        for x in range(size):
            u = abs(x - centre) / (size / 2.0)
            v = abs(y - centre) / (size / 2.0)
            distance = (u ** SHAPE_EXPONENT + v ** SHAPE_EXPONENT) ** (1.0 / SHAPE_EXPONENT)
            rows += bytes((255, 255, 255, round(255 * alpha_at(distance))))

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(bytes(rows), 9)) + chunk(b"IEND", b"")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--size", type=int, default=128, help="width and height in pixels")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    arguments = parser.parse_args()
    arguments.output.write_bytes(png(arguments.size))
    print("Wrote", arguments.output)


if __name__ == "__main__":
    main()
