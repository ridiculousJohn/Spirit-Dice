# Repository tools

- [Presentation](Presentation/README.md): author the host animation curves and deformation meshes.
- [Audio](../Docs/Reference/Audio/README.md): synthesize the project-authored audio clips.
- [Reference analysis](Reference/DiceOfKalma/README.md): extract and inspect the preserved reference game inputs and build its design report.

Reusable source, dependency declarations, and usage instructions belong here. Durable evidence, source manifests, and reference inputs belong under `Docs/Reference`. Production assets remain under `Dice-Unity/Assets`.

Generated previews, extraction output, downloaded tool installations, dependency environments, temporary test harnesses, logs, and art iterations belong under ignored `Docs/work`. Do not commit those files merely because a tool produced them. Review any new evidence before explicitly promoting it to `Docs/Reference`.

## Unity tests

Regression tests belong in `Dice-Unity/Assets/Dice/Tests/EditMode` or `PlayMode`, using the existing NUnit test assemblies. Run them through the Unity CLI. If the project is open in the editor, stop and tell John so he can close it or explicitly choose another approach. Do not close the editor or substitute menu-driven checks without his direction.

From the repository root in PowerShell, with the project editor closed:

```powershell
$testOutput = Join-Path (Resolve-Path '.').Path ('Docs/work/unity-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testOutput | Out-Null
& 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe' -batchmode -projectPath "$PWD/Dice-Unity" -runTests -testPlatform EditMode -testResults "$testOutput/results.xml" -logFile "$testOutput/unity.log"
```

Use `-testFilter` to select the relevant fixtures, or `-testPlatform PlayMode` for play-mode tests. Keep graphics enabled for shader and preview tests; `-nographics` cannot validate their GPU output. The XML report records failures and skipped tests. Test output stays under ignored `Docs/work`; tests must not install menu commands, run on editor startup, or depend on local art iterations.
