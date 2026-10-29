# Collection content

Content refers to charms by direct references to their `ModifierAsset`s. The game's
encounters are the `EncounterAsset`s (Create > Dice > Encounter, in `Encounters`,
named after the encounter) that `Standard.asset` lists; an encounter's id is its
asset GUID, kept like a family's. An encounter's reward policy is optional and has
no id: the inspector sets or clears it, and an encounter without one makes no reward
offers. Each slot of an encounter's reward policy layouts references the
`RewardPoolAsset` it draws from (Create > Dice > Reward Pool, in `RewardPools`,
named after the pool): its charms in order and an optional fallback charm. A pool
has no id and is never saved; one pool asset can serve any number of slots and
encounters, and load errors name it by its asset name. The engine catalog
is built from the charms those encounters and their pools reference (encounter
lists, unlock rewards and pools, including fallbacks), so a charm or pool referenced
nowhere is not part of the game. Hands are authored inline in `Standard.asset` and
identified by their category. Each charm references its family, a `ModifierFamilyAsset`
(Create > Dice > Modifier Family) in `ModifierFamilies`, named after the family as
its charms are named after themselves; the catalog's families are those of its
charms. A family's id is its asset GUID, which the family asset stores and the
editor keeps equal to the GUID in its .meta file. Saves and profiles store charm
ids, family ids and encounter ids, which resolve through the loaded catalog.

Each spirit's encounter asset authors its six `unlockRewardModifiers` and its stage
3, 6, and 10 unlock opportunities. Each opportunity chooses one still-locked entry
from that same list. Fox uses Base and Mult variants of Open Gate, Still Reflection,
and Rising Sun. Owl and moth use Base and Mult variants of their three established
milestone families; these lists are provisional.

## Family discovery and story grants

Create a `Dice/Progression Reward` asset with independent `discoveredFamilies`
(family asset references) and `grantedModifiers` (charm references) lists. Discovering a family reveals its variants and
allows its entries in authored unlock lists to be offered. Granting an individual
charm permanently owns it and also discovers its family. Neither operation equips
the charm or inserts it into an encounter's ordinary pool.

Reference the asset from an encounter journey beat's `progressionReward` or the
journey's `completionReward`. Beat rewards apply when continuing past the beat,
including skipped beats, and invitation rewards apply on acceptance. Completion
rewards apply when the journey completes at its last beat: on victory when that beat is its Encounter beat, otherwise after the final explicit continue.
Replaying these events is safe: discoveries and ownership
are sets, so they do not accumulate duplicates. Other story systems can call
`SpiritEncounterController.ApplyProgressionReward` with a built definition.

The authored invitation rewards under `ProgressionRewards` (fox, owl, moth, merchant
and nymph) reveal each encounter's three unlock families. Fox also grants Open Gate / Base
(`modifier.initial_roll.first_hand.base_score`, +10 Base, tier 1).
For a guide who gives protection before a region, author both discovery and grant
lists, and include the granted charms in that region's ordinary run pools so they
can appear during play. Starter and previously owned families need no explicit
discovery reward. Permanent Collection excludes inert and encounter-only content.

Chapter 1 follows fox, owl, moth, then the return journey (a journey without an
encounter) `ChapterOneReturnJourney`. Each requires completion of the preceding
journey; the first fox visit has no prerequisite. Returning to the fox uses
`ChapterOnePreparations` to reveal Sheltering Branch and Boundary Stone, and
grants Sheltering Branch / Base (+15 Base, tier 2) and Boundary Stone / Mult
(+2 Mult, tier 1). These ordinary permanent charms already belong to the existing
Base and Mult pools. Gifts become eligible for future offers without being equipped.
The return journey completes Chapter 1. Chapter 2, The Drowned Road, follows the
merchant and then the bound tree; each requires completion of the preceding journey.

A journey beat can reference a `charm` mechanical asset to show a compact detail
panel below its narration. Use `{charm}` in the narration for the highlighted
family name without the variant suffix. The icon, effect, condition, and trigger wording come from the
existing charm presentation; keep mechanical descriptions out of dialogue.
This reference only controls presentation. Ownership still comes from the beat
or journey's progression reward.

Ordinary modifiers can be offered by any encounter whose pools or lists hold them. Permanent unlock requirements
remain on existing variants, including combined and reroll variants that are not
currently awarded by these lists. Existing authored ordinary reward pools retain
their membership.

Moth's `encounterModifiers` contains four encounter-exclusive Lantern Seekers
variants. They trigger when a played hand contains at least two dice showing the
favored face supplied by the encounter rule. Provisional effects are +10 Base,
+2 Mult, +5 Base and +1 Mult, and +1 reroll. Each is tier 1 and once per hand.
Their access is Encounter Exclusive, and the moth's `encounterModifiers` list is the
only statement of the encounter that offers them: loading fails, naming the charm and
where it is listed, when an encounter-exclusive charm is in a reward pool or its
fallback, an unlock reward list, an encounter's starting charms or a progression
grant. The charm inspector's read-only Offered by section lists each encounter whose
`encounterModifiers` holds the charm (click to ping, double-click to select) and
warns when no encounter or more than one lists it.
The family provisionally reuses the existing Last Lantern artwork and the standard
effect-category assets. Replace the shared artwork only when dedicated art is
approved. The favored-face rule itself still adds +2 Mult per matching scored die.

The mechanical condition is `FavoredFaceCountAtLeastConditionConfig` with
`count: 2`, which builds `FavoredFaceCountAtLeastCondition`. Each modifier and its presentation remain
separate ScriptableObjects: the catalog references the modifier and the modifier skin
(`Presentation/SpiritHouse/SpiritHouseModifierSkin.asset`) lists its presentation.

## Tree nymph encounter

The `encounter.nymph.bound_tree` encounter asset authors the Base and Mult variants of Great Oak
(`modifier.play_hand.three_smallest_sum_at_most_maximum`), Close Branches
(`modifier.play_hand.adjacent_difference_at_most`) and Unturned Leaf
(`modifier.play_hand.no_dice_rerolled`) as its six `unlockRewardModifiers`, with
unlock opportunities at stages 3, 6, and 10. `ProgressionRewards/NymphFamilyDiscovery`
discovers those three families and grants nothing; the bound tree journey references it.

Its `encounterModifiers` contains four encounter-exclusive Grafted Branch
variants, offered only through that list as Lantern Seekers are through the
moth's. They trigger when at least three dice show the face of a die bound by
the encounter's binding rule (`NymphBinding.asset`); the bound die counts toward
the three. Provisional effects are +10 Base, +2 Mult, +5 Base and +1 Mult, and
+1 reroll. Each is tier 1 and once per hand. The family provisionally reuses the
Joined Roots icon and artwork and the standard effect-category assets until
dedicated art is approved. The mechanical condition is
`BoundFaceCountAtLeastConditionConfig` with `count: 3`, which builds
`BoundFaceCountAtLeastCondition`.

`encounterModifiersFirstClearedStageNumber: 3` makes the stage 3 clear's charm
offer the first that can contain Grafted Branch. Encounters that do not author
the field use 1 and can offer their additions from the first clear.
