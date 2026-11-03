# Sunken Larder layers

Reference for the hag encounter's scene art: what each texture is for, how the layers register, and how to rebuild them. Artwork is under `Dice-Unity/Assets/Dice/Art/SunkenLarder` (paths below are relative to it unless stated). Coordinates are concept pixels with a top-left origin. These are the first cuts: the clean plate and the old woman's figure. Relighting (unlit and fire-lit versions), her host, motion and ambient layers are not built.

## Textures

Every texture is 941 x 1672 in the concept's frame, so all layers register by drawing them over the same rect; no offsets or scales are involved. They import like SunkenLarderOriginal.png.meta (sRGB, bilinear, clamped, uncompressed, no mipmaps or non-power-of-two scaling, alpha is transparency).

| Texture | Format | Content |
| --- | --- | --- |
| SunkenLarderOriginal.png | RGB | The accepted scene concept. Source of every layer; never edited. |
| SunkenLarderBackground.png | RGB | Clean plate: the concept with the old woman removed and the room behind and under her reconstructed within 30 px of her silhouette (full weight within 22 px, so a breathing lift, small offsets and a 1.03 scale about her seat uncover only reconstruction): the plank wall, the shelves left and right of her continued behind her with more stoppered jars, the gap in the boards continued down as a dark slit, the stone hearth and its fire opening where her shawl overlapped it, the dark space under the shelves and the bare tabletop and its far edge under her hands. Identical to the concept outside that declared region. |
| SunkenLarderHag.png | RGBA | The old woman: a generated derivative, not concept pixels. Her transparent extraction (Docs/Reference/Art/SunkenLarderHagDonor.png) registered to the concept frame, its alpha her matte (soft on hair strands and the shawl's fringe) and its colour low-frequency matched to the concept on her visible pixels. It holds her hair, face, shawl with its fringe, blouse, sleeves and both hands. She ends along the table's far edge (concept row 900); only her hands and fingertips lie on the tabletop, down to row 920. The table stays on the plate. |

Draw order: SunkenLarderBackground.png, then SunkenLarderHag.png. Composited over the plate she reads as the concept at rest; she differs from it where her generated extraction differs in fine detail, within the error reported by the script. Neither layer holds her contact shadow on the table: the extraction drops it and the plate reconstructs clean wood under her hands.

## Generated edits

The plate follows "Generated edits" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md) and [BoundTreeLayers.md](BoundTreeLayers.md): the generated image is a donor, registered to the concept frame by an affine fit, and only pixels inside the declared region are transferred, with the donor's low-frequency colour matched to the surrounding concept and a linear blend from full weight near her silhouette to zero at the declared edge. Pixels outside the declared region stay byte-identical. The old woman is a generated transparent extraction, made the way MerchantHost.png and BoundTreeNymph.png were, and installed whole after registration and colour matching.

| Donor | Edit target | Installed as | Region |
| --- | --- | --- | --- |
| Docs/Reference/Art/SunkenLarderHagDonor.png | concept box (80, 230)-(920, 1490) | SunkenLarderHag.png | her alpha |
| Docs/Reference/Art/SunkenLarderBackgroundDonor.png | concept box (80, 230)-(920, 1490) | SunkenLarderBackground.png | within 30 px of her silhouette, full weight within 22 px |

Both donors are edits of the same target, a Lanczos 1024 x 1536 upscale of the concept box (1.219x; her figure is about 770 px wide, so a 2:3 box that holds her and the declared region is this large). Registration maps donor pixels from concept pixels by `donor = M (p - c) + t` (c the box centre (500, 860)), fitted by normalized cross-correlation of high-passed luminance on a 3 px grid inside the box: for her on her interior (at least 6 px inside her silhouette under the upscale's own mapping), for the plate donor excluding pixels within 34 px of her silhouette. The fitted values are in the script's report.json. The prompts are in [prompts/](prompts/README.md); the other candidate of each generation and the edit target are working files under `Docs/work/sunken-larder-layers`.

Two settings differ from the Bound Tree cut, both because this target is large and only slightly upscaled:

- Prefilter: before a donor is sampled at concept resolution it is anti-aliased with an exact Gaussian of sigma 0.45 donor px per unit of upscale (0.55 donor px here). The Bound Tree script's three-box blur cannot go below a sigma of about 1.4 donor px, which at 1.22x would soften her and the reconstruction by more than a concept pixel.
- Plate colour match: masked Gaussian averages at 160, 64, 24, 10 and 4 px, coarse to fine. The removal region is several hundred px across, and the box approximation of a Gaussian reaches only three sigma, so with the Bound Tree's coarsest 24 px the match stopped about 72 px inside the region with a visible step (a dark band above the table between her hands). The 160 px level reaches every pixel of the region; the finer levels take over near the declared edge. Her own colour match uses 24, 10 and 4 px, as the nymph's does.

## Old woman

- Matte: the donor's own alpha, rescaled so its levels 2 and 250 map to 0 and 1 (the extraction's interior sits at 250 to 253), warped with the registration. Hair strands and the shawl's fringe threads keep the extraction's soft alpha.
- Colour: the donor's colour, unpremultiplied where her alpha is above 0.05 and spread outward over a few px (normalized Gaussian, sigma 3 px) so bilinear filtering at her edge does not mix in black. Its low-frequency colour is matched to the concept (sigmas 24, 10 and 4 px) on her visible pixels: at least 4 px inside her alpha-0.5 silhouette, excluding pixels that still differ from the concept by 40 levels or more after a 24 px match (269 pixels). Elsewhere the match is extrapolated from those pixels.
- Silhouette: every pixel with alpha at or above 0.02, concept box (121, 270)-(888, 920). Her seat, the bottom centre of her alpha-0.5 silhouette, is (502.5, 919). The plate is reconstructed at full weight within 22 px of her silhouette; the motion margin the script checks is 20 px, which covers the 1.03 scale about her seat (at most 19.05 px, at the top of her head), a 6 px lift and 4 px offsets.

## Rebuilding

`Tools/Presentation/cut-sunken-larder-layers.py` (Python 3.9+, standard library only; it imports the PNG, registration, colour-match and review functions of `cut-bound-tree-layers.py` beside it) rebuilds both textures from the concept, the two donors and SunkenLarderLayers.json in this folder:

```
python Tools/Presentation/cut-sunken-larder-layers.py                 # fresh Docs/work/sunken-larder-layers-* folder
python Tools/Presentation/cut-sunken-larder-layers.py --write-assets  # also replaces the installed textures
```

The output folder holds the textures under `layers/`, review images under `review/` (plate, plate at 2x, concept beside her over the plate, her over the plate, on grey and on a checker, her matte, the composite error x4 against the concept, her moved 4 px left and right, 6 px up and scaled 1.03 about her seat over the plate, and the plate's difference from the concept x4 with the declared edge in cyan) and `report.json` with the registrations and checks (concept unchanged, plate byte-identical to the concept outside the declared region, composite error inside her silhouette, the largest displacement of each review motion, plate at full weight within the motion margin). `--donor-hag` and `--donor-background` substitute a candidate donor for review; `--write-assets` installs only from the preserved donors, keeps existing .meta files and writes a .meta with a fresh GUID for a texture that has none.

To change the region, edit its distances in SunkenLarderLayers.json, rebuild into Docs/work and review before writing assets. A replacement donor must be an edit of the same target box at 1024 x 1536 (RGBA with a transparent background for her). The target is made with Pillow (on this machine `Docs/work/merchant-table-clear/.venv/Scripts/python.exe`):

```
python -c "from PIL import Image; Image.open('Dice-Unity/Assets/Dice/Art/SunkenLarder/SunkenLarderOriginal.png').convert('RGB').crop((80, 230, 920, 1490)).resize((1024, 1536), Image.LANCZOS).save('Docs/work/sunken-larder-layers/targets/hag-target.png')"
```
