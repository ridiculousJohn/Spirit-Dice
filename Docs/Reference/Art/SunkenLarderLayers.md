# Sunken Larder layers

Reference for the hag encounter's scene art: what each texture is for, how the layers register, and how to rebuild them. Artwork is under `Dice-Unity/Assets/Dice/Art/SunkenLarder` (paths below are relative to it unless stated). Coordinates are concept pixels with a top-left origin. These are the first cuts, the clean plate and the old woman's figure, their lighting split (see Relighting), her host with its eyelid overlays (see Host), the ambient steam and hearth fire (see Ambient) and the arrival picture's drifting haze and fog (see Arrival motion).

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
- One scene light, the hearth fire, light 0, by the Bound Tree's light 0 convention: the reference is the painted plate itself, SunkenLarderBackground.png, so light 0 at 1 (tint white) reproduces the concept exactly; the fire flickers around 1 at rest and jolts when a die withers. Her figure's pair is SunkenLarderHagUnlit.png (base) and SunkenLarderHag.png (light 0 reference) with the same response map, which her host uses (see Host).
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
- Flicker: the shared flicker noise, intensity = multiplier * (1 + 0.1 * noise), quicker than the Bound Tree's glow pulse. It is the fire's only resting flicker; the table's ambient activity holds the multiplier at 1 at rest and stutters it on the wither jolt (see Ambient).
- Receivers: none (Receiver Intensity 0); the table scene sets them.

## Host

The old woman is the encounter's host: `Content/Presentation/SunkenLarder/SunkenLarderHost.asset` (a SpiritHostPresentationAsset) with portrait `SunkenLarderHost.prefab`, deformation mesh `SunkenLarderHostMesh.asset` and the layer materials `SunkenLarderHost{Figure,Downcast,Blink}.mat` (under `Dice-Unity/Assets/Dice`), all written by `Tools/Presentation/author-sunken-larder-host.py`. She is a living still like the merchant (see "Merchant layers" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md)): every layer keeps the 941 x 1672 concept frame and shares one deformation mesh, so her figure and the overlays move identically and no seam can open; her timelines are on the shared host state API, reauthored by the script rather than edited by hand.

### Layers

Her figure is one layer: no part of her moves in front of another (her breathing, head and mouth are smooth deformations of the same pixels), so nothing needs splitting, and her forearms, hands and the shawl's hem at the table stay still because every shape is zero there. Two painted overlays draw over her face, made as the merchant's blink was (see Eyelid overlays).

| Layer | Texture (Source Masked Image `_SourceTex`) | `_LayerMask`, `_InvertMask` | Lighting: base, light 0 reference | UV rect | Colour alpha at rest |
| --- | --- | --- | --- | --- | --- |
| Figure | SunkenLarderHag.png | none, 1 | Relight/SunkenLarderHagUnlit.png, SunkenLarderHag.png | full | 1 |
| Downcast | SunkenLarderHagDowncast.png | SunkenLarderHagDowncastMask.png, 0 | Relight/SunkenLarderHagDowncastUnlit.png, SunkenLarderHagDowncast.png | overlay crop | 0 |
| Blink | SunkenLarderHagBlink.png | SunkenLarderHagBlinkMask.png, 0 | Relight/SunkenLarderHagBlinkUnlit.png, SunkenLarderHagBlink.png | overlay crop | 0 |

- Prefab: root SunkenLarderHost (RectTransform 941 x 1672, anchors and pivot 0, SpiritHostPortrait with Portrait Root `Portrait Layers` and the three layers in draw order), `Portrait Layers` (941 x 1672 at the root's origin), and per layer a SpiritPortraitImage (its texture and material, mesh SunkenLarderHostMesh.asset, five weights at 0, anchored position (470.5, 836) with pivot 0.5 so its rect covers the frame, no raycast target, cull transparent mesh on).
- The overlays' textures are crops of concept box (404, 436)-(548, 494) (144 x 58); their UV rect (-2.80556, -20.31034, 6.53472, 28.82759) stretches the crop over the full frame, so they keep the concept registration and deform with the figure; the clamped crop's mask is 0 at its border, so nothing draws outside it.
- Lighting: every layer is lit per pixel by light 0, the hearth fire of SunkenLarderSceneLighting.asset, by reference through Relight/SunkenLarderFireResponse.png over the full map rect, exactly as the plate is: at light 0 = 1 each layer draws its painted texture, and below it fades toward its unlit base. No depth or computed light.
- Asset: Reference Size 941 x 1672, Scale 1, Offset 0, Lit Layer Shader `Dice/Scene Lit Image`, no smoke, and Sigil Prefab `Content/Presentation/UI/SunkenLarderHostSigil.prefab` (her stoppered jar, drawn from `Tools/Presentation/host-sigils/sunken-larder-jar.svg`).

### Eyelid overlays

| Texture | Format | Content |
| --- | --- | --- |
| SunkenLarderHagBlink.png | RGBA 144 x 58, alpha 1 | Her closed eyelids, as painted (light 0 reference): the registered, colour-matched donor over the crop. |
| SunkenLarderHagDowncast.png | RGBA 144 x 58, alpha 1 | Her eyelids lowered over eyes looking down and slightly toward the hearth, as painted. |
| SunkenLarderHag{Blink,Downcast}Mask.png | RGBA 144 x 58, white | Alpha is the soft eyelid regions; imports like HauntedMerchant/MerchantBlinkMask.png (sRGB off). |
| Relight/SunkenLarderHag{Blink,Downcast}Unlit.png | RGBA 144 x 58, alpha 1 | The overlay with the fire out: its colour times her own unlit-over-painted ratio. |

The colour textures import like SunkenLarderHag.png.meta. The method is the merchant's blink (`Docs/work/merchant-blink`):

- Edit target: the visible composite (SunkenLarderHag.png over SunkenLarderBackground.png) in concept box (370, 380)-(630, 640), Lanczos-upscaled 4x to 1040 x 1040 (`target`). Donors: Docs/Reference/Art/SunkenLarderHagBlinkDonor.png and SunkenLarderHagDowncastDonor.png (1254 x 1254 each, the first of two candidates); prompts in [prompts/](prompts/README.md).
- Registration: `donor = M (p - c) + t` (c the box centre (500, 510)), fitted by normalized cross-correlation of high-passed luminance (6 px) over her face in (395, 415)-(595, 600), excluding the eye ellipses ((436, 474) and (515, 457), radii 30 x 19) and pixels where her alpha is below 0.99; the donor is anti-aliased with a Gaussian of 0.45 donor px per unit of upscale before sampling. Blink: M = [[4.8267, -0.0040], [0.0037, 4.8219]], t = (627.25, 626.33), NCC 0.9718 to 0.9744. Downcast: M = [[4.8263, -0.0032], [0.0099, 4.8154]], t = (627.27, 626.70), NCC 0.9675 to 0.9696.
- Transfer: only inside two soft elliptical eyelid regions, centres (437, 476) and (515, 461), radii 22 x 13 and 23 x 13 (Blink) or 22 x 14 and 23 x 14 (Downcast); the mask is 1 inside 0.72 of the normalized radius and 0 beyond 1.12. The donor's low-frequency colour is matched to her face by a per-channel gain, a normalized Gaussian (7 px) of face over donor in the ring between 1.15 and 1.9 radii (her alpha above 250), extended inward. Gain in the mask: Blink 0.82 to 1.14 (mean 0.94, 0.96, 1.04), Downcast 0.87 to 1.27 (mean 1.01, 1.04, 1.09). Mask pixels: Blink 2308 nonzero, 948 at full; Downcast 2500 and 1036. The script checks that each mask vanishes at the crop's border.
- Unlit base: the overlay times her ratio SunkenLarderHagUnlit.png / SunkenLarderHag.png per channel in linear light, a normalized Gaussian (3 px) over her interior, at most 1 (0.15 to 0.92 in the mask, mean 0.53).

### Deformation mesh

SunkenLarderHostMesh.asset is a 48 x 84 grid over the 941 x 1672 frame (about 19.6 x 19.9 px cells, the merchant's grid) with five shapes. Every layer's vertices are rebuilt each frame while she breathes, so the grid is as coarse as the shapes allow: against the nymph's 96 x 168 grid, rendered with the same timelines, her face crops differ by at most 26 levels (99th percentile 4) at the smile's peak and the two smiles and head poses look alike, with no folds on either grid. The coarser grid departs from the authored fields over her face (380, 380)-(620, 620) by up to 1.49 px (Smile, at the mouth corners, whose bumps span about one cell), 0.34 px (HeadLower) and 0.19 px (HeadTurn), against 0.62, 0.12 and 0.06 px on 96 x 168. Two envelopes recur: the head, a soft ellipse centred (485, 450) with radii 160 x 230 (full inside 0.8, none beyond 1.25) faded out below the chin between rows 590 and 670, so the shoulders never follow a head shape; and the body, full above row 670 and zero from row 770, so everything from row 770 down (the sleeves, forearms and hands from row 790, the shawl's hem at the table, row 900) never moves and no gap opens at the table.

| Shape | Region | Full weight (right, up) |
| --- | --- | --- |
| Breath (0) | The body envelope; the head envelope takes 70% of the lift; the chest (soft ellipse (485, 660), 230 x 150) widens sideways. | Shoulders and chest up 4.5 px, head up 3.15 px, chest sides out 1.5 px. |
| HeadLower (1) | The head envelope. | Down 7 px from the brow (row 430) to the chin, easing to 3 px at the crown (row 300): the hair above the brow stretches and the neck below the chin takes up the rest, as a head pitching down. |
| HeadTurn (2) | The head envelope; the front of the face (soft ellipse (482, 495), 80 x 105, full inside 0.5, none beyond 1.2) most. | Left (toward the hearth) 4.5 px at the face, 1.5 px at the head's outline. |
| Smile (3) | The mouth corners (467, 546) and (521, 535) within 22 px, the cheeks (446, 522) and (538, 512) within 30 px (full inside 0.3 of the radius). | Corners 1.5 px outward and 2.4 px up, cheeks 0.6 px outward and 1.2 px up. A negative weight relaxes her painted faint smile. |
| Settle (4) | The body envelope. | Everything above it shrinks by 0.99 about her face (485, 470) and rises 2 px: the shoulders draw in and up and the head stays almost in place, as she leans back from her hunch. |

Measured by the script on the rendered grid over her pixels (alpha at least 0.02): largest displacement Breath 4.6 px, HeadLower 7.0 px, HeadTurn 4.5 px, Smile 2.7 px (the authored 3.1 px, softened by the grid), Settle 5.0 px; nothing at or below row 780 moves under any shape or state (0 px); largest strain 0.14 for a single shape (HeadLower) and 0.17 in Waiting (HeadLower with HeadTurn); no folds. Over every state's timeline the largest displacement on her is 9.5 px at (792, 676), the shawl's edge at her left shoulder (image right), in Victory at 1.65 s (Breath 1.16 with the victory breath, Settle 0.98, Smile -0.49); her face moves at most 6.2 px (Waiting). The plate is reconstructed at full weight within 22 px of her silhouette, so no motion uncovers unreconstructed plate.

### States

Idle loops; Waiting and Reaction play in real time; Arrival, Victory, Defeat and Farewell follow the progress their caller sets, so their durations set relative timing and their idle is sampled at progress times duration. Waiting and every directed state include the idle and enter through entry weights from the pose on screen: shapes over 0.35 to 0.8 s, an interrupted expression relaxing over 0.6 s, a blink in progress fading over 0.1 s. An eyelid overlay never sits between 0 and 1 longer than a blink: every change of its alpha is a quick step of 0.1 s (OVERLAY_STEP; the merchant's blink fades in over 0.04 s and out over 0.055 s), so the painted eye and the lids never show together for longer than that; the script reports the longest stretch each overlay track spends between 0.02 and 0.98 (0.083 s for every step, 0.046 s for the idle blink). All transitions are eased (smoothstep). Deformation tracks drive all three layers alike.

| State | Sequence | Duration | Timeline |
| --- | --- | --- | --- |
| Idle | Living still | 19.9 s loop | Four breaths (an eased inhale over 40% of the breath, a brief turn, a longer exhale and a pause, as the merchant's) of 4.8, 5.3, 4.6 and 5.2 s at depths 1.0, 0.84, 0.95 and 0.9; blinks (Blink alpha up to 1 for about 120 ms) at 1.9, 6.35, 9.2, 14.8 and 17.55 s. |
| Waiting | Head lowered over the pot | 1.5 s, then held | HeadLower and HeadTurn to 1 over 1.5 s; once the head has begun to lower, her eyes drop in one quick step (Downcast alpha 0 to 1 over 0.3 to 0.4 s; lids already down stay down); no blinks (Blink alpha held at 0); the breathing continues from a restarted idle. No stirring. |
| Arrival | Looks up | 3 s | Her eyes open in one quick step first (Downcast alpha 1 to 0 over 0.15 to 0.25 s), so she looks up from under her brow; then HeadLower 1 to 0 over 0.35 to 2.2 s and HeadTurn 1 to 0 over 0.5 to 2.5 s, with an intake of breath (additive Breath 0.45, settling to -0.12 and back by 2.95 s). It ends in the concept pose. |
| Reaction | Slight smile | 2.4 s | Additive, gesture on start: Smile 0 to 0.8 over 0.4 s, held to 1.3 s, back to 0 by 2.3 s. A reaction restarted mid-smile blends from its pose over 0.1 s. |
| Victory | Settles back | 3.5 s | Settle 0 to 1 and Smile 0 to -0.5 over 0.1 to 1.8 s (she leans back a little and her smile eases), held to the end; her eyes close for a moment, a slow blink of quick fades: Blink alpha 0 to 1 over 1.0 to 1.1 s, held closed to 1.9 s, open by 2.0 s; a quiet breath (additive Breath 0.4, settling to -0.2 and back by 3.4 s). She stays settled. The spoon is part of the plate and does not move. |
| Defeat | Satisfied nod | 4 s | Smile 0 to 1 over 0.1 to 1.3 s and held; a slow small nod, HeadLower 0 to 0.45 over 1.0 to 2.1 s and back over 2.3 to 3.6 s. |
| Farewell | Farewell settle | 3 s | The merchant's farewell: additive Breath 0.15, settling to -0.3 and back by 2.9 s; any expression relaxes to rest over 0.6 s. |

A held outcome pose (Victory's settled pose, Defeat's smile) lasts until the next state is set: the host system has no entry weights into Idle, so setting Idle after an ending returns her to the concept pose at once.

### Preview

`Assets/Scenes/SunkenLarderPreview.unity` is the isolated art preview, made like BoundTreePreview.unity; it is not registered in the campaign, Addressables or build scenes, and no image is a raycast target.

| Object | Content |
| --- | --- |
| Preview Camera | Orthographic size 480, solid dark background, UI layer only. |
| Sunken Larder Preview Canvas | Screen Space - Camera on the preview camera; Canvas Scaler 540 x 960, match 0.5. No raycaster. |
| Sunken Larder Plate | RawImage `Relight/SunkenLarderAmbientUnlit.png` filling the 540 x 960 authoring frame, with a SceneLightingPresentation: Lighting Asset SunkenLarderSceneLighting.asset, Background this image, Intensity Multipliers [1], Hosts [Old Woman Host with SunkenLarderHost.asset, Position (0, 0, 0)]. It draws the ambient base plus the fire, builds the host and passes it the same lighting sample every frame. |
| Sunken Larder Plate/Old Woman Host | Instance of SunkenLarderHost.prefab, its root stretched over the plate's rect (anchors 0 to 1, pivot 0, size 0). |

Enter Play mode to see the idle; drive the fire with the plate's Scene Lighting Presentation > Intensity Multipliers element 0 (1 is rest; values above 1 brighten further). As for the Bound Tree host, the other states are reached only through `SpiritHostPortrait.SetState(state, progress)` and `React()`.

### Review captures

`SunkenLarderHostCaptureTests` (EditMode, [Explicit], [Category("Capture")]) renders the preview scene in batch mode with the fire at its resting level (light 0 sampled where its flicker is exactly 1). For idle and each state it renders nine frames at evenly spaced times across it (idle across its loop, Waiting over twice its duration, Reaction over its duration, the directed states at progress 0 to 1), each at the concept's scale with the face (concept box (370, 360)-(630, 640)) and the table edge and hands ((100, 720)-(920, 940)) at 2x, and the idle at each blink. Its frames.txt lists every frame's state, progress and layer values, and the transition jumps content validation measures for a host a table uses (Arrival from Waiting, Reaction start and end, each outcome's entry) plus Waiting's entry from Idle. Run it by name with DICE_SPIRIT_CAPTURE_ROOT set (output under `sunken-larder-host` in it):

```
Unity.exe -batchmode -projectPath Dice-Unity -runTests -testPlatform EditMode -testFilter Dice.Tests.SunkenLarderHostCaptureTests -testResults <dir>/results.xml -logFile <dir>/unity.log
```

At rest the render equals her layer over the plate (against the linear-light composite, resampled to the concept frame, a mean of 0.95 levels). The largest transition jump listed is 0.04 in Blink alpha at Reaction end, which is the idle blink's own motion during the 2 ms the measurement straddles, not a jump.

### Rebuilding the host

`Tools/Presentation/author-sunken-larder-host.py` (Python 3.10+ with numpy, Pillow and scipy; on this machine `Docs/work/merchant-table-clear/.venv/Scripts/python.exe`) runs from the repository root; its constants (overlay boxes and regions, shape regions and magnitudes, timelines) are at the top of the script:

```
python Tools/Presentation/author-sunken-larder-host.py target                    # the overlays' edit target under Docs/work/sunken-larder-host/targets
python Tools/Presentation/author-sunken-larder-host.py overlays                  # fresh Docs/work/sunken-larder-host-* folder
python Tools/Presentation/author-sunken-larder-host.py overlays --write-assets   # also writes the six overlay textures
python Tools/Presentation/author-sunken-larder-host.py host                      # fresh Docs/work/sunken-larder-host-* folder
python Tools/Presentation/author-sunken-larder-host.py host --write-assets       # reauthors the host in place
```

`overlays` writes the textures under `textures/`, `review/overlays-face-3x.png` (her face painted and unlit, each overlay at 0, 0.5 and 1, and the registered donor with the mask's full and edge ellipses) and `report.json` (registrations, gains, mask counts, unlit ratio); `--donor-blink` and `--donor-downcast` substitute a candidate donor for review, and `--write-assets` installs only from the preserved donors. `host` writes the mesh, the three materials, the prefab and the host asset (to the output folder, or in place with `--write-assets`) and `report.json` with the measurements above. Existing .meta files are kept; a missing one is written (textures copy SunkenLarderHag.png.meta or MerchantBlinkMask.png.meta under a fresh GUID). Re-run `host` after changing a constant rather than editing the assets; the prefab's object ids are fixed, so scene instances keep their overrides.

## Ambient

The hearth fire's flicker and jolt, and steam rising from the cauldron on two drifting tile layers, built by `Tools/Presentation/author-sunken-larder-ambient.py` (see "Sunken Larder ambient layers" in the [Presentation tools](../../../Tools/Presentation/README.md)) from the installed plates and a kept generated donor, by the Bound Tree's mist method ("Cloud and mist drift" and "Ambient scene layers" in [BoundTreeLayers.md](BoundTreeLayers.md)). The concept already paints steam above the pot, and it stays on the plate: the moving layers show only inside the painted plume, so they read as that steam moving. The hanging herb bundles do not move.

### Hearth fire

`Content/Presentation/SunkenLarder/SunkenLarderAmbientActivity.asset`, referenced by SunkenLarderTable.asset's Ambient Activity, has one light actor, light 0, and no props or prop groups.

- Resting flicker: the scene lighting's own flicker noise (Flicker Amount 0.1, Speed 1.5; see Scene lighting) owns it, a smooth, irregular flicker of three noise octaves near 1.1, 3.2 and 8.6 Hz, at most 10 percent either side of rest (0.93 to 1.06 over 7.5 s of captures). The light actor makes no idle flicker, since its steps cut between levels, share their levels with the jolt, and a jolt is skipped while one runs: its outages are empty (Sputter Flickers 0 to 0, Dark Duration 0 to 0; Idle Interval 30 to 60 s schedules them), so the fire never goes out on its own and its multiplier rests at 1. The two flickers never stack.
- Wither jolt: the rot presenter jolts the activity as a die withers. The light stutters through 4 to 6 flickers of 0.12 to 0.22 s each (Stutter Flickers, Flicker Duration), alternating dim (Dim Level 0.75 to 0.85) and bright (Bright Level 1.1 to 1.2), starting dim; each flicker eases from the level before it over its whole length (Flicker Ease 1), and the light then eases back to 1 over the activity's Light Recovery (1 s): the fire wavers for about 1.5 to 2.3 s while the mould closes over the die (0.6 s). The flicker noise multiplies it. Below 1 the room farther from the fire dims sooner than the flames (Distance Falloff 0.8, Source Response 0.5); above 1 every lit pixel brightens in proportion. Her host and the steam are lit by light 0, so they gutter and flare with the room.
- Endings: Won, Lost and Abandoned all settle (Light Recovery 1 s). With no props, Jolt Strength 1, Defeat Strength 2.5 and Settle Time 0.35 s move nothing; Burst Clips is empty until the audio step.

### Steam textures

| Texture | Format | Content |
| --- | --- | --- |
| SunkenLarderSteamBroad.png | RGBA 399 x 614, wraps both ways | The broad layer's light 0 reference (fire-lit): the donor mirrored, softened (sigma 5 donor px) and scaled 0.48. |
| SunkenLarderSteamWisps.png | RGBA 250 x 384, wraps both ways | The fine wisps layer's light 0 reference: the donor scaled 0.30. |
| Relight/SunkenLarderSteam{Broad,Wisps}Unlit.png | RGBA, as above | Each layer's base, the steam with the fire out: the lit tile times the plates' own unlit-over-lit ratio inside the plume (median per channel, linear light: 0.070, 0.135, 0.232), so it is faint and cool. Alpha equals the lit tile's. |
| SunkenLarderSteamMask.png | RGBA, white, 941 x 1672 | Alpha is the plume window (below), the steam occluder's mask. |

The donor, Docs/Reference/Art/SunkenLarderSteamDonor.png (prompt in [prompts/](prompts/README.md); style reference SunkenLarderSteamReference.png, concept box (0, 150)-(320, 630)), is an even field of rising wisps on transparency. The script divides its alpha by its Gaussian average (sigma 160 donor px, never by less than a quarter of its mean), so no denser band pulses past as the tile scrolls; makes it wrap both ways by a premultiplied cross-fade of its last 192 columns into its first and its last 256 rows into its first; and builds each layer's tile at concept resolution. The fire-lit tiles' alpha-weighted mean colour is set in linear light to the painted steam's bright colour, the mean of the brightest tenth of the plate inside the plume's core between rows 400 and 545 (sRGB 173, 111, 74). The tiles import like SunkenLarderHag.png with wrap U and V Repeat, the mask like SunkenLarderHagBlinkMask.png (sRGB off).

### Plume window

The window is where the moving steam shows. Its envelope is a soft band around the painted plume's centre line, which leans right as it rises (centre x 98 at row 560, 104 at 470, 135 at 360, 156 at 240; half width 70 to 80 above the pot, 58 at the top), full within 0.6 of the half width and none beyond 1.35; zero at the pot's rim (row 556), full from row 520 to 400, 0.8 at row 300 and zero at row 205, below the herb bundles; and zero right of column 186 over rows 250 to 400, so the toy horse and the jars stay clear. Inside the envelope its level follows the painted steam: 0.6 plus 0.4 times the plume's presence, the plate's luminance above its grey opening (61 px, blurred 12 px, softened 6 px), normalised by its 95th percentile in the envelope, so the moving wisps are strongest where steam is painted and still show between its wisps and a little beyond the painted plume. Its bounds with 4 px of margin are the concept rect (0, 204, 239, 355). None of it lies on her figure, which draws after it.

### Steam drift

Each layer's legacy clip (Loop Time on) moves its RawImage's uvRect x from 0 to -1 (one tile right) and y from 0 down by a whole number of tiles (the wisps rise) over its period, the texture wrapping both ways, so the wisps rise and drift right along the plume's lean and the loop is seamless. Content validation (check 4) lists each clip's uvRect jumps at the loop: whole tiles, which the repeating texture shows unchanged.

| Layer | Clip | Opacity | Rise | Drift | Period | uvRect at the start (x, y, width, height) |
| --- | --- | --- | --- | --- | --- | --- |
| Broad | SunkenLarderSteamBroadRise.anim | 0.7 +/- 0.08 (2 cycles per period) | 14.8 px/s (4 tiles) | 2.4 px/s | 166.25 s | 0, 0, 239 / 399, 355 / 614 |
| Wisps | SunkenLarderSteamWispsRise.anim | 0.9 | 22.1 px/s (4 tiles) | 3.6 px/s | 69.44 s | 0, 0, 239 / 250, 355 / 384 |

Speeds are concept px per second. The broad layer's swell is a sine of 8 keys a cycle with the sine's own slopes. The steam changes a visible plate pixel by at most 80 levels, 68 at the 99th percentile inside the window (the script's report, sampled every 0.5 s over 30 s).

### Ambient scene layers

In `Assets/Scenes/SunkenLarderEncounter.unity` the Sunken Larder Scene frame draws, in order:

| Object | Content |
| --- | --- |
| Steam Broad, Steam Wisps | RawImage over the window's rect (anchored to the frame's top-left, pivot top-left, position (0, -117.1292), size (137.152, 203.8278)) with the layer's Unlit tile as texture, `SunkenLarderSteam{Broad,Wisps}Lit.mat` (Scene Lit Image, `_SourceTex` the Unlit tile, `_InvertMask` 1, no mask), colour alpha the layer's opacity and uvRect its start; a SceneLitImage on SunkenLarderSceneLighting.asset (base the Unlit tile, light 0 reference the lit tile through Relight/SunkenLarderFireResponse.png, map rect the rect's part of the frame); and an Animation playing its clip. The SceneLitImage maps the response map through the current uvRect, so the firelight's response stays fixed in the room while the texture drifts. |
| Steam Occluder | RawImage over the same rect with uvRect and map rect (0, 0.665670, 0.253985, 0.212321), the same crop of the full frame; `SunkenLarderSteamOccluderLit.mat` (`_SourceTex` Relight/SunkenLarderAmbientUnlit.png, `_LayerMask` SunkenLarderSteamMask.png, `_InvertMask` 1) and a SceneLitImage lighting it as the scene lighting lights the background (base Relight/SunkenLarderAmbientUnlit.png, light 0 reference SunkenLarderBackground.png through SunkenLarderFireResponse.png). It draws the lit plate wherever the window is below 1, so the steam shows only inside the window, and is invisible at rest. |
| Old Woman Host | Her host, after the steam, so no steam crosses her. |

The script's review images (`review/` of its output) are `steam-frames.png` and `steam-sequence-1s.png` (the plume at moments of the drift beside the plate without the layers), `steam-window.png` (the window over the plate, with its rect), `steam-tiles.png` (each tile 2 x 2 over a dark wall colour, for its seams), `steam-light-levels.png` (the fire out, at rest and at 1.4), `steam-frame-540.png`, `steam-frame-443.png` and `steam-change-x4.png`. In game the steam sits at the frame's upper left and is clearly visible at phone size, clear of the dice, the HUD's panels and her face at 540 and at 443 wide.

## Arrival motion

The arrival picture, SunkenLarderArrival.png (941 x 1672, accepted and never changed), shown at the journey's first beat by `Content/Presentation/SunkenLarder/SunkenLarderArrivalPicture.asset`, has its painted haze and low fog drifting on three picture scene drift layers and its painted chimney plume moving by its own pixels on a flow layer ("Picture scenes" in [presentation authoring](../../PRESENTATION-AUTHORING.md)), built by `Tools/Presentation/author-sunken-larder-arrival-motion.py` and `Tools/Presentation/author-sunken-larder-arrival-plume.py` (see "Sunken Larder arrival motion" in the [Presentation tools](../../../Tools/Presentation/README.md)). The method is the cauldron steam's (see Ambient): a kept generated donor made into tiles that wrap both ways, for the haze a broad, softened layer and a finer wisps layer drifting at calm speeds, the broad one a little slower, and for the low fog one layer, each seen only through a window. A drift layer draws its tile through its own window, so no occluder is needed. The picture has no scene lighting; the layers are unlit.

The picture scene's plate is the picture itself; the layers drift over the painted haze and fog, which stay on the plate.

### Arrival motion textures

| Texture | Format | Content |
| --- | --- | --- |
| SunkenLarderArrivalFogFar.png | RGBA 960 x 136, wraps both ways | The broad haze: the dense band (donor rows 450 to 680) of Docs/Reference/Art/BoundTreeMistFarDonor.png, its brightness over black turned into density (divided by its Gaussian average at 140 donor px, levels 0.85 to 1.8 mapped to alpha 0 to 1 by smoothstep), the donor's own colour kept, cross-faded to wrap, mirrored, softened 6 donor px, scale 0.75. Mean alpha 0.30. |
| SunkenLarderArrivalFogFarWisps.png | RGBA 576 x 82, wraps both ways | The haze wisps: the same band, not mirrored or softened, scale 0.45. |
| SunkenLarderArrivalFogNear.png | RGBA 1088 x 153, wraps both ways | The low fog: Docs/Reference/Art/BoundTreeMistNearDonor.png's band (rows 450 to 690) as density alone (sigma 160, levels 0.85 to 1.3), scale 0.85, in one colour, sRGB (38, 58, 75). |
| SunkenLarderArrival{FogFar,FogNear}Window.png | RGBA, white | Alpha is the window over its layers' rectangle (below), a crop of the full-frame window; the haze's two layers share FogFarWindow. |

Colour, as the cauldron steam's: each haze tile keeps its donor's shading, its alpha-weighted mean colour set in linear light to the painted colour, not brightened: the mean of the brightest third of the picture where its window is at least 0.8, sRGB (39, 61, 81). The low fog's colour is the brightest 40 percent of its window times 1.2. The tiles import like SunkenLarderSteamWisps.png (sRGB, wrap U and V Repeat), the windows like SunkenLarderSteamMask.png (sRGB off, clamped).

### Arrival windows

- Far haze: the cauldron steam's window, 0.6 plus 0.4 times the painted presence (luminance above its grey opening of 61 px, blurred 12 px, softened 6 px, normalised by its 95th percentile), inside an envelope that covers the painted haze: its bluish brightness (luminance between the 25th and 90th percentiles of its rows, times B - R from 4 to 16 levels) blurred 14 px and mapped from 0.12 to 0.4, inside rows rising from 300 to full at 360, full to 700, gone at 790, and zero on the hut, its chimney and porch (`HUT`). It covers 98.4 percent of the painted haze at half or more.
- Low fog: its rows (rising from 690 to full at 745, full to 930, gone at 1020), zero on the hut's walls above row 745 and 0.45 of itself over the walkway's near planks and posts (`WALKWAY`), times 0.3 plus 0.7 times the painted fog's bluish brightness (between the 20th and 90th percentiles).

### Arrival drift

Rectangles are each window's bounds with 2 px of margin, in concept px (x, y, w, h, top-left origin); the asset holds them, the tile sizes and velocities in picture widths and heights (rectangle origin bottom left, velocity up positive), as the script's report gives them. Speeds are concept px per second.

| Layer | Rect | Velocity | Opacity | Swell, period |
| --- | --- | --- | --- | --- |
| FogFar | 0, 300, 941, 489 | 7 right | 0.7 | 0.08, 83 s |
| FogFarWisps | 0, 300, 941, 489 | 11 right | 0.9 | none |
| FogNear | 0, 689, 941, 330 | 33 right | 0.75 | 0.15, 27 s |

Drawn in that order. The script simulates the layers over the picture as the drift layer draws them. The far haze's window changes 1.9 levels a second at the median from one second to the next, and 7 percent of it, mostly the bright haze right of the porch behind the narration panel, where the painted haze matches the tiles' colour, barely changes. At the arrival beat the narration panel covers rows 592 to 1420 at phone size, so the far haze shows above it and the low fog only at the panel's sides. At 443 wide the journey screen shows the picture at the same scale and crops its sides about its centre (its Horizontal Crop Anchor, 0.5), as "Framing at narrow widths" in [presentation authoring](../../PRESENTATION-AUTHORING.md) describes; its layers follow the crop.

### Arrival plume

The chimney's painted plume, a thin curl rising from the chimney's cap at about (233, 248) and bending right to about (470, 175), moves on the picture scene's one flow layer, drawn before the drift layers. Nothing is drawn over the painting: the layer redraws the plate inside its rectangle, pulled along the plume, and the painting outside the window stays exactly as it is. The window and flow are measured from the picture by `author-sunken-larder-arrival-plume.py`; no donor is used.

| Texture | Format | Content |
| --- | --- | --- |
| SunkenLarderArrivalPlumeWindow.png | RGBA 317 x 144, white | Alpha is the window over the layer's rectangle. Imports with sRGB off, clamped, no mipmaps, uncompressed. |
| SunkenLarderArrivalPlumeFlow.png | RGB 317 x 144 | RG the flow (direction times relative speed, encoded * 0.5 + 0.5, x right, y up), B the phase noise. Imports linear (sRGB off), clamped, no mipmaps, uncompressed. |

- Centre line: measured, not drawn. In every second column from 244 to 484 the mean row of the plume's presence squared (the picture's luminance above its grey opening of 41 px, blurred 10 px, softened 3 px), within 30 rows of a rough guide line that keeps the measure off the lit branches, smoothed 12 px along x, starting at the cap's top (233, 248). Every 20 px along it: (233, 248), (250, 238), (269, 234), (288, 226), (303, 214), (321, 205), (340, 211), (359, 208), (375, 196), (390, 183), (407, 173), (427, 173), (447, 174), (467, 175); 277 px long.
- Window: the presence from 7 to 22 levels (smoothstep), spread 4 px past the smoke and softened 3 px, so it is full on the smoke and fades to zero just beyond it; inside a band full to 26 px from the centre line and none past 40 px; faded out over 24 px past the line's end and 6 px below the cap's top; zero on the chimney's cap and flue and within 1 px of them, full from 14 px. 13113 px are at half or more.
- Flow: along the centre line, rising straight up out of the chimney and turning onto the line over its first 20 px; relative speed 1 on the line, 0.7 at the band's edge, so the sides lag and the wisps shear; 0.1 at the cap, rising to 1 at 50 px from it, so wherever the window is above 0.2 no phase pulls a pixel as far as its distance from the cap (at most 0.92 of it) and the cap's colour does not enter the smoke. Mild low-frequency noise (sigma 14 px) turns the direction by 18 degrees (one deviation) and scales the speed by up to 15 percent either way, so the wisps break up. B is a broader noise (sigma 24 px).
- Values: Rect (0.204038, 0.836124, 0.336876, 0.086124), concept (192, 130, 317, 144); Speed 12 px/s, a slow drifting plume, a little slower than the cauldron steam's rise (14.8 and 22.1 px/s), so the smoke leaves the chimney calmly into still night air; Phase Period 3 s, so a phase pulls the plate at most 36 px, about one painted wisp, before it fades out, and the drawn motion repeats every 1.5 s; Noise Strength 0.5, half a period of phase spread across the plume, so its parts do not fade in unison.
- Zero displacement: the script draws the layer with no speed over the background, both sampled as the screen samples the plate (bilinear, the background stored at 8 bits) and blended in linear light, at 2x over the plume and at 540 x 960. It differs from the plate by 0 levels everywhere: with no displacement both phases sample the plate where the background does. On the GPU the background is stored at 8 bits and the layer's sample is not, so a difference of at most 1 level is possible where texture filtering lands a value near a rounding boundary.

The narration panel at the arrival beat covers rows 592 and below at phone size, so the whole plume shows above it. John has not seen it in play.

### Arrival review

The script's review images (`review/` of its output) are `far-frames.png` and `near-frames.png` (each region of the picture, then the layers over the picture at eight moments 1 s apart, 1.5x), `one-second-change.png` (the haze's window: green the mean change from one second to the next, red where no step changes a pixel by a level), `windows.png` (the windows tinted over the picture with their rectangles), `tiles.png` (each tile 2 x 2 in its colour), `frames-540.png` (the whole picture at 540 x 960 two seconds apart) and `change-x4.png` (the most each pixel changes over half a minute, x4).

The plume script's review images (`review/` of its output) simulate the flow layer's shader over the picture: `plume-frames.png` (the plume region beside the picture without the layer, then at eight moments 0.5 s apart, 2x), `plume-window.png` (the window tinted over the picture with the rectangle, the centre line and the mouth, 2x), `plume-flow.png` (the flow as arrows over the picture, their length the relative speed, 2x) and `frame-540.png` (the whole picture with the flow layer at 540 x 960). Its report holds the zero-displacement check.
