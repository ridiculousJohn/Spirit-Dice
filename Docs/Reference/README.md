# Durable reference material

- `DiceOfKalma`: extracted design data, original localization tables, relevant native disassembly, type excerpts, and screenshots of the reference game. `Inputs/sources.json` identifies the preserved version and SHA-256 hashes of its original APKs; APKs and screenshots use Git LFS.
- `Modifiers`: catalog mapping, condition/effect findings, and resolver disassembly.
- `Rewards`: reward pools and native selection/flow findings.
- `Scoring`: native scoring sequence evidence.
- `Audio`: source credits and synthesis/processing recipes for the production audio.

These files support implementation and further investigation on another computer. The approved game artwork is under `Dice-Unity/Assets`; art experiments and test results remain local.

See `Tools/Reference/DiceOfKalma/README.md` for dependency setup and reproduction commands. The bulky extracted native library, metadata, complete code dumps, and installed extraction tools are reproducible local output, not additional copies to track. Historical evidence may name their original `Docs/work/dice-of-kalma` paths. Recreate those files from the preserved APK inputs when further native investigation needs them.

The existing maintained design report and implementation documents remain in `Docs/outputs`.
