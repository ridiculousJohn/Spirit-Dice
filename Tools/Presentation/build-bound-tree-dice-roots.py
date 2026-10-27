"""Build the Bound Tree's die root textures from their preserved generated sources.

The binding rule's dice visuals wrap two textures around a die with Shared/RuleDieRoots.shader: the bound die's roots
(dark gnarled roots on transparency, alpha-tested) and the marked die's tendrils (a glow mask of thin creeping
tendrils). Texture u runs around the die's four sides, so both must tile horizontally; texture v is the growth
coordinate from the die's base (bottom row) to the centre of its top face (top row).

The sources are the raw image_gen outputs in Docs/Reference/Art (prompts in BoundTreeDiceRootsPrompt.txt):
  BoundTreeDiceRootsRaw.png     1254 x 1254 RGBA, roots on transparency, empty above row ~345
  BoundTreeDiceTendrilsRaw.png  1254 x 1254 RGB, white tendrils on black, empty above row ~500

For each source the script crops the empty top (ROOTS_TOP, TENDRILS_TOP), turns the tendrils' brightness into
alpha on white (black level TENDRILS_BLACK), stretches the roots' alpha so their opaque core is fully opaque,
evens out the remaining mismatch between the left and right edge columns over SEAM columns so the texture tiles,
and resizes to SIZE with premultiplied Lanczos filtering.

Python 3.10 or later with numpy and Pillow; on this machine use Docs/work/merchant-table-clear/.venv/Scripts/python.exe.
Run from any directory:

  python Tools/Presentation/build-bound-tree-dice-roots.py
  python Tools/Presentation/build-bound-tree-dice-roots.py --write-assets

The default writes a fresh ignored Docs/work/bound-tree-dice-roots-* directory with the textures and review images
(each texture tiled twice across, over grey); --output chooses an unused directory under Docs/work. --write-assets
also writes BoundTreeDiceRoots.png and BoundTreeDiceTendrils.png to Dice-Unity/Assets/Dice/Art/BoundTree/Dice and
keeps their .meta files. Sources are only read, so repeated runs give the same textures.
"""

import argparse
from pathlib import Path
import tempfile

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "Docs/Reference/Art"
ART = ROOT / "Dice-Unity/Assets/Dice/Art/BoundTree/Dice"
WORK = ROOT / "Docs/work"
ROOTS_SOURCE = "BoundTreeDiceRootsRaw.png"
TENDRILS_SOURCE = "BoundTreeDiceTendrilsRaw.png"
ROOTS = "BoundTreeDiceRoots.png"
TENDRILS = "BoundTreeDiceTendrils.png"

SIZE = (1024, 768)
# First source rows kept; the rows above hold nothing.
ROOTS_TOP = 300
TENDRILS_TOP = 420
# Source alpha at which a root is treated as fully opaque (the extraction leaves the roots' core at about 250).
ROOTS_OPAQUE = 248
# Brightness of the tendril source's background noise, mapped to zero alpha.
TENDRILS_BLACK = 18
# Columns over which each edge is eased toward the mean of both edges.
SEAM = 24


def seamless(premultiplied):
    """Eases the outer columns toward the mean of the two edge columns so the left and right edges meet."""
    result = premultiplied.copy()
    mean = (premultiplied[:, :1] + premultiplied[:, -1:]) * 0.5
    for column in range(SEAM):
        weight = (1 - column / SEAM) ** 2
        result[:, column] += (mean[:, 0] - premultiplied[:, 0]) * weight
        result[:, -1 - column] += (mean[:, 0] - premultiplied[:, -1]) * weight
    return np.clip(result, 0, 1)


def resize(premultiplied):
    channels = [Image.fromarray(premultiplied[..., c].astype(np.float32), mode="F").resize(SIZE, Image.LANCZOS)
                for c in range(premultiplied.shape[2])]
    return np.clip(np.stack([np.asarray(channel) for channel in channels], axis=2), 0, 1)


def to_image(premultiplied):
    alpha = premultiplied[..., 3:4]
    color = np.where(alpha > 1e-4, premultiplied[..., :3] / np.maximum(alpha, 1e-4), 0)
    rgba = np.concatenate([np.clip(color, 0, 1), alpha], axis=2)
    return Image.fromarray(np.round(rgba * 255).astype(np.uint8), mode="RGBA")


def build_roots():
    source = np.asarray(Image.open(SOURCES / ROOTS_SOURCE).convert("RGBA")).astype(np.float64) / 255
    source = source[ROOTS_TOP:]
    alpha = np.clip(source[..., 3] * 255 / ROOTS_OPAQUE, 0, 1)
    premultiplied = np.concatenate([source[..., :3] * alpha[..., None], alpha[..., None]], axis=2)
    return to_image(resize(seamless(premultiplied)))


def build_tendrils():
    source = np.asarray(Image.open(SOURCES / TENDRILS_SOURCE).convert("RGB")).astype(np.float64)
    brightness = source[TENDRILS_TOP:].mean(axis=2)
    alpha = np.clip((brightness - TENDRILS_BLACK) / (255 - TENDRILS_BLACK), 0, 1)
    premultiplied = np.concatenate([np.repeat(alpha[..., None], 3, axis=2), alpha[..., None]], axis=2)
    return to_image(resize(seamless(premultiplied)))


def review(image, path):
    """The texture tiled twice across over mid grey, so the seam and the alpha read at a glance."""
    width, height = image.size
    sheet = Image.new("RGBA", (width * 2, height), (96, 96, 96, 255))
    sheet.alpha_composite(image, (0, 0))
    sheet.alpha_composite(image, (width, 0))
    sheet.convert("RGB").save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--output", type=Path, help="unused directory under Docs/work")
    parser.add_argument("--write-assets", action="store_true", help="also write the textures under Art/BoundTree/Dice")
    arguments = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    if arguments.output is not None:
        output = arguments.output.resolve()
        if WORK.resolve() not in output.parents or output.exists():
            parser.error("--output must name an unused directory under Docs/work")
        output.mkdir(parents=True)
    else:
        output = Path(tempfile.mkdtemp(prefix="bound-tree-dice-roots-", dir=WORK))

    textures = {ROOTS: build_roots(), TENDRILS: build_tendrils()}
    for name, image in textures.items():
        image.save(output / name)
        review(image, output / ("review-" + name))
        print(output / name)
    if arguments.write_assets:
        ART.mkdir(parents=True, exist_ok=True)
        for name, image in textures.items():
            image.save(ART / name)
            print(ART / name)


if __name__ == "__main__":
    main()
