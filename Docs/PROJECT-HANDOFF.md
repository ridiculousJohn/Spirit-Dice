# Spirit Dice: start here

Verified October 15, 2026 against code baseline `2f7fc06` (`Fix modifier die artwork orientation`). The working tree was clean before this document update. Check current Git status; this is not a claim about later edits or remote synchronization.

## What to do next

**No implementation task is currently authorized. Wait for John's next request.** Historical proposals and missing features are not assignments.

For that request:

1. Inspect the relevant implementation and authored assets. Explain current behavior, the concrete problem, and the smallest proposed change. Identify who owns state and when it is saved where relevant.
2. Wait for an explicit implementation directive unless the request already contains one. Agreement with an observation or design discussion is not permission to edit.
3. Keep the work within that scope. Report incidental problems separately; do not silently add systems, defensive machinery, or unrelated fixes. Establish a failure's cause before choosing a remedy.
4. Run relevant checks, inspect the results, and state what changed and what remains unverified. Do not turn a focused task into an open-ended validation project.

## Workspace and working rules

- Repository: `C:/Users/jsp73/Projects/Dice`; project: `Dice-Unity`; branch: `main`.
- Unity: `6000.6.5f1`; editor: `C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe`.
- Main scene: `Dice-Unity/Assets/Scenes/SpiritHousePrototype.unity`.
- Read `C:/Users/jsp73/.codex/AGENTS.md` and [Tools/README.md](../Tools/README.md). John is a Unity expert. Explain decisions without teaching programming fundamentals.
- Preserve identifiers and useful comments; use ASCII in comments and strings. No worktrees unless requested.
- All changes are user-owned, including tool-generated changes. Never restore, delete, or discard any without explicit authorization for that action. Inspect fresh Git status and report unexpected changes.
- Delegation is authorized within approved work. Prefer GPT-6.1 Sol with high reasoning for bounded coding/tests; the orchestrator owns architecture, integration, and review. Recording a model preference does not change the active model.

## Implemented game

Three ten-stage encounters use shared investigation, arrival, invitation, gameplay, and ending systems:

| Encounter | Always-active rule |
| --- | --- |
| The Last Lantern / fox | First reroll of each hand spends no paid reroll. |
| The Watchtower / owl | Adds one multiplier per distinct scored face. |
| The Unmarked Fork / moth | Favored face cycles from 1 through 6 by encounter hand count; matching scored dice add +2 multiplier each. |

The standard 25-stage encounter remains separate. Targets and tier weights are provisional; no balance study is claimed.

Rewards use authored encounter pools, eligibility scopes, and tier weighting. Stages 3, 6, and 10 grant authored permanent unlock lists, including all four variants of their milestone families. Unlocks survive defeat and enter eligible future rewards; they are not automatically equipped. Charms shows the current run's owned modifiers.

Scoring presents dice sum, complete upgraded hand-rank values, qualifying modifiers, then Base times Mult. Roll-triggered bonuses can accumulate beforehand. The small tray previews qualifying Play Hand modifiers; it is not a history tray. Tapping a tile opens and highlights its owned charm. Transient action-button cues show activations.

Charm and hand-upgrade rewards require selection then confirmation. Family variants share artwork; category icons/colors distinguish Base, Mult, combined, and reroll effects. Trigger phrase colors are independent of effect category. The baseline includes corrected modifier-die icon orientation.

## Code and content entry points

Paths below are relative to `Dice-Unity/Assets/Dice`.

| Responsibility | Start here |
| --- | --- |
| State and action resolution | `Engine/EncounterSession.cs` |
| Conditions and rewards | `Engine/Conditions/`, `Engine/ModifierDefinitions.cs`, `Engine/RewardGenerator.cs` |
| Authored mechanics and discovery | `UnityContent/ModifierAsset.cs`, `UnityContent/MechanicalCatalogAsset.cs`, `UnityContent/ModifierAssetLoader.cs` |
| Application and persistence | `App/SpiritEncounterController.cs`, `App/EncounterApplication.cs`, `App/PlayerProfile.cs`, App codecs/file stores |
| Gameplay, journey, ending UI | `Presentation/SpiritEncounterView.cs`, `Presentation/EncounterJourneyView.cs`, `Presentation/SpiritEncounterEndingView.cs` |
| Host and table rendering | `Presentation/SpiritHostPortrait.cs`, `Presentation/SpiritHostPresentationAsset.cs`, `Presentation/EncounterTablePresentationAsset.cs`, `Presentation/SpiritDiceTray.cs` |

Keep mechanics, mechanical content, progression, and presentation separate. Mechanical content must not acquire display names, artwork, dialogue, or audio. Each modifier has its own mechanical ScriptableObject and separate presentation asset. Conditions have individual evaluation classes. Do not introduce central evaluation switches or presentation lookup dictionaries.

Use explicit references or Addressables; no project assets in Resources. Discovery labels are `dice.modifiers` and `dice.theme.spirit-house.modifiers`.

Newtonsoft JSON profiles use format 1 and store permanent unlocks, visits, and completions. Separate run saves use format 3 and preserve RNG state and pending offers. Loading a run must not erase permanent progress. New runs use fresh seeds; fixed seeds are a debug option.

## Presentation authoring

Portrait painted backgrounds/hosts combine with physical 3D dice and bays. Encounters author their materials, geometry, camera composition, lighting, and host sequences. Shared runtime code must not branch on specific spirits or encounter IDs.

Assets are under `Content/Presentation/SpiritHouse`, `Watchtower`, and `MothCrossroads`. Use `Dice > Tray Composition Preview` with its gameplay UI overlay for perspective and placement. Preserve accepted composition and pacing unless asked to change them.

Read [Tools/Presentation/README.md](../Tools/Presentation/README.md) for authoring and regeneration. Do not run historical scratch generators against production assets.

## Validation and delivery

- Follow [Tools/README.md](../Tools/README.md). Ask John to save and close Dice-Unity before CLI tests if open. Leave other projects alone.
- Launch Unity with `require_escalated` for licensing access, hidden, and track its PID. Never run two editors against this project. Verify results XML and process exit.
- Rendered checks need graphics; omit `-batchmode` for `WaitForEndOfFrame` tests. Use isolated test profiles, never the player's saves.
- Tests are in `Tests/EditMode` and `Tests/PlayMode`. Check affected behavior and portrait layout as needed. Historical results do not validate current edits.
- Generated output belongs in ignored `Docs/work`; durable source/assets belong in the repository. Unexpected output still requires authorization before deletion.
- Build/install Android only when requested. Check the actual device; do not assume its installed app matches current source.

For reference mechanics, use [the extracted design](outputs/dice-of-kalma-design-reference.html) and [reference inventory](Reference/README.md). Historical proposals are background, not current requirements or authorization. Inspect code before relying on their implementation claims.
