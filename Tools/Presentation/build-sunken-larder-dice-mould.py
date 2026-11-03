"""Build the Sunken Larder's die mould textures from their preserved generated sources.

The rot rule's dice visuals wrap two textures around a die with Shared/RuleDieRoots.shader, on the binding's per-die
shell visual: the mould that creeps up a kept 1 and closes over a withered die (an opaque fur of green-black mould that
blackens toward the top, alpha-tested, so that once it has closed no pip shows through), and the faint spotting of a
kept die (a mask of small mould spots). Texture u runs around the die's four sides, so both must tile horizontally;
texture v is the growth coordinate from the die's base (bottom row) to the centre of its top face (top row).

The sources are the raw image_gen outputs in Docs/Reference/Art (prompts in SunkenLarderDiceMouldPrompt.txt):
  SunkenLarderDiceMouldRaw.png  1254 x 1254 RGB, opaque mould, green and furred at the bottom, near black at the top
  SunkenLarderDiceSpotsRaw.png  1254 x 1254 RGB, grey mould spots on black, densest at the bottom, nearly empty above row ~600

The mould keeps its colour and becomes fully opaque. The spots lose the source's nearly empty top (SPOTS_TOP), so they
reach the top face, and their brightness becomes alpha on white (black level SPOTS_BLACK, full alpha from SPOTS_WHITE),
so the material's tint colours them. Both have the remaining mismatch between
their left and right edge columns evened out over SEAM columns so they tile, and are resized to SIZE with premultiplied
Lanczos filtering, as build-bound-tree-dice-roots.py does for the Bound Tree's roots.

Python 3.10 or later with numpy and Pillow; on this machine use Docs/work/merchant-table-clear/.venv/Scripts/python.exe.
Run from any directory:

  python Tools/Presentation/build-sunken-larder-dice-mould.py
  python Tools/Presentation/build-sunken-larder-dice-mould.py --write-assets

The default writes a fresh ignored Docs/work/sunken-larder-dice-mould-* directory with the textures and review images
(each texture tiled twice across, over bone); --output chooses an unused directory under Docs/work. --write-assets
also writes SunkenLarderDiceMould.png and SunkenLarderDiceSpots.png to Dice-Unity/Assets/Dice/Art/SunkenLarder/Dice and
keeps their .meta files. Sources are only read, so repeated runs give the same textures.
"""

import argparse
from pathlib import Path
import tempfile

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "Docs/Reference/Art"
ART = ROOT / "Dice-Unity/Assets/Dice/Art/SunkenLarder/Dice"
WORK = ROOT / "Docs/work"
MOULD_SOURCE = "SunkenLarderDiceMouldRaw.png"
SPOTS_SOURCE = "SunkenLarderDiceSpotsRaw.png"
MOULD = "SunkenLarderDiceMould.png"
SPOTS = "SunkenLarderDiceSpots.png"

SIZE = (1024, 768)
# First spot source row kept; the rows above hold only a few specks, and the spots must reach the top face.
SPOTS_TOP = 600
# Brightness of the spot source's faint background haze, mapped to zero alpha.
SPOTS_BLACK = 16
# Brightness at which a spot is fully opaque (the source's spot cores reach about 100 to 190).
SPOTS_WHITE = 120
# Columns over which each edge is eased toward the mean of both edges.
SEAM = 24
# Bone die colour behind the review images, so the spots read as they will on a die.
REVIEW_BONE = (232, 222, 200, 255)


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


def build_mould():
    source = np.asarray(Image.open(SOURCES / MOULD_SOURCE).convert("RGB")).astype(np.float64) / 255
    opaque = np.ones(source.shape[:2] + (1,))
    image = to_image(resize(seamless(np.concatenate([source, opaque], axis=2))))
    # Every texel stays fully opaque, so the closed mould hides the die's pips everywhere.
    image.putalpha(255)
    return image


def build_spots():
    source = np.asarray(Image.open(SOURCES / SPOTS_SOURCE).convert("RGB")).astype(np.float64)
    brightness = source[SPOTS_TOP:].mean(axis=2)
    alpha = np.clip((brightness - SPOTS_BLACK) / (SPOTS_WHITE - SPOTS_BLACK), 0, 1)
    premultiplied = np.concatenate([np.repeat(alpha[..., None], 3, axis=2), alpha[..., None]], axis=2)
    return to_image(resize(seamless(premultiplied)))


def review(image, path, ink=None):
    """The texture tiled twice across over bone, so the seam and the alpha read at a glance; ink colours a mask."""
    width, height = image.size
    if ink is not None:
        alpha = image.getchannel("A")
        image = Image.new("RGBA", image.size, ink)
        image.putalpha(alpha)
    sheet = Image.new("RGBA", (width * 2, height), REVIEW_BONE)
    sheet.alpha_composite(image, (0, 0))
    sheet.alpha_composite(image, (width, 0))
    sheet.convert("RGB").save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--output", type=Path, help="unused directory under Docs/work")
    parser.add_argument("--write-assets", action="store_true", help="also write the textures under Art/SunkenLarder/Dice")
    arguments = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    if arguments.output is not None:
        output = arguments.output.resolve()
        if WORK.resolve() not in output.parents or output.exists():
            parser.error("--output must name an unused directory under Docs/work")
        output.mkdir(parents=True)
    else:
        output = Path(tempfile.mkdtemp(prefix="sunken-larder-dice-mould-", dir=WORK))

    textures = {MOULD: build_mould(), SPOTS: build_spots()}
    for name, image in textures.items():
        image.save(output / name)
        print(output / name)
    review(textures[MOULD], output / ("review-" + MOULD))
    review(textures[SPOTS], output / ("review-" + SPOTS), ink=(40, 52, 30, 255))
    if arguments.write_assets:
        ART.mkdir(parents=True, exist_ok=True)
        for name, image in textures.items():
            image.save(ART / name)
            print(ART / name)


if __name__ == "__main__":
    main()
