# Registered canopy layers

## Source of truth

`Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantOriginal.png` is an exact byte copy of `Docs/work/merchant-concept-20261020/merchant-scene-concept.png`, the actual original merchant concept. Both are 941 x 1672. SHA256: `6145EF6F90D4580FFB83FF6B96D0347787219B0D252EDB7BB75D2CCA8612BFAD`.

The older `Canopy.png` was a generated extraction that changed the cloth's appearance. Comparisons against that file did not establish fidelity to the original scene. It is not the source for the active preview.

## Active assets

The scene now uses `MerchantBackground.png` and separate merchant/candle layers derived from the registered plate. See [merchant and candle layers](HauntedMerchantHostLayers.md). The registered roof plate below remains the preserved source for that separation; canopy artwork and animation are unchanged.

- `MerchantOriginal.png`: full original source, without resizing or repainting.
- `CanopyOriginalOverhang.png`: full-canvas transparent extraction of front-facing striped overhang and visible rear hanging panels. Selected pixels retain original RGB at the same coordinates; roof, supports and scenery have zero alpha.
- `CanopyOriginalOverhangMask.png`: binary source mask sampled with bilinear filtering for the extracted cloth.
- `MerchantRegisteredRoofPlate.png`: the complete clean background with its upper roof edge registered to the original attachment seam. The roof remains and the overhangs are fully removed. The source `MerchantCleanRoofPlate.png` is retained. See [attachment repair](HauntedMerchantAttachmentRepair.md) for anchors and provenance.

All active textures are 941 x 1672, with mipmaps, compression and non-power-of-two resizing disabled. The extraction contains 70,564 selected pixels. The outline manifest is `HauntedMerchantOriginalOverhang.json`; the derived extraction and mask are saved assets. The generated hidden fill is retained as `HauntedMerchantOverhangFill.png`. Tool prompts and rejected generation details are recorded in `HauntedMerchantOriginalOverhangPrompts.md`.

## Rendering and motion

The scene renders `MerchantBackground.png` as its background with separate merchant and candle images. The `Fixed Canopy` gameobject is disabled; its inverse-mask material must not redraw original fringe remnants over the clean plate. The animated fabric alone uses `SourceMaskedImage.shader`, sampling original scene RGB through `CanopyOriginalOverhangMask.png`. Mask samples retain fractional alpha coverage at the cut boundary. Coverage changes alpha only; applying it to RGB as well would double-attenuate the edge during straight-alpha blending and create a dark fringe. The original fabric remains registered to the native-size clean background. Both use the existing 540 x 960 authoring geometry and full-frame UVs. This UI size does not downsample source textures.

`Canopy.prefab` retains one `SpiritPortraitImage`, the 96 x 192 deformation grid, two local deformation channels and the irregular 31-second native animation. Movement is localized to loose ends and the rear panel, with attachment regions fixed. The separate hanging-cup instance is disabled in this preview because the true original contains its own cookware; the reusable cup prefab remains available.

## Validation

The earlier 20-test run established rest reconstruction against the original scene but did not establish a clean background with the canopy hidden. It is not evidence that the masked background was correct. `MerchantOriginalRoofPlate.png` and the complementary static layer retained fragments and are not active in the scene.

Regression coverage now checks that the installed clean plate is byte-identical to the displayed reference, that the fixed overlay is inactive, and that hiding the animated canopy renders the clean plate without additional artwork. Native rest, hidden-canopy and gust captures verify the actual scene configuration.

Historical clean-background check (before attachment repair), Unity CLI run clean-plate-8efed04d4a504204b3061c9825a67aa7: 21/21 focused EditMode tests passed. With the canopy GameObject hidden, the actual scene render matches the clean plate reference with zero differing RGBA pixels at all six tested sizes, including 941 x 1672. Native background-only and cloth-rest captures were visually inspected. No full-suite or Android run was performed.

Attachment validation: Unity CLI run canopy-attachments-7c73848ebeb94366979b44ba182b674a passed 29/29 focused EditMode tests. Eight attachment-mask samples and six additional fixed-support samples cover the corrected edges. The hidden-canopy render matches the registered background at all six tested sizes. Native941x1672 rest, two gust poses and background-only captures were independently inspected: the rear gap is closed, the front seam follows the roof, and no hanging fragments remain with the canopy hidden. A 12-second native-resolution motion capture is retained as canopy-attachments.webm. No full-suite or Android run was performed.

Edge filtering validation: Unity CLI run canopy-edges-dcb9962750b143f5b781923700ccade8 passed 30/30 focused EditMode tests, including a GPU comparison of fractional mask coverage against default UI alpha blending. Native rest and gust captures were inspected; edge-comparison.png compares the original, previous Unity render and filtered Unity render. Existing attachment and hidden-background checks pass at all six sizes. No full-suite or Android run was performed.
