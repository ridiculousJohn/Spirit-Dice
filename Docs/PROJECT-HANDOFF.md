# Spirit Dice: start here

Updated October 16, 2026 for the collection and individual unlock pass. Check current Git status; this is not a claim about remote synchronization.

## What to do next

John authorized collection and individual unlocks (pass 1). Player-built decks are a separate future pass, not authorized by this implementation. Android builds remain deferred.

Pass 1 is implemented and validated, and John has playtested it successfully. Collection is accessible on the investigation board and in the gameplay menu.

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

Run charm choices use the encounter's authored ordinary pools plus optional `EncounterModifierIds`, with tier weighting retained. Player-built decks can later replace the ordinary pool. Ordinary charms have global scope; pool membership still determines where they can appear.

Stages 3, 6, and 10 offer up to three unowned individual charms from the encounter's separate `UnlockRewardModifierIds` list. Choose one permanent unlock, not an entire family. Empty lists show "All charms collected" and require Continue. The final unlock choice precedes victory and the ending. Pending choices and their continuation are saved automatically; selected unlocks persist in the profile and are not automatically equipped. Existing unlocks are preserved. Legacy milestone IDs remain solely for old-save validation.

Fox currently awards six Base/Mult variants of Open Gate, Still Reflection and Rising Sun. Owl/moth use six Base/Mult variants of their established milestone families, provisionally. Combined/reroll variants remain collected if already owned; otherwise they await future unlock sources. Edit explicit lists to tune rewards, not code. See [collection authoring](../Dice-Unity/Assets/Dice/Content/Mechanical/COLLECTION-AUTHORING.md).

Collection is available from the investigation board and gameplay menu. One row shows each family's shared trigger and four effect variants; locked variants are dimmed but inspectable. It reads existing profile progress without writing it. Charms remains the current run's equipped modifiers.

Only the moth currently supplies encounter-specific additions: Lantern Seekers, four variants triggering on at least two dice matching the favored face. Its optional introduction explains these additions before starting; fox/owl skip it. Provisional effects are +10 Base, +2 Mult, +5 Base/+1 Mult, and +1 reroll. Artwork reuses Last Lantern until dedicated artwork is requested.

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

Newtonsoft JSON profiles remain format 1 and store permanent unlocks, visits, and completions. Separate run saves use format 4/rules 6 and preserve RNG state and pending offers, with formats 1-3 migration. Loading a run must not erase permanent progress. New runs use fresh seeds; fixed seeds are a debug option. No deck/profile-format-2 system is implemented in this checkout.

## Presentation authoring

Portrait painted backgrounds/hosts combine with physical 3D dice and bays. Encounters author their materials, geometry, camera composition, lighting, and host sequences. Shared runtime code must not branch on specific spirits or encounter IDs.

Assets are under `Content/Presentation/SpiritHouse`, `Watchtower`, and `MothCrossroads`. Use `Dice > Tray Composition Preview` with its gameplay UI overlay for perspective and placement. Preserve accepted composition and pacing unless asked to change them.

Read [Tools/Presentation/README.md](../Tools/Presentation/README.md) for authoring and regeneration. Do not run historical scratch generators against production assets.

## Validation and delivery

October 16 collection validation: full EditMode run followed by targeted repairs/reruns leaves 1,394 distinct EditMode cases and 22 exercised PlayMode cases passing on their latest results. This includes unlock persistence/save-failure recovery, exhausted lists, endings, Addressables, and collection/intro portrait layouts at 443 and 540 pixels wide. Final layout evidence is in ignored `Docs/work/collection-layout-93f0d098975547938faf9537ae8dec93`; focused EditMode evidence is in `Docs/work/collection-edit-final-2fd1bc85841c4426a5564be5e99967d4`. No Android build was made.

- Follow [Tools/README.md](../Tools/README.md). Ask John to save and close Dice-Unity before CLI tests if open. Leave other projects alone.
- Launch Unity with `require_escalated` for licensing access, hidden, and track its PID. Never run two editors against this project. Verify results XML and process exit.
- Rendered checks need graphics; omit `-batchmode` for `WaitForEndOfFrame` tests. Use isolated test profiles, never the player's saves.
- Tests are in `Tests/EditMode` and `Tests/PlayMode`. Check affected behavior and portrait layout as needed. Historical results do not validate current edits.
- Generated output belongs in ignored `Docs/work`; durable source/assets belong in the repository. Unexpected output still requires authorization before deletion.
- Build/install Android only when requested. Check the actual device; do not assume its installed app matches current source.

For reference mechanics, use [the extracted design](outputs/dice-of-kalma-design-reference.html) and [reference inventory](Reference/README.md). Historical proposals are background, not current requirements or authorization. Inspect code before relying on their implementation claims.
