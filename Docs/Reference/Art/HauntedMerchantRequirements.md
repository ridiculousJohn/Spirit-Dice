# Haunted merchant requirements and implementation audit

Initial audit, 2026-10-20, against John's decisions and the then-current scene. The failure table below is historical. The scene now has separate merchant parts and flame, an ambient plate without candle glare, independent shared-system lighting, and placement-following candle illumination. See [current installed layers](HauntedMerchantHostLayers.md) for implementation and verification. Host animation/concealed artwork and full spatial relighting of the painted merchant remain limitations.

## Scope and agreed behavior

Establish the encounter presentation before implementing gameplay. The human merchant sits at his broken cart on a muddy road beside a dark swamp at night. Preserve the agreed composition, lighting direction, native source resolution and canopy registration when separating artwork.

The protective candle is movable. Before each opening roll, the player selects a bay, then presses Roll. It stays there for that hand and can be repositioned before the next hand. Its flame, glow and illumination must follow its position. Its old position must not retain candle-specific glare. Lantern illumination is independent and may remain when the candle is off or elsewhere.

The merchant needs separate pieces for the existing host presentation system. John explicitly requested separate pieces and a separate flame. Head/hat, torso, supporting arm and resting arm are the current proposed breakdown, not a requirement to invent four independent animation controllers. The supporting hand touches the cheek; shoulder, neck and tabletop contacts constrain the artwork and motion.

The canopy roof remains static. Only the original front and rear hanging cloth animates, with fixed attachment points and irregular motion. Do not reopen the canopy design or replace its source to solve unrelated lighting or host work.

Haunting remains an always-active encounter rule; protection is independent of haunting; placement belongs to shared hand preparation. No charm knockout in this encounter. Carry-over, equipment, deck construction and other encounters remain outside this presentation task. Scene tests of candle positions do not authorize implementing gameplay or persistence here.

## Existing mechanisms to use

| Concern | Verified implementation | Boundary |
| --- | --- | --- |
| Host layers and states | SpiritHostPresentationAsset, SpiritPortraitLayer, SpiritHostPortrait.Build/ApplyPose, authored SpiritPortraitImage layers and portrait prefabs | Supports transforms, alpha, deformation, state sequences and a shared scene-light tint. Static RawImage children alone do not integrate the host. |
| Painted lighting | SceneLightingAsset, SceneLightLayer, SceneLightingMaterial, Shared/SceneLighting.shader | Up to four independent contributions over an ambient base, composed in linear color. Contributions can be direct light or full-lit references with shared ambient subtraction. |
| Light masks | SceneLightLayer.ResponseMap and Tools/Presentation/author-scene-light-response.py | Linear R stores authored surface distance; G stores source/glow response. These maps control a fixed painted contribution. They do not infer scene geometry or relocate a reflection. |
| Shared flicker | SceneLightingState.Sample and SceneLightFlicker | One sampled state supplies painted light and receiver intensity. Do not create unrelated candle/lantern flicker clocks. |
| Receiver lighting | SceneLightingReceiverMaterial and SceneLightingState.EvaluateReceiver | ReceiverPosition affects rig-space lighting and the flat host tint sample. It does not move painted background illumination. |
| Existing example | MothCrossroadsTableLighting.asset and MothCrossroadsEntryLighting.asset | Moth uses aligned ambient/reference/response assets. Fox and Watchtower table lighting references are currently null; do not claim all three already use light masks. |
| Source selection | Shared/SourceMaskedImage.shader | Selects source alpha through a mask. It provides neither lighting integration nor concealed artwork. |

Source code is under Dice-Unity/Assets/Dice/Presentation. Existing authored examples are under Dice-Unity/Assets/Dice/Content/Presentation and Art/MothCrossroads. Inspect these implementations before extending them.

EncounterTableLighting.Update is the existing integration path: sample one state, apply the painted composition to room/table images, then send that state to trays, history and host. The isolated preview currently has no equivalent binding. SceneLighting.shader reads its material's base/contribution textures, not the RawImage source texture; assigning the background lighting material to a merchant part would render the wrong artwork. Replacing the canopy material would also remove its source mask. Preserve each layer's texture/alpha contract when integrating lighting.

Lantern and candle contributions must be separated from each other. Assigning the same full-lit painting containing both sources to two light slots doubles their illumination rather than making them independent. SceneLightingState also has no intensity-override argument even though the compositor's preview API does; do not assume a compositor override automatically reaches receivers.

## Current failures and limitations

| Requirement | Current verified state | Required correction |
| --- | --- | --- |
| Independently lit lantern and candle | Merchant Ambient Plate, Merchant and Protective Candle use ordinary RawImages with null materials. No merchant lighting asset/driver is wired into this scene. | Prepare a true ambient base, independent contributions and response maps; bind the existing lighting system. |
| Candle leaves no light behind | MerchantBackground.png still contains the candle's bright table illumination. The earlier visual review explicitly observed it. | Separate candle-specific spill/glare from ambient and lantern contribution. Inspect with candle off and at every supported placement. |
| Candle light follows placement | The shader samples every painted contribution at fixed scene UVs. Moving ReceiverPosition alone cannot satisfy this. | Supply placement-dependent painted lighting and matching receiver positions through the existing system. Authored contributions for discrete bays are a viable reuse path; continuous travel, if animated, must also handle the light transition. Do not invent a separate merchant lighting framework. |
| Flame separate from fixture | ProtectiveCandle.png contains flame, wick, wax and holder in one image. Lantern source/glow is baked into the background. | Separate candle flame from body/holder and separate lantern source/glow from its illumination. Keep wick attachment and foreground occlusion correct. Whole-lantern movement is not assumed. |
| Merchant has reusable host pieces | MerchantHost.png is one posed image in a single RawImage; no merchant portrait prefab/presentation asset/state binding exists. | Author pieces and the host integration using the established portrait system. |
| Pieces remain valid during motion | Original artwork has no cheek beneath the fist or torso behind either arm. A mask-only split would reveal holes or broken contacts. | Define intended motion, then provide concealed artwork/overlaps or constrained deformation. Validate extremes, not only a rest composite. |
| Lighting affects layered artwork coherently | Merchant, candle and cloth contain warm painted illumination. SourceMaskedImage has no scene-light input; uniform host tint is an approximation. | Check for permanent local highlights and double lighting. Choose ambient/contribution artwork or an explicit compatible binding using the shared sampled state. A background-only fix does not prove this requirement. |
| Appearance is preserved | Latest generated merchant/candle are brighter and sharper than the original. Generated background also changes roof-region colors. | Preserve current source assets and measure future changes against declared baselines; do not silently regenerate unrelated artwork. |
| Tests describe installed content | Interrupted AmbientMotionTests edits expect four merchant parts and two candle parts, but scene still contains the two unsplit images. | Complete integration and tests together. Current fixture has not been compiled/run after those edits and would fail hierarchy assertions. Do not discard the partial changes. |

## Evidence and acceptance checks

The last executed focused run, merchant-layers-49ed02f134be46f590112455c402f938, passed 30 tests before the interrupted part-test edits. It checked structural separation and background identity, not movable lighting. Its full-composite versus original comparison was diagnostic only: mean absolute RGB channel error 11.1211/255. It did not establish an exact reconstruction or an accepted error threshold.

Before claiming the corrected presentation complete:

1. Name both baselines: MerchantOriginal.png is the unmodified 941 x 1672 concept; current MerchantHost.png and ProtectiveCandle.png are the unsplit generated layer sources. An exact split comparison against the latter does not prove fidelity to the former.
2. Render the actual installed scene and compare the assembled split pieces against their unsplit sources at rest, pixel by pixel, using the same transforms and color pipeline. Report differences and investigate them; do not silently weaken assertions. Compare with the original separately and explain intentional reconstruction differences.
3. Hide each part and each complete object. Check the intended region visually, not merely that some pixel changed. Verify no duplicate silhouettes, fragments, unwanted edge pixels or baked candle-specific light remain.
4. Render lights off, lantern only, candle only and both on. Candle-off must remove its source/glow/spill while retaining the intended lantern and ambient light. These require authored lighting expectations and visual review; comparing the background with a copy of itself cannot prove removal.
5. Preview the candle at all five bay placements and any visible travel transition. Check body/flame registration, illumination at the new position, disappearance of candle-specific glare at the old position, and matching receiver position. Keep the lantern independent.
6. Sample low/high flicker with the shared clock. Verify flame/source response, painted contribution, host tint and 3D receiver behavior where those receivers are present. Do not claim gameplay-scene receiver coverage from a UI-only preview.
7. Exercise intended host motion extremes and transitions. Inspect cheek/hand, neck/collar, shoulders, arm overlap and table contact for holes or sliding. A neutral-pose partition test does not validate animation.
8. Recheck canopy rest/gust attachment, stationary roof, clean hidden-cloth background and smooth source edges after lighting integration. Use native 941 x 1672 plus the existing five alternative size/scale cases.
9. Run the relevant Unity CLI tests only with the Dice editor closed, inspect result XML and actual renders, then record exact source/run scope. Preserve unexpected generated changes and report them. Historical passing runs are not evidence for subsequent edits.

## Correction order

First establish the ambient/lantern/candle lighting assets and the placement-dependent lighting contract. Then prepare merchant pieces and concealed surfaces for the intended host motion, split the flame with its attachment intact, and integrate both through the existing presentation systems. Complete the corresponding tests and inspect actual Unity renders before reporting completion. Do not make further isolated art edits without checking their effect on this complete composition.
