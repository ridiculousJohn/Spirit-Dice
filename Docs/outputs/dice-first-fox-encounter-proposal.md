# First fox encounter and progression proposal

Draft for John Price. October 8, 2026.

The first fox encounter introduces spirit bargains through ten stages, twelve modifier conditions, and a free first reroll on each hand. Planned milestone rewards persist after a loss. Modifier strength stays at the existing catalog values; the encounter's length, targets, and reward policy accommodate the smaller pool.

The first playable configuration implements the ten-stage encounter, starting pools, reward policy, and free-first-reroll rule described below. The thresholds remain playtest starting values, not a measured difficulty curve. Persistent milestone rewards, profile unlocks, consumables, story rewards, and rematches remain proposals for subsequent work.

## Implementation boundaries

`SpiritHousePrototype` starts `encounter.fox.introduction`. The existing 25-stage `encounter.standard` remains available to diagnostics, tests, and existing saves. Mechanical rules are immutable engine objects built from encounter rule ScriptableObjects. The fox's rule wording and optional sprite are authored separately in `FoxCourtesy.asset`, referenced by the presentation.

Accepted reroll actions still drive modifier conditions. Separate stage and cumulative free-use counters account for the resource discount. Save format 2 / rules version 4 stores this accounting; format 1 / rules version 3 can still restore encounters without encounter rules and is written in the current format on the next save.

## Encounter configuration

The investigator enters The Last Lantern following evidence that a missing person visited the fox. Victory earns a concrete lead, the fox's token, and access to an optional rematch. Spirits and encounters have separate identities, allowing this host to return with a different challenge and story purpose.

Use a separate mechanical encounter definition for this first meeting. Preserve `encounter.standard` as the existing 25-stage reference configuration. The first meeting starts with no owned charms and all eight hand categories at level zero. Every stage starts with three hands and three paid rerolls. Paid rerolls are shared across the stage; they do not refill for each hand. Stage resources reset on entering the next stage as they do now.

| Stage | Proposed target | Existing Standard target | Permanent reward on first clear of this milestone |
| --- | ---: | ---: | --- |
| 1 | 120 | 160 | None |
| 2 | 170 | 225 | None |
| 3 | 230 | 320 | Unlock Open Gate; gain one extra-reroll consumable |
| 4 | 300 | 410 | None |
| 5 | 380 | 520 | None |
| 6 | 470 | 635 | Unlock Still Reflection; gain one free-offer-refresh consumable |
| 7 | 570 | 750 | None |
| 8 | 680 | 860 | None |
| 9 | 800 | 975 | None |
| 10 | 930 | 1,090 | Unlock Rising Sun; gain one extra-hand consumable, the fox token, the story lead, and the rematch entry |

The target curve grows smoothly, while the free reroll provides help on every hand. It must be tested with only the initial unlocks and no consumables. Optional power must not be needed to finish the opening encounter.

## Signature rule

Player wording: **Your first reroll each hand does not spend a reroll.** A provisional presentation name is **The Host's Courtesy**.

- The first accepted reroll costs zero paid rerolls. Subsequent rerolls use the ordinary cost.
- It works when the paid reroll counter is zero. The button shows that the free use remains available.
- It refreshes on each new hand, including the first hand of a new stage. An unused free reroll does not carry forward.
- Any nonempty legal selection consumes the free use, even if every die happens to roll the same face again. Invalid actions do not consume it.
- A free reroll is still a reroll action: it updates dice history, counts toward conditions based on how many times the hand was rerolled, and can activate ordinary reroll modifiers. It invalidates a condition requiring no rerolls.
- The fox's sigil flashes over Reroll and a spirit effect crosses the dice. The paid counter does not decrement for the cost. Ordinary modifiers may independently add rerolls, with their own feedback. The signature-rule icon then dims until the next hand.
- Save/load preserves whether this hand's free use has already been consumed.

The engine currently uses `RerollsSpentOnHand` and related counters to count accepted reroll actions as well as validate resource expenditure. The rule requires separating action history from paid resource accounting without losing either. The presentation should query action availability from the application/engine instead of assuming that a zero resource counter means no legal reroll.

Conditions involving zero remaining rerolls or the last reroll are outside this encounter's allowed pool. Before introducing them elsewhere, their wording and interaction with free actions need an explicit design decision; this proposal does not silently redefine them.

## Starting modifier pool

Start with **12 families and all four existing effect variants of each: 48 definitions out of 292 functional definitions**. Players learn twelve conditions. The variants provide base score, multiplier, combined scoring, or reroll recovery without requiring more conditions to learn.

All amounts below come from the current mechanical assets. Each owned modifier instance activates at most once per hand. Initial-roll and reroll scoring bonuses remain banked for that hand even if subsequent rolls change its category or faces.

| Current charm name | Condition and timing | Base variant | Mult variant | Combined variant | Reroll variant |
| --- | --- | ---: | ---: | --- | ---: |
| One Spark | When played: at least one die shows 1 | +10 | +2 | +5 base, +1 mult | +1 |
| Tall Pine | When played: at least one die shows 6 | +10 | +2 | +5 base, +1 mult | +1 |
| Quiet Grove | When played: no die shows 1 or 6 | +15 | +3 | +5 base, +2 mult | +2 |
| Light Basket | When played: dice sum is below 15 | +15 | +3 | +10 base, +1 mult | +2 |
| Full Basket | When played: dice sum exceeds 22 | +10 | +2 | +5 base, +1 mult | +1 |
| Narrow Bridge | When played: largest face minus smallest face is less than 3 | +15 | +3 | +10 base, +1 mult | +2 |
| Twin Knots | When played: the category is exactly Two Pair | +15 | +3 | +10 base, +1 mult | +2 |
| Lone Mountain | When played: the category is High Die | +30 | +6 | +15 base, +3 mult | +3 |
| Lone Watcher | When played: the middle die's face appears nowhere else | +10 | +2 | +5 base, +1 mult | +1 |
| Welcoming Pair | On the initial roll: the category is exactly Pair | +10 | +2 | +5 base, +1 mult | +1 |
| Second Chance | After rerolling: at least one selected die kept its previous face | +10 | +2 | +5 base, +1 mult | +1 |
| Single Firefly | After rerolling: exactly one selected die shows 1 | +10 | +2 | +5 base, +1 mult | +1 |

This supports several directions from the outset: low faces with Light Basket and One Spark, high faces with Full Basket and Tall Pine, clustered dice with Quiet Grove and Narrow Bridge, and alternative scoring through Two Pair or High Die. Welcoming Pair, Second Chance, and Single Firefly introduce bonuses before playing a hand. Reroll variants let those builds recover resources.

Retain exact-definition ownership exclusions. Different variants of the same family may be collected across a run and activate together, as in the current rules. A single offer must contain different families.

### Concrete scoring examples

These arithmetic examples show that the smaller pool retains strong combinations; they do not establish their frequency or an encounter win rate. Each example assumes level-zero hands, the listed owned charms, and no other contributions.

| Dice and category | Owned charm variants that activate | Calculation | Score |
| --- | --- | --- | ---: |
| 1, 1, 2, 2, 3: Two Pair | Light Basket base; Twin Knots mult; One Spark combined | (9 dice + 10 category + 15 + 5) x (3 category + 3 + 1) | 273 |
| 6, 6, 6, 5, 5: Full House | Tall Pine base; Full Basket mult | (28 dice + 20 category + 10) x (6 category + 2) | 464 |
| 3, 3, 3, 3, 2: Four of a Kind | Narrow Bridge base; Quiet Grove combined | (14 dice + 25 category + 15 + 5) x (8 category + 2) | 590 |
| 1, 2, 3, 5, 6: High Die | Lone Mountain base; Lone Mountain mult | (17 dice + 5 category + 30) x (1 category + 6) | 364 |

## Between-stage rewards

Clearing stages 1 through 9 gives one charm selection and one hand upgrade. Stage 10 ends the encounter and grants its persistent victory rewards; it does not grant another run-only charm or upgrade.

Charm offers normally contain one base variant, one multiplier variant, and one combined variant. After stages 3, 6, and 9, the third slot offers a reroll variant instead. This keeps a scoring choice available at every boundary and introduces resource choices without an entire offer of reroll rewards.

Keep one starting offer refresh, one earned per nonfinal stage clear, and a cost of one per refresh. Retain the existing offer-history behavior and distinct-family rule. Use no inert fallback in these pools.

Each effect pool begins with twelve distinct families. Before the ninth offer, at most eight definitions can be owned, so every effect pool still has at least four unowned definitions. Even after excluding the families picked for the first two offer slots, the third slot has candidates. Existing refresh-history relaxation prevents historical exclusions from exhausting an otherwise valid pool.

Keep four hand-upgrade choices and the existing upgrade amounts and six-level caps. Exclude only the immediately previous upgrade choice, instead of Standard's three-choice history window. This lets a short run invest repeatedly in a preferred hand category while still making occasional alternative choices. Hands remain at level zero when a new encounter begins.

## Permanent unlocks and consumables

Unlock all four variants of each milestone family together:

| Milestone | Family | Trigger | Base / Mult / Combined / Rerolls |
| --- | --- | --- | --- |
| Clear stage 3 | Open Gate | Initial roll of the first hand of a stage | +10 / +2 / +5 base and +1 mult / +1 |
| Clear stage 6 | Still Reflection | Rerolled hand has the same category as immediately before the reroll | +10 / +2 / +5 base and +1 mult / +1 |
| Win stage 10 | Rising Sun | Initial hand ranks above the previously played hand; requires a previous hand | +15 / +3 / +5 base and +2 mult / +2 |

The initial pool is 48 definitions. These rewards expand the player's available set to 52, then 56, then 60. Future modifiers are eligible rewards, not automatically owned charms.

Freeze the run's eligible modifier IDs when it starts. A milestone unlock is recorded immediately in the player profile and becomes available when starting the next encounter or retry. The unlock message must say this explicitly. Saving and resuming a run preserves that run's eligibility set even if the profile subsequently changes. Later visits to the first encounter may use its initial families plus these three unlocked families; unrelated future unlocks do not automatically enter every encounter's pool.

Milestones grant their profile rewards once, on first achievement. A loss after clearing stage 3 or 6 retains those rewards. Replaying or loading the same milestone cannot duplicate them. These initial rewards are not a repeatable consumable farm; repeatable earning rules can be designed separately.

The proposed consumables are one extra paid reroll, one free modifier-offer refresh, and one extra hand. Bring at most three individual consumable charges into an encounter, with duplicates allowed. Newly earned charges enter the persistent inventory for a future loadout. Unused brought charges remain owned; used charges remain consumed even if the encounter is lost or an earlier run save is loaded. Inventory and run saves must agree on each consumption transaction. An extra hand must be used while the stage is active, before a failed encounter has ended.

Victory records the fox token, advances the investigation, and unlocks an optional fox rematch entry. That rematch keeps the signature rule and is tuned against the 60 definitions guaranteed by the first victory. Its longer target curve should follow testing of this opening configuration; it does not need a new spirit model or location to exercise the progression flow.

## Exact catalog mapping

For each family below, include the four existing IDs formed by appending `.base_score`, `.multiplier`, `.combined`, and `.rerolls`. Mechanical IDs remain separate from the presentation names used above.

| Availability | Presentation name | Existing mechanical family ID |
| --- | --- | --- |
| Initial | One Spark | `modifier.play_hand.contains_face` |
| Initial | Tall Pine | `modifier.play_hand.contains_upper_face` |
| Initial | Quiet Grove | `modifier.play_hand.no_extreme_faces` |
| Initial | Light Basket | `modifier.play_hand.sum_less_than` |
| Initial | Full Basket | `modifier.play_hand.sum_greater_than` |
| Initial | Narrow Bridge | `modifier.play_hand.range_less_than` |
| Initial | Twin Knots | `modifier.play_hand.two_pair` |
| Initial | Lone Mountain | `modifier.play_hand.high_die` |
| Initial | Lone Watcher | `modifier.play_hand.middle_unique` |
| Initial | Welcoming Pair | `modifier.initial_roll.initial_pair` |
| Initial | Second Chance | `modifier.player_reroll.selected_face_unchanged` |
| Initial | Single Firefly | `modifier.player_reroll.exactly_one_selected_face` |
| Stage 3 first clear | Open Gate | `modifier.initial_roll.first_hand` |
| Stage 6 first clear | Still Reflection | `modifier.player_reroll.rank_equals_before_reroll` |
| First victory | Rising Sun | `modifier.initial_roll.initial_rank_above_previous` |

## Implementation and validation order

1. Add the reusable free-first-reroll rule, including zero-budget action availability, ordinary modifier activation, action/resource accounting, and save continuity. Keep host identity and visual cues in presentation data.
2. Author the ten-stage encounter and its pools. Eligible content must be explicit in each run, with no hidden dependency on mutable profile data during resolution.
3. Add the Newtonsoft JSON player profile, milestone grants, and encounter selection. Grant completion rewards once, preserve them through losses, and keep multiple encounters associated with the same presentation host.
4. Implement the three consumables and loadout, with persistence that prevents duplicate use or duplicated milestone grants.
5. Test fresh-profile attempts with no consumables across many seeds, then compare experienced strategies and human novice sessions. Measure stage-of-loss distribution, full-run completion rate, run duration, and which families are selected and activate. Tune targets and offers from these results before specifying the rematch's difficulty.

Validation must include the ninth offer after concentrating on one effect role, heavy refresh use, early stage clears, reaching zero paid rerolls with an unused free action, unchanged faces on a free reroll, saving before and after its use, and losing after a permanent milestone. The initial target table must not be described as balanced until those gameplay results exist.

## Source configuration

Catalog values and identities are taken from `Dice-Unity/Assets/Dice/Content/Mechanical/Standard.asset`, the individual assets in `Dice-Unity/Assets/Dice/Content/Mechanical/Modifiers`, and the theme assets in `Dice-Unity/Assets/Dice/Content/Presentation/SpiritHouse/Modifiers`. Existing rule behavior is defined in `EncounterSession`, `RewardGenerator`, `EncounterPersistence`, and the individual condition classes. The proposal changes encounter configuration and future progression policy; it does not change the imported modifier effect amounts.
