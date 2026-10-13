# Spirit presentation authoring

## Crossroads moth

`author-moth-presentation.py` authors the moth host and deformation mesh using the same layer/timeline format. Its default output is a new ignored `Docs/work/moth-authoring-*` directory; `--write-assets` writes only `MothCrossroadsHost.asset` and `MothBodyMesh.asset`. It requires Python 3.9 or later with no third-party packages and imports the shared authoring helpers beside it.

The production body and wing smoke use aligned 1536x1024 canvases. `--foot-x` and `--foot-y` describe their contact point in bottom-left UV coordinates (defaults `0.5`, `0.28`); `--host-width` defaults to 370 in the 540x960 reference portrait. Waiting, Arrival, Idle and Reaction are authored sequences. Arrival lasts seven seconds. The smoke layer uses distortion `0.035`, speed `0.7`, opacity `0.6`, and does not receive scene lighting.

Any host layer can enable `Smoke` and supply its own motion mask and repeating flow-noise texture. Missing textures use generated defaults. Import custom mask/noise textures as linear data; smoke artwork uses ordinary color and alpha. Smoke and silhouette materials belong on separate layers. Tint, deformation and timeline opacity still apply to smoke layers.

## Inn and watchtower

`author-spirit-presentations.py` contains the approved motion formulas for the Lantern Keeper and Watchtower host. It generates two host assets and nine deformation meshes. The game loads the checked-in Unity assets and does not run this script.

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

## Scene light response maps

`author-scene-light-response.py` creates a linear data texture from authored triangles and feathered source masks. It does not read or edit scene artwork. Red contains normalized surface distance; green identifies the visible source and glow. Blue is unused. PNG coordinates in the JSON use a top-left origin. Import these maps with sRGB disabled; the lighting preview loads them as linear data automatically.

```text
python Tools/Presentation/author-scene-light-response.py --config Tools/Presentation/scene-light-response-moth.json --output Docs/work/moth-lighting-prototype/lantern-response-v2.png
```

The script requires Python 3.9 or later with no third-party packages, and refuses existing output paths. The moth preset contains estimated distances for the post, surroundings, rails and cart floor, plus the lantern's source mask. These are artistic controls, not depth reconstructed from the painting. Edit or copy the JSON for another composition; triangle patches are applied in order. Keep experimental PNG output under ignored `Docs/work`; only approved production maps belong in `Assets`.

`scene-light-response-moth-entry.json` supplies the corresponding causeway approach map. Use the same command with that config and a new output path. The selected entry and playfield maps are imported with sRGB disabled under `Assets/Dice/Art/MothCrossroads`.

`Dice > Encounter Lighting Preview` previews the authored entry/table assets with the actual dice and history renderers. Its intensity and animation controls operate on temporary copies. `Dice > Lighting Preview` remains the local-PNG exploration tool.
