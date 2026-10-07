# Dice modifier implementation

The engine resolves mechanical modifiers during initial rolls, player rerolls, and played hands. The Standard catalog contains 74 families and 296 definitions: 292 functional variants and four inert variants with reward eligibility disabled. The catalog version is `standard.modifiers.v1`; the engine rules version is 2.

## Content and presentation

Definitions contain stable mechanical IDs, family IDs, phase, a typed condition with parameters, effect operations and amounts, activation limits, and reward eligibility. They contain no display names, descriptions, localization keys, colors, artwork, audio, or lore. Source names and asset provenance are retained only in the research mapping under `Docs/work/modifier-reference`.

`MechanicalCatalogAsset` builds immutable engine definitions. An `EncounterDefinition` can specify ordered starting modifier IDs, including duplicate copies. Each owned copy receives its own encounter-local instance ID and activation counters. Standard still starts with an empty inventory; acquiring modifiers through rewards belongs to the next implementation pass.

## Resolution contract

The resolver evaluates all conditions against one immutable phase context and then applies eligible effects in owned-instance order. A reroll grant cannot change another condition's eligibility during the same phase. Effects add base score, multiplier, or rerolls. Each activation and resource grant carries its source definition and instance ID.

Starting a hand clears its score contributions and per-hand activation counts before initial-roll effects resolve. Contributions from initial rolls and rerolls persist until that hand is played. Starting a stage resets stage counters. Every reference definition uses the verified per-instance, once-per-hand limit. The native final-reroll condition is effectively once per stage because it can activate only on the last hand. Explicit unlimited and once-per-stage limits are available for custom content.

Player rerolls spend their action before evaluating modifiers. Playing spends its hand before evaluating modifiers. Five reference predicates explicitly use the pre-play hand count: rerolls exceeding hands, largest matching group equaling hands or rerolls, minimum/maximum equaling hands, endpoint sum equaling hands or rerolls, and even hands. Those conditions add the spent hand back to the context count.

All arithmetic and resolution happen on a private state copy. Rejected actions and failed arithmetic leave the committed state, instance counters, and future random stream intact.

## Preview and inspection

`PreviewPlayHand()` returns the score breakdown from the same resolver used for a committed play. `PreviewHandResolution()` additionally exposes projected remaining rerolls, modifier instance counters, and current-play events. Neither commits state nor draws the next hand or reward.

Snapshots expose owned modifiers, activation counters, accumulated base score and multiplier, and immutable contributions. Score breakdowns include contributions from all phases of the current hand. These are mechanical facts for a presenter to render.

## Reference fidelity

Native code inspection verified phase timing, frozen resource sampling, inventory-slot activation identity, accumulator lifetime, initial predicates, ordering with ties, absolute differences, and several conditions whose internal enum names disagree with their descriptions. The family mapping distinguishes native-verified branches from conditions supported by matching extracted enum/text. Effect amounts and activation phases are taken directly from all 296 extracted assets.

Two explicit initialization choices matter. Previous-play rank comparisons begin at High Die, matching the reference's initialized comparison hand. An initial-roll rank comparison in a custom encounter with starting modifiers and no previous played hand returns false. The reference compares against an earlier random initialization hand in that case; ordinary Standard starts with no modifiers, so that difference is not encountered there. The engine does not reproduce the reference's complete random initialization or challenge-specific branches.

## Validation and remaining work

The Edit Mode suite covers all 7,776 ordered hands, predicate boundaries, all 296 catalog variants, resource timing, duplicate instances, activation resets, preview parity, rejected actions, and transactional overflow. Final results are recorded alongside this report's validation artifacts in `Docs/work`.

Reward selection, hand-upgrade selection, persistence, and a diagnostic Unity scene are subsequent work. This pass supplies the modifier engine and catalog; it does not yet make the Standard encounter playable through its reward boundaries.

Final validation: Unity 6000.6.2f1, 517 Edit Mode tests passed, zero failed or skipped. Results: `Docs/work/modifier-editmode-2.xml`. No source, package, or project-settings files changed during the final validation run.
