# Spirit presentation authoring

## Modifier families and categories

Each `ModifierPresentationAsset` supplies its family artwork and references a shared `ModifierCategoryPresentationAsset`. The four category assets in `Assets/Dice/Presentation/Shared` author the UI accent, ink on bone, and secondary symbol: teal plus for Base, red cross for Mult, purple plus/cross for Base + Mult, and gold circular arrow for Rerolls. Change these assets to restyle categories; runtime presentation does not infer categories from identifier suffixes or contribution amounts.

All variants of a family use the same artwork. All 74 standard families have bespoke icons: seven existing code-authored drawings, including Twin Knots, and 67 SVG-authored sprite assets. Unregistered custom content can still use the provisional sigil fallback. Reward cards, owned charms, inspection, action cues and ready tray dice use the same category reference. The category mark is separate from the charm's activation-status badge and gold selection frame. Modifier dice retain neutral bone, with colored ink and a small symbol on the top face. The four inert Sleeping Stone variants have their own artwork but no category.

Category assets are explicit dependencies of the Addressable modifier presentation assets. No Resources lookup or separate startup discovery is needed. Mechanical modifier assets remain independent of this styling.

Reward description trigger phrases use `SpiritUiTheme` colors independently of effect categories: green for playing a hand, amber for rerolling, and blue for the initial roll. Condition keywords use the theme's standard gold. Icons and effect text retain their category colors.

### Family icon sources

`modifier-icons/catalog.json` maps each of the 67 SVG sources to its mechanical family identity and existing display name. The drawings were authored for this project; no external icon pack is used. Each source has a 64-unit viewBox, transparent background, white 3.2-unit rounded strokes and a six-unit safe inset. Keep silhouettes distinct and check at 32 pixels. Category symbols and colors belong to the shared presentation assets, not these sources.

Run `python Tools/Presentation/render-modifier-icons.py` from the repository root with Python 3.10+ and Pillow installed. It writes PNG previews and a labeled contact sheet into a fresh ignored `Docs/work/modifier-icons-*` directory. `--output` selects another fresh directory under `Docs/work`. The renderer supports absolute SVG M/L/C/Q/Z paths, circles, ellipses, rectangles, lines, polylines, polygons and inherited group paint; it rejects unsupported styling instead of silently dropping it.

`--write-assets` also writes the 256-pixel PNG sprites under `Dice-Unity/Assets/Dice/Art/ModifierFamilies` and assigns each to its four presentation assets. It preserves existing asset GUIDs and importer metadata. This intentionally replaces authored PNGs and icon references for those families; review the intended sources before running it. Unity uses ordinary explicitly referenced sprites loaded as Addressables dependencies, with no SVG package or runtime rasterizer for these assets. Import sprites uncompressed with alpha transparency, no mipmaps, bilinear filtering and clamped edges.

## Encounter trays

The Watchtower tray uses mottled stone for its rails and recessed bays. The Crossroads tray uses faded,
split-grain wood. These are material surface kinds 7 (stone) and 8 (weathered wood) on the shared dice
surface shader; tint, seed and grain scale remain editable per material. Their surface detail follows
each piece's local axes and physical scale. Existing material asset names are retained for stable references.

Each `EncounterTablePresentationAsset` references its authored encounter scene and a `SpiritTrayPresentationAsset`. The tray asset references the rig prefab and its render-texture size. The rig and encounter scene own the camera, geometry, materials, lights, and placement. Both dice and modifier bays share the authored camera. The table's scene-lighting asset drives surface illumination.

The three authored scenes are `Assets/Scenes/FoxEncounter.unity`, `WatchtowerEncounter.unity`, and `MothEncounter.unity`. Their rig prefabs are under `Assets/Dice/Content/Presentation/Rigs`. See [Presentation authoring](../../Docs/PRESENTATION-AUTHORING.md) for screen and prefab ownership.

Open **Dice > Tray Composition Preview** and select the encounter table. It previews that encounter's actual scene at 540x960 or 443x960. **Open encounter scene** opens the source hierarchy; edit the HUD's `DiceTrayFrame` for screen placement and the rig camera for perspective. **Open encounter rig** opens the shared rig prefab. Normal scene/prefab Inspector editing provides Unity Undo and Redo. The preview uses an isolated copy, including unsaved changes when the source scene is open, and refreshes when source assets or Undo change them.

**Show gameplay UI** is enabled by default and displays the scene's authored HUD with sample values. No gameplay session runs and preview controls do not accept game input. Turn it off to inspect the host, background, and trays. Sample dice, modifier count/scroll, and lighting time remain preview controls. Encounter Lighting Preview uses the same authored scene source in table mode.

Legacy placement, angle, projection, and material-seed fields on tray assets remain serialized for initial authoring compatibility. They do not override existing scenes or rig prefabs. Do not use historical generators to apply visual edits to existing production assets.

`SpiritTrayGeometry` defines five dice surface anchors and five selection-label anchors. Dice settle and roll relative to each surface anchor, with `DiceScale` controlling their size. `ModifierRoot` defines the modifier bay's local axes and surface; its width, depth, die size, spacing and visible capacity control the scrolling row. The live bay shows ready PlayHand modifiers and highlights each resolving tile. Additional ready entries remain scrollable. Runtime click targets follow the authored projection, and clipping follows the bay's local coordinates. Keep anchors inside the prefab and its materials on the scene-lighting-compatible dice shader. Scene instances clone materials before applying lighting.

`author-tray-presentations.py` generates the three editable prefabs, native meshes, materials and tray assets. It requires Python 3.9 or later without third-party packages. The default writes a fresh ignored `Docs/work/tray-authoring-*` directory. `--output` chooses an unused directory under `Docs/work`. `--write-assets` intentionally regenerates production content and can replace manual asset edits; review those changes before using it. The game loads the serialized assets and does not execute this tool.

## Crossroads moth

`author-moth-presentation.py` authors the moth host and deformation mesh using the same layer/timeline format. Its default output is a new ignored `Docs/work/moth-authoring-*` directory; `--write-assets` writes only `MothCrossroadsHost.asset` and `MothBodyMesh.asset`. It requires Python 3.9 or later with no third-party packages and imports the shared authoring helpers beside it.

The production body and wing smoke use aligned 1536x1024 canvases. `--foot-x` and `--foot-y` describe their contact point in bottom-left UV coordinates (defaults `0.5`, `0.28`); `--host-width` defaults to 370 in the 540x960 reference portrait. Waiting, Arrival, Idle, Reaction, Victory, Defeat and Farewell are authored sequences. Arrival lasts seven seconds. The smoke layer uses distortion `0.035`, speed `0.7`, opacity `0.6`, and does not receive scene lighting.

The small moths follow independent closed waypoint schedules with short darts, holds, reversals and banking. Their arrival tracks use `EntryWeight` to start at the currently displayed waiting pose, then decay that offset before the blue layers appear. Entry weights apply to state transitions through the shared host renderer; repeated arrival progress updates retain the original entry pose. The departing moth doubles back before growing toward the rail. Arrival continues directly into dialogue, static narration waits for input without a timed lock, and gameplay fades in for 0.3 seconds before rolling.

Any host layer can enable `Smoke` and supply its own motion mask and repeating flow-noise texture. Missing textures use generated defaults. Import custom mask/noise textures as linear data; smoke artwork uses ordinary color and alpha. Smoke and silhouette materials belong on separate layers. Tint, deformation and timeline opacity still apply to smoke layers.

## Haunted merchant

`author-merchant-presentation.py` authors the merchant's living-still host using the same layer/timeline format. Its default output is a new ignored `Docs/work/merchant-authoring-*` directory; `--write-assets` writes only `Ambient/MerchantHost.asset` and `Ambient/MerchantBreathMesh.asset`. It requires Python 3.9 or later with no third-party packages, imports the shared authoring helpers beside it, and refuses to run if an artwork `.meta` it references is missing. It does not edit artwork, materials or `MerchantHost.prefab`.

All five merchant layers (torso, head, resting arm, supporting arm, blink) keep the native 941 x 1672 source frame and reference the one `Breath` shape, so the field moves overlapping layers identically and nothing moves as a rigid part. At full weight the field lifts the shoulders and chest 6 native pixels (about 1% of the figure), the head, hat and the hand under the cheek 70% of that, and spreads the chest up to about 1.4 pixels; the lift fades to zero between source rows 760 and 950, leaving forearms, elbows and hands on the table fixed. The 13.8-second idle loop holds three uneven breaths (4.4, 4.9 and 4.5 seconds) and three blinks at 1.55, 5.45 and 11.25 seconds; each blink fades the closed-eye overlay in over 40 ms, holds it and fades out by 140 ms. Arrival, Reaction, Victory, Defeat and Farewell are three-second additive breaths over the continuing idle: a quick intake that settles slightly below rest for Reaction, a relieved breath for Victory, a sigh for Defeat and short settles for Arrival and Farewell. Edit the constants at the top of the script, generate into scratch and compare before writing assets.

The blink overlay samples `MerchantBlinkEyes.png` through `MerchantBlinkMask.png` with the source-masked shader. Both are 136 x 58 crops of source box (264, 576)-(400, 634); the layer's UV rectangle stretches them across the full source frame and clamped, zero-alpha mask edges keep everything outside the crop transparent. Its provenance is recorded in [merchant host layers](../../Docs/Reference/Art/HauntedMerchantHostLayers.md).

## Inn and watchtower

`author-spirit-presentations.py` contains the approved motion formulas for the Lantern Keeper and Watchtower host. It generates two host assets and nine deformation meshes. The game loads the checked-in Unity assets and does not run this script.

The Watchtower arrival registers the face across its painted poses, uses short pose blends, and settles the body above planted feet. Airborne foot positions follow the crouched flight artwork; they are not landing anchors. Landing registration and grounded settling are authored mesh channels. Idle and reaction curves remain independent of the arrival.

Journey beats can enable `Auto Advance` to continue after their motion completes. The Watchtower flight uses this to lead directly into dialogue. The final invitation always waits for acceptance. Table presentation assets expose `Opening Fade Duration`; the Watchtower uses 0.3 seconds to reveal gameplay before the opening roll. Zero leaves the opening immediate. Both settings use the shared journey and presentation code.

Requires Python 3.9 or later, with no third-party packages. Commands below run from the repository root; the script resolves its inputs relative to its own location and also works from another working directory.

```text
python Tools/Presentation/author-spirit-presentations.py
```

The default writes a new, ignored `Docs/work/spirit-authoring-*` directory, preserving repository-relative paths and existing asset GUIDs. Compare these preview files with the production assets before accepting a formula change. `--output Docs/work/my-unused-preview` selects a new preview directory and refuses an existing one.

To intentionally regenerate the production assets in place:

```text
python Tools/Presentation/author-spirit-presentations.py --write-assets
```

This writes both hosts and all nine meshes under `Dice-Unity/Assets/Dice/Content/Presentation`. It does not generate or replace artwork. Preserve the existing `.meta` files and review the resulting diff.

## Encounter outcomes

Each journey has inline `victoryPresentation`, `defeatPresentation`, and `abandonPresentation` configurations. Set `enabled` on each outcome that should run a farewell. Choose `hostState`, a nonnegative `holdDuration` before the host gesture, a nonnegative `fadeDuration` afterward, and `showDialogue` to wait for Continue before the summary. Optional `artwork` and `audioCue` provide an ending illustration and a cue at farewell start. Missing or disabled configurations show the summary immediately.

Host motion duration comes from the selected state's authored sequences, independently of the hold and fade. `Victory` (4), `Defeat` (5), and `Farewell` (6) use normalized progress through `SetState(state, progress)`, like Arrival. These outcome sequences cannot loop. `Advance` keeps continuous smoke effects moving while holding the sampled pose; `EntryWeight` blends from an interrupted gesture. A host without the selected sequence contributes zero motion time.

The Lantern Keeper has a 1.4-second victory nod, a gentler 1.2-second defeat tilt, and a subdued 0.5-second farewell. Gold and cyan silhouettes lead the physical head by 0.15 seconds, then settle into the idle pose. `LastLanternJourney.asset` demonstrates enabled configurations for all three outcomes. Reauthor these curves with `author-spirit-presentations.py` into a scratch directory first; copy only the intended sequence blocks when preserving manually edited production curves or layers.

The Watcher uses a deliberate victory head tilt, a quieter opposite tilt on defeat, and a brief farewell settle. Its spectral head anticipates the physical turn while the feet remain planted. `WatchtowerJourney.asset` supplies its outcome timing and highlights `encounter.moth.crossroads` after a first victory. Both hosts use the same ending view and authoring script.

The Lightkeeper's outcome curves are authored by `author-moth-presentation.py`. Victory settles the body and turns toward the right-hand road, with a brief cyan halo from the existing spectral layer. Defeat folds the wings, dims the smoke and withdraws slightly toward the lantern. Farewell is a short, subdued fold. Entry weights preserve continuity from an interrupted reaction; the final poses hold while the independent smoke clock continues. These curves leave the smoke material's baseline settings unchanged. Generate into scratch and transfer only the intended outcome sequence blocks when preserving manual production asset edits.

`MothCrossroadsJourney.asset` enables the three outcomes, with dialogue for victory and defeat and a direct summary after the brief abandon motion. Its existing clue points toward the old crossing. It has no follow-up encounter ID because that destination is not yet playable.

Farewell dialogue uses the journey's `victoryText`, `defeatText`, or `abandonText`. The summary follows the optional dialogue and presents the score, victory clue, and earned unlocks; defeat offers Retry and Return to the trail. `followUpEncounterIds` identifies existing journey IDs to highlight after victory, such as the lantern journey's `encounter.owl.watchtower`. These are trail recommendations: all encounter cards remain selectable, and selecting a highlighted encounter clears its highlight. Keep follow-up IDs unique, different from the source journey, and present in the investigation catalog.

## Scene light response maps

`author-scene-light-response.py` creates a linear data texture from authored triangles and feathered source masks. It does not read or edit scene artwork. Red contains normalized surface distance; green identifies the visible source and glow. Blue is unused. PNG coordinates in the JSON use a top-left origin. Import these maps with sRGB disabled; the lighting preview loads them as linear data automatically.

```text
python Tools/Presentation/author-scene-light-response.py --config Tools/Presentation/scene-light-response-moth.json --output Docs/work/moth-lighting-prototype/lantern-response-v2.png
```

The script requires Python 3.9 or later with no third-party packages, and refuses existing output paths. The moth preset contains estimated distances for the post, surroundings, rails and cart floor, plus the lantern's source mask. These are artistic controls, not depth reconstructed from the painting. Edit or copy the JSON for another composition; triangle patches are applied in order. Keep experimental PNG output under ignored `Docs/work`; only approved production maps belong in `Assets`.

`scene-light-response-moth-entry.json` supplies the corresponding causeway approach map. Use the same command with that config and a new output path. The selected entry and playfield maps are imported with sRGB disabled under `Assets/Dice/Art/MothCrossroads`.

`Dice > Encounter Lighting Preview` previews the authored entry/table assets with the actual dice and history renderers. Its intensity and animation controls operate on temporary copies. `Dice > Lighting Preview` remains the local-PNG exploration tool.
