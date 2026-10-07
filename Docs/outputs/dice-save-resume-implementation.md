# Dice encounter save and resume

An encounter can be captured at any committed action boundary and restored without rolling dice, generating offers, resetting modifier counters, or replaying gameplay events. Persistence remains separate from presentation.

## Boundaries and APIs

`Dice.Engine` owns the portable `EncounterSaveData` schema, `EncounterSession.CaptureSave()`, and `EncounterSession.RestoreEncounter(catalog, save)`. Capture returns detached data. Restore validates it and returns either a new session or a structured `EncounterRestoreError`; it does not modify an existing session. The restored initial resolution contains the saved snapshot and no events.

The data includes dice and their history, stage and hand resources, thresholds and totals, owned modifier instances and activation counters, accumulated contributions, upgrades, offer and option identities, reward exclusion history, cached upgrade choices, the action revision, and the complete state of both random streams. Modifying a captured object or the object supplied to restore cannot change a live session.

`Dice.App` owns the encoding, file storage, and application restoration bridge. `EncounterApplication.CaptureSave()` exposes capture through the host. `EncounterApplication.TryRestore(...)` creates an application only after engine restoration succeeds. Attaching a presenter synchronizes directly to the saved snapshot.

## JSON and file storage

`EncounterSaveCodec` uses Newtonsoft.Json to serialize the data as indented UTF-8 JSON. Public codec methods retain byte-array inputs and outputs so encoding remains separate from file I/O. Enums are written by name. Signed and unsigned 64-bit values retain their exact integer values, including random states above the signed 64-bit range.

The installed Unity package is `com.unity.nuget.newtonsoft-json` 3.2.2, containing Newtonsoft.Json 13.0.2. It is referenced by the application and test assemblies. The package is documented in [Unity's Newtonsoft guide](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html).

Known fields are required, including fields whose value can legitimately be null. This prevents a missing random state from silently becoming zero. Unknown additional fields are ignored; duplicate properties and malformed or incomplete payloads are rejected. Newtonsoft dependencies and serialization policy remain in the application assembly; the engine has no serializer or Unity dependency.

`EncounterSaveFileStore.Save(path, data)` writes a complete temporary file before replacing the target. The previous successful save is retained at `path + ".bak"`. `Load(path)` reads and decodes the file; the application then supplies the catalog for engine validation. Paths are caller-owned. A failed write is reported to the caller rather than silently loading a different save or starting another encounter.

## Compatibility and validation

The save carries separate format, rules, random-algorithm, and mechanical-catalog versions, plus its encounter ID. This implementation supports format 1, engine rules 3, random algorithm 1, and the exact catalog version supplied at restore. Unsupported versions fail explicitly; no migration or mechanical reinterpretation is performed. Presentation changes do not participate in compatibility.

Validation checks required arrays, dice and masks, counter/resource consistency, known definitions, modifier contributions and activation limits, legal phases and outcomes, and pending reward eligibility and identities. Completed, abandoned, and custom encounters at the external reward boundary are supported alongside Standard.

The tests compare full saved data, snapshots, and events against uninterrupted execution. They also mutate capture/restore inputs to verify independence, reject malformed or incompatible saves, exercise file replacement and backup behavior, and verify that a restored presenter receives synchronization without replayed gameplay.

The Standard victory replay serializes and restores after all 264 actions across all 25 stages, including both reward-selection phases. Its final score, 80,561, and subsequent deterministic results match uninterrupted execution.

Final validation on Unity 6000.6.2f1 passed all 649 EditMode tests with zero failures or skipped tests and process exit code 0. The full suite also includes the existing modifier and reward tests and a legal Standard loss. No compiler errors or warnings were reported. The only Assets/Packages/ProjectSettings file changed during the final run was `Packages/packages-lock.json`, which recorded the requested Newtonsoft dependency.

Results and logs are retained as `Docs/work/save-json-editmode-1.xml` and `Docs/work/save-json-editmode-1.log`. File-store test saves and backups remain under `Docs/work/save-resume-file-tests/09878748582b4950b5cbae92cc30ddb3`. Intermediate validation logs and the interim binary codec are preserved under `Docs/work`; the application uses JSON.

## Next work

The diagnostic Unity scene is next. Save/resume is available through the application API; no menu, autosave timing, profile progression, or cloud storage has been added.
