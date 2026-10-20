# Spirit Dice: start here

Current state: October 19, 2026. This is a working contract and current-state summary, not a development diary. Verify relevant code, assets, and fresh Git status before acting.

## Working contract

Read [John's global instructions](C:/Users/jsp73/.codex/AGENTS.md), this document, [presentation authoring](PRESENTATION-AUTHORING.md), and [Tools/README.md](../Tools/README.md).

- Follow John's approved design. Do not invent screens, confirmation steps, celebrations, delays, rewards, or flow changes. Preserve accepted visuals and pacing unless the requested change requires otherwise.
- Inspect first; explain the cause and smallest complete proposed fix. Wait for implementation approval unless John has already directed the work. Agreement with an observation is not authorization. Historical proposals are not authorization.
- Keep work within scope. Report incidental issues separately. Do not add speculative safeguards, fallback systems, or unrelated refactors.
- Finish authorized implementation and relevant validation without repeatedly asking to continue. Keep progress visible during long work. If blocked, identify the actual blocker and what remains; do not present unfinished work as complete.
- John is a Unity expert. Use normal Unity mechanisms and explain concrete tradeoffs, not programming fundamentals.
- All workspace changes are user-owned, including generated assets and test output. Never restore, discard, delete, or roll back changes without explicit authorization for that action. No worktrees unless requested.
- Preserve existing identifiers and useful comments. Use ASCII comments and strings. Never abbreviate "checkpoint."
- Prefer Astra for orchestration, architecture, integration, and review; use GPT-6.1 Sol with high reasoning for bounded coding/test assignments. Delegation is authorized within approved work. Assign separate responsibilities and review the combined result.
- Tests support evidence, not design authority. Check actual player-visible behavior and rendered layouts where relevant. Do not add arbitrary waits to make failures disappear, weaken checks to match a bug, or equate structural assertions with visual correctness. Run appropriate checks, then stop when the requested work is verified.

## Current workspace and outstanding work

- Root: C:/Users/jsp73/Projects/Dice; Unity project: Dice-Unity; branch: main.
- Unity: 6000.6.6f1 at C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe.
- Entry scene: Dice-Unity/Assets/Scenes/Application.unity; menu: Dice > Open Application.
- The scene/prefab migration and removal of the unsolicited unlock notice are committed in 7d04b58. Check fresh Git status for subsequent work; do not discard any changes.
- Confirming a permanent charm unlock now proceeds directly to the next offer, with no extra "CHARM UNLOCKED" notice or timed pause. The selected unlock is still persisted.
- Editor migration generators (Spirit*Authoring, related helpers, and repair commands) still exist. Removal was proposed but has not been implemented or explicitly authorized. They are not the intended editing workflow. Open Application still invokes generation if its scene is missing; do not use that fallback as a repair strategy.
- No Android build/device validation has been performed for the migration or latest unlock fix. Do not assume the installed app matches the workspace.
- Player-built decks, further chapters, balance changes, and additional encounters are deferred, not current implementation tasks. Chapter 2 is narrative setup only.
- This handoff update does not authorize code changes, generator deletion, a commit, or an Android build. Follow John's next request.

## Architecture and coding boundaries

Mechanics, mechanical content, progression, presentation, and persistence remain separate. The engine must support a different skin or a larger game. Mechanical content contains rules and values, not display names, artwork, dialogue, or audio.

Each modifier has its own mechanical ScriptableObject and separate presentation asset. Conditions have individual evaluation classes with companion helpers where appropriate. Do not reintroduce central evaluation switches or presentation lookup dictionaries. Use explicit references or Addressables; nothing belongs in Resources. Modifier discovery labels are dice.modifiers and dice.theme.spirit-house.modifiers.

Every screen, popup, card, button, host and tray hierarchy is authored in a scene or prefab. Runtime code binds data/state, animates, and instantiates explicitly referenced prefabs for dynamic lists. Do not construct replacement hierarchies, choose styles by object name, or overwrite authored layout/typography during binding. Edit assets in place, preserving GUIDs, references and unrelated overrides. Unity Editor API operations through CLI/tooling are acceptable; retained generators are not necessary.

Before adding a system, identify its state, responsibility, dependencies and lifetime, then decide whether it belongs to an existing owner. Do not accumulate unrelated responsibilities in a controller/view or extract classes without a useful boundary. Shared encounter logic must not branch on fox/owl/moth or specific encounter IDs.

Paths below are relative to Dice-Unity/Assets/Dice.

| Owner | Responsibility |
| --- | --- |
| Engine/EncounterSession.cs, conditions, reward generator | State, deterministic resolution, modifier evaluation, offers |
| UnityContent/ModifierAsset.cs, catalog and loader | Authored mechanics and labeled discovery |
| App/DiceBootstrap.cs, DiceApplicationConfig.cs | Content/theme loading and lifetime before presentation is ready |
| App/SpiritApplicationPresentation.cs | Addressable frontend/encounter scene lifetime and binding; state remains in Application |
| App/SpiritEncounterController.cs, EncounterApplication.cs | Live run, actions, busy state, coordinated transitions and notifications |
| App/EncounterJourneyNavigation.cs | Journey/chapter selection, prerequisites, beat transitions and follow-up focus; no persistence/gameplay mutation |
| App/PlayerProgression.cs | Profile, grants, visits/completion and ordered run-before-profile writes; no view/controller reference |
| App/SpiritCampaignController.cs, CampaignSaveSlots.cs, CampaignRecoveryStore.cs | Main menu, slot mapping and recoverable campaign storage |
| Presentation/SpiritEncounterView.cs | Coordinates authored screen components and lifecycle |
| Presentation/SpiritGameplayHud.cs, SpiritScorePlayback.cs | HUD binding and ordered resolution presentation |
| Rewards, owned charms, popup, collection, journey and ending components | Their respective screen state and serialized references |

Install replacement run state and refresh caches before publishing notifications. Journey selection can differ from the retained run; visiting the board/story must not destroy a resumable encounter. Each presentation component owns its lifecycle; hiding gameplay must not reset an ending in progress.

## Current game design

The Old Road chapter is Fox, Owl, Moth, then a story-only return to the fox. Three encounters have ten stages each; the standard 25-stage encounter remains separate. Completed encounters remain replayable. Targets and tier weights are provisional.

| Encounter | Always-active rule |
| --- | --- |
| The Last Lantern / fox | First reroll of each hand spends no paid reroll |
| The Watchtower / owl | Adds one multiplier per distinct scored face |
| The Unmarked Fork / moth | Favored face cycles 1 through 6 by encounter hand count; matching scored dice add +2 multiplier each |

Chapters supply ordered journey references, title, description and artwork. Journey prerequisites determine availability; chapters do not duplicate progress flags. The board defaults to the latest available incomplete chapter, or latest available chapter if all are complete. Earlier chapters remain selectable. The return story saves gifts and completion on final Continue; interrupted/repeated stories are safe and grants are idempotent.

Main menu order is **Continue, Load Game, Chapter, New Game, Options**. Continue is disabled without a resumable save. Load Game selects one of three independent campaigns; empty slots are disabled. Chapter restores the saved run behind the board so "Continue at the table" resumes it. New Game selects a slot and warns before replacing an occupied campaign. Options remains a placeholder.

### Charms and progression

- Run offers use encounter-authored ordinary pools plus optional encounter-only additions, with tier weighting. Future player decks replace only the ordinary pool; deck building is not implemented.
- Milestones at stages 3, 6 and 10 offer up to three unowned individual charms from an explicit UnlockRewardModifierIds list. Select one permanent unlock, not a family. Show fewer if fewer remain; exhausted lists show "All charms collected" and Continue.
- Ownership persists after defeat/restart. It makes a charm eligible for applicable ordinary pools; it does not equip it. Pending choices/continuations are saved. Final unlock selection precedes the ending.
- Fox awards six Base/Mult variants across Open Gate, Still Reflection and Rising Sun. Owl/moth also use six Base/Mult variants from their authored milestone families. Some other locked variants have no current unlock source; do not silently add sources.
- Family discovery and individual ownership are separate. Starter/owned families are implicitly discovered. Invitations discover their authored unlock families; fox also grants Open Gate / Base. The return reveals Sheltering Branch and Boundary Stone and grants their authored Base and Mult charms.
- Story/beat grants use ProgressionRewardAsset; repeated grants are idempotent. Failed grant persistence blocks continuation without granting ownership. Presentation references do not grant charms.
- Only moth currently adds an encounter-only family: Lantern Seekers, triggered by at least two favored-face dice. Its introduction explains the additions; encounters without additions skip that screen. These are excluded from permanent Collection.
- Collection filters: Owned (default), Discovered, All. One row per family shares its trigger and shows four variants with ownership/availability. Undiscovered families are anonymous locked rows. Charms shows the current run's equipped modifiers.

### Presentation behavior

Portrait painted backgrounds/hosts combine with physical 3D dice and bays. Each encounter authors its camera, geometry, materials, lighting and host sequences. Keep narration separate from mechanical details; highlight gifted charm names in narration and show effects separately.

Intro Continue/Accept are available immediately during animation. Advancing cancels the previous beat safely; an interrupted automatic beat must not advance again.

Charm and hand-upgrade choices require selection followed by confirmation. Family artwork is shared; effect-category icons/colors distinguish variants. Trigger phrases have consistent colors independent of effect category.

Scoring accumulates dice sum, complete upgraded hand-rank values, qualifying modifiers, then Base times Mult. Upgrades are part of rank evaluation, not a separate step. Roll-triggered bonuses may accumulate beforehand. The small tray previews ready Play Hand modifiers; it is not a history tray. Tapping a tile opens a compact popup for that exact instance, dismissed by any tap without passing input through. Charms opens the full owned list. Action-button cues show transient activations.

## Persistence constraints

Use Unity's Application.persistentDataPath. Do not inspect or reject parent directories, redirected profiles or platform-managed links. Deletion checks stay within the application's owned campaign subtree.

Newtonsoft JSON stores campaign indexes, profiles and run saves. Run saves preserve RNG and pending offers; fresh runs choose new seeds, with fixed seeds only for debugging. Loading a run must not erase permanent progress. Keep current codec migrations; do not infer incompatible formats from old discussions.

Consumed unlock offers are saved to the run before the profile grant, preventing duplicate choices after interruption. Pending writes must finish before advancing/replacing the run. Autosaved journey actions persist before presentation. Continue restores state without replaying the opening roll or consuming RNG.

Corrupt slot indexes reconstruct uniquely identified campaigns; unassigned readable campaigns remain available through Load Game > Recover saves. Keep only the latest generated corrupt-index copy after successful reconstruction. Confirmed New Game replacement removes only the replaced campaign; recovering into an occupied slot preserves the displaced campaign for recovery. Ordinary access errors are not treated as corruption.

## Validation and tools

Follow [Tools/README.md](../Tools/README.md). Before CLI Unity runs, check whether Dice-Unity is open and ask John to close it if necessary. Do not close it yourself or affect other projects. Use the documented escalated, hidden Unity process; track its PID and inspect results XML. Use isolated test saves. Do not introduce delays merely to make tests pass.

Latest relevant evidence is under ignored Docs/work/unity-tests:

- Migration EditMode: full run 1,653/1,657, followed by 118/118 targeted checks covering all four failures. No single clean full-suite run is claimed. Evidence: authored-style-final-4e3901d37ef04c968abc437cc0ca2b28 and authored-edit-regressions-5bead733e73c4b2dbcf27302cf9a1823. One filesystem recovery fixture failed moving its temporary directory and passed on rerun; the external cause is unproven.
- Migration PlayMode: all 89 exercised cases pass on their latest results across full/focused runs. Final evidence: authored-play-ui-final-74c601e298954429a236a75c9aeb3e67 and authored-scene-resume-ca1e9c880cc748e6a767762818713a1a. Rendered portrait captures were inspected for scenes, previews, endings, rewards, hand upgrades, owned charms and Collection. This does not substitute for John's playtest or Android verification.
- Latest unlock fix: 3/3 progression presentation tests passed in unlock-direct-7e1ec2d311f74de68e820de56169dbf4. The nonfinal unlock test asserts the next offer and available input immediately after confirmation, without yielding a frame, plus persisted ownership. The full suites were not rerun after this narrow change.

Build/install Android only when requested. Generated output stays in ignored Docs/work; unexpected generated changes still require permission before deletion. Historical test results do not validate subsequent edits.

## Reference documents

- [Presentation assets and direct editing](PRESENTATION-AUTHORING.md)
- [Host, lighting and composition tools](../Tools/Presentation/README.md)
- [Collection and unlock authoring](../Dice-Unity/Assets/Dice/Content/Mechanical/COLLECTION-AUTHORING.md)
- [Extracted reference mechanics](outputs/dice-of-kalma-design-reference.html) and [reference inventory](Reference/README.md)

Use Git history for past changes. Do not restore the chronological progress log to this handoff.
