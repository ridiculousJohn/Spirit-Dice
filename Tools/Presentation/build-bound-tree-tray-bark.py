"""Build the Bound Tree tray's root bark texture from the encounter's own painting.

The Bound Tree's tray keeps the merchant tray's geometry and is re-skinned as the dark, wet bark of the exposed root
the player plays on. Its slats sample one tileable texture, Art/BoundTree/Tray/BoundTreeTrayRoot.png, with the
dice surface shader's painted-texture surface kind (9), which maps it at a fixed physical scale on each face.

The texture is cut from the scene concept, Art/BoundTree/BoundTreeOriginal.png (941 x 1672), so the tray matches the
root surface around it:
  1. CROP, a band of the root's flat top where its bark, cracks and the moss in them run across the frame, as the
     slats do, is converted to linear light.
  2. Its painted lighting (the moonlit wet sheen on the right, the shade on the left) is flattened: each pixel's
     luminance is divided by its Gaussian low-pass (FLATTEN_SIGMA, factor clamped to FLATTEN_LIMITS), so cracks,
     scales, glints and the moss's colour stay while the large-scale light goes.
  3. It is resized to SIZE, which also stretches it vertically to undo the painting's foreshortening of those rows.
  4. It is made tileable, one direction after the other: a copy shifted by half a tile fills a band of BLEND of
     each dimension at the edges, blended about the two images' low-pass means so the bark keeps its contrast.
  5. It is scaled per channel so that, lit as an upward face of the tray is lit, its median is the median painted
     colour of TARGET, the part of the root's surface the tray covers. The tray's light on an upward face is the
     table scene lighting's receiver ambient plus its receiver key times the key's upward component times 0.92 (the
     shader's diffuse factor), read from Content/Presentation/BoundTree/BoundTreeSceneLighting.asset, so the texture
     is the bark's albedo under that lighting rather than a copy of the moonlit painting.

Python 3.10 or later with numpy, Pillow and scipy; on this machine use
Docs/work/merchant-table-clear/.venv/Scripts/python.exe. Run from any directory:

  python Tools/Presentation/build-bound-tree-tray-bark.py
  python Tools/Presentation/build-bound-tree-tray-bark.py --write-assets

The default writes a fresh ignored Docs/work/bound-tree-tray-bark-* directory with the texture, review images
(review-tiled.png: the texture tiled 2 x 2; review-lit.png: the painted CROP and TARGET above the texture lit as an
upward tray face, at the painting's scale) and report.json (crop, target, lighting, per-channel medians, clipped
texels). --output chooses an unused directory under Docs/work. --write-assets also writes BoundTreeTrayRoot.png to
Dice-Unity/Assets/Dice/Art/BoundTree/Tray and keeps its .meta file. The painting and the lighting asset are only
read, so repeated runs give the same texture.
"""

import argparse
import json
from pathlib import Path
import re
import tempfile

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Dice-Unity/Assets/Dice"
SOURCE = ASSETS / "Art/BoundTree/BoundTreeOriginal.png"
LIGHTING = ASSETS / "Content/Presentation/BoundTree/BoundTreeSceneLighting.asset"
ART = ASSETS / "Art/BoundTree/Tray"
WORK = ROOT / "Docs/work"
TEXTURE = "BoundTreeTrayRoot.png"

# Concept pixels (x0, y0, x1, y1). CROP is the band of the root's top whose bark and mossy cracks run across the
# frame; TARGET is the root surface the tray covers in the 540 x 960 table frame (screen rows 590 to 700).
CROP = (40, 1205, 920, 1455)
TARGET = (60, 1025, 880, 1225)
SIZE = (1024, 512)
FLATTEN_SIGMA = 48
FLATTEN_LIMITS = (0.33, 3.0)
BLEND = 0.18
# Low-pass about which the tiling blend keeps the bark's contrast.
BLEND_MEAN_SIGMA = 16
# The dice surface shader's diffuse factor on the receiver key light.
KEY_DIFFUSE = 0.92
LUMA = np.array([0.2126, 0.7152, 0.0722])


def to_linear(srgb):
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)


def to_srgb(linear):
    linear = np.clip(linear, 0, 1)
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * linear ** (1 / 2.4) - 0.055)


def read_lighting():
    """The scene lighting asset's receiver ambient, key colour (sRGB, linearised as Color.linear) and key direction."""
    text = LIGHTING.read_text(encoding="utf-8")

    def vector(name, keys):
        match = re.search(r"^\s*" + name + r": \{([^}]*)\}", text, re.MULTILINE)
        if match is None:
            raise SystemExit(f"{LIGHTING.name} has no {name}")
        values = dict(part.strip().split(": ") for part in match.group(1).split(","))
        return np.array([float(values[key]) for key in keys])

    ambient = to_linear(vector("ReceiverAmbient", "rgb"))
    key = to_linear(vector("ReceiverKeyColor", "rgb"))
    direction = vector("ReceiverKeyDirection", "xyz")
    direction /= np.linalg.norm(direction)
    upward = ambient + key * max(direction[1], 0.0) * KEY_DIFFUSE
    return dict(ambient=ambient, key=key, direction=direction, upward=upward)


def low_pass(image, sigma, mode):
    return np.stack([ndimage.gaussian_filter(image[..., c], sigma, mode=mode) for c in range(image.shape[2])], axis=2)


def flatten(linear):
    luminance = linear @ LUMA
    smooth = ndimage.gaussian_filter(luminance, FLATTEN_SIGMA, mode="reflect")
    factor = np.clip(np.median(smooth) / np.maximum(smooth, 1e-6), *FLATTEN_LIMITS)
    return linear * factor[..., None]


def resize(linear):
    channels = [Image.fromarray(linear[..., c].astype(np.float32), mode="F").resize(SIZE, Image.LANCZOS)
                for c in range(3)]
    return np.clip(np.stack([np.asarray(channel) for channel in channels], axis=2), 0, None)


def tileable_along(linear, axis):
    """Fills the edge bands across one axis from a copy shifted by half a tile along it. The copy is continuous
    across the tile's wrap and has its own seam at the middle, where the image keeps full weight. One axis at a
    time, so the second pass shifts an image that already wraps along the first and adds no seam to it."""
    count = linear.shape[axis]
    shift = count // 2
    distance = np.minimum(np.arange(count) + 0.5, count - np.arange(count) - 0.5) / (BLEND * count)
    t = np.clip(distance, 0, 1)
    weight = t * t * (3 - 2 * t)
    weight = weight[:, None, None] if axis == 0 else weight[None, :, None]
    other = 1 - weight
    shifted = np.roll(linear, shift, axis=axis)
    mean = low_pass(linear, BLEND_MEAN_SIGMA, "wrap")
    shifted_mean = np.roll(mean, shift, axis=axis)
    blended_mean = mean * weight + shifted_mean * other
    detail = ((linear - mean) * weight + (shifted - shifted_mean) * other) / np.sqrt(weight ** 2 + other ** 2)
    return np.clip(blended_mean + detail, 0, None)


def tileable(linear):
    return tileable_along(tileable_along(linear, 1), 0)


def region(linear, box):
    x0, y0, x1, y1 = box
    return linear[y0:y1, x0:x1]


def build():
    painting = to_linear(np.asarray(Image.open(SOURCE).convert("RGB")).astype(np.float64) / 255)
    if painting.shape[:2] != (1672, 941):
        raise SystemExit(f"{SOURCE.name} is {painting.shape[1]} x {painting.shape[0]}, expected 941 x 1672")
    lighting = read_lighting()
    texture = tileable(resize(flatten(region(painting, CROP))))
    target = np.median(region(painting, TARGET).reshape(-1, 3), axis=0)
    texture_median = np.median(texture.reshape(-1, 3), axis=0)
    gain = target / lighting["upward"] / texture_median
    albedo = texture * gain
    clipped = int(np.count_nonzero((albedo > 1).any(axis=2)))
    report = dict(source=SOURCE.relative_to(ROOT).as_posix(), crop=CROP, target=TARGET, size=SIZE,
                  flatten_sigma=FLATTEN_SIGMA, flatten_limits=FLATTEN_LIMITS, blend=BLEND,
                  lighting=dict((name, value.round(5).tolist()) for name, value in lighting.items()),
                  target_median_linear=target.round(5).tolist(), gain=gain.round(4).tolist(),
                  albedo_median_linear=np.median(np.clip(albedo, 0, 1).reshape(-1, 3), axis=0).round(5).tolist(),
                  clipped_texels=clipped)
    image = Image.fromarray(np.round(to_srgb(albedo) * 255).astype(np.uint8), mode="RGB")
    return image, albedo, painting, lighting, report


def review(output, image, albedo, painting, lighting):
    width, height = image.size
    tiled = Image.new("RGB", (width * 2, height * 2))
    for x in (0, width):
        for y in (0, height):
            tiled.paste(image, (x, y))
    tiled.save(output / "review-tiled.png")

    def picture(linear):
        return Image.fromarray(np.round(to_srgb(linear) * 255).astype(np.uint8), mode="RGB")

    crop = picture(region(painting, CROP))
    target = picture(region(painting, TARGET))
    lit = picture(albedo * lighting["upward"]).resize((CROP[2] - CROP[0], CROP[3] - CROP[1]), Image.LANCZOS)
    sheet = Image.new("RGB", (crop.width, crop.height + target.height + lit.height + 8), (96, 96, 96))
    sheet.paste(crop, (0, 0))
    sheet.paste(target, (0, crop.height + 4))
    sheet.paste(lit, (0, crop.height + target.height + 8))
    sheet.save(output / "review-lit.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--output", type=Path, help="unused directory under Docs/work")
    parser.add_argument("--write-assets", action="store_true", help="also write the texture under Art/BoundTree/Tray")
    arguments = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    if arguments.output is not None:
        output = arguments.output.resolve()
        if WORK.resolve() not in output.parents or output.exists():
            parser.error("--output must name an unused directory under Docs/work")
        output.mkdir(parents=True)
    else:
        output = Path(tempfile.mkdtemp(prefix="bound-tree-tray-bark-", dir=WORK))

    image, albedo, painting, lighting, report = build()
    image.save(output / TEXTURE)
    review(output, image, albedo, painting, lighting)
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(output / TEXTURE)
    print(json.dumps(report, indent=2))
    if arguments.write_assets:
        ART.mkdir(parents=True, exist_ok=True)
        image.save(ART / TEXTURE)
        print(ART / TEXTURE)


if __name__ == "__main__":
    main()
