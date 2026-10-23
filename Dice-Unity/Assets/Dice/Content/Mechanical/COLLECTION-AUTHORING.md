# Collection content

`Standard.asset` authors each spirit's six `unlockRewardModifierIds` and its stage
3, 6, and 10 unlock opportunities. Each opportunity chooses one still-locked entry
from that same list. Fox uses Base and Mult variants of Open Gate, Still Reflection,
and Rising Sun. Owl and moth use Base and Mult variants of their three established
milestone families; these lists are provisional.

Each milestone retains its four legacy `modifierIds` for validating existing run
saves. Those IDs do not select the contents of new unlock offers.

## Family discovery and story grants

Create a `Dice/Progression Reward` asset with independent `discoveredFamilyIds`
and `grantedModifierIds` lists. Discovering a family reveals its variants and
allows its entries in authored unlock lists to be offered. Granting an individual
charm permanently owns it and also discovers its family. Neither operation equips
the charm or inserts it into an encounter's ordinary pool.

Reference the asset from an encounter journey beat's `progressionReward` or the
journey's `completionReward`. Beat rewards apply when continuing past the beat,
including skipped beats, and invitation rewards apply on acceptance. Completion
rewards apply on victory, or after the final explicit continue in a story journey.
Replaying these events is safe: discoveries and ownership
are sets, so they do not accumulate duplicates. Other story systems can call
`SpiritEncounterController.ApplyProgressionReward` with a built definition.

The three authored invitation rewards under `ProgressionRewards` reveal each
encounter's existing three unlock families. Fox also grants Open Gate / Base
(`modifier.initial_roll.first_hand.base_score`, +10 Base, tier 1).
For a guide who gives protection before a region, author both discovery and grant
lists, and include the granted charms in that region's ordinary run pools so they
can appear during play. Starter and previously owned families need no explicit
discovery reward. Permanent Collection excludes inert and encounter-only content.

Chapter 1 follows fox, owl, moth, then the story journey
`story.fox.chapter-one-return`. Each requires completion of the preceding
encounter; the first fox visit has no prerequisite. Returning to the fox uses
`ChapterOnePreparations` to reveal Sheltering Branch and Boundary Stone, and
grants Sheltering Branch / Base (+15 Base, tier 2) and Boundary Stone / Mult
(+2 Mult, tier 1). These ordinary permanent charms already belong to the existing
Base and Mult pools. Gifts become eligible for future offers without being equipped.
The return journey completes Chapter 1. Chapter 2 is not authored here.

A journey beat can reference a `charm` mechanical asset to show a compact detail
panel below its narration. Use `{charm}` in the narration for the highlighted
family name without the variant suffix. The icon, effect, condition, and trigger wording come from the
existing charm presentation; keep mechanical descriptions out of dialogue.
This reference only controls presentation. Ownership still comes from the beat
or journey's progression reward.

All ordinary modifiers have global availability. Permanent unlock requirements
remain on existing variants, including combined and reroll variants that are not
currently awarded by these lists. Existing authored ordinary reward pools retain
their membership.

Moth's `encounterModifierIds` contains four encounter-exclusive Lantern Seekers
variants. They trigger when a played hand contains at least two dice showing the
favored face supplied by the encounter rule. Provisional effects are +10 Base,
+2 Mult, +5 Base and +1 Mult, and +1 reroll. Each is tier 1 and once per hand.
The family provisionally reuses the existing Last Lantern artwork and the standard
effect-category assets. Replace the shared artwork only when dedicated art is
approved. The favored-face rule itself still adds +2 Mult per matching scored die.

The mechanical condition is `FavoredFaceCountAtLeastCondition`, serialized as
`conditionKind: 74` with `parameter: 2`. Each modifier and its presentation remain
separate ScriptableObjects registered in the existing mechanical and presentation
Addressables groups under their standard discovery labels.

## Tree nymph encounter

`encounter.nymph.bound_tree` authors the Base and Mult variants of Great Oak
(`modifier.play_hand.three_smallest_sum_at_most_maximum`), Close Branches
(`modifier.play_hand.adjacent_difference_at_most`) and Unturned Leaf
(`modifier.play_hand.no_dice_rerolled`) as its six `unlockRewardModifierIds`, with
unlock opportunities at stages 3, 6, and 10. `ProgressionRewards/NymphFamilyDiscovery`
discovers those three families and grants nothing; no journey references it yet.

Its `encounterModifierIds` contains four encounter-exclusive Grafted Branch
variants. They trigger when at least three dice show the face of a die bound by
the encounter's binding rule (`rule.nymph.binding`); the bound die counts toward
the three. Provisional effects are +10 Base, +2 Mult, +5 Base and +1 Mult, and
+1 reroll. Each is tier 1 and once per hand. The family provisionally reuses the
Joined Roots icon and artwork and the standard effect-category assets until
dedicated art is approved. The mechanical condition is
`BoundFaceCountAtLeastCondition`, serialized as `conditionKind: 75` with
`parameter: 3`.

`encounterModifiersFirstClearedStageNumber: 3` makes the stage 3 clear's charm
offer the first that can contain Grafted Branch. Encounters that do not author
the field use 1 and can offer their additions from the first clear.
