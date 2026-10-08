# Dice Engine Architecture Proposal

Draft for review by John Price. October 6, 2026.

Build a portable, deterministic C# engine for the reference game's Standard encounter. Keep all mechanical definitions independent of names, descriptions, artwork, color, audio, animation, dialogue, and lore. Unity supplies authoring and presentation adapters. A surrounding game selects encounters and handles their results.

This proposal establishes implementation boundaries. The reference remains the mechanical baseline; the animal spirit setting will be a presentation and campaign layer added afterward. Implementation begins after review of this proposal.

## Project and reference baseline

The project is `Dice-Unity`, using Unity 6000.6.2f1 and the Universal 2D template. Its Assets directory currently contains the template scenes, renderer settings, input actions, and welcome code. There is no gameplay assembly to integrate with. URP 17.6.0 and Unity Test Framework 1.8.0 are already present.

The reference inventory contains 74 modifier families and 296 variants, including one no-op family with four variants. There are eight active hand categories. The first milestone implements the 25-stage Standard encounter and the functional modifier catalog, with the no-op definitions retained for future challenge support.

Mechanical terminology will be `Modifier`, `BaseScore`, `Multiplier`, `HandScore`, `Stage`, and `Encounter`. An encounter is one complete run through the configured stages. The original Bones/Mult labels and skull imagery are presentation vocabulary. These are proposed names for new code; existing project identifiers remain unchanged.

## Assembly boundaries

Start with four production assemblies. Mechanical content definitions live in the engine; their Unity authoring assets live in a separate adapter assembly. Content and progression are separate responsibilities, not one subsystem.

| Assembly | Responsibilities | Project assembly references |
| --- | --- | --- |
| `Dice.Engine` | Mechanical definition types, encounter state, actions, validation, resolution, deterministic randomness, snapshots, save data, and result/event contracts | None |
| `Dice.UnityContent` | ScriptableObjects containing mechanical data; validation and conversion to immutable engine definitions | `Dice.Engine` |
| `Dice.Presentation` | Diagnostic UI initially; later host visuals, effects, names, descriptions, localization, audio, and narrative responses | `Dice.Engine` |
| `Dice.App` | Composition root, catalog loading, session ownership, command routing, persistence, encounter selection, and later campaign progression | The other three assemblies |

`Dice.Engine` has no Unity engine references. It contains no MonoBehaviours, ScriptableObjects, Unity serialization attributes, scene references, global random calls, clock reads, file access, or presentation callbacks. Test it in Unity Edit Mode while keeping the source usable from an ordinary C# host. No separate package distribution is necessary for the first milestone.

`Dice.Presentation` reads engine contracts and sends typed action requests through an application-supplied callback. It never holds a mutable engine state or calls a rules implementation directly. `Dice.App` connects the presentation to a session. Editor-only authoring tooling and tests receive their own assemblies when introduced.

```text
Dice.App
  -> Dice.UnityContent -> Dice.Engine
  -> Dice.Presentation -> Dice.Engine
  -> Dice.Engine

Dice.Engine never references the other assemblies.
Dice.UnityContent never references Dice.Presentation.
```

Proposed source location: `Assets/Dice/{Engine,UnityContent,Presentation,App}`. Mechanical and presentation assets use separate directories and schemas. The first milestone does not reorganize or remove the template assets.

## Mechanical content has no presentation fields

The engine consumes an immutable `MechanicalCatalog`, independent of how it was authored. A Unity host loads ScriptableObjects and converts them once; another host can construct the same definitions from JSON or code.

| Definition | Mechanical data |
| --- | --- |
| `HandDefinition` | Stable ID, rank, base score, base multiplier, upgrade increments, maximum upgrade count |
| `ModifierDefinition` | Stable definition and family IDs, activation phase, typed predicate and parameters, effects and amounts, activation limits |
| `RewardPolicyDefinition` | Eligible definition IDs, offer composition, selection policy, refresh cost, duplicate/exclusion rules, recent-choice history rules |
| `StageDefinition` | Threshold, starting hands, starting rerolls, reward policy reference |
| `EncounterDefinition` | Stable ID, ordered stages, starting inventory, starting upgrades, starting offer-refresh currency, enabled rule adjustments, completion conditions |

None of these includes a display name, descriptive sentence, localization key, icon, color, audio cue, animation identifier, host, house, or narrative reference. A Unity asset's editor label is authoring metadata and is never used as its runtime identity.

Use opaque or mechanically descriptive stable IDs, such as `modifier.play_without_reroll.base_score`. A family groups related variants when rules need that relationship; an owned instance has a separate identity for activation history and event attribution. Display order must not silently become resolution order.

The reference's four colors translate to mechanical effect variants: base score, multiplier, combined base score and multiplier, and restored rerolls. Reward policies select those variants by mechanical IDs or effect roles. They do not select the color blue or yellow. Each variant retains its actual extracted numbers; there is no universal numerical conversion between variants.

For example, the reference Blind family includes a play-without-rerolling variant that adds 10 base score. Its engine definition describes that condition and effect. A separate presentation record can call it Bear's Patience, assign artwork, and describe it to the player. A different presentation catalog can use another name and image without changing the mechanical catalog or save.

Descriptions generated from parameters still belong to presentation. Presentation formats structured rule data into text; the engine never returns a player-facing explanation string. Missing presentation mappings fall back to neutral IDs and mechanical values in the diagnostic UI.

### Importing the reference

The extracted JSON is research material, not the runtime catalog. It intentionally mixes current localized wording, source IDs, color labels, and mechanical values. Create an explicit mapping from each source variant to a neutral definition, preserving provenance in a separate development document or tooling file under Docs.

Do not use the reference's internal trigger number as the identity of a modifier. Scholar and Merchant share trigger ID 16 but activate in different phases and have different values. Some trigger enum names also disagree with current condition text. Predicates require an explicit, reviewed mapping, not parsing display prose at runtime.

## Encounter state and ownership

`EncounterSession` owns all mutable mechanical state. Callers receive immutable snapshots or copies that cannot mutate the session. Mechanical definitions are shared and immutable; owned modifier instances and their activation counters belong to the session.

| Scope | Required state |
| --- | --- |
| Encounter | Encounter ID, rules/catalog version, phase, stage index, inventory instances, hand upgrades, reward-refresh currency, aggregate statistics, random state, action revision |
| Stage | Initial and remaining threshold, initial hand/reroll budgets, remaining hands/rerolls, stage activation limits, hands played, prior-stage starting and unused hand counts |
| Hand | Ordered current dice, initial dice, dice before the latest reroll, latest selected mask, ever-rerolled mask, latest changed mask, reroll actions spent, accumulated base score/multiplier, hand activation limits |
| History | Previous played hand/category, latest pre/post-reroll categories, recent upgrade choices, reward exclusion history |
| Pending reward | Offer ID, offered definition IDs, reward-selection phase, refresh eligibility and cost |

Selected dice and dice whose values actually changed are different facts. Both must be available to predicates. Dice positions remain fixed during ordinary play; category evaluation may sort a copy but cannot reorder authoritative dice.

The reference has conditions concerning the previous stage, the previous played hand, and the middle or last hand of the current stage. Those require explicit history and original stage budgets. Do not reconstruct them from remaining resources after effects have changed those resources.

UI selection highlights, hover state, camera state, animation progress, and dialogue position are presentation state. A selected-dice mask becomes mechanical input only when a reroll is submitted.

### Phases

The externally actionable phases are `Playing`, `ChoosingModifier`, `ChoosingHandUpgrade`, and `Completed`. Completion has an outcome such as Won, Lost, or Abandoned. Automatic transitions between those phases resolve synchronously.

An accepted action advances to the next point where a player decision is required. A separate Continue button can delay presentation of the next state without inventing a mechanical animation-complete action. Exact reference reward transition timing is an implementation verification item below.

## Player actions and session API

The proposed public surface is deliberately small. These are contract names, not implemented signatures.

| Operation | Inputs and result |
| --- | --- |
| `CreateEncounter` | Catalog, encounter ID, seed, supported starting configuration; returns a session and its initial resolution |
| `Execute` | Typed action plus expected state revision; returns acceptance/rejection, new snapshot, and ordered rule events |
| `PreviewPlayHand` | Current state; returns the current hand's score breakdown and projected effects without committing them |
| `CaptureSave` | Returns complete portable mechanical save data |
| `RestoreEncounter` | Validated catalog and save data; returns a session at the saved decision boundary |

Actions are `RerollDice(mask)`, `PlayHand`, `RefreshModifierOffer(offerId)`, `SelectModifier(offerId, optionId)`, `SelectHandUpgrade(offerId, optionId)`, and `AbandonEncounter`. An offer selection uses a stable option identity rather than a visual slot index. The engine checks phase, resource availability, offer identity, and action revision before acceptance.

Rejected actions return structured rejection codes and arguments. They do not consume randomness, change state, spend resources, or emit gameplay events. Presentation translates the code into text if needed.

Accepted actions resolve transactionally against working state, then commit the result. No subscriber can modify resolution through an event callback. A programming or invalid-content failure must not leave a partially applied command exposed as the next playable state.

## Rule resolution

Keep hand evaluation, condition evaluation, effect application, reward generation, and encounter transitions as focused engine components. They operate on the same session state and mechanical definitions. Use typed built-in predicates and small parameter records for the extracted conditions; a general scripting language or node editor is unnecessary.

Initial effect operations are AddBaseScore, AddMultiplier, and AddRerolls. The combined variant applies two effects from one modifier activation. Activation timing and per-hand/per-stage limits are explicit data. Each event carries its source definition and owned instance ID.

The score formula remains:

```text
BaseScore = sum of dice + category base + category upgrades + modifier contributions
Multiplier = category multiplier + multiplier upgrades + modifier contributions
HandScore = BaseScore * Multiplier
```

The base category multiplier already includes the initial multiplier; there is no additional universal +1. Use integer arithmetic and propose 64-bit score totals and thresholds, with checked intermediate arithmetic. This is an implementation choice to avoid accidental overflow, not a claim about the reference's integer widths. Invalid mechanical data is rejected before an encounter starts.

The eight active categories retain these base values:

| Category | Base score | Multiplier | Each upgrade |
| --- | ---: | ---: | --- |
| High Die | 5 | 1 | +5 base score, +1 multiplier |
| Pair | 5 | 2 | +5 base score, +1 multiplier |
| Two Pair | 10 | 3 | +5 base score, +1 multiplier |
| Three of a Kind | 15 | 4 | +5 base score, +1 multiplier |
| Full House | 20 | 6 | +5 base score, +1 multiplier |
| Four of a Kind | 25 | 8 | +5 base score, +1 multiplier |
| Straight | 25 | 10 | +5 base score, +1 multiplier |
| Five of a Kind | 30 | 14 | +5 base score, +2 multiplier |

Store upgrade counts from zero through six. Presentation can display levels one through seven. Flush and Flush House remain outside the active classifier.

### Resolution boundaries

1. Starting a stage establishes its budgets and resets stage limits. Starting a hand resets hand limits and accumulators, captures the initial dice, and resolves initial-roll activations.
2. Rerolling records the selected dice, spends one action, produces the new faces, updates history, and resolves reroll activations.
3. Playing evaluates the best category, its upgrades, and eligible play-hand effects. It produces a score breakdown, applies the score to the threshold, accounts for the spent hand, and resolves clear/loss/continuation.
4. Clearing the final Standard stage completes the encounter. An earlier clear enters the configured reward sequence; finishing that sequence starts the next stage.

This describes the boundaries, not a settled answer to every ordering detail. The exact resource values visible inside a phase and the ordering of multiple activations must follow the reference where verified. Keep those decisions inside the resolver, with tests that make the chosen semantics explicit.

For example, a no-rerolls condition can interact with a modifier that grants rerolls. Evaluating every predicate against one frozen snapshot can differ from evaluating against state changed by earlier effects. Similarly, checking hands remaining before or after spending a hand changes several predicates. Neither behavior should arise accidentally from collection iteration order.

### Preview uses the scoring resolver

Preview evaluates the same current-hand resolution code against temporary state. It never commits activation limits, advances authoritative random streams, generates the next hand or reward offer, or emits live presentation events. Preview returns only information about the hand being considered, so it cannot expose future random outcomes.

The committed PlayHand result must match the preview when no intervening action occurs. Keep the current-hand calculation distinct from subsequent automatic transitions to make that boundary explicit.

## Presentation contract

Each accepted action returns one ordered resolution batch with the previous and new state revisions, a final snapshot, and typed events. Event payloads include the values at that point in resolution; they do not require presentation to reconstruct intermediate values from the final state.

| Event family | Required information |
| --- | --- |
| StageStarted and HandStarted | Stage/hand identity and starting resources |
| DiceRolled | Initial or player reroll, affected positions, before/after faces |
| ModifierActivated | Definition and instance IDs, activation phase, affected dice where applicable |
| ResourceChanged | Resource kind, before/after values, amount, and mechanical cause |
| HandScored | Category, dice sum, category/upgraded/modifier contributions, final base score, multiplier, and hand score |
| ThresholdChanged and StageCleared | Threshold before/after, applied score, and stage identity |
| RewardOffered and RewardSelected | Offer/option IDs and acquired modifier or upgrade |
| EncounterCompleted | Outcome, final stage, and mechanical statistics |

The contract contains no animation names, display durations, dialogue keys, emotional reactions, scene objects, or sound cues. Numeric cause codes and stable mechanical IDs are sufficient for attribution.

The presenter starts from its previously displayed state and plays through the batch while the engine already owns the final state. It can animate normally, accelerate, skip directly to the final snapshot, or render only text. The application normally waits until the presenter is ready before accepting another user action; the engine itself never waits for presentation.

An initial roll for the next hand may be in the same batch as the preceding hand's result. Event payloads therefore carry their stage and hand identities, not just the latest session values.

Narrative reacts to those facts outside the engine. StageCleared can make snow melt from a window; EncounterCompleted can resolve a haunting. Changing the presentation catalog or omitting the presenter entirely must leave the mechanical result identical.

## Randomness and persistence

Use an explicitly versioned integer random generator with serializable state. A seed alone is insufficient to resume a partially played encounter. Separate dice and reward streams so changes to offer generation do not silently change subsequent dice. Presentation owns any cosmetic randomness and cannot consume either mechanical stream.

This guarantees reproducibility within our versioned rules and catalog. It does not promise the original game's rolls for the same numerical seed. The reference contains a LuckAssist path whose exact behavior is unresolved; a uniform distribution is a proposed explicit deviation if that path is not adopted.

Save at committed decision boundaries. The save contains catalog/rules versions, encounter identity, all state/history described above, modifier instance counters, pending offers, exclusion history, state revision, and complete random stream state. Restoring must preserve the existing offer rather than generate another one.

Persistence encoding and file I/O belong to `Dice.App`; the engine owns the portable save schema and restoration validation. Save schema, mechanical catalog, and rule behavior have separate versions. Unsupported combinations return an explicit compatibility result instead of silently reinterpreting a run. A presentation-only change does not invalidate a mechanical save.

Save the committed state even if its animation has not finished. The initial implementation resumes at that final snapshot rather than replaying a partly shown animation. Action logs are useful for reproduction but are not required to reconstruct every save; this is not an event-sourced persistence architecture.

## Progression outside an encounter

The engine can run one encounter without a player profile or campaign. For the first milestone, `Dice.App` launches the Standard encounter directly and records the result.

Later progression code owns available encounter IDs, unlock conditions, persistent rewards, daily selection, and profile statistics. Keep its mechanical records separate from journal text, trophy art, host identities, house scenes, and story beats. A campaign route definition can connect mechanical encounter IDs; a presentation mapping supplies the hosts and locations shown on that route.

A spirit-house game can choose an encounter from a travel map. An RPG can choose the same encounter from an NPC interaction. Both supply starting configuration and receive EncounterCompleted. The external application grants any persistent reward once, using a stable encounter-instance identity to avoid granting it again after loading the same completed run.

Host-specific mechanical variations eventually become encounter data and typed rule adjustments. The engine should never branch on Fox, Bear, a house name, or a narrative case ID. Do not build a campaign framework before the encounter baseline works.

## First playable milestone

Deliver one complete Standard encounter through a diagnostic Unity scene. This milestone includes the functional catalog, not merely a few example modifiers; development can introduce families incrementally on the way there.

The diagnostic UI provides five ordered dice, dice selection, reroll and play actions, remaining resources and threshold, hand ranks/upgrades, current inventory, score preview/breakdown, reward refresh and selection, a result screen, and save/resume. A seed field and an event inspector support reproduction. Labels and colors live in the diagnostic presenter.

The Standard definition uses the first 25 entries of `easyPileThresholds`, beginning at 160 and ending at 7,725. Starting hands/rerolls are 3/3 for stages 1-10, 4/4 for 11-20, and 5/5 for 21-25. The separately named `pileThresholds` array belongs to the harder challenge and must not accidentally become the baseline.

Acceptance criteria:

- Complete a 25-stage victory and a resource-exhaustion loss through legal actions, including the actual reward sequence.
- Cover all 73 functional modifier families and their 292 effect variants, retaining the four no-op variants as explicit definitions with controlled eligibility.
- Verify every predicate with meaningful positive/negative and boundary examples; specifically cover resource-sensitive ordering, history, positions, and activation limits.
- Exhaustively classify all 7,776 ordered five-die outcomes against an independently expressed category oracle. Position changes preserve category while remaining visible to positional predicates.
- Confirm base and upgraded scoring examples, including the reference Full House example: 2,2,2,5,5 scores 216 at base and 287 after one upgrade, without modifiers.
- Preview matches the committed hand calculation and leaves state/randomness untouched. Rejected actions do the same.
- A fixed catalog, seed, and action sequence reproduces state and events. Save/resume at play and both reward phases matches uninterrupted execution, including future rolls/offers.
- Swapping between two minimal presentation mappings changes labels/styles only. Headless execution and the diagnostic scene produce the same mechanical results.

Use Edit Mode tests for rules and small Play Mode checks for application wiring. No art production, spirit dialogue, campaign map, achievements, monetization, second chances, alternate reroll abilities, Cursed mode, Daily, Endless, or Challenge implementation is required for this first milestone. Their later addition should not require presentation dependencies in the engine.

## Reference questions to resolve during implementation

These are bounded fidelity questions, not a request to redesign the baseline. Inspect the existing native evidence first. Where that cannot establish behavior, record an explicit proposed rule and get agreement before calling it reference-equivalent.

| Question | Why it matters | When to settle it |
| --- | --- | --- |
| Activation order and resource sampling | Restored rerolls can affect another predicate; spent hands affect hands-left tests | Before the modifier resolver is finalized |
| Limit identity and reset timing | Once-per-hand or once-per-stage may apply by owned instance, definition, or shared trigger; same-family variants matter | Before importing the complete catalog |
| Predicate details | Strict/non-strict ordering, signed/absolute differences, adjacency endpoints, and selected-vs-changed dice can differ from prose | Alongside each predicate implementation |
| Initial history and cross-stage reset | Previous-hand and previous-stage predicates need defined behavior on the first hand/stage | During session initialization |
| Reward sequencing and eligibility | Verify which depth drives the four-offer cycle, startup inventory, duplicate rules, exclusions, and pool-exhaustion fallback | Before the first complete run |
| Reward refresh economy | The inspected path suggests one starting refresh and one per clear; confirm ordinary play and cost/carry behavior | During reward implementation |
| Luck assistance | Uniform dice may intentionally differ from an assisted first-win path | Before baseline balance is treated as representative |
| Aggregate score bookkeeping | Threshold reduction, overkill, and run-score totals need distinct definitions | During scoring and result statistics |

Offer color cadence, upgrade-history suppression, and much of the scoring are already decoded. The questions above concern remaining edge behavior; they do not invalidate the extracted values or justify replacing them with invented balance.

## Implementation order after review

1. Establish the assemblies, pure mechanical definitions, state, hand evaluator, and unmodified scoring. Run a deterministic encounter without a UI.
2. Implement the shared resolver, history, activation limits, and all modifier families, verifying the fidelity questions as they arise.
3. Implement the Standard stage/reward flow, save/resume, and replay checks.
4. Connect the diagnostic scene, presentation mappings, and event inspection. Meet the complete-run acceptance criteria before adding the first spirit-house presentation.

The review decision is whether to adopt these boundaries: a pure encounter engine, mechanical authoring with no presentation fields, an ordered result contract, and an application layer that owns progression and persistence. No gameplay implementation accompanies this proposal.

## Spirit-house prototype implementation

The first playable presentation is `Dice-Unity/Assets/Scenes/SpiritHousePrototype.unity`. Open it through **Dice > Open Spirit House Prototype**, then enter Play Mode. A portrait Game view is recommended; wider views center the portrait table. Tap dice to select them for reroll, play hands, choose the free charm and hand upgrade after clearing a stage, and use **Charms** for owned modifier details. **Hand Ranks**, beside Charms, lists each category's upgrade level and effective Base/Mult bonuses, with the current category highlighted. These values include upgrades and exclude dice and modifier contributions. The menu exposes JSON save/load, restart, and abandon.

`SpiritEncounterController` owns the existing `EncounterApplication` and exposes `ISpiritEncounterControls`. `SpiritEncounterView` consumes committed snapshots and ordered resolution events, blocking commands until their animations finish. Loading a save synchronizes directly and does not invent or replay a previous hand's score. `SpiritDiceTray` animates real meshes toward engine-provided faces; cosmetic motion never consumes engine randomness.

The dice have irregular rounded bevels, recessed pips, and a generated aged-bone surface texture. A perspective camera frames five deep felt-lined wooden bays with thick worn rails, raised dividers, brass trim, cast shadows, and contact shadows. The wood and bone textures live in `Presentation/Resources`; generation prompts are retained in `Docs/outputs/spirit-house-prototype-assets/dice-material-prompts.json`. Selected dice receive layered amber outlines and a REROLL plate anchored to their projected slot. The visible top faces remain the engine's committed results; camera-relative face checks account for perspective at every slot. Rolling dice use their rotated geometry to stay above the felt. The tray, materials, selection effects, and motion belong entirely to presentation. A rendered Play Mode check verifies that both the tray and bone dice produce visible pixels, alongside result parity and selection-label checks.

The scene uses generated room/body/head image layers and `SpiritFoxPortrait` for breathing, occasional ear twitches and tail swishes, and a scored-hand reaction. `SpiritPortraitImage` deforms a small UI mesh locally so the chest, ears, and upper tail can move while the elbows, forearms, and tail root remain planted. Cyan/gold spectral poses lead the physical head and tail by 0.18 seconds, fading as the physical form catches up. Idle gestures have their own schedule; a scored hand interrupts them and plays a 1.8-second response. Loading or starting a run resets the pose without replaying reactions. These motions consume no engine randomness and do not change the encounter snapshot.

The scene-specific Universal Renderer supports the dice while `SpiritPresentationEnvironment` leaves the project renderer selection unchanged outside this scene. Art references, text, UI, animation, and materials remain in Unity presentation code and assets. Mechanical catalog data and engine rules do not contain the fox setting. `SpiritFoxPortrait.Advance` supports deterministic animation checks; the motion capture test records the idle cycle and reaction when `DICE_SPIRIT_MOTION_ROOT` names a fresh output directory.

`Presentation/Resources/SpiritHouseTheme.asset` supplies the UI fonts, palette, button states, and green, amber, dark, and wood frame artwork. Frame rectangles and nine-slice borders use the reference texture dimensions so Unity's platform texture resizing preserves the frame layout. Text, resource counts, score counters, and selected-dice counts remain live UI. `SpiritUiOrnament` supplies canvas-rendered action symbols and dividers; decorative graphics do not intercept input. The gameplay screen uses a large target plaque, separate hands/rerolls plaques, aligned Base/Mult/Total counters, stacked action buttons, and a centered charms control. Rewards, charm details, and menus share the same theme.

The display font comes from [Cormorant](https://github.com/CatharsisFonts/Cormorant), and body text uses [Source Sans 3](https://github.com/adobe-fonts/source-sans). Original font licenses are retained beside the fonts under `Presentation/Resources/Fonts`. The built-in image generator's four frame prompts are retained in `Docs/outputs/spirit-house-prototype-assets/ui-theme-prompts.json`.

`SpiritEncounterAudio` plays recorded d6 rolls from Halfwits & Failed Crits' CC0 Rolling Dice pack, with separate clips for one through five rolled dice. `SpiritRollTiming` shares a start reference between scheduled audio and the tray animation. Per-recording seek offsets skip quiet preparation sounds, while contact markers align a short toss and small settling rebounds to recorded impacts. Muting preserves the visual timing. The source recordings are unchanged; provenance and hashes are retained under `Docs/work/spirit-audio/recorded-dice`, with transient analysis under `Docs/work/spirit-audio/roll-timing-01`. Rain uses InspectorJ's "Rain on Windows, Interior, A.wav" under CC BY 4.0, with a 0.8-second loop crossfade and a visible menu credit supplied by the theme. Its original WAV and processing record are retained under `Docs/work/spirit-audio/inspectorj-rain`. Occasional creaks, a spirit accent, scoring ticks, and a stage-clear cue are project-authored. `Presentation/Resources/SpiritAudio/SpiritAudioTheme.asset` supplies replaceable clips, timing tracks, credits, and mix levels; roll arrays are ordered by dice count. The menu sound toggle affects only this encounter's sources; focus loss, pause, and disable stop playback. Cosmetic variation uses an independent random source. The offline synthesis source and WAV validation are retained under `Docs/work/spirit-audio`.

Base, Mult, and Total display earned contributions rather than a predicted hand score. Each hand starts at zero; initial-roll and reroll modifiers visibly add to Base and Mult, with those bonuses retained through subsequent rerolls while Total stays zero. Selecting dice or restoring a save reads the banked amounts from the current snapshot. The current hand category may be shown before play, but its score is not added until the hand is played.

Normal startup and New Run obtain a fresh 64-bit seed from the platform random-number generator. The controller's Inspector offers **Use Fixed Seed** with **Initial Seed** for repeatable debugging; the toggle is off in the prototype scene. Explicit initialization with a supplied seed remains deterministic for tests. Loading a save resumes its stored dice and reward RNG states without reseeding.

The score sequence holds the played dice on the table while it adds the raw dice sum, the fully upgraded category's Base and Mult as one contribution, and each recorded PlayHand contribution in order. Each modifier shows its charm name and deltas; resource grants appear at their matching activation using recorded values. Each activation also slides a small 3D bone die into a wooden history tray beneath the numbered dice. These pieces reuse the main dice's rounded mesh construction, bone texture, and surface shader, with modifier artwork on their top faces. The perspective renderer provides raised wooden rails, recessed felt, and contact shadows. The frame stays fixed while overflow dice scroll inside it, with clipping at the tray edges. Invisible UI targets follow the projected dice for tapping and swiping.

Initial-roll and reroll activations remain in this history, including repeated activations of the same instance. Tapping a die reviews its contribution; the history clears when the next hand starts. Loading or rebinding restores the ordered contributions from the snapshot. Provisional sigils and sprite mappings belong to presentation, independently of mechanical definitions.

Counter steps take 0.52 seconds with a 0.35-second hold; modifiers receive a 0.55-second hold. Only after all contributions does Base times Mult become Total. A brief hold precedes the goal reduction; the next hand then resets the counters, or a cleared stage advances to rewards. Presentation never reruns modifier predicates. Reference investigation in `Docs/work/scoring-reference/native-scoring-sequence-2026-10-08.json` found that Dice of Kalma resolves modifiers together, rolls aggregate counters, and cycles modifier icons separately. Our individual numerical steps are a presentation choice. The reference's last-reroll/last-hand modifier activates after reroll cost and banks its result, which the existing engine already models.

This is a playable presentation prototype. Final character rigging, authored charm names and individual icons, final recorded audio, additional spirit hosts, and physical Android device validation remain future work. Generated asset prompts are retained under `Docs/outputs/spirit-house-prototype-assets`; test results and captured screens are retained under `Docs/work/spirit-validation`.

## Local references

- [Extracted design reference](dice-of-kalma-design-reference.html)
- [Structured reference data](dice-of-kalma-design-data.json)
- `Docs/work/dice-of-kalma/code/dump.cs`: recovered type layouts and method signatures, particularly RunSaveData, Item, Inventory, and BattleHandler.
- `Docs/work/dice-of-kalma/asm-BattleHandler__CheckHand.txt` and `asm-BattleHandler__DoPreviewCalc.txt`: active categories and score calculation.
- `Docs/work/dice-of-kalma/asm-HandsSyncToUI__SelectHandsForUpgrade.txt`, `asm-InventoryVisualizer__GenerateLoot.txt`, and `asm-BattleHandler._PointsCountCycle_d__194__MoveNext.txt`: upgrade, offer, and encounter transition evidence.
- `Dice-Unity/ProjectSettings/ProjectVersion.txt` and `Dice-Unity/Packages/manifest.json`: inspected project version and packages.
