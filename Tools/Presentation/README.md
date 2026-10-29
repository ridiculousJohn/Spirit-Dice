# Spirit presentation authoring

## Modifier families and categories

Each `ModifierPresentationAsset` supplies its family artwork and references a shared `ModifierCategoryPresentationAsset`. The four category assets in `Assets/Dice/Presentation/Shared` author the UI accent, ink on bone, and secondary symbol: teal plus for Base, red cross for Mult, purple plus/cross for Base + Mult, and gold circular arrow for Rerolls. Change these assets to restyle categories; runtime presentation does not infer categories from identifier suffixes or contribution amounts.

All variants of a family use the same artwork. All 74 standard families have bespoke icon sprites in `Art/ModifierFamilies`. Every charm presentation must reference an icon; loading fails without one. Reward cards, owned charms, inspection, action cues and ready tray dice use the same category reference. The category mark is separate from the charm's activation-status badge and gold selection frame. Modifier dice retain neutral bone, with colored ink and a small symbol on the top face. The four inert Sleeping Stone variants have their own artwork and the None category (`SpiritModifierCategoryNone.asset`), which shows no mark.

The modifier skin (`Content/Presentation/SpiritHouse/SpiritHouseModifierSkin.asset`) lists every charm presentation and the category assets they use; presentations, categories and their sprites load as dependencies of the application configuration. No Resources lookup, label or separate startup discovery is needed. Mechanical modifier assets remain independent of this styling.

Reward description trigger phrases use `SpiritUiTheme` colors independently of effect categories: green for playing a hand, amber for rerolling, and blue for the initial roll. Condition keywords use the theme's standard gold. Icons and effect text retain their category colors.

### Family icon sources

`modifier-icons/catalog.json` maps each of the 67 SVG sources to its mechanical family identity and existing display name. The drawings were authored for this project; no external icon pack is used. Each source has a 64-unit viewBox, transparent background, white 3.2-unit rounded strokes and a six-unit safe inset. Keep silhouettes distinct and check at 32 pixels. Category symbols and colors belong to the shared presentation assets, not these sources.

Run `python Tools/Presentation/render-modifier-icons.py` from the repository root with Python 3.10+ and Pillow installed. It writes PNG previews and a labeled contact sheet into a fresh ignored `Docs/work/modifier-icons-*` directory. `--output` selects another fresh directory under `Docs/work`. The renderer supports absolute SVG M/L/C/Q/Z paths, circles, ellipses, rectangles, lines, polylines, polygons and inherited group paint; it rejects unsupported styling instead of silently dropping it.

`--write-assets` also writes the 256-pixel PNG sprites under `Dice-Unity/Assets/Dice/Art/ModifierFamilies` and assigns each to its four presentation assets. It preserves existing asset GUIDs and importer metadata. This intentionally replaces authored PNGs and icon references for those families; review the intended sources before running it. Unity uses ordinary explicitly referenced sprites loaded as dependencies of the application configuration, with no SVG package or runtime rasterizer for these assets. Import sprites uncompressed with alpha transparency, no mipmaps, bilinear filtering and clamped edges.

## Encounter trays

The Watchtower tray uses mottled stone for its rails and recessed bays. The Crossroads tray uses faded,
split-grain wood. These are material surface kinds 7 (stone) and 8 (weathered wood) on the shared dice
surface shader; tint, seed and grain scale remain editable per material. Their surface detail follows
each piece's local axes and physical scale. Existing material asset names are retained for stable references.

Each `EncounterTablePresentationAsset` references its authored encounter scene and a `SpiritTrayPresentationAsset`. The tray asset references the rig prefab and its render-texture size. The rig and encounter scene own the camera, geometry, materials, lights, and placement. Both dice and modifier bays share the authored camera. The table's scene-lighting asset drives surface illumination.

The three authored scenes are `Assets/Scenes/FoxEncounter.unity`, `WatchtowerEncounter.unity`, and `MothEncounter.unity`. Their rig prefabs are under `Assets/Dice/Content/Presentation/Rigs`. See [Presentation authoring](../../Docs/PRESENTATION-AUTHORING.md) for screen and prefab ownership.

Open **Dice > Tray Composition Preview** and select the encounter table. It previews that encounter's actual scene at 540x960 or 443x960. **Open encounter scene** opens the source hierarchy; edit the HUD's `DiceTrayFrame` for screen placement and the rig camera for perspective. **Open encounter rig** opens the shared rig prefab. Normal scene/prefab Inspector editing provides Unity Undo and Redo. The preview uses an isolated copy, including unsaved changes when the source scene is open, and refreshes when source assets or Undo change them.

**Show gameplay UI** is enabled by default and displays the scene's authored HUD with sample values. No gameplay session runs and preview controls do not accept game input. Turn it off to inspect the host, background, and trays. Sample dice, modifier count/scroll, and lighting time remain preview controls. Encounter Lighting Preview uses the same authored scene source in table mode.

A tray asset holds only its rig prefab and render texture size; placement, angle, projection and materials live in the scene and the rig prefab. Do not use historical generators to apply visual edits to existing production assets.

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

### Candle light footprint

`author-candle-light-footprint.py` derives the merchant candle's centered light footprint and response map (`Art/HauntedMerchant/CandleLightFootprintCentered.png` and `CandleLightResponseCentered.png`) from the preserved sources `Docs/Reference/Art/HauntedMerchantCandleFootprintSource.png` and `HauntedMerchantCandleResponseSource.png`. It scales both about the candle point (the response map's distance minimum) by `REACH` (0.8, a tighter pool) with bilinear sampling; texels from outside the source read as unlit, and the script refuses a result with a lit border. It requires Python 3.9 or later with no third-party packages. The default writes a fresh ignored `Docs/work/candle-footprint-*` directory; `--output` chooses an unused directory under `Docs/work`; `--write-assets` replaces the two installed PNGs and keeps their `.meta` files. Source files are only read, so repeated runs give the same textures.

## Bound Tree layers

`cut-bound-tree-layers.py` cuts the tree nymph encounter's scene concept, `Art/BoundTree/BoundTreeOriginal.png`, into its first layers in the concept's 941 x 1672 frame: the clean plate `BoundTreeBackground.png` without the nymph, the nymph `BoundTreeNymph.png`, the faceless trunk `BoundTreeFaceless.png` and the face region `BoundTreeFaceMask.png`. Each generated donor (`Docs/Reference/Art/BoundTree*Donor.png`) is registered to the concept frame. The nymph donor is her generated transparent extraction: its alpha is her matte and its colour is low-frequency matched to the concept on her visible pixels. The plate is reconstructed within a declared distance of her silhouette and the faceless trunk inside the face polygon's region; every other pixel is copied byte for byte. The face polygon, target boxes and blend distances are declared in `Docs/Reference/Art/BoundTreeLayers.json`. What each texture is for and how it registers is in [Bound Tree layers](../../Docs/Reference/Art/BoundTreeLayers.md).

It requires Python 3.9 or later with no third-party packages and takes a few minutes. The default writes a fresh ignored `Docs/work/bound-tree-layers-*` directory: the four textures under `layers/`, review images under `review/` and `report.json` with the registrations and checks (byte identity outside each declared region, the nymph composite error inside her silhouette, full plate reconstruction within her motion margin, glow pixels left in the face region); the review images include the nymph moved by small offsets and a 1.03 scale about her seat over the plate. `--output` chooses an unused directory under `Docs/work`. `--write-assets` also replaces the four textures under `Art/BoundTree`, keeps existing `.meta` files and writes one (a copy of `BoundTreeOriginal.png.meta` with a fresh GUID) for a texture that has none. The concept and donors are only read, so repeated runs give the same textures.

### Bound Tree relighting

`author-bound-tree-relight.py` splits the layers' painted lighting into a moonlit ambient base and a reference lit by a strong flare of the spirit's glow (the flare light; the painted glow is a second light, see Bound Tree layers), with the merchant's method ("Relighting sources" in [merchant host layers](../../Docs/Reference/Art/HauntedMerchantHostLayers.md)): it imports the registration, ratio-map, cap and harmonic-fill functions from `author-merchant-relight.py`. It writes `Art/BoundTree/Relight/BoundTreeAmbientUnlit.png`, `BoundTreeGlowLit.png`, `BoundTreeFacelessUnlit.png`, `BoundTreeNymphUnlit.png`, `BoundTreeNymphGlowLit.png` and the linear response map `BoundTreeGlowResponse.png` (R the elliptical distance from the glow, G the eyes and mouth) from the preserved donors `Docs/Reference/Art/BoundTreeRelight*Donor.png`. It requires Python 3.10 or later with numpy, Pillow and scipy; on this machine use `Docs/work/merchant-table-clear/.venv/Scripts/python.exe` (Python 3.12 with those packages).

`targets` writes the imagegen edit targets under `Docs/work/bound-tree-relight/targets` (`--from` names a build's texture folder for the glow and nymph targets). `build` writes a fresh ignored `Docs/work/bound-tree-relight-*` directory with the textures under `textures/`, review images under `review/` and `report.json` (registrations, donor drift, byte identity, glow-lit at least unlit, alpha equality, and the resting intensity k at which unlit + k * (glowlit - unlit) best fits the concept); `--output` chooses an unused directory under `Docs/work`. `--write-assets` also replaces the six installed textures, keeps existing `.meta` files and writes one with a fresh GUID for a texture that has none. Source artwork and donors are only read, so repeated runs give the same textures. Conventions, parameters and the resting k are in [Bound Tree layers](../../Docs/Reference/Art/BoundTreeLayers.md), "Relighting".

`plates` writes the background's faceless plates under the spirit host's face: `BoundTreeFacelessPlateUnlit.png`, `BoundTreeFacelessPlateGlow.png` and `BoundTreeFacelessPlateGlowLit.png`, which are `BoundTreeAmbientUnlit.png`, `BoundTreeBackground.png` and `BoundTreeGlowLit.png` with the face region replaced by `BoundTreeFacelessUnlit.png` through `BoundTreeFaceMask.png` alpha (soft edge included), blended in linear light. It reads the textures in `--from` (default the installed `Relight` folder) and writes a fresh ignored `Docs/work/bound-tree-plates-*` directory (textures, face-region review images, `report.json` with the identity checks and the count of reference channels below the base); `--output` chooses an unused directory under `Docs/work`. `--write-assets` also writes the three plates to `Art/BoundTree/Relight`, keeps existing `.meta` files and gives a new one a copy of the replaced texture's `.meta` with a fresh GUID. Rerun it after rebuilding the relighting.

### Bound Tree die roots

`build-bound-tree-dice-roots.py` builds the binding dice visuals' textures, `Art/BoundTree/Dice/BoundTreeDiceRoots.png` (gnarled roots on transparency, alpha-tested on the bound die) and `BoundTreeDiceTendrils.png` (a white glow mask of thin tendrils on the marked die), from the raw image_gen outputs `Docs/Reference/Art/BoundTreeDiceRootsRaw.png` and `BoundTreeDiceTendrilsRaw.png` (prompts in `BoundTreeDiceRootsPrompt.txt`). It crops each source's empty top, turns the tendrils' brightness into alpha, makes the roots' opaque core fully opaque, eases the left and right edge columns together so both tile horizontally around the die, and resizes to 1024 x 768. It requires Python 3.10 or later with numpy and Pillow; on this machine use `Docs/work/merchant-table-clear/.venv/Scripts/python.exe`. The default writes a fresh ignored `Docs/work/bound-tree-dice-roots-*` directory with the textures and review images (each tiled twice over grey); `--output` chooses an unused directory under `Docs/work`; `--write-assets` also writes the two textures and keeps their `.meta` files. Sources are only read, so repeated runs give the same textures. How the shader maps them is under "Rule dice visuals" in [presentation authoring](../../Docs/PRESENTATION-AUTHORING.md).

### Bound Tree spirit host

`author-bound-tree-host.py` authors the possessing spirit as the encounter's host, as `author-merchant-presentation.py` authors the merchant: `Content/Presentation/BoundTree/BoundTreeHost.asset` (one lit layer, the face: `Art/BoundTree/BoundTreeBackground.png` in the 941 x 1672 concept frame, lit with base `Relight/BoundTreeAmbientUnlit.png`, light 0 reference `BoundTreeBackground.png`, light 1 reference `Relight/BoundTreeGlowLit.png` and response `Relight/BoundTreeGlowResponse.png`) and its deformation mesh `BoundTreeFaceMesh.asset`, with every state's timeline. Its default output is a new ignored `Docs/work/bound-tree-host-*` directory with the assets and `report.json`; `--output` chooses an unused directory under `Docs/work`; `--write-assets` writes only the host asset and the mesh (and a `.meta` for one that has none). It requires Python 3.9 or later with no third-party packages and imports the shared authoring helpers and the PNG reader and distance transform of `cut-bound-tree-layers.py`. It does not edit artwork, `BoundTreeFace.mat` or `BoundTreeHost.prefab`; it reads the prefab to reference its SpiritHostPortrait and leaves the reference empty before the prefab exists.

Idle has no motion; the face's only idle life is the scene's light 0 glow pulse. The 128 x 224 grid holds two shapes over the face only, `Brow` (the brows drop 4.5 pixels at their inner ends and 3 at their outer ends) and `MouthWide` (each lip of the mouth crack moves 4 pixels outward; a negative weight narrows it), both zero at every vertex within 18 pixels of the face mask's alpha-1 boundary, so the face's outline never moves. Waiting is faceless, Arrival fades the face in over 1.8 of 2.5 seconds as the crack opens from narrowed, Reaction (1.4 seconds) drops the brows sharply and widens the crack briefly, Victory and Farewell fade the face out over 2.5 seconds, and Defeat widens the crack slowly over 3 seconds and returns to rest. The report lists displacements at named points and, per shape, the largest displacement, the displacement at or outside the edge band (zero), the nearest moving pixel to the edge, the largest strain and folds. Edit the constants at the top of the script, generate into scratch and compare before writing assets. Details are under "Spirit host" in [Bound Tree layers](../../Docs/Reference/Art/BoundTreeLayers.md).

### Bound Tree nymph motion

`author-bound-tree-presentation.py` authors the tree nymph's deformation mesh, `Content/Presentation/BoundTree/NymphBreathMesh.asset`, for the single nymph layer `Art/BoundTree/BoundTreeNymph.png` in the 941 x 1672 concept frame. Its default output is a new ignored `Docs/work/bound-tree-presentation-*` directory with the mesh and `report.json`; `--output` chooses an unused directory under `Docs/work`; `--write-assets` writes only `NymphBreathMesh.asset` (and a `.meta` if it has none). It requires Python 3.9 or later with no third-party packages and imports the shared authoring helpers beside it and the PNG reader and distance transform of `cut-bound-tree-layers.py`. It does not edit artwork, materials, clips or scenes.

The 96 x 168 grid holds two shapes, one smooth field each, so her whole figure moves as one: `Breath` lifts the upper body 4 native pixels, the shoulders 5, and rounds the back up to 1.5 pixels outward; `Flinch` draws the upper body 4 pixels toward the tree (image right) and 1 down, tucks the head a further 1.5 down and hunches the shoulders 3.5 up. Both fade to zero between concept rows 735 and 870, so the ankle bindings, hem and feet never move, and both keep full weight within 16 pixels of her silhouette (alpha at or above 0.02) and reach zero at 34. The report lists displacements at named points, the largest displacement on her planted pixels, the farthest moving vertex and the largest strain. The sob and flinch clips (`NymphSob.anim`, `NymphFlinch.anim`) are Animation assets edited in the Animation window, as the canopy's are. Edit the constants at the top of the script, generate into scratch and compare before writing assets. Details are under "Nymph motion" in [Bound Tree layers](../../Docs/Reference/Art/BoundTreeLayers.md).

## HUD target number font

`build-lining-figures-font.py` builds `Assets/Dice/Presentation/Shared/Fonts/CormorantGaramondLining-SemiBold.ttf`, the font of the HUD's target number (`TargetLabel` in `SpiritGameplayHud.prefab`; every other label keeps `CormorantGaramond-SemiBold.ttf`). Cormorant Garamond's default digits are old-style (3, 5, 7 and 9 descend, 6 and 8 rise), and legacy `UI.Text` cannot apply the OpenType `lnum` feature, so the copy maps U+0030-U+0039 to the font's own lining glyphs `zero.lf` ... `nine.lf` in every cmap subtable that maps digits. It is renamed "Cormorant Garamond Lining SemiBold" (PostScript name `CormorantGaramondLining-SemiBold`) in name records 1, 3, 4, 6 and 16; the copyright (0), license (13, 14), style names and every other record, glyph and table are unchanged. After writing, the script rereads both fonts and refuses a result whose digits do not map to the lining glyphs, whose names differ from the intended ones, or whose other tables differ from the source (the head table apart from its checksum adjustment); it prints the digit mappings and the name records.

License: Cormorant Garamond is licensed under the SIL Open Font License 1.1 and declares no Reserved Font Name, so a modified version may keep a related name; it is still given a distinct one. The modified font is distributed under the same license, and `CormorantGaramond-OFL.txt` in the same folder is its license text.

It requires Python 3.9 or later with fontTools. fontTools is not installed system-wide; install it into an ignored folder and put that folder on `PYTHONPATH` (PowerShell, from the repository root):

```powershell
python -m pip install --target Docs/work/python-fonttools fonttools
$env:PYTHONPATH = 'Docs/work/python-fonttools'
python Tools/Presentation/build-lining-figures-font.py
```

The default writes a fresh ignored `Docs/work/lining-figures-font-*` directory; `--output` chooses an unused directory under `Docs/work`. `--write-assets` writes the font beside the source font and keeps its `.meta` file if one exists. The installed font's `.meta` is a copy of `CormorantGaramond-SemiBold.ttf.meta` with its own GUID, so both import with the same settings. The source font is only read, so repeated runs give the same font.

The lining digits are much taller than the old-style ones, so `TargetLabel` is sized to the 57.6-unit target panel: font size 26 (Best Fit 22-26, shrinking only a number too wide for the label) and anchors y 0.372-0.945, which centre the digits so that the digits and the comma's tail stay inside the frame's inner gold lines and clear of the 17-point caption, also at the 1.06 pulse of a changing number.

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

`MothCrossroadsJourney.asset` enables the three outcomes, with dialogue for victory and defeat and a direct summary after the brief abandon motion. Its existing clue points toward the old crossing, and its follow-up is the return-to-the-fox story journey.

Farewell dialogue uses the journey's `victoryText`, `defeatText`, or `abandonText`. The summary follows the optional dialogue and presents the score, victory clue, and earned unlocks; defeat offers Retry and Return to the trail. `followUpJourneys` lists the journey assets to highlight after victory, such as the lantern journey's Watchtower journey. These are trail recommendations: all encounter cards remain selectable, and selecting a highlighted encounter clears its highlight. Keep follow-ups unique, different from the source journey, and present in the investigation catalog.

## Scene light response maps

`author-scene-light-response.py` creates a linear data texture from authored triangles and feathered source masks. It does not read or edit scene artwork. Red contains normalized surface distance; green identifies the visible source and glow. Blue is unused. PNG coordinates in the JSON use a top-left origin. Import these maps with sRGB disabled; the lighting preview loads them as linear data automatically.

```text
python Tools/Presentation/author-scene-light-response.py --config Tools/Presentation/scene-light-response-moth.json --output Docs/work/moth-lighting-prototype/lantern-response-v2.png
```

The script requires Python 3.9 or later with no third-party packages, and refuses existing output paths. The moth preset contains estimated distances for the post, surroundings, rails and cart floor, plus the lantern's source mask. These are artistic controls, not depth reconstructed from the painting. Edit or copy the JSON for another composition; triangle patches are applied in order. Keep experimental PNG output under ignored `Docs/work`; only approved production maps belong in `Assets`.

`scene-light-response-moth-entry.json` supplies the corresponding causeway approach map. Use the same command with that config and a new output path. The selected entry and playfield maps are imported with sRGB disabled under `Assets/Dice/Art/MothCrossroads`.

`Dice > Encounter Lighting Preview` previews the authored entry/table assets with the actual dice and history renderers. Its intensity and animation controls operate on temporary copies. `Dice > Lighting Preview` remains the local-PNG exploration tool.
