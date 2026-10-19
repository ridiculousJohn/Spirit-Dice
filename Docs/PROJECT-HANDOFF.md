# Spirit Dice: start here

Updated October 19, 2026 for Unity 6000.6.6f1, application startup configuration, the profile progression and journey navigation extractions, encounter and journey view splits, and test repairs; the first chapter's encounter and story sequence are unchanged since October 16. Check current Git status; this is not a claim about remote synchronization.

## What to do next

John authorized collection and individual unlocks (pass 1). Player-built decks are a separate future pass, not authorized by this implementation. Build Android only when requested.

John playtested the individual collection/unlock pass successfully. Family discovery, story grants, and Collection filters are now implemented. Deck building remains deferred; its future ordinary pool replaces the encounter-authored ordinary pool, while encounter additions remain separate.

The first chapter is authored as Fox, Owl, Moth, then a story-only return to the fox. Chapter 2 is a narrative setup, not a playable region. Playtest this sequence and its rewards before expanding it.

For that request:

1. Inspect the relevant implementation and authored assets. Explain current behavior, the concrete problem, and the smallest proposed change. Identify who owns state and when it is saved where relevant.
2. Wait for an explicit implementation directive unless the request already contains one. Agreement with an observation or design discussion is not permission to edit.
3. Keep the work within that scope. Report incidental problems separately; do not silently add systems, defensive machinery, or unrelated fixes. Establish a failure's cause before choosing a remedy.
4. Run relevant checks, inspect the results, and state what changed and what remains unverified. Do not turn a focused task into an open-ended validation project.

## Workspace and working rules

- Repository: `C:/Users/jsp73/Projects/Dice`; project: `Dice-Unity`; branch: `main`.
- Unity: `6000.6.6f1`; editor: `C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe`.
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

The investigation board follows authored completion prerequisites: fox first, owl after fox, moth after owl, and `story.fox.chapter-one-return` after moth. Previously visited/completed encounters remain replayable. The return reuses the fox's room and host through a story-only journey; it never creates a dice encounter. Its explicit final Continue saves story completion and gifts together, then returns to the board. Interrupted stories can be replayed safely. Completed stories have a Read Again card. No chapter-specific runtime branch is used.

Run charm choices use the encounter's authored ordinary pools plus optional `EncounterModifierIds`, with tier weighting retained. Player-built decks can later replace the ordinary pool. Ordinary charms have global scope; pool membership still determines where they can appear.

Stages 3, 6, and 10 offer up to three unowned individual charms from the encounter's separate `UnlockRewardModifierIds` list. Choose one permanent unlock, not an entire family. Empty lists show "All charms collected" and require Continue. The final unlock choice precedes victory and the ending. Pending choices and their continuation are saved automatically; selected unlocks persist in the profile and are not automatically equipped. Existing unlocks are preserved. Legacy milestone IDs remain solely for old-save validation.

Fox currently awards six Base/Mult variants of Open Gate, Still Reflection and Rising Sun. Owl/moth use six Base/Mult variants of their established milestone families, provisionally. Combined/reroll variants remain collected if already owned; otherwise they await future unlock sources. Edit explicit lists to tune rewards, not code. See [collection authoring](../Dice-Unity/Assets/Dice/Content/Mechanical/COLLECTION-AUTHORING.md).

Collection is available from the investigation board and gameplay menu. Its filter cycles Owned (default), Discovered, and All. Owned includes starter families and families with an owned variant. Discovered also shows explicitly revealed families without owned variants. All includes compact anonymous locked rows for undiscovered families. Discovered rows show the shared trigger and four variants; locked variants remain inspectable. Inert and encounter-only families are excluded from permanent Collection. Charms remains the current run's equipped modifiers.

Family discovery is separate from individual ownership. Starter and already-owned families are implicitly discovered; an individual grant also discovers its family. New unlock offers require the family to be discovered. Each existing encounter's invitation discovers its three authored unlock families; the fox also grants Open Gate / Base. The return story reveals Sheltering Branch and Boundary Stone and grants Sheltering Branch / Base (+15 Base) and Boundary Stone / Mult (+2 Mult). `ProgressionRewardAsset` has explicit discovered family and granted modifier lists. Journeys optionally reference these assets on beats and on completion; future story systems can call `SpiritEncounterController.ApplyProgressionReward`. Beat rewards apply on continuation, skipping, and acceptance; repeated grants are idempotent. A failed story grant save blocks continuation without changing ownership. These rewards do not equip charms or change the run pool.

Only the moth currently supplies encounter-specific additions: Lantern Seekers, four variants triggering on at least two dice matching the favored face. Its optional introduction explains these additions before starting; fox/owl skip it. Provisional effects are +10 Base, +2 Mult, +5 Base/+1 Mult, and +1 reroll. Artwork reuses Last Lantern until dedicated artwork is requested.

Scoring presents dice sum, complete upgraded hand-rank values, qualifying modifiers, then Base times Mult. Roll-triggered bonuses can accumulate beforehand. The small tray previews qualifying Play Hand modifiers; it is not a history tray. Tapping a tile opens a compact centered inspection popup for that exact charm instance. Close or a tap anywhere dismisses it without passing the tap to gameplay. The Charms button still opens the full owned list. Transient action-button cues show activations.

Charm and hand-upgrade rewards require selection then confirmation. Family variants share artwork; category icons/colors distinguish Base, Mult, combined, and reroll effects. Trigger phrase colors are independent of effect category. The baseline includes corrected modifier-die icon orientation.

## Code and content entry points

Paths below are relative to `Dice-Unity/Assets/Dice`.

| Responsibility | Start here |
| --- | --- |
| State and action resolution | `Engine/EncounterSession.cs` |
| Conditions and rewards | `Engine/Conditions/`, `Engine/ModifierDefinitions.cs`, `Engine/RewardGenerator.cs` |
| Authored mechanics and discovery | `UnityContent/ModifierAsset.cs`, `UnityContent/MechanicalCatalogAsset.cs`, `UnityContent/ModifierAssetLoader.cs` |
| Application startup and content lifetime | `App/DiceBootstrap.cs`, `App/DiceApplicationConfig.cs`, `Content/DiceApplicationConfig.asset` |
| Application and persistence | `App/SpiritEncounterController.cs`, `App/EncounterApplication.cs`, `App/PlayerProgression.cs`, `App/PlayerProfile.cs`, App codecs/file stores |
| Journey navigation | `App/EncounterJourneyNavigation.cs` (location, prerequisites, transitions, follow-up highlights) |
| Gameplay screen | `Presentation/SpiritEncounterView.cs` (lifecycle, tray, table, overlays, refresh), `Presentation/SpiritGameplayHud.cs`, `Presentation/SpiritScorePlayback.cs` |
| Rewards and charm UI | `Presentation/SpiritRewardsPanel.cs`, `Presentation/SpiritOwnedCharmsPanel.cs`, `Presentation/SpiritCharmPopup.cs`, `Presentation/SpiritCharmVisuals.cs` |
| Journey and ending UI | `Presentation/EncounterJourneyView.cs` (beats, motion, lighting, invitation layout), `Presentation/SpiritInvestigationBoard.cs`, `Presentation/SpiritEncounterCharmsScreen.cs`, `Presentation/SpiritEncounterEndingView.cs` |
| Host and table rendering | `Presentation/SpiritHostPortrait.cs`, `Presentation/SpiritHostPresentationAsset.cs`, `Presentation/EncounterTablePresentationAsset.cs`, `Presentation/SpiritDiceTray.cs` |

`SpiritEncounterView` coordinates its screen components rather than drawing them. Add a self-contained panel as a sibling component following `SpiritHandRanksPanel` (`Build`, `Render`, `Root`) instead of growing the view. `SpiritGameplayHud` builds in ordered phases because its widgets interleave with the tray, rewards, and ending under the portrait; the composition preview builds the HUD alone. `SpiritScorePlayback` runs resolution events against the HUD and reads live view state through `ISpiritScorePlaybackHost`. `EncounterJourneyView` follows the same rule: the investigation board and Encounter Charms screen are sibling components, while the view keeps the beat sequence, motion, lighting, and invitation layout. Accept finishes the current beat at its authored final pose (`FinishAnimation`) so Back to invitation returns to that pose; `CancelAnimation` resets motion for a new beat.

`PlayerProgression` owns the loaded profile, catalog-filtered progress, transactional story/reward/visit writes, and pending run-before-profile persistence. It is plain C# with no view, controller, or live application reference. An unlock operation supplies the captured run save and its explicit destination; a successful run write is not repeated when only the profile write needs retrying. Pending progress must be flushed before the live run advances or is replaced.

`EncounterJourneyNavigation` owns the selected journey, phase, beat, prerequisite/skip/resume/retry rules, and temporary follow-up highlights. It consumes read-only progression facts and authored journey assets; it has no profile writer, live application, controller, view, or notification callbacks. Reward-bearing transitions are synchronous proposals: the controller persists their rewards and prepares any replacement run before committing navigation. Failed writes leave the current location intact; previously persisted grants remain idempotent. The selected journey is independent of the retained run, so investigation and story visits can preserve a resumable encounter.

`SpiritEncounterController` coordinates those components and keeps the existing view interfaces. It owns the live run, busy state, and outward notifications. The navigator is the single runtime source of its journey catalog. Acceptance, loading, and direct initialization install the run and refresh its preview/inspections before publishing journey changes. Successful story grants merge into the retained run and refresh its caches before observers are notified. Ordinary beat advances and intro skipping notify journey observers; story completion additionally notifies gameplay observers. Keep navigation decisions out of the controller and persistence/gameplay mutations out of the navigator.

`DiceBootstrap` loads `Content/DiceApplicationConfig.asset` through the Addressable address `dice.application`. That asset references the existing mechanical and journey catalogs and owns discovery labels, the optional direct encounter ID, and the seed policy. The scene has no serialized controller content configuration. Bootstrap owns the configuration, modifier and presentation loads and the installed theme until destruction, releasing failed or superseded startup loads. Disabling it after startup retains installed content; disabling it during startup cancels the unfinished load. The diagnostic scene retains its separate startup path.

Keep mechanics, mechanical content, progression, and presentation separate. Mechanical content must not acquire display names, artwork, dialogue, or audio. Each modifier has its own mechanical ScriptableObject and separate presentation asset. Conditions have individual evaluation classes. Do not introduce central evaluation switches or presentation lookup dictionaries.

Use explicit references or Addressables; no project assets in Resources. Discovery labels are `dice.modifiers` and `dice.theme.spirit-house.modifiers`.

Newtonsoft JSON profiles use format 4 and migrate formats 1 and 3, retaining unlocks, visits, discoveries, and encounter completions while adding completed story IDs. Historical deck-format-2 profiles remain unsupported. Separate run saves use format 5/rules 7 and preserve RNG state, discovery, and pending offers, with formats 1-4 migration. Older runs retain their previous family eligibility; this does not reveal the entire permanent Collection. Loading a run must not erase permanent progress. New runs use fresh seeds; fixed seeds are a debug option. No deck system is implemented.

## Presentation authoring

Portrait painted backgrounds/hosts combine with physical 3D dice and bays. Encounters author their materials, geometry, camera composition, lighting, and host sequences. Shared runtime code must not branch on specific spirits or encounter IDs.

Assets are under `Content/Presentation/SpiritHouse`, `Watchtower`, and `MothCrossroads`. Use `Dice > Tray Composition Preview` with its gameplay UI overlay for perspective and placement. Preserve accepted composition and pacing unless asked to change them.

Narrative gift beats use an optional `EncounterJourneyBeat.Charm` reference and a `{charm}` name token. Dialogue highlights the charm name; a separate compact panel beneath it shows existing family artwork, category-colored effect, and trigger/condition text. The initial Open Gate gift and both return-story gifts use this layout. The reference does not grant ownership; progression rewards remain separate.

Invitations also separate narration from mechanics. Narration uses normal cream dialogue text; a divider introduces the rule icon/name and smaller effect text. Fox, owl, and moth dialogue avoids repeating numerical bonuses. Author exact effects in the rule presentation, and keep encounter-wide duration in the shared footer.

Intro Continue and Accept buttons are available immediately while authored animation plays. Advancing cancels the previous beat through the existing journey transition path; interrupted automatic beats must not advance again. This change is newer than the October 16 Android APK listed below.

Read [Tools/Presentation/README.md](../Tools/Presentation/README.md) for authoring and regeneration. Do not run historical scratch generators against production assets.

## Validation and delivery

October 19 application startup extraction: `DiceApplicationConfig.asset` owns authored startup settings at the local Addressable address `dice.application`; `DiceBootstrap` owns loading and content lifetime. The production controller has no serialized content settings and reads its journey catalog from the navigator. Ordinary beat/skip transitions no longer send redundant gameplay notifications. Full EditMode initially passed 1,513/1,514; the remaining scene-dependency assertion was updated to check encounter lighting through the Addressable configuration, and its fixture passed 4/4. The focused rendered run passed 22/26; four new lifecycle tests had omitted the required view theme in their test object, so their fixture now loads the real scene and passed 5/5. All 26 selected PlayMode cases therefore pass across those runs, including loading, cancellation, explicit initialization superseding pending startup, content retention on disable, and journey/story/save flows. Evidence: `Docs/work/unity-tests/994e6add854949249de6a2da58bdb609` (initial EditMode), `adf7e63505134165ba880d79365631a3` (dependency fixture, exit 0), `50c66c170be04b7ab224af6fbf8b260e` (initial PlayMode), and `4fdb01607cd249d3ab68a0667912391c` (startup fixture, exit 0). No Android build was made.

October 19 journey navigation extraction: `EncounterJourneyNavigation` owns journey location, transition rules, prerequisite checks, and temporary follow-up highlights. The controller coordinates persistence and run installation before publishing navigation changes; story completion refreshes the retained run and notifies both observer groups. Nineteen direct navigation cases cover deferred transitions, reward ranges, prerequisites, resume/retry/load, and highlights. Four controller regressions inspect state inside callbacks for acceptance, loading, investigation initialization, and story completion. On Unity 6000.6.6f1, full EditMode passed 1,497/1,497 and focused rendered PlayMode passed 16/16, covering journey/chapter flows, rapid input and automatic-beat cancellation, resume/load, unlock presentation, and catalog reinitialization. Evidence is in ignored `Docs/work/unity-tests/ff57e2619ba94fea907606c782c4265c` and `8e061c22fa0340f4982ba544907032f3`. Both Unity processes exited with code 0. Save formats, authored content, and view interfaces are unchanged. No Android build was made.

October 19 profile progression extraction: `PlayerProgression` owns profile state and ordered persistence; controller APIs, save formats, journey flow, and notification ordering are retained. Nine direct tests cover profile replacement/merge, catalog projections, transactional write failures, idempotent rewards, and run-save-before-profile retry behavior. A controller regression additionally verifies that failed replacement initialization cannot redirect the active run's unlock saves. Full EditMode passed 1,473/1,473; after adding that regression, the affected controller fixture passed 8/8. Focused rendered PlayMode passed 6/6 for permanent unlock UI, chapter story completion, catalog reinitialization, and return/resume/load. Evidence is in ignored `Docs/work/unity-tests/d5b00da472654854a3964efe51b20358`, `42843265e2c441788ea4586ef803b3fb`, and `b7ef16c2f66c491ebc300caf4ef4642f`. All three Unity runs exited with code 0. No Android build was made.

October 19 journey split, test repairs, and initialization ordering:
- `19da928` moved the investigation board and Encounter Charms screen out of `EncounterJourneyView` (883 to 691 lines), made Accept finish the beat at its final pose rather than resetting motion, and removed the always-true `SetButtons` parameter and dead theme fallbacks. No visible change today: the moth invitation, the only beat with Encounter Charms, has no motion.
- `adaa879` gives journey parting panels fixed halves of the outgoing background, since nothing crops journey backgrounds; `SceneLightingViewLifecycleTests` now asserts fixed halves instead of cropped UVs.
- `3ca7b40` repaired `RealCharmActivationsLinkTheirIconToOnlyTheAffectedCounters`. It sampled counter icons on the first frame the action-button cue reached full opacity, but the 0.52-second counter highlight can end before that frame, so it failed intermittently in this checkout (cause of the slow frame unproven). Counter selection is now checked by `SteppedCharmActivationsLightOnlyTheAffectedCounterIcons`, which reflects into the view's playback, steps `AnimateContribution` to its first yield, and inspects the real icons; it loads a fresh scene per charm. It verifies UI wiring, not whether `Play` dispatches the contribution. The original test keeps its sampled cue, artwork, result, and natural-cleanup checks.
- `494454c` fixed an ordering defect in `SpiritEncounterController.Initialize`: `JourneyChanged` fired after `Catalog` changed but before `Install`, so a refresh could pair the new catalog with the previous run's hand preview and throw `KeyNotFoundException` when the ready-charm tray was out of sync. Gameplay never switches catalogs this way; tests did. `ReinitializingWithAnotherCatalogRefreshesOnlyTheNewRun` was shown failing with the old ordering (`Docs/work/unity-tests/638fafdd994e4e929dc899ea2276df5f`) and passing with the fix (`6ac7a65f9af44fd191fb8a9ba8ddd390`).
- With the ordering fix applied and before that regression test was added, full EditMode passed 1,464/1,464 (`98b2eb021f184a4995fa679d5fb4704e`) and full rendered PlayMode passed 63/63 (`1afe9e858b6242cead4f315906a55d50`); the readability fixture passed 6/6 twice (`1a7140f6c1394e45a76895c2668a925e`, `800256f3147948b381644352e7a39e8f`). The full suites have not been rerun on `494454c` itself. The `EncounterSaveFileStore` save race below remains unaddressed. No Android build was made.

October 18 encounter view split: `SpiritEncounterView` was reduced from 2,281 to 884 lines by moving rewards, owned charms, and the charm popup (`ccdcac7`), then the gameplay HUD and score playback (`c4df307`), into their own components. Build order, GameObject names, and the view's public API are unchanged; the unused `Pulse` coroutine and the unused reward branch of the owned-charm card factory were removed. Three PlayMode tests that predated the split and failed identically on `5cc7d40` were updated in `a933918`: moth and owl arrivals now expect Continue to be available during animation, and the scene save comparison uses the controller's discovered families. On the `c4df307` tree, the full EditMode run passed 1,464/1,464 and the full rendered PlayMode run passed 62/62. Evidence is in ignored `Docs/work/unity-tests/16ee684ec49747608adc3d53d5aae363` and `f578602f937c45d2b9a0268fd6c9b49b`; Unity exited with code 0. One earlier EditMode run failed `ConsecutiveDefaultNewRunsUseDifferentDiceAndRewardStreams` because `File.Replace` in `EncounterSaveFileStore` did not swap in a written temporary save (a transient Windows file lock is the likely cause); its rerun passed, and the save race itself is not addressed. No Android build was made. An unrecorded APK, `Docs/work/android-build-a07913f2dd324071844c164d2f5a98b2/SpiritDice.apk` (October 16, 15:11), also exists; its contents and install state are unverified.

October 16 charm popup: 3/3 focused PlayMode checks passed for tray inspection of duplicate instances, dismissal over card/Close/gameplay buttons, overlay transitions, long authored descriptions at 443x912 and 540x960, and the full owned-charms layout. Rendered popup captures were inspected. Evidence is in ignored `Docs/work/charm-popup-97727dfdeb924537a0cde496266bd4d6`; Unity exited with code 0. No Android rebuild was made for this change.

October 16 final-offer transition: confirming the final unlock dismisses its offer and surrounding HUD before the farewell starts, preventing Skip farewell from overlapping the offer or Hand Ranks controls. All three victory PlayMode checks passed; a rendered farewell capture was inspected. Evidence is in ignored `Docs/work/final-offer-transition-5a7bb0769f9a45eb85285d50d31376ea`; Unity exited with code 0. This fix is newer than the Android APK below.

October 16 immediate intro buttons: 2/2 focused PlayMode checks passed for rapid pointer navigation through the fox intro, accepting during animation with one opening roll, and cancelling an automatic owl beat without a second advance. Evidence is in ignored `Docs/work/intro-buttons-ec3072e9b6bd46b3a05ea32deaef1349`; Unity exited with code 0. No Android rebuild was made for this change.

October 16 Android build: requested ARM64 IL2CPP development APK built with Unity 6000.6.5f1, including the chapter sequence and narrative/detail presentation changes. APK: `Docs/work/android-build-f30885fe143e41e7916013afae86d672/SpiritDice.apk` (121,333,251 bytes), package `net.ridiculousgaming.diceprototype`, version 1.0/code 1, min SDK 26, target SDK 36. APK signature verified; Unity exited with code 0, no errors, and three `SpiritDiceSurface.shader` pow warnings across graphics backends. Installed with adb install -r on the connected SM_S947U, preserving app data; package update time verified and game process launched. Phone was locked, so its gameplay screen was not visually verified. Startup logged a missing AssetPackManager class; the process remained running. Build-generated entry ordering changes in `Dice Modifiers.asset` and `Spirit House Modifier Presentation.asset` were retained.

October 16 invitation narration: 4/4 focused rendered checks passed, covering all three encounter invitations at 443x912, 443x960, and 540x960, multiple-rule layouts, ordinary dialogue transitions, and the existing charm gift flow. Captures were visually inspected. Evidence is in ignored `Docs/work/invitation-narration-e484127a4d1047edb6c79b62f580aba1`. Unity exited successfully. No Android build was made.

October 16 narrative charm presentation: 10/10 rendered journey checks passed, followed by 2/2 focused checks after removing variant suffixes from the spoken charm names. All three gift panels were captured at 443 and 540 pixels wide; inspected captures show narration above a separate charm detail block. Evidence is in ignored `Docs/work/narrative-charms-09ba394bca7244f493dd5152299997b4` and `narrative-charms-final-cd94a6ccc29c4d85912b87972bae1151`. Unity exited successfully. No Android build was made.

October 16 chapter validation: 1,464/1,464 full EditMode tests passed. Rendered chapter, journey, collection, and ending checks cover 23 distinct PlayMode cases, all passing on their latest results. The first rendered run passed 22/23; the remaining assertion assumed enough unlocked leads to align the next card at the viewport top. It now checks the content bottom when scrolling is exhausted, and the focused rerun passed 3/3. Portrait captures at 443 and 540 pixels wide were inspected. Evidence is retained in ignored `Docs/work/chapter-one-edit-fea4c7c37fd34584b34d59d0d8d36226`, `chapter-one-play-355f7f3d2c864290a2903cc7a8d1752b`, and `chapter-one-final-998b3243e55048d5a666c6348f6ea861`. Successful runs exited with code 0. No Android build was made.

October 16 family discovery validation: 1,435/1,435 full EditMode tests passed, then 37/37 focused journey/progression tests passed after the final active-run grant refresh. Rendered Collection/unlock/ending checks passed 15/15, with 443/540 portrait captures inspected. Evidence is retained in ignored `Docs/work/family-discovery-edit-86f162ed97c84a449cb66de5936c4dc5`, `family-discovery-final-da109d41175748df9b76d47b55f61d22`, and `family-discovery-play-c663f5fa913a4f5cb8c2a929d19fa143`. All Unity runs exited successfully. No Android build was made.

October 16 collection validation: full EditMode run followed by targeted repairs/reruns leaves 1,394 distinct EditMode cases and 22 exercised PlayMode cases passing on their latest results. This includes unlock persistence/save-failure recovery, exhausted lists, endings, Addressables, and collection/intro portrait layouts at 443 and 540 pixels wide. Final layout evidence is in ignored `Docs/work/collection-layout-93f0d098975547938faf9537ae8dec93`; focused EditMode evidence is in `Docs/work/collection-edit-final-2fd1bc85841c4426a5564be5e99967d4`. No Android build was made.

- Follow [Tools/README.md](../Tools/README.md). Ask John to save and close Dice-Unity before CLI tests if open. Leave other projects alone.
- Launch Unity with `require_escalated` for licensing access, hidden, and track its PID. Never run two editors against this project. Verify results XML and process exit.
- Rendered checks need graphics; omit `-batchmode` for `WaitForEndOfFrame` tests. Use isolated test profiles, never the player's saves.
- Tests are in `Tests/EditMode` and `Tests/PlayMode`. Check affected behavior and portrait layout as needed. Historical results do not validate current edits.
- Generated output belongs in ignored `Docs/work`; durable source/assets belong in the repository. Unexpected output still requires authorization before deletion.
- Build/install Android only when requested. Check the actual device; do not assume its installed app matches current source.

For reference mechanics, use [the extracted design](outputs/dice-of-kalma-design-reference.html) and [reference inventory](Reference/README.md). Historical proposals are background, not current requirements or authorization. Inspect code before relying on their implementation claims.
