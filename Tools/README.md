# Repository tools

- [Presentation](Presentation/README.md): author the host animation curves and deformation meshes.
- [Audio](../Docs/Reference/Audio/README.md): synthesize the project-authored audio clips.
- [Reference analysis](Reference/DiceOfKalma/README.md): extract and inspect the preserved reference game inputs and build its design report.

Reusable source, dependency declarations, and usage instructions belong here. Durable evidence, source manifests, and reference inputs belong under `Docs/Reference`. Production assets remain under `Dice-Unity/Assets`.

Generated previews, extraction output, downloaded tool installations, dependency environments, temporary test harnesses, logs, and art iterations belong under ignored `Docs/work`. Do not commit those files merely because a tool produced them. Review any new evidence before explicitly promoting it to `Docs/Reference`.

## Save games

Dice > Save Games (Dice-Unity/Assets/Dice/Editor/SaveGamesWindow.cs, with its operations in SaveGameStore.cs) shows and edits the save games in Application.persistentDataPath. It reads and writes them only through the game's save code (CampaignSaveSlots, the profile store and codec, SaveFileCommit), so seals and formats stay valid, and it reads the content through the project's application configuration.

- Slots: the three slots, the active one, each slot's campaign id, its profile (with load errors) and whether it has a run in progress, and the campaigns available for recovery. Set active selects the slot; New game starts a new game in the slot as the game's New Game does, replacing the campaign it holds; Delete run deletes the slot's run save and nothing else.
- Profile of the selected slot: completed journeys, completed encounters, visited encounters, discovered families and unlocked charms as checkboxes named from the content; stored ids the content does not list appear under "Not in the content". Families the game treats as discovered without a stored entry (starter families, families of unlocked charms) show as checked and not editable. Save writes the profile through SaveFileCommit as it is, keeping the replaced file as .bak; Revert reads it again.
- Presets: "Everything before journey..." writes the profile of a player who completed every journey before the chosen one in chapter order, playing each through the game's journey navigation with every beat, invitation and completion reward and every milestone unlock applied; the chosen journey stays available and not started. Fresh writes a new player's profile. Both leave the run save as it is.
- Back up saves copies every save file and campaign folder in the save root to a new timestamped folder under Docs/work/save-backups. Clear saves makes the same backup, then moves every save file and campaign folder out of the save root into that backup's `moved` folder; nothing is deleted. Save files are the slot index, its corrupt-index copies, the root run saves and profile of direct play and the diagnostic scene, and the .bak and temporary files beside them.

Destructive actions ask for confirmation. In Play Mode the window shows the saves read-only. It reads the saves again on focus, on Refresh and after every write and shows the load errors the game reports; Save refuses to write when the profile file changed on disk after it was read.

## Unity tests

Regression tests belong in `Dice-Unity/Assets/Dice/Tests/EditMode` or `PlayMode`, using the existing NUnit test assemblies. Run them through the Unity CLI. If the project is open in the editor, stop and tell John so he can close it or explicitly choose another approach. Do not close the editor or substitute menu-driven checks without his direction.

When launching Unity from Codex on this Windows machine, use `exec_command` with `sandbox_permissions: "require_escalated"` for the Unity process. Request this per-command exception on the first launch. The workspace sandbox prevents the editor from connecting to the local Unity licensing client and accessing its profile cache; repeated sandboxed retries time out before tests begin. Normal file edits and inspection can remain sandboxed. Do not change global sandbox settings, Windows ACLs, or Unity licensing files to run these tests.

When using `Start-Process`, pass `-WindowStyle Hidden` and capture its process ID. Process creation or a successful shell exit does not establish that tests passed: wait for Unity's results XML and inspect failures. If a test launch stalls, stop only the process launched for that run, confirm it has exited, and retain its log before attempting another launch.

From the repository root in PowerShell, with the project editor closed:

```powershell
$testOutput = Join-Path (Resolve-Path '.').Path ('Docs/work/unity-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testOutput | Out-Null
& 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe' -batchmode -projectPath "$PWD/Dice-Unity" -runTests -testPlatform EditMode -testResults "$testOutput/results.xml" -logFile "$testOutput/unity.log"
```

Use `-testFilter` to select the relevant fixtures, or `-testPlatform PlayMode` for play-mode tests. Omit `-batchmode` for rendered PlayMode checks that use `WaitForEndOfFrame`; these require the editor's rendered frame callback. They still run through the CLI with `-runTests`. Keep graphics enabled for shader and preview tests; `-nographics` cannot validate their GPU output. The XML report records failures and skipped tests. Test output stays under ignored `Docs/work`; tests must not install menu commands, run on editor startup, or depend on local art iterations.
