# Dice modifier implementation

The engine resolves mechanical modifiers during initial rolls, player rerolls, and played hands. The Standard catalog contains 74 families and 296 definitions: 292 functional variants and four inert variants with reward eligibility disabled. The current catalog version is `standard.rewards.v1`; the engine rules version is 3.

## Content and presentation

Definitions contain stable mechanical IDs, family IDs, phase, a typed condition with parameters, effect operations and amounts, activation limits, and reward eligibility. They contain no display names, descriptions, localization keys, colors, artwork, audio, or lore. Source names and asset provenance are retained only in the research mapping under `Docs/Reference/Modifiers`.

Each modifier variant is authored in its own `ModifierAsset` ScriptableObject under `Assets/Dice/Content/Mechanical/Modifiers`. All 296 assets belong to the local `Dice Modifiers` Addressables group and carry the `dice.modifiers` label. `ModifierAssetLoader` discovers them asynchronously by label at startup; `MechanicalCatalogAsset.Build` takes that collection, orders it by stable ID, and builds immutable engine definitions. The catalog asset retains hands, encounters, and reward pools; it does not embed modifier data or an inventory of modifier assets.

Each spirit-house presentation is a `ModifierPresentationAsset` under `Assets/Dice/Content/Presentation/SpiritHouse/Modifiers`. Its reference points to the mechanical asset; the mechanical asset has no presentation reference. The local `Spirit House Modifier Presentation` group uses the `dice.theme.spirit-house.modifiers` label. Entries contain the display name, optional sprite or authored artwork selection, and optional condition wording. Parameter tokens read the live condition values, while effects and timing are formatted from the engine definition. Missing custom wording uses the mechanical diagnostic description. Existing provisional family sigils remain available for entries without artwork.

The spirit controller loads mechanics and the selected presentation label before creating the encounter. Theme installation validates full coverage, unique IDs, names, and mechanical references, then publishes one ID-to-presentation lookup. Handles stay owned until controller destruction, and a stale controller cannot clear a newer theme. The diagnostic controller loads only mechanics. Neither runtime path uses `AssetDatabase` or `Resources` to discover modifiers. The engine retains no Unity or Addressables dependency.

To add content, create a modifier asset and its presentation asset with Unity's `Create > Dice` menus, then assign their groups and labels. `Dice > Configure Modifier Addressables` registers assets in the standard content folders without replacing authored values. The Android build also configures these groups and builds local Addressables content with the player. Additional themes use their own group and label, selected by the spirit controller's `Modifier Presentation Label` field.

An `EncounterDefinition` can specify ordered starting modifier IDs, including duplicate copies. Each owned copy receives its own encounter-local instance ID and activation counters. Standard starts with an empty inventory and adds modifiers through reward selection.

## Condition objects

`ModifierDefinition.Condition` holds an `IModifierCondition`. The resolver calls its `Evaluate(in ModifierContext)` method after checking the phase and instance activation limits. Each built-in condition implements that interface directly in its own sealed class under `Assets/Dice/Engine/Conditions`, including `ThreeSmallestSumAtMostMaximumCondition`, `SelectedFaceUnchangedCondition`, and `SumDivisibleByCondition`. Each implementation reads only the context values needed by its predicate. Shared calculations and context validation are ordinary static functions in `ModifierConditionHelpers`; there is no condition base class.

Each modifier asset holds one condition config in a `[SerializeReference]` field: an instance of a `ModifierConditionConfig` subclass under `Assets/Dice/UnityContent/Conditions`, one per built-in condition, chosen in the inspector from the classes the field allows. A config owns its condition's authored parameters and their defaults, builds its engine condition when the catalog is built, and exposes its numbers as named text values for the charm's condition wording. Parameterized conditions validate and own only the values they use, exposed through get-only parameter properties. Parameterless conditions carry no configuration.

Custom engine content can supply any `IModifierCondition` directly to `ModifierDefinition`; a new built-in condition is a condition class and its config class. Conditions must be immutable and deterministic: they can be shared by multiple modifiers and sessions and evaluated during preview. Per-instance activation state belongs to the session, not the condition object.

Each effect is its own `ModifierEffect` subclass (`Assets/Dice/Engine/ModifierEffects.cs`) that applies its amount to the totals of an activation; modifier assets author effects as `ModifierEffectConfig` instances that build them. Condition objects decide whether the modifier activates. The resolver collects contributions and applies them in the established order; save restore checks contributions against the same totals. Each effect names the kind the skin words it by. Presentation remains outside the engine.

Save data references stable modifier definition IDs and stores instance state. It does not serialize condition objects or CLR type names. Restore obtains condition objects from the supplied catalog without reevaluating past activations.

## Resolution contract

The resolver evaluates all conditions against one immutable phase context and then applies eligible effects in owned-instance order. A reroll grant cannot change another condition's eligibility during the same phase. Effects add base score, multiplier, or rerolls. Each activation and resource grant carries its source definition and instance ID.

Starting a hand clears its score contributions and per-hand activation counts before initial-roll effects resolve. Contributions from initial rolls and rerolls persist until that hand is played. Starting a stage resets stage counters. Every reference definition uses the verified per-instance, once-per-hand limit. The native final-reroll condition is effectively once per stage because it can activate only on the last hand. Explicit unlimited and once-per-stage limits are available for custom content.

Player rerolls spend their action before evaluating modifiers. Playing spends its hand before evaluating modifiers. Five reference predicates explicitly use the pre-play hand count: rerolls exceeding hands, largest matching group equaling hands or rerolls, minimum/maximum equaling hands, endpoint sum equaling hands or rerolls, and even hands. Those conditions add the spent hand back to the context count.

All arithmetic and resolution happen on a private state copy. Rejected actions and failed arithmetic leave the committed state, instance counters, and future random stream intact.

## Preview and inspection

`PreviewPlayHand()` returns the score breakdown from the same resolver used for a committed play. `PreviewHandResolution()` additionally exposes projected remaining rerolls, modifier instance counters, and current-play events. Neither commits state nor draws the next hand or reward.

Snapshots expose owned modifiers, activation counters, accumulated base score and multiplier, and immutable contributions. Score breakdowns include contributions from all phases of the current hand. These are mechanical facts for a presenter to render.

`InspectModifiersForPlayHand()` reports each instance's eligibility, condition result, and activation-limit status without applying effects or advancing random streams. It shares the resolver's context construction and activation policy. PlayHand conditions see the remaining hand count after spending the proposed hand. Other phases have no predicted condition result; outside the Playing phase, inspection reports that the encounter is not playing.

## Playable diagnostic scene

In Unity, choose **Dice > Open Diagnostic Scene**, then press **Play**. The scene at `Assets/Scenes/DiagnosticEncounter.unity` is also the first enabled scene in Build Settings. Its controller references the Standard mechanical catalog and starts a 25-stage encounter with seed 60.

Select dice to reroll; selected dice display REROLL, and unselected dice display KEEP. Play Hand submits all five current dice. The display includes resources, stage target, total score, category upgrades, a score preview, reward choices and refreshes, owned modifier conditions/effects, activation counters, and per-instance score contributions. The event log retains the latest 160 events and displays the newest 60, including complete scored-hand breakdowns. The page scrolls; a wide Game view places inventory and events beside the controls.

Save and Load use the existing Newtonsoft JSON codec and file store. The current path is displayed in the scene and defaults to `Application.persistentDataPath/diagnostic-encounter.json`. Loading restores the committed mechanical state and clears selection and presentation history. The New run seed field is a suggestion for starting another run, not a recovered seed: saves preserve random-stream states, not the original seed. Start new and Abandon operate on the current in-memory run; Save is explicit.

`DiagnosticEncounterController` in `Dice.App` owns the application instance, commands, preview cache, event history, and file operations. `DiagnosticEncounterView`, `DiagnosticText`, and `IDiagnosticEncounterControls` in `Dice.Presentation` own the IMGUI surface and descriptive text. Mechanical definitions still contain no labels, colors, descriptions, or Unity objects. The diagnostic presentation can be replaced without changing the engine or save format.

## Reference fidelity

Native code inspection verified phase timing, frozen resource sampling, inventory-slot activation identity, accumulator lifetime, initial predicates, ordering with ties, absolute differences, and several conditions whose internal enum names disagree with their descriptions. The family mapping distinguishes native-verified branches from conditions supported by matching extracted enum/text. Effect amounts and activation phases are taken directly from all 296 extracted assets.

Two explicit initialization choices matter. Previous-play rank comparisons begin at High Die, matching the reference's initialized comparison hand. An initial-roll rank comparison in a custom encounter with starting modifiers and no previous played hand returns false. The reference compares against an earlier random initialization hand in that case; ordinary Standard starts with no modifiers, so that difference is not encountered there. The engine does not reproduce the reference's complete random initialization or challenge-specific branches.

## Validation and remaining work

The Edit Mode suite covers all 7,776 ordered hands, predicate boundaries, all 296 catalog variants, resource timing, duplicate instances, activation resets, preview parity, rejected actions, and transactional overflow. Final results are recorded alongside this report's validation artifacts in `Docs/work`.

Reward selection, hand-upgrade selection, JSON persistence, and the diagnostic Unity scene are implemented. The reward and save/resume reports document the underlying contracts.

Diagnostic scene validation on October 7, 2026: Unity 6000.6.2f1 passed 852 EditMode tests and one rendered PlayMode integration test. The diagnostic controller completes the existing 25-stage victory trace with score 80,561 and matches the headless engine after every action; another test reaches a resource-exhaustion loss. The PlayMode test loads the authored scene, submits dice actions and both reward types, inspects an acquired modifier, saves/resumes, abandons, and starts another run. Five rendered states were visually inspected. Final graphics-enabled compilation reported no C# warnings or errors. Preview/inspection failures are isolated from successful engine synchronization, with overflow and custom-condition failure coverage.

Results and screenshots are under `Docs/work/diagnostic-validation`: `editmode-3.xml`, `playmode-2.xml`, their logs, and `captures-2`. Earlier attempts are retained: assembly-reference and type-name compilation issues were fixed, a deprecated test lookup was replaced, and screenshot capture required a normal graphics-enabled editor instead of batch mode. Source/configuration hashes matched across the successful validation runs. The earlier batch PlayMode attempt generated `ProjectSettings/SceneTemplateSettings.json`; that file was retained untouched. Diagnostic save fixtures remain under `Docs/work/diagnostic-file-tests`.

Initial modifier validation: Unity 6000.6.2f1, 517 Edit Mode tests passed, zero failed or skipped. Results: `Docs/work/modifier-editmode-2.xml`. No source, package, or project-settings files changed during that validation run.

Condition-object refactor validation on October 7, 2026: Unity 6000.6.2f1 passed all 662 EditMode tests, with zero failures or skipped tests, no compiler warnings or errors, and exit code 0. Tests cover custom conditions without enum registration, phase and activation-limit skips, stable phase resource sampling, preview parity, and save/restore without reevaluation. The existing full Standard encounter and JSON persistence tests passed. All 291 files under Assets, Packages, and ProjectSettings matched their pre-validation hashes.

An offline differential harness compared the condition implementations against the evaluator from commit `f989cd6`: all 13,810,176 evaluations matched across 7,776 ordered hands, eight resource/history/mask contexts, and three parameter sets for each of the 74 condition kinds. Validation logs, file hashes, the summary, and Unity test results are retained under `Docs/work/condition-refactor-validation`; the temporary harness and baseline source have been removed. Permanent regression tests remain in the Unity test assembly. The JSON file-store test retained its save and backup under `Docs/work/save-resume-file-tests/ce743bc79ad1473d9e3f5f290ff25c04`.

Companion helper validation on October 7, 2026: all 74 conditions implement `IModifierCondition` directly, with 19 owning parameters and 55 stateless. Normalized source hashes verify that every evaluation body preserves its predicate, allowing only the helper-call qualification, parameter-access, and method declaration changes. Unity 6000.6.2f1 passed all 745 EditMode tests with no failures, skipped tests, or compiler warnings/errors and exit code 0. Coverage includes direct constructor defaults, zero normalization, valid/invalid bounds, 44 nondefault behavior witnesses, and evaluation of imported catalog variants against the independent boundary witnesses. All 291 source/configuration files matched their pre-test hashes. Results are retained as `composition-editmode-1.xml`, `composition-editmode-1.log`, and `composition-validation.json` under `Docs/work/condition-refactor-validation`. File-store test saves remain under `Docs/work/save-resume-file-tests/cbc78c68dc6e492eb8425ee2c37edd5b`.
