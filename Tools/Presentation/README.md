# Spirit presentation authoring

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
