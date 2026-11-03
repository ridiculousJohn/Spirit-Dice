# Sunken Larder layers

Reference for the hag encounter's scene art: what each texture is for, how the layers register, and how to rebuild them. Artwork is under `Dice-Unity/Assets/Dice/Art/SunkenLarder` (paths below are relative to it unless stated). Coordinates are concept pixels with a top-left origin. These are the first cuts, the clean plate and the old woman's figure, and their lighting split (see Relighting). Her host, motion and ambient layers are not built.

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

## Relighting

The textures in `Relight` separate the painted lighting into an ambient base with the hearth fire out and the fire as scene light 0, following "Relighting sources" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md) and "Relighting" in [BoundTreeLayers.md](BoundTreeLayers.md). The concept is lit almost entirely by the fire under the cauldron at the left; the ambient base is the room with the fire out, dim under faint cold moonlight from the gap in the boards at the upper right, whose moonlit swamp is part of the base. All are 941 x 1672 in the concept frame and register like the layers above.

| Texture | Format | Content |
| --- | --- | --- |
| SunkenLarderAmbientUnlit.png | RGB | SunkenLarderBackground.png with the fire out: no firelight anywhere, the flames under the cauldron and in the hearth's fire opening dark, the room dim under faint cold moonlight. Changed over the whole frame except the swamp seen through the gap in the boards (`GAP` in the script), which is byte-identical to SunkenLarderBackground.png. No channel is above SunkenLarderBackground.png. |
| SunkenLarderHagUnlit.png | RGBA | The old woman under the same ambient only: no firelight on her face, hair, shawl, sleeves or hands. Alpha equals SunkenLarderHag.png; no channel is above SunkenLarderHag.png. |
| SunkenLarderFireResponse.png | RGB, linear data | R: normalized distance from the fire. G: the fire source mask (flames and embers under the cauldron and in the hearth opening). B: 0. |

Colour textures import like SunkenLarderOriginal.png.meta, her texture like SunkenLarderHag.png.meta and the response map like HauntedMerchant/MerchantLanternResponse.png.meta (sRGB off); all bilinear, clamped, uncompressed, without mipmaps or non-power-of-two scaling.

### Conventions

- Lighting pairs follow SceneLighting.shader in linear light: fire 1 = unlit + max(reference - unlit, 0) * tint * response scale; fire 0 = unlit. The reference is at least the unlit value in every channel of every pixel.
- One scene light, the hearth fire, light 0, by the Bound Tree's light 0 convention: the reference is the painted plate itself, SunkenLarderBackground.png, so light 0 at 1 (tint white) reproduces the concept exactly; the fire flickers around 1 at rest and jolts when a die withers. Her figure's pair is SunkenLarderHagUnlit.png (base) and SunkenLarderHag.png (light 0 reference) with the same response map; her host uses it when it is built.
- Response: R = min(1, hypot((x - 85) / 880, (y - 690) / 1000)) in continuous concept pixels (pixel centres at +0.5), centred on the fire under the cauldron, the convention of MerchantLanternResponse.png; the far right of the room at the fire's height is at R 0.97, and R reaches 1 at the right edge above and below it. G is 1 inside the flames under the cauldron and the hearth opening (`FIRE_POT` and `FIRE_OPENING` in the script), falling to 0 over 6 px outside (smoothstep).

### Method

This is the merchant method, through the functions of author-bound-tree-relight.py (which imports author-merchant-relight.py): each donor is generated from an edit target and registered to the concept frame by an affine fit of locally contrast-normalized, high-passed luminance plus a smooth residual displacement. The illumination change is a linear-light ratio map, ratio = G(w * donor) / G(w * source) per channel with a 3 px Gaussian, with the highlight cap (1.15 times the donor's local maximum, 9 px window) and saturation cap (1.1 times, floor 0.08). Relit colour is source * ratio, so painted detail is the source's own.

| Donor | Edit target | Transferred into |
| --- | --- | --- |
| Docs/Reference/Art/SunkenLarderRelightUnlitDonor.png | SunkenLarderBackground.png box (0, 0)-(941, 1412) | SunkenLarderAmbientUnlit.png |
| Docs/Reference/Art/SunkenLarderRelightHagUnlitDonor.png | SunkenLarderHag.png over SunkenLarderAmbientUnlit.png, box (80, 230)-(920, 1490) | SunkenLarderHagUnlit.png |

Each target is a Lanczos 1024 x 1536 upscale of its box. Registration excludes pixels within 10 px of the fire source on the plate and uses her interior (alpha above 250) for her.

- Unlit plate: the fire lights nearly the whole frame, so the field covers the whole frame. The plate box is the full width at 2:3 from the top; below row 1412 (the near tabletop) the field continues from the box, blurred 16 px. The ratio is at most 1 (removing light only darkens) and the result is limited per channel to the plate. Inside the gap polygon the field is the identity (faded over 4 px outside it), so the swamp keeps its bytes.
- Drift: the plate donor's drift is measured only where the fire cannot reach, the moonlit swamp seen through the gap (the gap polygon eroded 3 px, 13117 px). The region is small and at one side of the frame, so the drift is one constant per channel, exp of the mean log ratio there, divided out of the whole field. Her donor's drift is the normalized Gaussian average (40 px, falling back to 160 px) of the log ratio on the unlit plate 3 to 40 px outside her alpha, which her target already shows unlit.
- Unlit old woman: her own ratio and caps on her interior, extended to her soft edges from the nearest interior value; the ratio is at most 1 and the result is limited per channel to SunkenLarderHag.png. Her alpha is copied.

The registrations, drift values and checks (unlit never above the plate, the gap unchanged, light 0 at 1 reproducing the plate, her layer and the composite, her alpha equal) are in the build's report.json.

### Rebuilding the relighting

`Tools/Presentation/author-sunken-larder-relight.py` (Python 3.10+ with numpy, Pillow and scipy; on this machine `Docs/work/merchant-table-clear/.venv/Scripts/python.exe`) runs from the repository root:

```
python Tools/Presentation/author-sunken-larder-relight.py targets --from <build>/textures  # edit targets under Docs/work/sunken-larder-relight/targets
python Tools/Presentation/author-sunken-larder-relight.py build                           # fresh Docs/work/sunken-larder-relight-* folder
python Tools/Presentation/author-sunken-larder-relight.py build --write-assets            # also writes Art/SunkenLarder/Relight
```

`targets` writes the plate target and, from the unlit plate in `--from` (default `Art/SunkenLarder/Relight`), her target. `build` writes the textures under `textures/`, review images under `review/` and `report.json`. The review images are `plates-full.png` (concept, unlit plate, unlit composite with her, light 0 = 1), `intensity-strip.png` (the composite at light 0 = 0, 0.5, 1 and 1.3, through SceneLighting.shader's response scale with Distance Falloff 0.8 and Source Response 0.5) with `intensity-strip-fire-2x.png` and `intensity-strip-face-2x.png`, `hag-pairs.png` and `hag-pairs-face-2x.png` (her unlit over the unlit plate beside the painted pair), `fire-contribution-x2.png` (the composite at light 0 = 1 minus light 0 = 0, linear x2) and `response.png` (R with 0.1 contours, G over the plate, G at the fire 2x, the gap region in cyan). `--donor-unlit` and `--donor-hag` substitute a candidate donor; `--save-registered` also writes the registered donors. `--write-assets` installs only from the preserved donors, keeps existing .meta files and writes a .meta with a fresh GUID for a texture that has none. The prompts are in [prompts/](prompts/README.md); the other candidates and the edit targets are working files under `Docs/work/sunken-larder-relight`.

### Scene lighting

`Content/Presentation/SunkenLarder/SunkenLarderSceneLighting.asset` (under `Dice-Unity/Assets/Dice`) lights the background by the conventions above: shader `Dice/Scene Lighting`, Base Texture `Relight/SunkenLarderAmbientUnlit.png`, and one light, the hearth fire.

| Setting | Light 0, hearth fire |
| --- | --- |
| Contribution | SunkenLarderBackground.png |
| Response Map | Relight/SunkenLarderFireResponse.png |
| Contribution Includes Base | on |
| Constrain Ambient To Reference, Modulate By Base Color | off |
| Tint, Intensity, UV Offset | white, 1, (0, 0) |
| Distance Falloff, Source Response | 0.8, 0.5 |
| Flicker Amount, Speed, Seed | 0.1, 1.5, 5309 |
| Receiver Color, Intensity, Range | (1, 0.6, 0.3), 0, 10 |

- Reproduction: at a sampled intensity of exactly 1 the response scale is 1 for every pixel, so the light adds max(SunkenLarderBackground.png - base, 0) and reproduces the plate; her layer reproduces SunkenLarderHag.png the same way. The tint is white because the shader multiplies the contribution by it, so any other tint would change the reproduced concept; the fire's warmth is the painted plate's own, and its receiver colour is warm. Below 1, Distance Falloff 0.8 dims the room farther from the fire (response R) sooner than near it, and Source Response 0.5 keeps the flames (response G) burning longer.
- Flicker: the shared flicker noise, intensity = multiplier * (1 + 0.1 * noise), quicker than the Bound Tree's glow pulse. The wither jolt and any ambient activity light actor are authored with the table.
- Receivers: none (Receiver Intensity 0); the table scene sets them.
