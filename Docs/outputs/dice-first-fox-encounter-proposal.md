# First fox encounter and progression proposal

Design and implementation notes for John Price. Updated October 9, 2026.

The first fox encounter introduces spirit bargains through ten stages, twelve modifier conditions, and a free first reroll on each hand. Planned milestone rewards persist after a loss. Modifier strength stays at the existing catalog values; the encounter's length, targets, and reward policy accommodate the smaller pool.

The playable configuration implements the ten-stage encounter, curated pools, reward tiers, free-first-reroll rule, persistent modifier unlocks, completion history, investigation selection, the arrival and bargain sequence, and a story clue earned on victory. The thresholds and tier assignments remain playtest starting values, not a measured difficulty curve. Consumables, a physical token collection, and rematches remain proposals for subsequent work.

## Implementation boundaries

`SpiritHousePrototype` opens the investigation catalog. Selecting The Last Lantern presents its approach, arrival, bargain, and invitation. Accepting creates `encounter.fox.introduction` and animates its initial roll. The 25-stage `encounter.standard` remains available to diagnostics and direct initialization with its existing targets and resource cadence. The investigation's load action requires an authored journey for the saved encounter ID. New offers use scoped collection eligibility and tier schedules; tests retain the historical uniform reward trace in an explicit reference fixture. Mechanical rules are immutable engine objects built from encounter rule ScriptableObjects. The fox's rule wording and optional sprite are authored separately in `FoxCourtesy.asset`, referenced by the presentation.

Accepted reroll actions still drive modifier conditions. Separate stage and cumulative free-use counters account for the resource discount. Save format 3 / rules version 5 stores rule accounting, permanent modifier eligibility, run-earned unlocks, and the unlock context used to generate a pending offer. Format 1 / rules version 3 and format 2 / rules version 4 remain readable; the oldest format requires an encounter without rules. Legacy owned charms and already-generated offers survive migration. Future offers use the progression rules.

## Encounter configuration

### Journey authoring

`Assets/Dice/Content/Presentation/SpiritHouse/SpiritJourneyCatalog.asset` lists selectable journeys. Each `EncounterJourneyAsset` owns its mechanical encounter ID, investigation lead, ordered presentation beats, table presentation, ending dialogue, and completion clue. `LastLanternJourney.asset` is the authored first example; the shared controller and views do not select behavior by its ID or spirit identity.

The six player-facing steps are investigation selection, the illustrated approach with a moving silhouette, arrival at the table, the host's bargain, the invitation with its exact rule description, and acceptance followed by the opening roll. Beat data selects textures, foreground clipping and motion, timing, optional audio, and a fade or parting-panel transition. `LastLanternTable.asset` selects the room, sound theme, rule presentations, and host factory. `LanternKeeperHost.asset` supplies the specialized fox portrait and sigil implementation through the common host interface.

Investigation and introduction do not create a dice session or advance gameplay randomness. Acceptance records the visit in the JSON profile before creating the session. Returning visits can skip immediately to the invitation. Back cancels the introduction. Returning to the investigation during an active run retains that run for Continue at the table; loading resumes the saved phase and pending offer without replaying the introduction or initial roll. Wins and losses show their authored closing exchange, statistics, and unlocks. Victory exposes the authored completion clue on the investigation board through the persistent completion record.

To add an encounter, supply its mechanical definition, create a journey and table asset, author the beats and endings, and add the journey to the catalog. Use a host presentation asset compatible with the desired renderer, or omit the host. Presentation data validates its rule IDs against the selected mechanical definition. The current illustrated sequence uses the same controller and views as the unrelated encounter fixtures in the regression tests.

The doorway illustration is `Assets/Dice/Art/SpiritHousePrototype/LanternDoorway.png`. Its built-in image generation prompt and provenance are recorded in `last-lantern-journey-art.json` beside this document.

The investigator enters The Last Lantern following evidence that a missing person visited the fox. Victory earns a concrete lead. The larger progression proposal adds the fox's token and access to an optional rematch. Spirits and encounters have separate identities, allowing this host to return with a different challenge and story purpose.

Use a separate mechanical encounter definition for this first meeting. Keep the existing 25-stage targets and resource cadence of `encounter.standard`; apply progression eligibility and its own tier schedule to new offers. The first meeting starts with no owned charms and all eight hand categories at level zero. Every stage starts with three hands and three paid rerolls. Paid rerolls are shared across the stage; they do not refill for each hand. Stage resources reset on entering the next stage as they do now.

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

Each effect pool has twelve initial families plus three milestone families. Before the ninth offer, at most eight definitions can be owned, so initial content alone retains at least four unowned definitions per effect pool before tier filtering. History relaxation and minimum-tier exhaustion fallback retain choices without bypassing collection or scope restrictions.

Keep four hand-upgrade choices and the existing upgrade amounts and six-level caps. Exclude only the immediately previous upgrade choice, instead of Standard's three-choice history window. This lets a short run invest repeatedly in a preferred hand category while still making occasional alternative choices. Hands remain at level zero when a new encounter begins.

## Permanent unlocks and consumables

Unlock all four variants of each milestone family together:

| Milestone | Family | Trigger | Base / Mult / Combined / Rerolls |
| --- | --- | --- | --- |
| Clear stage 3 | Open Gate | Initial roll of the first hand of a stage | +10 / +2 / +5 base and +1 mult / +1 |
| Clear stage 6 | Still Reflection | Rerolled hand has the same category as immediately before the reroll | +10 / +2 / +5 base and +1 mult / +1 |
| Win stage 10 | Rising Sun | Initial hand ranks above the previously played hand; requires a previous hand | +15 / +3 / +5 base and +2 mult / +2 |

The initial pool is 48 definitions. These rewards expand the player's available set to 52, then 56, then 60. Future modifiers are eligible rewards, not automatically owned charms.

Milestone unlocks are recorded immediately in spirit-profile.json through Newtonsoft JSON and can appear in the current run's subsequent offers, subject to its tier schedule. They survive losses, retries, application restarts, and loading an older run. Unlocking does not equip a charm. Already-generated offers retain their choices when a newer profile is merged; refreshes and later offers use the expanded set. The three fox milestone families use Spirit scope with spirit.fox, so they are available only in permitted pools for fox encounters. A later fox encounter can reuse those unlocks; another spirit cannot offer them.

Milestones grant their profile rewards once, on first achievement. A loss after clearing stage 3 or 6 retains those rewards. Replaying or loading the same milestone cannot duplicate them. These initial rewards are not a repeatable consumable farm; repeatable earning rules can be designed separately.

The proposed consumables are one extra paid reroll, one free modifier-offer refresh, and one extra hand. Bring at most three individual consumable charges into an encounter, with duplicates allowed. Newly earned charges enter the persistent inventory for a future loadout. Unused brought charges remain owned; used charges remain consumed even if the encounter is lost or an earlier run save is loaded. Inventory and run saves must agree on each consumption transaction. An extra hand must be used while the stage is active, before a failed encounter has ended.

Victory records the fox token, advances the investigation, and unlocks an optional fox rematch entry. That rematch keeps the signature rule and is tuned against the 60 definitions guaranteed by the first victory. Its longer target curve should follow testing of this opening configuration; it does not need a new spirit model or location to exercise the progression flow.


## Modifier availability and reward tiers

Each mechanical ModifierAsset authors powerTier, access, availabilityScope, and availabilityScopeId. Presentation names, artwork, and descriptions remain in separate presentation assets. Reward pool membership, access, scope, ownership, family exclusions, and tier rules must all allow a choice.

Access is Starter (no unlock needed), PermanentUnlock (the player profile must contain its ID), or EncounterExclusive (available without a permanent unlock within its authored scope). Scope is Global, Spirit with a spirit ID, or Encounter with an encounter ID. An exclusive modifier requires a non-global scope. A permanently unlocked modifier may have any of the three scopes.

The initial collection contains 44 global starter definitions. Welcoming Pair's four variants are exclusive to encounter.fox.introduction and bring its initial pool to 48. Each fox effect pool also lists the three locked milestone definitions for that effect, making 60 possible definitions after all milestones. Other functional definitions require permanent unlocks; their future unlock sources are not yet authored. The standard encounter also uses the player's eligible collection and a tier schedule.

Tier values are authored data. The initial assignment groups the existing effect magnitudes into three provisional tiers; activation difficulty and combinations still require playtesting. No tier calculation runs when loading content.

| Incoming playable stages | Fox start stage | Standard start stage | Tier 1 / 2 / 3 weights |
| --- | ---: | ---: | --- |
| Opening | 1 | 1 | 100 / 0 / 0 |
| Developing | 4 | 9 | 60 / 35 / 5 |
| Late | 7 | 17 | 30 / 50 / 20 |
| Final | 10 | 23 | 15 / 45 / 40 |

At the first developing and late offers, choices require at least tier 2 while an eligible candidate exists in that slot. The first final offer requires tier 3 while available. If the minimum tier has been exhausted or excluded by current-offer family uniqueness, the slot uses its normally weighted eligible pool. This never bypasses unlock or scope requirements. A tier is chosen by weight, then a modifier uniformly within that tier, so adding more assets to a tier does not inflate its probability.

Unlock milestones refer to the stage just cleared; tier schedules refer to the stage the reward prepares for. Clearing fox stage 3 grants Open Gate before generating the incoming stage 4 offer. Its tier 1 variants can appear in subsequent ordinary offers; the stage 4 guaranteed stronger offer still follows its tier rule.

Profile writes use a temporary file and atomic replacement with a backup. A corrupt profile is reported without overwriting it. A failed milestone write retains dirty progress in memory and blocks further actions, loading, or starting another run until a retry succeeds. Encounter saves remain separate and cannot remove persistent unlocks. Consumable transactions are outside this implementation.

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
3. The Newtonsoft JSON player profile, milestone grants, investigation selection, authored introduction sequence, and return/resume flow are implemented. Multiple encounter definitions can share a spirit ID without sharing presentation data.
4. Implement the three consumables and loadout, with persistence that prevents duplicate use or duplicated milestone grants.
5. Test fresh-profile attempts with no consumables across many seeds, then compare experienced strategies and human novice sessions. Measure stage-of-loss distribution, full-run completion rate, run duration, and which families are selected and activate. Tune targets and offers from these results before specifying the rematch's difficulty.

Validation must include the ninth offer after concentrating on one effect role, heavy refresh use, early stage clears, reaching zero paid rerolls with an unused free action, unchanged faces on a free reroll, saving before and after its use, and losing after a permanent milestone. The initial target table must not be described as balanced until those gameplay results exist.

## Source configuration

Catalog values and identities are taken from `Dice-Unity/Assets/Dice/Content/Mechanical/Standard.asset`, the individual assets in `Dice-Unity/Assets/Dice/Content/Mechanical/Modifiers`, and the theme assets in `Dice-Unity/Assets/Dice/Content/Presentation/SpiritHouse/Modifiers`. Existing rule behavior is defined in `EncounterSession`, `RewardGenerator`, `EncounterPersistence`, and the individual condition classes. The proposal changes encounter configuration and future progression policy; it does not change the imported modifier effect amounts.
