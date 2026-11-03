"""Build the Sunken Larder tray's worktable wood texture from the encounter's own painting.

The Sunken Larder's tray keeps the merchant tray's geometry and is re-skinned as the old woman's stained worktable
wood. Its slats sample one tileable texture, Art/SunkenLarder/Tray/SunkenLarderTrayWood.png, with the dice surface
shader's painted-texture surface kind (9), which maps it at a fixed physical scale on each face.

The texture is cut from the scene concept, Art/SunkenLarder/SunkenLarderOriginal.png (941 x 1672), so the tray
matches the tabletop around it. The method is the Bound Tree tray bark's, and its steps are the functions of
build-bound-tree-tray-bark.py beside this script, run on this encounter's painting and lighting:
  1. CROP, a band of the tabletop below the tray whose planks, grain and seams run across the frame, as the slats
     do, is converted to linear light.
  2. Its painted lighting (the hearth fire's warm fall-off from the left) is flattened: each pixel's luminance is
     divided by its Gaussian low-pass, so grain, stains and seams stay while the large-scale light goes.
  3. It is resized to 1024 x 512, which also stretches it vertically to undo the painting's foreshortening.
  4. It is made tileable, one direction after the other.
  5. It is scaled per channel so that, lit as an upward face of the tray is lit (the receiver ambient and key of
     Content/Presentation/SunkenLarder/SunkenLarderSceneLighting.asset), its median is the median painted colour
     of TARGET, the part of the tabletop the tray covers. The texture is thus the wood's albedo under the tray's
     light rather than a copy of the firelit painting.

Python 3.10 or later with numpy, Pillow and scipy; on this machine use
Docs/work/merchant-table-clear/.venv/Scripts/python.exe. Run from any directory:

  python Tools/Presentation/build-sunken-larder-tray-wood.py
  python Tools/Presentation/build-sunken-larder-tray-wood.py --write-assets

The default writes a fresh ignored Docs/work/sunken-larder-tray-wood-* directory with the texture, review images
(review-tiled.png: the texture tiled 2 x 2; review-lit.png: the painted CROP and TARGET above the texture lit as an
upward tray face, at the painting's scale) and report.json (crop, target, lighting, per-channel medians, clipped
texels). --output chooses an unused directory under Docs/work. --write-assets also writes SunkenLarderTrayWood.png to
Dice-Unity/Assets/Dice/Art/SunkenLarder/Tray and keeps its .meta file. The painting and the lighting asset are only
read, so repeated runs give the same texture.
"""

import argparse
import importlib.util
import json
from pathlib import Path
import tempfile

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Dice-Unity/Assets/Dice"
SOURCE = ASSETS / "Art/SunkenLarder/SunkenLarderOriginal.png"
LIGHTING = ASSETS / "Content/Presentation/SunkenLarder/SunkenLarderSceneLighting.asset"
ART = ASSETS / "Art/SunkenLarder/Tray"
WORK = ROOT / "Docs/work"
TEXTURE = "SunkenLarderTrayWood.png"

# Concept pixels (x0, y0, x1, y1). CROP is a band of the tabletop below the tray whose planks, grain and seams run
# across the frame; TARGET is the tabletop the tray covers in the 540 x 960 table frame (screen rows 590 to 700).
CROP = (30, 1235, 910, 1485)
TARGET = (60, 1025, 880, 1225)

# The bark script's steps read their source, lighting and boxes from its module; they are pointed at this encounter's.
_spec = importlib.util.spec_from_file_location("build_bound_tree_tray_bark",
                                               Path(__file__).with_name("build-bound-tree-tray-bark.py"))
BARK = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(BARK)
BARK.SOURCE, BARK.LIGHTING, BARK.CROP, BARK.TARGET = SOURCE, LIGHTING, CROP, TARGET


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--output", type=Path, help="unused directory under Docs/work")
    parser.add_argument("--write-assets", action="store_true", help="also write the texture under Art/SunkenLarder/Tray")
    arguments = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    if arguments.output is not None:
        output = arguments.output.resolve()
        if WORK.resolve() not in output.parents or output.exists():
            parser.error("--output must name an unused directory under Docs/work")
        output.mkdir(parents=True)
    else:
        output = Path(tempfile.mkdtemp(prefix="sunken-larder-tray-wood-", dir=WORK))

    image, albedo, painting, lighting, report = BARK.build()
    image.save(output / TEXTURE)
    BARK.review(output, image, albedo, painting, lighting)
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(output / TEXTURE)
    print(json.dumps(report, indent=2))
    if arguments.write_assets:
        ART.mkdir(parents=True, exist_ok=True)
        image.save(ART / TEXTURE)
        print(ART / TEXTURE)


if __name__ == "__main__":
    main()
