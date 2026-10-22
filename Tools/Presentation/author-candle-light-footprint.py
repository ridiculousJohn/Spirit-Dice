"""Derive the haunted merchant candle's centered light footprint and response map from their preserved sources.

The movable candle lights the table through two aligned 941 x 1672 data textures that its piece light translates
with the candle: a gray footprint (the contribution, multiplied by the table's ambient color) and a linear response
map whose red channel is the normalized distance from the candle. Their preserved sources are
Docs/Reference/Art/HauntedMerchantCandleFootprintSource.png and HauntedMerchantCandleResponseSource.png.

This script scales both sources about the candle point (the response map's distance minimum) so the pool of light
reaches REACH times as far in every direction, its sharp far edge included. The value at the candle point and the
center of the brightest texels are unchanged, and the response distance stays normalized to the scaled reach. Texels sampled from outside the source
read as unlit (footprint 0, distance 1), so the footprint keeps its dark border.

Python 3.9 or later, no third-party packages. Run from any directory:

  python Tools/Presentation/author-candle-light-footprint.py
  python Tools/Presentation/author-candle-light-footprint.py --write-assets

The default writes a fresh ignored Docs/work/candle-footprint-* directory; --output chooses an unused directory
under Docs/work. --write-assets replaces CandleLightFootprintCentered.png and CandleLightResponseCentered.png under
Dice-Unity/Assets/Dice/Art/HauntedMerchant and keeps their .meta files. Source files are only read.
"""

import argparse
import math
from pathlib import Path
import struct
import tempfile
import zlib

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "Docs/Reference/Art"
ART = ROOT / "Dice-Unity/Assets/Dice/Art/HauntedMerchant"
WORK = ROOT / "Docs/work"
FOOTPRINT_SOURCE = "HauntedMerchantCandleFootprintSource.png"
RESPONSE_SOURCE = "HauntedMerchantCandleResponseSource.png"
FOOTPRINT = "CandleLightFootprintCentered.png"
RESPONSE = "CandleLightResponseCentered.png"
SIZE = (941, 1672)

# Reach of the pool relative to its source: 0.8 keeps a smaller, tighter pool on the table.
REACH = 0.8


def read_png(path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG.")
    offset, chunks = 8, []
    header = None
    while offset < len(data):
        length, kind = struct.unpack(">I4s", data[offset:offset + 8])
        body = data[offset + 8:offset + 8 + length]
        offset += 12 + length
        if kind == b"IHDR":
            header = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            chunks.append(body)
    width, height, depth, color, _, _, interlace = header
    if depth != 8 or color != 2 or interlace != 0:
        raise ValueError(f"{path} must be a non-interlaced 8-bit RGB PNG.")
    raw = zlib.decompress(b"".join(chunks))
    stride, channels = width * 3, 3
    pixels = bytearray(height * stride)
    previous = bytearray(stride)
    position = 0
    for y in range(height):
        kind = raw[position]
        line = bytearray(raw[position + 1:position + 1 + stride])
        position += 1 + stride
        if kind == 1:
            for x in range(channels, stride):
                line[x] = (line[x] + line[x - channels]) & 255
        elif kind == 2:
            for x in range(stride):
                line[x] = (line[x] + previous[x]) & 255
        elif kind == 3:
            for x in range(stride):
                left = line[x - channels] if x >= channels else 0
                line[x] = (line[x] + (left + previous[x]) // 2) & 255
        elif kind == 4:
            for x in range(stride):
                a = line[x - channels] if x >= channels else 0
                b = previous[x]
                c = previous[x - channels] if x >= channels else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        elif kind != 0:
            raise ValueError(f"{path} uses unknown PNG filter {kind}.")
        pixels[y * stride:(y + 1) * stride] = line
        previous = line
    return width, height, pixels


def write_png(path, width, height, pixels):
    rows = b"".join(b"\x00" + bytes(pixels[y * width * 3:(y + 1) * width * 3]) for y in range(height))

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b""))


def channel(width, height, pixels, index):
    return [pixels[i * 3 + index] for i in range(width * height)]


def candle_point(width, height, distance):
    """Centroid of the response map's minimum-distance texels, in texel coordinates (top-left origin)."""
    lowest = min(distance)
    xs = ys = count = 0
    for i, value in enumerate(distance):
        if value == lowest:
            xs += i % width
            ys += i // width
            count += 1
    return xs / count, ys / count


def resample(width, height, values, anchor, outside):
    ax, ay = anchor
    result = bytearray(width * height)
    for y in range(height):
        sy = ay + (y - ay) / REACH
        y0 = math.floor(sy)
        fy = sy - y0
        for x in range(width):
            sx = ax + (x - ax) / REACH
            x0 = math.floor(sx)
            fx = sx - x0

            def texel(tx, ty):
                return values[ty * width + tx] if 0 <= tx < width and 0 <= ty < height else outside

            top = texel(x0, y0) * (1 - fx) + texel(x0 + 1, y0) * fx
            bottom = texel(x0, y0 + 1) * (1 - fx) + texel(x0 + 1, y0 + 1) * fx
            result[y * width + x] = max(0, min(255, round(top * (1 - fy) + bottom * fy)))
    return result


def gray(width, height, values):
    pixels = bytearray(width * height * 3)
    for i, value in enumerate(values):
        pixels[i * 3:i * 3 + 3] = bytes((value, value, value))
    return pixels


def red(width, height, values):
    pixels = bytearray(width * height * 3)
    for i, value in enumerate(values):
        pixels[i * 3] = value
    return pixels


def author():
    width, height, footprint = read_png(SOURCES / FOOTPRINT_SOURCE)
    rw, rh, response = read_png(SOURCES / RESPONSE_SOURCE)
    if (width, height) != SIZE or (rw, rh) != SIZE:
        raise ValueError("Candle light sources must be 941 x 1672.")
    distance = channel(rw, rh, response, 0)
    if any(response[i * 3 + 1] or response[i * 3 + 2] for i in range(rw * rh)):
        raise ValueError("The candle response source must contain distance only.")
    anchor = candle_point(width, height, distance)
    light = resample(width, height, channel(width, height, footprint, 0), anchor, 0)
    reach = resample(width, height, distance, anchor, 255)
    for x in range(width):
        if light[x] or light[(height - 1) * width + x]:
            raise ValueError("The resampled footprint must stay dark at its border.")
    for y in range(height):
        if light[y * width] or light[y * width + width - 1]:
            raise ValueError("The resampled footprint must stay dark at its border.")
    return width, height, anchor, gray(width, height, light), red(width, height, reach)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", help="unused directory under Docs/work for the derived textures")
    parser.add_argument("--write-assets", action="store_true", help="replace the installed footprint and response map")
    args = parser.parse_args()
    width, height, anchor, footprint, response = author()
    if args.write_assets:
        targets = ART
        for name in (FOOTPRINT, RESPONSE):
            if not (ART / (name + ".meta")).exists():
                raise FileNotFoundError(f"{name}.meta is missing; import the texture before replacing it.")
    elif args.output:
        targets = (ROOT / args.output).resolve()
        if WORK.resolve() not in targets.parents:
            raise ValueError("--output must be under Docs/work.")
        targets.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        targets = Path(tempfile.mkdtemp(prefix="candle-footprint-", dir=WORK))
    write_png(targets / FOOTPRINT, width, height, footprint)
    write_png(targets / RESPONSE, width, height, response)
    print(f"Candle point ({anchor[0]:.2f}, {anchor[1]:.2f}), reach {REACH}: wrote {targets}")


if __name__ == "__main__":
    main()
