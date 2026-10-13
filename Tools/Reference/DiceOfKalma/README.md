# Dice of Kalma reference tools

Run these tools from any working directory with Python 3.10 or later. The preserved
reference snapshot is `Docs/Reference/DiceOfKalma`; original APK inputs are in its
`Inputs` directory. No installed Android device or user-specific directory is needed.
The inputs describe version 1.6.3; the native metadata header layout and named
constant hashes are specific to that build.

## Setup

From the repository root in PowerShell:

```powershell
python -m venv Docs/work/reference-venv
Docs/work/reference-venv/Scripts/python.exe -m pip install -r Tools/Reference/DiceOfKalma/requirements.txt
```

The requirements pin versions recorded in the analysis environment's installed
distribution metadata, including UnityPy's type-tree generator and ARM64 analysis
dependencies. Installations and caches remain under ignored `Docs/work`.
Fetch the Git LFS APK inputs when cloning the repository.

## Commands

Each command accepts `--input-dir`, `--apk-dir`, and `--output-dir`.
The default output is a fresh directory under `Docs/work/reference-analysis`.
An explicit output directory must be empty and under `Docs/work`; existing output
is never replaced. Commands print findings to the console. Use `--help` for options.

```powershell
Docs/work/reference-venv/Scripts/python.exe Tools/Reference/DiceOfKalma/build_reference.py
Docs/work/reference-venv/Scripts/python.exe Tools/Reference/DiceOfKalma/inspect_assets.py
Docs/work/reference-venv/Scripts/python.exe Tools/Reference/DiceOfKalma/decode_design.py
Docs/work/reference-venv/Scripts/python.exe Tools/Reference/DiceOfKalma/extract_constants.py
Docs/work/reference-venv/Scripts/python.exe Tools/Reference/DiceOfKalma/scene_links.py
```

- `build_reference.py` reads the committed design snapshot and emits the HTML
  report and companion JSON into scratch output. It computes source hashes from
  the original APKs, including the embedded native library and metadata. It does
  not update maintained reports in `Docs/outputs`.
- `inspect_assets.py` emits the generic asset inventory, metadata literals,
  four localization tables, and the extracted `libil2cpp.so` and
  `global-metadata.dat` required by native analysis tools.
- `decode_design.py` reads the four localization tables from the input directory
  and reconstructs custom MonoBehaviour data using UnityPy and Unity 6000.0.58f2
  type trees. Its default input tables are the preserved snapshot. Pass
  `--input-dir` with an `inspect_assets.py` output directory to use regenerated tables.
- `extract_constants.py` reads `metadata-header.json` from the input directory and
  extracts named static integer arrays from APK metadata. The preserved header
  applies to this exact build; a different build requires its own verified header.
- `scene_links.py` reads `design-assets.json` from the input directory, resolves
  GameObject paths, and emits scene-localization and challenge-event records.
  Pass a `decode_design.py` output directory as input to inspect regenerated data.

To build a report from a regenerated design snapshot, use the decoded output as
`--input-dir` after placing the independently regenerated `constants.json` there.
All required files must be present: `design-assets.json`, `constants.json`, and
`Skulls-english.json`, `DiceHands-english.json`, `GameUI-english.json`, and
`Dialogues-english.json`. Keep regenerated snapshots in `Docs/work` until explicitly
promoting reviewed evidence into the maintained reference directory.

## Native method analysis

Install Il2CppDumper separately in ignored scratch storage. Its executable,
dependencies, dummy assemblies and generated code dumps are not repository
inputs. Run it with `libil2cpp.so` and `global-metadata.dat` from
`inspect_assets.py` and a scratch output directory. The analysis used Il2CppDumper
6.7.46; its `script.json` provides method addresses and names for this APK build.

```powershell
Docs/work/reference-venv/Scripts/python.exe Tools/Reference/DiceOfKalma/disasm.py --script-json Docs/work/dumper-output/script.json 'BattleHandler.*CheckHand'
```

`disasm.py` requires an explicit local `--script-json` and one or more regular
expressions matching method names. It loads the native ELF from the preserved
ARM64 APK and emits annotated method disassembly into fresh scratch output.
The script.json and native library must come from the same APK version.
