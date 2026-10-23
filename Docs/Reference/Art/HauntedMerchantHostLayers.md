# Merchant and candle layers

Reference for the haunted merchant scene's art: what each texture, mask and material is for, how the layers register and move, and how to rebuild or change them. Artwork is under `Dice-Unity/Assets/Dice/Art/HauntedMerchant` (paths below are relative to it unless stated); presentation assets are under `Dice-Unity/Assets/Dice/Content/Presentation/Ambient`. Coordinates are native source pixels with a top-left origin unless stated.

## Scenes

`MerchantEncounter.unity` (the playable encounter) and `AmbientMotionPreview.unity` (the isolated art preview) draw, in order: the props-removed background plate, the four movable props, the canopy cloth, the hanging cup and MerchantHost.prefab. The encounter registers these under its `Merchant Scene` frame and the preview under `Merchant Ambient Plate`; both are the 540 x 960 authoring frame the background fills. The preview also places CandleParts.prefab (body/wick and flame layers); in the encounter the protective candle is its movable table piece. None of the layers intercepts pointer input.

Lighting comes from `MerchantSceneLighting.asset`: ambient base `Props/MerchantAmbientUnlitPropsRemoved.png`, light 0 the lantern (reference `Props/MerchantLanternOnlyPropsRemoved.png`, response `MerchantLanternResponse.png`), light 1 the candle (footprint and response maps, see Candle light). SceneLightingPresentation samples it in the preview and SpiritEncounterView in the encounter. The merchant layers, canopy cloth, props and cup are lit layers (see "Lit layers" in Docs/PRESENTATION-AUTHORING.md): each draws an unlit base and adds the lantern through a lantern-lit reference; the merchant also receives computed candle light.

## Generated edits

Every generated edit of installed art is installed only through registration and masked transfer, never wholesale: the generated image is a donor, registered to the native frame, and only pixels inside a declared mask are transferred, with the donor's low-frequency colour matched to the surrounding source and the boundary blended. Pixels outside the mask stay byte-identical. Preserved donors:

| Donor | Transferred into |
| --- | --- |
| Docs/Reference/Art/HauntedMerchantHeadRepairDonor.png | MerchantHeadRepaired.png |
| Docs/Reference/Art/HauntedMerchantTorsoRepairDonor.png | MerchantTorsoAnimation.png |
| Docs/Reference/Art/HauntedMerchantRelightUnlitDonor.png | Relight merchant `*Unlit.png` textures |
| Docs/Reference/Art/HauntedMerchantRelightLanternDonor.png | Relight merchant `*LanternLit.png` textures |
| Docs/Reference/Art/HauntedMerchantRelightCanopyUnlitDonor.png | Relight/CanopyUnlit.png |
| Docs/Reference/Art/HauntedMerchantRelightCupUnlitDonor.png | Relight/HangingCupUnlit.png |
| Docs/Reference/Art/HauntedMerchantRelightCupLanternDonor.png | Relight/HangingCupLanternLit.png |

The blink, table-clear and prop-removal donors are working files under `Docs/work` (gitignored): `merchant-blink/donors/blink-v1-1.png`, `merchant-table-clear/donors/unlit_donor-2.png` and `merchant-props/donors/*-1.png`. Every prompt is saved in [prompts/](prompts/README.md).

## Scene plates and sources

| Asset | Size | Content |
| --- | --- | --- |
| MerchantOriginal.png | 941 x 1672 | The original scene concept. Canopy cloth source. |
| MerchantRegisteredRoofPlate.png | 941 x 1672 | Clean plate with the canopy overhang removed and the roof edge registered (see HauntedMerchantAttachmentRepair.md). Source for the three generated derivatives below. |
| MerchantBackground.png | 941 x 1672 | Generated removal of the merchant and candle from the registered plate. |
| MerchantHost.png | 941 x 1672, RGBA | Generated transparent extraction of the merchant. |
| ProtectiveCandle.png | 1142 x 1377, RGBA | Generated transparent candle and holder. |
| MerchantAmbientUnlit.png | 941 x 1672 | MerchantBackground.png under cool night ambient only, lantern and candle out. |
| MerchantLanternOnly.png | 941 x 1672 | MerchantAmbientUnlit.png plus the lit lantern and its light on the cart and roof. |
| Props/MerchantAmbientUnlitPropsRemoved.png, Props/MerchantLanternOnlyPropsRemoved.png | 941 x 1672 | The two lighting plates with the movable props and the cage removed (see Movable props). The scenes draw these. |

MerchantBackground.png, MerchantHost.png and ProtectiveCandle.png are generated derivatives, not pixel-exact cutouts. MerchantLanternOnly.png shares its ambient base with MerchantAmbientUnlit.png, so lantern-lit minus unlit isolates the lantern.

Both lighting plates have the bottle, cups and cloth on the far left of the tabletop removed: the unlit donor is transferred inside the polygon in `Docs/work/merchant-table-clear/PropsMaskHard.png` with an 8 px feather, and the lantern plate receives the same edit multiplied by a per-channel lantern gain field (blur(lit) / blur(unlit) outside the region, Laplace-interpolated inside). The scripts and manifest are in `Docs/work/merchant-table-clear`.

Registration to the 941 x 1672 background frame (RectTransforms convert to the 540 x 960 frame):

- Merchant: `x = 0.9136 * spriteX + 21`, `y = 0.94 * spriteY + 28.8`.
- Candle: `x = 0.115 * spriteX + 86.4`, `y = 0.15 * spriteY + 884`.

Artwork imports use bilinear filtering, uncompressed colour, no mipmaps and no non-power-of-two resizing; transparent assets keep their alpha.

## Merchant layers

MerchantHost.prefab has five layers. Each material uses SourceMaskedImage.shader to sample a full 941 x 1672 source through a mask, so all layers share the native frame and registration; MerchantHost.asset draws them lit with SceneLitImage.shader.

| Layer | Material | Source | Mask |
| --- | --- | --- | --- |
| Torso | MerchantTorso.mat | MerchantTorsoAnimation.png | MerchantTorsoAnimationMask.png |
| Head and hat | MerchantHead.mat | MerchantHeadRepaired.png | MerchantHeadCompleteMask.png |
| Blink | MerchantBlink.mat | MerchantBlinkEyes.png | MerchantBlinkMask.png |
| Resting arm | MerchantRestingArm.mat | MerchantHost.png | MerchantRestingArmMask.png |
| Supporting arm | MerchantSupportingArm.mat | MerchantHost.png | MerchantSupportingForearmAnimationMask.png |

- Torso: the complete torso, including the fixed upper supporting sleeve and reconstructed shirt, suspenders and neck behind the head and forearms. MerchantTorsoAnimationRepairMask.png declares the reconstructed pixels; outside it, colour and alpha equal MerchantHost.png.
- Head: the head and hat, with reconstructed cheek, ear, jaw and beard behind the supporting hand. MerchantHeadRepairMask.png declares every changed pixel, including a contact-shadow band at most 5 px into the visible head; outside it every RGBA value equals MerchantHost.png. The supporting-arm contour owns the complete knuckle and curled fingertips.
- Supporting arm: only the bare hand and forearm; its upper sleeve belongs to the torso.
- MerchantHeadCompleteMask.png and MerchantSupportingForearmAnimationMask.png use bilinear coverage at cut edges. Complete torso and head coverage overlaps the foreground parts, so concealed regions are backed.
- MerchantTorsoMask.png, MerchantHeadMask.png, MerchantSupportingArmMask.png and MerchantRestingArmMask.png are the binary, point-filtered partition of MerchantHost.png; only MerchantRestingArmMask.png renders.

Repairs: the head donor maps from MerchantHost.png by the affine `originalToDonor` in `Docs/Reference/Art/HauntedMerchantParts.json` (headRepair); the torso donor by source-to-donor scaling and translation with local shoulder and upper-sleeve adjustments (torsoRepair). HauntedMerchantParts.json also holds the partition polygons, pivots and material/mask GUIDs for the Merchant and Candle groups (candle body pivot (585, 1050), flame pivot (585, 434), split by CandleBodyMask.png and CandleFlameMask.png). Motion comes from MerchantHost.asset, not from that manifest.

### Breathing

`Tools/Presentation/author-merchant-presentation.py` generates MerchantHost.asset and MerchantBreathMesh.asset (`--write-assets`; without it, a preview under Docs/work). The mesh is a 48 x 84 grid with one `Breath` shape, a smooth displacement field over the native frame shared by all five layers, so overlapping layers move identically and no seam opens. Constants are at the top of the script:

| Region (source pixels) | Lift at full weight |
| --- | --- |
| Shoulders, chest and upper arms above y 760 | 6 px (LIFT), plus a lateral chest spread (SPREAD) |
| Head, hat and the hand under the cheek (HEAD_GROUP ellipses) | 70% of the shoulder lift (HEAD_SHARE) |
| Rows y 760..950 | Smooth fade to zero |
| Forearms, elbows and hands on the table (y 950 and below) | None |

The 13.8 s `Living still` idle loop holds three breaths of 4.4, 4.9 and 4.5 s at depths 1.0, 0.86 and 0.94 (eased inhale over 40% of the breath, a brief turn, a longer exhale, a pause). The rest pose is the exhaled source registration. Arrival, Reaction, Victory, Defeat and Farewell are 3 s additive breaths over the continuing idle with entry weights from an interrupted pose; each ends at rest.

The script also writes each layer's lighting: unlit base and lantern-lit reference from `Relight`, response `Relight/MerchantLanternResponseHost.png`, depth `Relight/MerchantDepth.png`, DepthRange (3.5, 1.5) rig Z for depth 0 and 1, candle strength 50, and the frame-to-rig mapping derived from the merchant registration and the candle rig in MerchantUnquietCartTable.asset (plate UV to rig: `x = -3.4 + 10 (u - 0.163656)`, `y = -1.5 + 8 (v - 0.336124)`, host lighting position (0, 1, 0)).

### Blink

The Blink layer draws directly above the head. MerchantBlinkEyes.png and MerchantBlinkMask.png are 136 x 58 crops of source box (264, 576)-(400, 634); the layer keeps the head's full-frame registration and its UV rectangle stretches the crop across the frame, with clamped, zero-alpha mask edges. Colour alpha is 0 at rest; the idle timeline raises it to 1 for about 120 ms at 1.55, 5.45 and 11.25 s. It breathes with the head.

The closed eyelids come from a donor generated from a 4x upscale of the visible composite in source box (205, 470)-(465, 730), registered by an affine fit over the face around the eyes (eyes excluded). Only two soft elliptical eyelid regions are transferred (eye ellipses (298, 614) and (366, 596), radii 22 x 13), with the donor's low-frequency colour matched to the source. Parameters are in `Docs/work/merchant-blink/blink-build.json`.

## Relighting sources

The textures in `Relight` separate the painted lighting into a cool night-ambient base, a lantern-lit reference and data maps for computed candle light. `Tools/Presentation/author-merchant-relight.py` (Python 3.10+, numpy, Pillow, scipy) rebuilds them from the preserved donors and keeps existing .meta GUIDs:

```
python Tools/Presentation/author-merchant-relight.py targets      # edit targets under Docs/work/merchant-relight/targets
python Tools/Presentation/author-merchant-relight.py build --replace --save-registered
python Tools/Presentation/author-merchant-relight.py cup-targets  # cup edit targets
python Tools/Presentation/author-merchant-relight.py cup --replace --save-registered
```

`--donor-unlit`, `--donor-lantern` and `--donor-canopy` substitute a candidate donor; `--out` writes elsewhere.

| Texture | Frame | Content |
| --- | --- | --- |
| MerchantHostUnlit.png | merchant 941 x 1672, RGBA | Cool night ambient only. Alpha equals MerchantHost.png. |
| MerchantHostLanternLit.png | merchant 941 x 1672, RGBA | Night ambient plus the lantern, no candle light. Every channel is at least the unlit value. |
| MerchantHeadRepaired{Unlit,LanternLit}.png | merchant 941 x 1672, RGBA | Head layer, same ratio maps. Alpha equals MerchantHeadRepaired.png. |
| MerchantTorsoAnimation{Unlit,LanternLit}.png | merchant 941 x 1672, RGBA | Torso layer, same ratio maps. Alpha equals MerchantTorsoAnimation.png. |
| MerchantBlinkEyes{Unlit,LanternLit}.png | 136 x 58 blink crop, RGBA | Blink crop, head-layer ratio maps. Alpha equals MerchantBlinkEyes.png. |
| MerchantDepth.png | merchant 941 x 1672, linear data | Depth for computed candle light in R (G and B repeat R). |
| MerchantLanternResponseHost.png | merchant 941 x 1672, linear data | MerchantLanternResponse.png in the merchant frame. |
| CanopyLanternLit.png | background 941 x 1672, RGB | The canopy cloth as painted: identical to MerchantOriginal.png. |
| CanopyUnlit.png | background 941 x 1672, RGB | The cloth under night ambient only, inside CanopyOriginalOverhangMask.png dilated 4 px; elsewhere identical to MerchantOriginal.png. |
| HangingCupUnlit.png | cup 1024 x 1536, RGBA | HangingCup.png under night ambient only. Alpha equals HangingCup.png. |
| HangingCupLanternLit.png | cup 1024 x 1536, RGBA | Night ambient plus the distant lantern from the upper left. Every channel is at least the unlit value. |

The arms use MerchantHostUnlit.png and MerchantHostLanternLit.png through their masks. Colour textures import like MerchantHost.png.meta (sRGB on); MerchantDepth.png and MerchantLanternResponseHost.png import like MerchantLanternResponse.png.meta (sRGB off); the cup textures import like HangingCup.png.meta. All are bilinear, clamped, uncompressed, without mipmaps or non-power-of-two scaling.

### Conventions

- Lighting pairs follow SceneLighting.shader in linear light: lantern 1 = unlit + max(lanternlit - unlit, 0) * tint * response scale; lantern 0 = unlit. lanternlit >= unlit holds in every channel of every pixel, so lantern 1 at full intensity reproduces the lantern-lit texture (for the canopy, the painted cloth).
- Depth: R = 0 farthest (torso back plane), R = 1 nearest (front-most hands and forearms on the table). It is authored procedurally from the masks and normalized so the merchant alpha spans 0..255: a torso plane with a chest bulge; the head (face and hat bulges) forward of the chest, blended into the torso over a 10 px Gaussian; the image-left sleeve rising from shoulder to elbow; the resting arm rising to its hand on the table (nearest); the bare supporting forearm from its elbow (near) to the hand under the cheek (in front of the face). A recession over 28 px inside the silhouette rounds the outer edges. Outside the alpha the field extends from the nearest silhouette pixel and is blurred. Shape constants are the DEPTH_* values in the script.
- Lantern response: MerchantLanternResponse.png R = min(1, hypot((x - 90) / 650, (y - 335) / 950)) in continuous background pixels (pixel centres at +0.5); G is the lantern source glow. Through the merchant mapping the lantern is at (75.525, 325.745) with radii (711.471, 1010.638) merchant pixels; MerchantLanternResponseHost.png evaluates that ellipse in R with G = B = 0. Both are 2D image-plane distances and ignore depth. The canopy and cup use MerchantLanternResponse.png directly.

### Method

Each donor is generated from an edit target that composites the art to be relit over the matching lighting plate at its registered placement (merchant-frame crop (0, 240)-(760, 1100), canopy background-frame crop (0, 0)-(720, 520), both upscaled 2x; the cup in its own frame). Registration fits an affine map by normalized cross-correlation of locally contrast-normalized, high-passed luminance inside the eroded region, then adds a smooth residual displacement from 40 px tile correlation peaks.

The illumination change is a linear-light ratio map, ratio = G(w * donor) / G(w * source) per channel, with a 3 px Gaussian G over the region interior w (alpha > 250 eroded 3 px), extended past the silhouette from the nearest interior value. Relit colour is source * ratio, so painted detail is the source's own. Each channel is capped at 1.15 times the donor's local maximum (9 px window), and saturation is capped at 1.1 times the donor's local maximum saturation (floor 0.08) by blending toward the pixel's luminance. Lantern-lit pixels take the per-channel maximum of the lantern and unlit results. The lantern donor's target is the unlit merchant over MerchantLanternOnly.png, so the lantern is added to the unlit result.

Per-layer textures use the MerchantHost-frame maps, so outside a layer's repair mask its output equals the host output. Inside MerchantHeadRepairMask.png and MerchantTorsoAnimationRepairMask.png the ratio and cap maps are a harmonic (Laplace) fill in log space from the surrounding visible pixels.

Canopy: the cloth is MerchantOriginal.png sampled through CanopyOriginalOverhangMask.png (the `_LayerMask` of CanopySource.mat and CanopyRoofSource.mat). No candle light reaches it. Each unlit cloth pixel keeps the donor's hue and is scaled uniformly (scale smoothed over 2 px) to fit under the painted value, then limited per channel.

Cup: `cup-targets` composites the painted cup over the scene resampled at its rest rect (props-removed plate, pot and pan cutouts and canopy, unlit or lantern-lit). Registration and caps are the merchant's, with the ratio Gaussian at 6 px because the cup is drawn at about 1/8 of native size. The cup image (`Swing/Cup` of each HangingCup.prefab instance) draws `Ambient/HangingCupLit.mat` (SceneLitImage.shader, `_SourceTex` HangingCupUnlit.png, no layer mask, `_InvertMask` 1) with a scene-instance SceneLitImage bound to MerchantSceneLighting.asset: base HangingCupUnlit.png, light 0 reference HangingCupLanternLit.png with MerchantLanternResponse.png, no candle light. Its MapRect is the cup's rest rect, plate (579.510, 239.201, 128.952, 193.325), from frame position (370, 144), image size 74 x 111 and pivot (0.506, 0.94): MapRect (0.615844, 0.741313, 0.137037, 0.115625). The response map is a smooth distance field over the cup, so sampling it at rest suffices while it swings.

## Candle light

The candle lights the table through two aligned 941 x 1672 data textures that its piece light translates with the candle's RectTransform: `CandleLightFootprintCentered.png`, a grey footprint multiplied by the table's ambient colour, and `CandleLightResponseCentered.png`, a linear response map whose R is the normalized distance from the candle. The footprint is padded with a dark border, so moving the candle moves only light, never a patch of wood texture or glare.

`Tools/Presentation/author-candle-light-footprint.py` (Python 3.9+, no third-party packages) derives both from the preserved sources `Docs/Reference/Art/HauntedMerchantCandleFootprintSource.png` and `Docs/Reference/Art/HauntedMerchantCandleResponseSource.png`, scaled by REACH 0.8 about the candle point (the response minimum): the value at the flame is unchanged and the distance stays normalized to the scaled reach. Default output is a new `Docs/work/candle-footprint-*` folder; `--write-assets` replaces the installed maps and keeps their .meta files.

The merchant receives the candle as computed light from MerchantDepth.png (see Breathing for the rig mapping). The canopy, props and cup receive no candle light.

## Movable props

Four poltergeist props are cut from the lighting plates into `Props`, each a `<Name>Unlit.png` / `<Name>LanternLit.png` pair with identical alpha. The props-removed plates show the reconstruction behind them, so each piece must be placed. `Props/props.json` holds every value below plus the silhouette primitives, mask paths and donor registrations. Coordinates are plate pixels.

| Piece | Motion | Crop rect (x, y, w, h) | Pivot (plate px) | Patch dilation |
| --- | --- | --- | --- | --- |
| HangingPot | swing from its ring on the post | 600, 407, 49, 86 | (614.5, 411.5) | 6 px |
| HangingPan | swing from the ring over the post | 556, 368, 73, 90 | (595.0, 373.5) | 7 px |
| ShelfBottle | rock on alternating base contacts (216.6, 383.2) and (242.4, 383.2) | 212, 320, 35, 67 | base centre (229.5, 383.2) | 4 px |
| TrunkLid | lift and drop on the hinge edge (170, 411.5)-(257, 423) | 166, 401, 97, 81 | hinge middle (213.0, 409.0) | 4 px |

Draw order is pot, pan, bottle, lid (the pan hangs in front of the pot). Each patch mask is the silhouette dilated by the piece's motion allowance plus 1 px (allowance 6% of the silhouette height for swings, 3% and at least 3 px for the bottle and lid), so a piece at its extreme never uncovers unreconstructed plate. The painted cage basket under the hanging cup (plate x 618..681, y 302..426) is removed from both patched plates as a removal (`removals` in props.json, 6 px margin), not a movable prop. Under the lid the patched plates show the open trunk: the front rim along the lid seam and a dark interior.

### Rebuilding

`Tools/Presentation/cut-merchant-props.py` (numpy and Pillow; `--no-sheet` skips the contact sheet) rebuilds every file in `Props` from MerchantAmbientUnlit.png, MerchantLanternOnly.png and the donors in `Docs/work/merchant-props/donors`:

- Silhouettes are vector primitives (ellipses, polygons, stroked lines) rasterized with 8x supersampling.
- Each region's donor is an edit of a Lanczos 1024 x 1536 upscale of an unlit-plate crop (pots (520, 252)-(712, 540), bottle (160, 280)-(300, 490), trunk (140, 360)-(290, 585)), registered by an affine fit on high-passed luminance around the props, props excluded.
- Inside the masks the donor's fine detail is scaled to the plate's local detail level, its low-frequency colour matched to the surrounding plate, and the transfer blends from full weight within 1 px of the silhouette to zero at the dilated mask edge.
- The lantern-lit patch is derived from the unlit patch with a masked local linear model lit = a * unlit + b per channel, fitted on plate pixels around each mask (guided-filter style, coarse to fine).
- Cutout pixels come from the respective plate, un-premultiplied at partial-coverage edges against the layers below, so the pieces composited at rest reproduce the plates. The pot's upper left and ring, hidden behind the pan, are completed from the remove-pan donor (`Docs/work/merchant-props/masks/HangingPot-completed.png`).

### Placement

Each piece is a RawImage named after it, a child of the background frame, anchored to its top-left corner with the crop rect converted at 540 / 941 horizontally and 960 / 1672 vertically, and its RectTransform pivot at the piece's pivot.

| Piece | Anchored position (frame units) | Size | Pivot | Map rect |
| --- | --- | --- | --- | --- |
| HangingPot | (352.636, -236.268) | 28.119 x 49.378 | (0.295918, 0.947674) | (0.637620, 0.705144, 0.052072, 0.051435) |
| HangingPan | (341.445, -214.450) | 41.892 x 51.675 | (0.534247, 0.938889) | (0.590861, 0.726077, 0.077577, 0.053828) |
| ShelfBottle | (131.700, -220.019) | 20.085 x 38.469 | (0.5, 0.056716) | (0.225292, 0.768541, 0.037194, 0.040072) |
| TrunkLid | (122.232, -234.833) | 55.664 x 46.507 | (0.484536, 0.901235) | (0.176408, 0.711722, 0.103082, 0.048445) |

Each piece draws `Ambient/<Name>Lit.mat` (SceneLitImage.shader, `_SourceTex` the unlit cutout, no layer mask, `_InvertMask` 1) with a SceneLitImage bound to MerchantSceneLighting.asset: base `<Name>Unlit.png`, light 0 reference `<Name>LanternLit.png` with MerchantLanternResponse.png through the map rect (x / 941, 1 - (y + h) / 1672, w / 941, h / 1672).

### Motion

`Dice-Unity/Assets/Dice/Content/Presentation/HauntedMerchant/MerchantAmbientActivity.asset` names each piece as a prop actor bound in the encounter screen's Ambient Props list (`hanging-pot`, `hanging-pan`, `shelf-bottle`, `trunk-lid`, `hanging-cup`).

| Asset | Motion at strength 1 |
| --- | --- |
| MerchantPotSwing.asset | 12 degrees, 2 s period, 1.8 s decay |
| MerchantPanSwing.asset | 12 degrees, 1.8 s period, 1.6 s decay |
| MerchantBottleWobble.asset | 12 degrees, 0.4 s period, 0.45 s decay; left contact for counterclockwise turns, right for clockwise |
| MerchantLidThump.asset | 5 frame units, 0.13 s hops, restitution 0.3, 1 degree tilt |

Idle strengths give about 7 to 10 degrees for the swings, the bottle and the cup, and lid lifts of about 3.6 to 5 frame units. Disturbance strength is 1.6 and the defeat knock 2.5. Idle intervals: pot 8 to 20 s, pan 9 to 20 s, bottle 10 to 20 s, lid 12 to 20 s, cup 8 to 18 s.

## Prompts

[prompts/README.md](prompts/README.md) indexes every generation prompt for this art, one file per edit, with its input images and the donor or asset it produced.
