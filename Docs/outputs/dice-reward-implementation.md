# Dice rewards and hand upgrades

The Standard encounter now progresses through modifier selection and hand-upgrade selection between stages. Its catalog version is `standard.rewards.v1` and engine rules version is 3. Reward data remains mechanical: explicit pool membership, ordered layouts, refresh amounts, upgrade counts, and history windows. It contains no colors, artwork, display text, or lore.

## Standard sequence

The run begins with no modifier offer and one reward refresh. Each nonfinal clear adds one refresh, opens a modifier offer, opens a hand-upgrade offer after modifier selection, and starts the next stage after upgrade selection. Unused refreshes carry forward. Refreshing costs one and replaces only the modifier offer. A final clear completes the encounter without rewards or another refresh credit.

Four uniform pools each contain 73 eligible modifier definitions. The four-layout cycle contains one layout with three reroll-effect choices and three layouts with one base-score, one multiplier, and one combined-effect choice. The cycle uses the upcoming zero-based stage index; the first interstage offer is base score, multiplier, and combined effects. The first reroll-only offer follows stage four.

## Eligibility and history

Normal selection excludes exact owned definitions and families previously shown during that reward boundary, and avoids duplicate families within the current offer. When fewer than three candidates remain for a slot, that slot retries while ignoring historical family exclusions; exact owned definitions and current-offer family exclusions still apply. Historical entries remain available to later slots. Each Standard pool has an explicit inert fallback for exhaustion. Selecting a modifier clears this boundary's shown-family history.

Hand upgrades offer up to four distinct uncapped categories, sorted in category order. Incoming stages through 16 exclude the three most recently selected upgrades; stages 17 through 21 exclude the most recent two; later stages have no history exclusion. When fewer than four candidates survive filtering, the generator retries without recent-history exclusions while retaining upgrade caps. Three or fewer uncapped categories are all offered; a completely capped inventory skips this choice and advances.

Hand candidates are generated and cached when rewards open, before the player can refresh modifiers. Modifier refreshes therefore cannot change the current boundary's hand-upgrade choices. The normal upgrade remains +5 base score and +1 multiplier per level, with +2 multiplier for Five of a Kind, up to six upgrades.

## Engine contract

`RefreshModifierOfferAction`, `SelectModifierAction`, and `SelectHandUpgradeAction` require the current offer ID; selections also require an offered option ID. Both identity counters are monotonically allocated within an encounter. Stale revisions, stale offers, invalid options, incorrect phases, and insufficient refresh currency return structured rejections without changing state or random streams.

Snapshots expose the immutable pending offer, refresh balance, shown-family history, and recent hand upgrades. Events identify offered and selected rewards, acquired modifier instances, hand-upgrade counts, currency changes, and skipped exhausted rewards. Acquired modifiers participate in the next stage's initial roll. Dice and rewards use separate deterministic streams; this preserves dice outcomes when reward generation consumes extra randomness. It does not reproduce the reference game's seed-to-outcome mapping.

Pure generator functions and mechanical authoring support custom policies. An exhausted custom pool without a fallback contributes no option; if no modifier options remain, the engine continues to the upgrade decision. Encounters without a reward policy retain their explicit `AwaitingRewards` boundary. Standard has a complete policy and continues through all 25 stages.

## Spirit-house reward presentation

The stage-clear presentation holds the table on its scored hand while the fox reacts. The reward panel then identifies the cleared stage, the current choice, and the upcoming stage's target and starting resources. Charm choices use the active theme's name, icon, condition, effect, and activation timing. Refreshing shows both the cost and the available balance.

`SpiritRewardCard` renders each offered option and retains its current offer and option IDs. Hand-upgrade cards compare the category's current Base/Mult with its upgraded Base/Mult, including all existing upgrade levels. These values exclude the dice sum and temporary charm contributions. All offered choices remain accessible in the scroll area, with Charms and Hand Ranks available beside it.

After the final reward choice, a brief stage introduction shows the next target and resources before the initial roll. Input remains blocked through the introduction and roll. The presentation consumes the existing engine events without changing reward generation, scoring, or progression.

Rendered validation covers reward screens at 540x960 and 443x912, all 296 modifier descriptions at narrow card width, upgrade values at existing levels, refreshed choice IDs, reward-screen save/load, and input blocking during the stage introduction. The PlayMode suite and affected reruns have passing results for all 16 tests. Logs, results, and screenshots are retained under `Docs/work/spirit-reward-validation/20261008-01`.

## Engine validation

Final validation on Unity 6000.6.2f1 passed all 547 EditMode tests, with zero failures or skipped tests. Unity exited with code 0. A before/after SHA-256 comparison found no file changes in Assets, Packages, or ProjectSettings during validation. Results are saved as `Docs/work/reward-editmode-2.xml` and the editor log as `Docs/work/reward-editmode-2.log`.

Tests cover imported Standard data, offer cadence, pool eligibility, history fallback, caps, refresh accounting, offer and option identity, rejected actions, preview/state isolation, independent dice randomness, and cached upgrade choices. A recorded legal Standard victory uses the actual 25 thresholds, budgets, starting state, and full catalog; its replay requires no injected dice or modified difficulty. A separate legal loss verifies exhaustion.

The reference disassembly findings and pool mapping are retained under `Docs/Reference/Rewards`. The temporary search harness and engine validation outputs remain local under `Docs/work`.

The recorded victory uses seed 60 and 264 accepted actions: 67 hand plays, 140 rerolls, 24 modifier selections, 24 upgrades, and 9 refreshes. It clears all 25 stages with a total score of 80,561. A seed 0 run without rerolls loses through resource exhaustion on stage five after 13 hands. These traces demonstrate reachable outcomes; they are not a balance evaluation.
