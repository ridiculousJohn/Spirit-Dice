# Bound Tree layers

Reference for the tree nymph encounter's scene art: what each texture and mask is for, how the layers register, and how to rebuild them. Artwork is under `Dice-Unity/Assets/Dice/Art/BoundTree` (paths below are relative to it unless stated). Coordinates are concept pixels with a top-left origin. These are the first cuts and their lighting split (see Relighting); moss, mist and creature layers are not built yet.

## Textures

Every texture is 941 x 1672 in the concept's frame, so all layers register by drawing them over the same rect; no offsets or scales are involved. They import like BoundTreeOriginal.png.meta (sRGB, bilinear, clamped, uncompressed, no mipmaps or non-power-of-two scaling, alpha is transparency).

| Texture | Format | Content |
| --- | --- | --- |
| BoundTreeOriginal.png | RGB | The approved scene concept. Source of every layer; never edited. |
| BoundTreeBackground.png | RGB | Clean plate: the concept with the nymph, her crown, bindings and shadow removed and the trunk base, roots, fallen leaves and swamp behind her reconstructed within 22 px of her silhouette (full weight within 14 px, so her idle and flinch motions uncover only reconstruction). Identical to the concept outside that declared region. |
| BoundTreeNymph.png | RGBA | The nymph: a generated derivative, not concept pixels. Her transparent extraction (Docs/Reference/Art/BoundTreeNymphDonor.png) registered to the concept frame, its alpha her matte (soft on hair strands and crown twigs) and its colour low-frequency matched to the concept on her visible pixels. It holds her hair, leaf crown, garment, the root bindings on her wrists and ankles, and the hem and feet the scenery hides in the concept: the hem resting on the ground and both feet with their toes. Roots lying on the ground stay on the plate. |
| BoundTreeFaceless.png | RGB | BoundTreeBackground.png with the spirit's face (eyes, brows, nose ridge, cheekbones, split mouth and its green glow) replaced by plain split bark, for the victory fade. Identical to BoundTreeBackground.png outside the declared face region. |
| BoundTreeFaceMask.png | RGBA, white | Alpha 1 over the whole face transfer region with a 2 px soft edge outside it. |

Draw order: BoundTreeFaceless.png, then the face layer (BoundTreeBackground.png through BoundTreeFaceMask.png alpha, for example SourceMaskedImage.shader, which reads the mask's alpha), then BoundTreeNymph.png. With the face layer at full opacity this reproduces BoundTreeBackground.png exactly, because the mask is 1 wherever the two differ; fading its opacity to 0 sinks the face into the bark. The nymph composited over the plate reads as the concept at rest; it differs from it where her generated extraction differs in fine detail and where she completes what the concept hides (the hem and toes over the leaves), within the error reported by the script.

## Generated edits

The plate and faceless reconstructions follow "Generated edits" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md): the generated image is a donor, registered to the concept frame by an affine fit, and only pixels inside the declared region are transferred, with the donor's low-frequency colour matched to the surrounding image (masked Gaussian averages at 24, 10 and 4 px, coarse to fine) and a linear blend from full weight near the edited shape to zero at the declared edge. Pixels outside a declared region stay byte-identical. The nymph is a generated transparent extraction, made the way MerchantHost.png was (see "Scene plates and sources" there), and installed whole after registration and colour matching.

| Donor | Edit target | Installed as | Region |
| --- | --- | --- | --- |
| Docs/Reference/Art/BoundTreeNymphDonor.png | concept box (96, 576)-(416, 1056) | BoundTreeNymph.png | her alpha |
| Docs/Reference/Art/BoundTreeBackgroundDonor.png | concept box (20, 520)-(460, 1180) | BoundTreeBackground.png | within 22 px of the nymph silhouette, full weight within 14 px |
| Docs/Reference/Art/BoundTreeFacelessDonor.png | concept box (290, 100)-(710, 730) | BoundTreeFaceless.png | within 12 px of the face polygon, full weight within 4 px |

Each edit target is a Lanczos 1024 x 1536 upscale of its concept box. Registration maps donor pixels from concept pixels by `donor = M (p - c) + t` (c the box centre), fitted by normalized cross-correlation of high-passed luminance on a 3 px grid inside the box: for the nymph on her interior (at least 6 px inside her silhouette under the upscale's own mapping), for the plate and face donors excluding pixels within 26 px (nymph silhouette) or 16 px (face polygon) of the edit. The fitted values are in the script's report.json. The prompts are in [prompts/](prompts/README.md); the other candidate of each generation and the edit targets are working files under `Docs/work/bound-tree-layers`.

## Nymph

- Matte: the donor's own alpha, rescaled so its levels 2 and 250 map to 0 and 1 (the extraction's interior sits at 250 to 253), warped with the registration. Hair strands and crown twigs keep the extraction's soft alpha.
- Colour: the donor's colour, unpremultiplied where her alpha is above 0.05 and spread outward over a few px (normalized Gaussian, sigma 3 px) so bilinear filtering at her edge does not mix in black. Its low-frequency colour is matched to the concept (sigmas 24, 10 and 4 px) on her visible pixels: at least 4 px inside her alpha-0.5 silhouette, excluding pixels that still differ from the concept by 40 levels or more after a 24 px match (leaves in front of her hem and feet in the concept). Elsewhere, including the completed hem and toes, the match is extrapolated from those pixels.
- Silhouette: every pixel with alpha at or above 0.02. The plate is reconstructed at full weight within 14 px of it; the motion margin the script checks is 12 px, which covers the 1.03 scale about her seat (about 11 px at the top of her head) and small offsets.

## Rebuilding

`Tools/Presentation/cut-bound-tree-layers.py` (Python 3.9+, standard library only) rebuilds all four textures from the concept, the three donors and BoundTreeLayers.json:

```
python Tools/Presentation/cut-bound-tree-layers.py                 # fresh Docs/work/bound-tree-layers-* folder
python Tools/Presentation/cut-bound-tree-layers.py --write-assets  # also replaces the installed textures
```

The output folder holds the textures under `layers/`, review images under `review/` (plate, concept beside nymph over plate, nymph on grey and checker, matte, composite error, the nymph moved 4 px left and right, 6 px up and scaled 1.03 about her seat over the plate, faceless trunk, face over faceless at full and half opacity, and a difference image for each transfer with the declared edge in cyan) and `report.json` with the registrations and checks. To change a region, edit its polygon or distances in BoundTreeLayers.json, rebuild into Docs/work and review before writing assets. A replacement donor must be an edit of the same target box at 1024 x 1536 (RGBA with a transparent background for the nymph).

## Relighting

The textures in `Relight` separate the painted lighting into a moonlit ambient base and a reference lit by a strong flare of the spirit's glow, with a response map, following "Relighting sources" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md). The moon is part of the ambient base. All are 941 x 1672 in the concept frame and register like the layers above.

| Texture | Format | Content |
| --- | --- | --- |
| BoundTreeAmbientUnlit.png | RGB | BoundTreeBackground.png under moonlit ambient only: eyes and mouth dark, no glow light on the bark. Changed only within 60 px of the glow source (full weight within 30 px); byte-identical to BoundTreeBackground.png elsewhere. No channel is above BoundTreeBackground.png. |
| BoundTreeGlowLit.png | RGB | The ambient base plus the flaring glow: the eyes and mouth burning yellow-green, the light on the face, trunk, moss and hanging strands falling off with distance and faintly reaching the roots, leaves and ground around the nymph. Every channel is at least the unlit value. Sky, moon and distant swamp are unchanged. |
| BoundTreeFacelessUnlit.png | RGB | BoundTreeFaceless.png under ambient only (the faceless trunk has no glow source; victory draws it unlit). Equal to BoundTreeAmbientUnlit.png wherever BoundTreeFaceMask.png alpha is below 1. |
| BoundTreeNymphUnlit.png | RGBA | The nymph under ambient only. The concept's glow does not reach her, so it equals BoundTreeNymph.png. |
| BoundTreeNymphGlowLit.png | RGBA | The nymph with the flare's faint yellow-green light from the upper right (head, crown, hands, knees, shins, right shoulder and dress, feet). Alpha equals BoundTreeNymph.png; every channel is at least the unlit value. |
| BoundTreeGlowResponse.png | RGB, linear data | R: normalized distance from the glow. G: the glow source mask (eyes and split mouth). B: 0. |

Colour textures import like BoundTreeOriginal.png.meta, the nymph pair like BoundTreeNymph.png.meta and the response map like HauntedMerchant/MerchantLanternResponse.png.meta (sRGB off); all bilinear, clamped, uncompressed, without mipmaps or non-power-of-two scaling. Content/Presentation/BoundTree/BoundTreeSceneLighting.asset uses them (see "Scene lighting and preview").

### Conventions

- Lighting pairs follow SceneLighting.shader in linear light: glow 1 = unlit + max(glowlit - unlit, 0) * tint * response scale; glow 0 = unlit. glowlit >= unlit in every channel of every pixel, so glow 1 reproduces the glow-lit texture.
- Two scene lights share the ambient base BoundTreeAmbientUnlit.png. Light 0, the painted glow: reference BoundTreeBackground.png (the concept's own resting glow; every channel is at least the unlit value), so light 0 at 1 reproduces the concept exactly; the idle pulse varies it around 1. Light 1, the flare: reference BoundTreeGlowLit.png, 0 at rest and raised for reactions. Victory takes both to 0. The nymph has no painted glow, so only light 1 reaches her (BoundTreeNymphUnlit.png is the base, BoundTreeNymphGlowLit.png the light 1 reference). The faceless trunk draws BoundTreeFacelessUnlit.png. Both lights use BoundTreeGlowResponse.png.
- A single light cannot serve both: the flare paints the eyes as glowing discs and fills the mouth's opening with light where the concept has pinpoint pupils and a dark opening with lit rims, so no fraction of the flare reproduces the concept (best least-squares fraction 0.118; the build report lists the fit per region).
- Response: R = min(1, hypot((x - 500) / 620, (y - 420) / 860)) in continuous concept pixels (pixel centres at +0.5), the convention of MerchantLanternResponse.png; it is a 2D image-plane distance. G is 1 inside the eye ellipses (centres (441, 244) and (568, 274), radii 17 x 13 and 14 x 10) and the mouth polygon (`CRACK` in the script), falling to 0 over 6 px outside (smoothstep).

### Method

This is the merchant method: each donor is generated from an edit target and registered to the concept frame by an affine fit of locally contrast-normalized, high-passed luminance plus a smooth residual displacement (`register` in author-merchant-relight.py, imported by the script). The illumination change is a linear-light ratio map, ratio = G(w * donor) / G(w * source) per channel with a 3 px Gaussian, with the merchant's highlight cap (1.15 times the donor's local maximum, 9 px window) and saturation cap (1.1 times, floor 0.08). Relit colour is source * ratio, so painted detail is the source's own.

| Donor | Edit target | Transferred into |
| --- | --- | --- |
| Docs/Reference/Art/BoundTreeRelightUnlitDonor.png | BoundTreeBackground.png box (290, 100)-(710, 730) | BoundTreeAmbientUnlit.png, BoundTreeFacelessUnlit.png, BoundTreeNymphUnlit.png |
| Docs/Reference/Art/BoundTreeRelightGlowDonor.png | BoundTreeAmbientUnlit.png box (40, 60)-(800, 1200), with the same box of BoundTreeBackground.png as a reference image for where the glow is | BoundTreeGlowLit.png |
| Docs/Reference/Art/BoundTreeRelightNymphGlowDonor.png | BoundTreeNymph.png over BoundTreeGlowLit.png, box (96, 576)-(416, 1056) | BoundTreeNymphGlowLit.png |

Each target is a Lanczos 1024 x 1536 upscale of its box. Registration excludes pixels within 10 px of the glow source on the plate (their look changes most) and uses her interior (alpha above 250) for the nymph.

- Drift: a generated edit also shifts the tone of what it should leave alone. Each ratio is divided by the donor's drift, the normalized Gaussian average (40 px, falling back to 160 px) of log ratio over pixels the light cannot change: the unlit donor's box beyond 80 px of the source; for the glow donor the distant swamp and sky left and right of the trunk (boxes (48, 300)-(140, 560), (700, 380)-(792, 700) and (700, 68)-(792, 300)); for the nymph donor the glow-lit plate 3 to 40 px outside her alpha, which her target already shows lit.
- Unlit: the ratio is at most 1 (removing light only darkens) and fades to 1 between 30 and 60 px from the source; beyond that the plate is copied byte for byte.
- Faceless: the unlit field (ratio and caps) is harmonically filled in log space over the face transfer region (BoundTreeFaceMask.png alpha 1) from the plate around it and applied to BoundTreeFaceless.png; elsewhere it is the plate's field, so the faceless output equals the unlit plate there.
- Nymph unlit: the unlit field, which is the identity over her.
- Glow-lit: the ratio is at least 1 (the glow only adds light) and kept only where its gain has the glow's colour (G / R gain, blurred 4 px, weighted by smoothstep from 0.3 to 0.6; this drops the donor's re-painting of dark roots). It is weighted by 1 - smoothstep((R - 0.55) / 0.45), so the light reaches zero at R = 1, and by a receiver weight that is 0 on the sky, moon and distant swamp: 1 - smoothstep((log2(B / R) - 0.3) / 1.0) on the unlit plate in linear light blurred 2 px (tree, moss, leaves and ground have B / R about 0.3 to 0.8, the distant background about 3), times 0 inside the moon circle at (804, 184), radius 72. Beyond the donor's box the field continues blurred (16 px) and fades to the identity over 60 px. Inside the source mask the registered donor's drift-corrected colour is a floor, so the eyes and mouth emit. Glow-lit = max(relit, unlit) per channel.
- Nymph glow-lit: the same on her alpha, with her own donor and drift and no receiver weight.

### Rebuilding the relighting

`Tools/Presentation/author-bound-tree-relight.py` (Python 3.10+ with numpy, Pillow and scipy; on this machine `Docs/work/merchant-table-clear/.venv/Scripts/python.exe`) runs from the repository root:

```
python Tools/Presentation/author-bound-tree-relight.py targets --from <build>/textures  # edit targets under Docs/work/bound-tree-relight/targets
python Tools/Presentation/author-bound-tree-relight.py build                           # fresh Docs/work/bound-tree-relight-* folder
python Tools/Presentation/author-bound-tree-relight.py build --write-assets            # also writes Art/BoundTree/Relight
```

`targets` writes the unlit target and, from the textures in `--from` (default `Art/BoundTree/Relight`), the glow target with its reference image and the nymph target. `build` writes the textures under `textures/`, review images under `review/` (concept, unlit, glow-lit and resting composite, full frame and the face and nymph at 2x; intensity strips at 0, k, 1 and 1.5; the nymph pairs over their plates; the faceless unlit beside the unlit plate; the glow contribution; R and G) and `report.json` (registrations, drift, verification and the resting fit). `--donor-unlit`, `--donor-glow` and `--donor-nymph-glow` substitute a candidate donor; `--save-registered` also writes the registered donors. `--write-assets` keeps existing .meta files and writes a .meta with a fresh GUID for a texture that has none. The prompts are in [prompts/](prompts/README.md); the other candidates and the edit targets are working files under `Docs/work/bound-tree-relight`.

## Scene lighting and preview

`Content/Presentation/BoundTree/BoundTreeSceneLighting.asset` (under `Dice-Unity/Assets/Dice`) lights the scene by the conventions above: shader `Dice/Scene Lighting`, Base Texture `Relight/BoundTreeAmbientUnlit.png`, and two lights sharing `Relight/BoundTreeGlowResponse.png`.

| Setting | Light 0, painted glow | Light 1, flare |
| --- | --- | --- |
| Contribution | BoundTreeBackground.png | Relight/BoundTreeGlowLit.png |
| Response Map | Relight/BoundTreeGlowResponse.png | Relight/BoundTreeGlowResponse.png |
| Contribution Includes Base | on | on |
| Constrain Ambient To Reference, Modulate By Base Color | off | off |
| Tint, Intensity, UV Offset | white, 1, (0, 0) | white, 1, (0, 0) |
| Distance Falloff, Source Response | 0.8, 0.5 | 0.8, 0.5 |
| Flicker Amount, Speed, Seed | 0.12, 0.35, 4127 | 0, 0, 0 (no flicker) |
| Receiver Intensity | 0 | 0 |

- Reproduction: at a sampled intensity of exactly 1 the response scale is 1 for every pixel whatever Distance Falloff and Source Response are, so each light adds max(reference - base, 0) and reproduces its reference: light 0 alone gives BoundTreeBackground.png, light 1 alone BoundTreeGlowLit.png, and both together add the flare to the resting glow. Below 1, Distance Falloff 0.8 dims bark farther from the glow (response R) sooner than bark near it, and Source Response 0.5 keeps the eyes and mouth (response G) burning longer, as for the merchant's lantern.
- Pulse: light 0's idle pulse is the shared flicker noise, intensity = multiplier * (1 + 0.12 * noise). Its main band changes about every 4 s at speed 0.35, so it swells slowly between about 0.90 and 1.11 and passes through exactly 1. Light 1 has no flicker; its level is exactly its multiplier.
- Receivers: no flat receivers or computed lights use these lights; their receiver fields, Receiver Ambient and Receiver Key are the class defaults with Receiver Intensity 0.

`Assets/Scenes/BoundTreePreview.unity` is the isolated art preview; it is not registered in the campaign, Addressables or build scenes. It draws, in order:

| Object | Content |
| --- | --- |
| Preview Camera | Orthographic size 480, solid dark background, UI layer only. |
| Bound Tree Preview Canvas | Screen Space - Camera on the preview camera; Canvas Scaler 540 x 960, match 0.5. No raycaster. |
| Bound Tree Plate | RawImage `Relight/BoundTreeAmbientUnlit.png` filling the 540 x 960 authoring frame, with a SceneLightingPresentation: Lighting Asset BoundTreeSceneLighting.asset, Background this image, Intensity Multipliers [1, 0], Lit Images [Nymph]. It draws the ambient base plus lights 0 and 1. |
| Bound Tree Plate/Faceless Trunk | RawImage with `Content/Presentation/BoundTree/BoundTreeFaceless.mat` (Source Masked Image: `_SourceTex` Relight/BoundTreeFacelessUnlit.png, `_LayerMask` BoundTreeFaceMask.png, `_InvertMask` 0), unlit, colour alpha 0. |
| Bound Tree Plate/Nymph | SpiritPortraitImage (a RawImage) with `Content/Presentation/BoundTree/BoundTreeNymphLit.mat` (Scene Lit Image), mesh `NymphBreathMesh.asset`, and a SceneLitImage on BoundTreeSceneLighting.asset: Base Texture Relight/BoundTreeNymphUnlit.png, light 1 Reference Relight/BoundTreeNymphGlowLit.png with Relight/BoundTreeGlowResponse.png, no light 0. Her alpha is the base texture's. A native Animation component plays `NymphSob.anim` on loop (see Nymph motion). |
| Bound Tree Plate/Nymph/Breath, Nymph/Flinch | SpiritPortraitDeformationChannel on the Nymph image, shape 0 (Breath) and shape 1 (Flinch), weight 0 at rest. |

Faceless Trunk and Nymph stretch over the plate's rect, so every layer registers to the concept frame like the plate. No image is a raycast target.

To drive it, enter Play mode and select Bound Tree Plate: Scene Lighting Presentation > Intensity Multipliers element 0 is the glow and element 1 the flare (applied every frame; values above 1 brighten further). Faceless Trunk > Raw Image > Color alpha fades the face into the bark. The states of the art plan:

| State | Light 0 | Light 1 | Faceless alpha |
| --- | --- | --- | --- |
| Rest | 1 (pulsing) | 0 | 0 |
| Flare (reaction, defeat) | 1 | raised, 1 matches BoundTreeGlowLit.png | 0 |
| Glow off | 0 | 0 | 0 |
| Victory | 0 | 0 | 1 |

The faceless trunk draws unlit, so it matches the bark around it only with both lights at 0; with a light above 0 the face region stays at ambient while the surrounding bark glows. At rest, when the pulse passes 1, the rendered plate with the nymph equals BoundTreeBackground.png with BoundTreeNymph.png over it.

## Nymph motion

The nymph is one layer, so nothing on her moves independently: one smooth displacement field over the 941 x 1672 frame moves her whole figure and no seam can open. `Content/Presentation/BoundTree/NymphBreathMesh.asset` is a 96 x 168 grid (about 9.8 x 10 concept px per cell) with two shapes, Breath (shape 0) and Flinch (shape 1). `Tools/Presentation/author-bound-tree-presentation.py` generates it; its constants are at the top of the script. The rest pose (both weights 0) is the exhaled source registration, so the rest render equals the lit preview without motion.

Both shapes share two envelopes:

- Rows: full above concept row 735 (the shoulder line), a smooth fade to zero at row 870 (the top of the ankle bindings). Everything from row 870 down, the bindings, dress hem and feet, never moves, so the bindings stay joined to the roots on the plate.
- Her region: full weight within 16 px outside her silhouette (alpha at or above 0.02, concept box (103, 635)-(377, 997)), falling to zero at 34 px. The inner distance exceeds a cell diagonal, so the grid's bilinear interpolation never reduces the motion of her own pixels; nothing beyond 34 px of her moves. The plate is reconstructed to full weight within 14 px of her, more than her largest displacement.

Soft ellipses (centre, radii; full inside 0.6 of the radius, none beyond 1.5) shape the upper body: head with the hands at her face (288, 695) 62 x 62, shoulders and upper back hump (212, 735) 62 x 44, curve of the back (160, 790) 52 x 72.

| Point (concept px) | Breath at full weight (right, up) | Flinch at full weight (right, up) |
| --- | --- | --- |
| Crown (270, 645) | 0, 4.0 | 4.0, -2.2 |
| Face and hands (300, 715) | 0, 4.0 | 4.0, -2.5 |
| Shoulder (205, 730) | -0.4, 5.0 | 4.0, 2.5 |
| Upper back (160, 780) | -1.1, 3.0 | 3.0, -0.5 |
| Knees (300, 805) | 0, 1.9 | 1.9, -0.5 |
| Shins (320, 850) | 0, 0.2 | 0.2, -0.1 |
| Ankle bindings, hem, toes | 0 | 0 |

- Breath: the upper body lifts 4 px (LIFT), the shoulders 25% more (SHOULDER_SHARE, 5 px), and the back rounds up to 1.5 px outward to image left (ROUND). Largest strain on her pixels (Jacobian deviation) 0.07.
- Flinch: the upper body draws 4 px toward the tree at image right (SHIFT) and 1 px down (DROP), the head tucks a further 1.5 px down (TUCK) and the shoulders hunch 3.5 px up (HUNCH), so the shoulders rise about 2.5 px while the head sinks about 2.5 px. Largest strain 0.13; with Breath also at full, largest displacement 8.4 px and strain 0.17. No sample folds.

The script's report lists these values, the largest displacement on her pixels at or below row 870 (under 0.01 px, inside the one cell row that spans it) and the farthest moving vertex outside her silhouette.

### Clips

Both clips are legacy AnimationClips authored like `CanopyFlutter.anim`: one scalar curve each on a `SpiritPortraitDeformationChannel` weight, keys with flat tangents, so each segment eases in and out. Edit the keys in the Animation window with the Nymph selected.

- `NymphSob.anim` (13.9 s, loop) drives `Breath`: three sobs, each of short catching inhales (a quick rise of about 0.25 s, then a small catch back), a longer shaky exhale that steps down with small reversals, and a pause at rest.

| Sob | Starts | Inhale (time s: weight) | Exhale reaches rest | Pause until |
| --- | --- | --- | --- | --- |
| 1 | 0 | 0.30: 0.38, 0.44: 0.33, 0.70: 0.70, 0.83: 0.65, 1.10: 1.00, 1.32: 0.97 | 3.85 (via 0.74, 0.78, 0.48, 0.52, 0.24, 0.27) | 4.90 |
| 2 | 4.90 | 5.22: 0.46, 5.37: 0.41, 5.64: 0.80, 5.82: 0.78 | 7.75 (via 0.55, 0.59, 0.30, 0.33) | 8.55 |
| 3 | 8.55 | 8.80: 0.27, 8.92: 0.24, 9.20: 0.56, 9.31: 0.53, 9.62: 0.92, 9.84: 0.90 | 12.95 (via 0.70, 0.74, 0.50, 0.54, 0.31, 0.34, 0.14, 0.16) | 13.90 |

- `NymphFlinch.anim` (2.2 s, Once) drives `Flinch` only: 0 to 1 in 0.12 s, a held tremor (0.94 at 0.38 s, 0.97 at 0.58 s), then a slower recovery (0.62 at 1.05 s, 0.25 at 1.6 s) to 0 at 2.2 s, ending at rest. Because it binds only the Flinch channel, it can play on a higher Animation layer over the looping sob without stopping the breath. What triggers it in the encounter is not decided.

The Nymph's Animation component lists both clips, with `NymphSob` as its default clip: Play Automatically, Wrap Mode Loop, Culling Always Animate.

### Viewing the motion

Enter Play mode in `Assets/Scenes/BoundTreePreview.unity`: the sob starts at once and loops. To see the flinch pose, select `Bound Tree Plate/Nymph/Flinch`, set the Spirit Portrait Deformation Channel's Weight (1 is the full flinch, combined additively with the breath) and then turn the component's enabled checkbox off and on. The channel applies its weight when it is enabled and when an animation samples it; the sob clip does not sample the Flinch channel, so a changed Weight alone shows nothing until the component is re-enabled (measured in the Play-mode check). Set it back to 0 the same way. The same works in Edit Mode but then saves the weight into the scene. In Edit Mode the Animation window can also preview either clip with the Nymph selected.

Captures, difference images and the contact strip of one sob loop are in `Docs/work/bound-tree-nymph-motion/captures`.
