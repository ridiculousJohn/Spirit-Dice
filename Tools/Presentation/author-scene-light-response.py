"""Rasterize authored distance and source-mask data for fixed-view scene lighting."""

import argparse
import json
import math
from pathlib import Path
import struct
import zlib


def smoothstep(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def triangle(data, width, height, vertices):
    a, b, c = vertices
    denominator = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(denominator) < 1e-9:
        raise ValueError("Distance triangles must have nonzero area.")
    x0 = max(0, math.floor(min(v[0] for v in vertices) * width))
    x1 = min(width, math.ceil(max(v[0] for v in vertices) * width))
    y0 = max(0, math.floor(min(v[1] for v in vertices) * height))
    y1 = min(height, math.ceil(max(v[1] for v in vertices) * height))
    for y in range(y0, y1):
        v = (y + 0.5) / height
        for x in range(x0, x1):
            u = (x + 0.5) / width
            wa = ((b[1] - c[1]) * (u - c[0]) + (c[0] - b[0]) * (v - c[1])) / denominator
            wb = ((c[1] - a[1]) * (u - c[0]) + (a[0] - c[0]) * (v - c[1])) / denominator
            wc = 1.0 - wa - wb
            if min(wa, wb, wc) >= -1e-8:
                data[(y * width + x) * 3] = round(255 * max(0.0, min(1.0, wa * a[2] + wb * b[2] + wc * c[2])))


def rasterize(config):
    width, height = config["size"]
    if not (isinstance(width, int) and isinstance(height, int) and 0 < width <= 8192 and 0 < height <= 8192):
        raise ValueError("Map dimensions must be integers from 1 to 8192.")
    data = bytearray([255, 0, 0]) * (width * height)
    for patch in config["distance_patches"]:
        vertices = patch["vertices"]
        for face in patch["triangles"]:
            triangle(data, width, height, [vertices[index] for index in face])
    for source in config["source_masks"]:
        cx, cy = source["center"]
        rx, ry = source["radius"]
        inner = source["inner_fraction"]
        strength = source["strength"]
        if rx <= 0 or ry <= 0 or not 0 <= inner < 1 or not 0 <= strength <= 1:
            raise ValueError("Source mask parameters are invalid.")
        for y in range(max(0, int((cy - ry) * height)), min(height, math.ceil((cy + ry) * height))):
            for x in range(max(0, int((cx - rx) * width)), min(width, math.ceil((cx + rx) * width))):
                distance = math.hypot(((x + 0.5) / width - cx) / rx, ((y + 0.5) / height - cy) / ry)
                weight = strength * (1.0 - smoothstep((distance - inner) / (1.0 - inner)))
                offset = (y * width + x) * 3 + 1
                data[offset] = max(data[offset], round(255 * weight))
    return width, height, data


def png_chunk(kind, payload):
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xffffffff)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New PNG path; existing files are never replaced.")
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Output already exists; choose a new path.")
    config = json.loads(args.config.read_text(encoding="utf-8"))
    width, height, data = rasterize(config)
    scanlines = b"".join(b"\x00" + data[y * width * 3:(y + 1) * width * 3] for y in range(height))
    encoded = b"\x89PNG\r\n\x1a\n"
    encoded += png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    encoded += png_chunk(b"IDAT", zlib.compress(scanlines))
    encoded += png_chunk(b"IEND", b"")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("xb") as output:
        output.write(encoded)
    print(f"Response map: {args.output.resolve()} ({width} x {height})")


if __name__ == "__main__":
    main()
