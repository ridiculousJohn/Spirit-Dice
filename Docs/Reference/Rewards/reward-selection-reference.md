# Standard reward selection reconstruction

Reference version: Dice of Kalma 1.6.3. Source evidence is the extracted design JSON and native ARM64 functions under `Docs/Reference/DiceOfKalma`. This document records provenance; runtime definitions use mechanical identifiers only.

## Modifier offer pools

`DatabaseObject.ReturnRandomItemOfColor` at 0x1927734 constructs candidates for a requested source role. It filters source meta progression, owned exact Item identity, blank Item, and excluded source family identity. All 296 extracted source variants have meta progression level zero. Normal authoring therefore contains 73 eligible modifiers per role, with the remaining family reserved as fallback.

The explicit pool membership in `reward-pools.json` is generated from each extracted variant's source role, joined to the modifier manifest by source asset identity. Runtime selection reads pool member IDs directly. It does not infer roles from modifier ID suffixes.

| Mechanical pool | Source role used only for authoring | Normal members | Exhausted fallback |
| --- | --- | ---: | --- |
| reward_pool.base_score | Blue | 73 | modifier.play_hand.never.base_score |
| reward_pool.multiplier | Red | 73 | modifier.play_hand.never.multiplier |
| reward_pool.combined | Purple / green asset suffix | 73 | modifier.play_hand.never.combined |
| reward_pool.rerolls | Yellow | 73 | modifier.play_hand.never.rerolls |

The draw at 0x1927fb4-0x1927fd4 calls Random.Next(0, candidate count); no weighted draw is present. Owned exclusion removes the exact modifier variant, so another variant of the same family can still be acquired in a later reward. Within one offer, different functional slots exclude already offered families.

Candidate count <= 2 at 0x1927c24-0x1927c28 bypasses historical exclusions for that slot. The rebuilt candidate scan at 0x1927cd8-0x1927f14 still excludes owned exact variants and current-offer families. The native branch clears ExpandedSaveData's list at +0x50, but does not clear RunSaveData.excludedLoot at +0xa0. Thus this slot can show a previously excluded family while later slots still consult the existing transient history. The engine retains supplied history and performs the same slot-only bypass. A zero count after rebuilding yields the source-role blank at 0x1927f20-0x1927f50.

GenerateLoot reads ExpandedSaveData +0x50 at 0x1904798, 0x19047d0 and 0x1904818, copying those existing entries into RunSaveData +0xa0. No append to Expanded +0x50 occurs in this function. Standard TakeLoot clears transient Run history after acceptance and skips the subsequent Cursed-only expanded-list writes. Standard encountered-offer history therefore persists across refreshes of one reward, and clears on acceptance. Account history inherited from other modes is outside this engine's Standard encounter scope.

## Offer cadence and economy

ConstantStorage.loopingLootOptions is [3, 6, 6, 6], verified from the static blob `A1CB9CF8A0C9119BC00C5F61A4D867E399324FD35161E77EC453DAF866B2D0F1` in constants.json. Source LootType 3 requests three reroll-role items, while type 6 requests base score, multiplier and combined roles. GenerateLoot indexes this array with `(depth - 1) % length` at 0x190422c-0x190424c.

The stage-clear coroutine increments depth before generating rewards. The first Standard interstage reward therefore uses cycle index 1 (incoming stage 2), and the reroll-only layout first appears after clearing stage 4. Standard has no fresh-run opening reward. These facts are corroborated in `Docs/Reference/Rewards/standard-reward-flow.md`.

The authored policy starts with one refresh token, grants one on each nonfinal clear, and charges one per modifier refresh. It requests four hand upgrade choices. The final Standard stage gives no subsequent reward or refresh grant.

## Hand upgrade generation

The engine maps the eight functional native hand categories to its eight existing HandCategory values; unused source categories remain absent. Standard upgrade caps are six stored upgrades for each functional category.

SelectHandsForUpgrade excludes the three most recent choices through incoming stage 16, the two most recent through stage 21, then none. History is ordered most recent first. If fewer than four candidates remain, it retries with caps only. With <= 3 candidates after that retry, native takes the valid candidate rows in category order without Random.Next; the engine returns that shorter sorted list. All capped categories produce an empty result.

For >= 4 candidates, native draws four independent uniform indices and redraws the entire tuple on duplicate indices, then sorts them at 0x1900b20. The engine draws uniformly without replacement and sorts chosen categories. The choice distribution matches; random draw consumption intentionally follows the deterministic engine's sampling implementation rather than native System.Random's rejected tuples. Exactly four eligible categories still consume random draws in both implementations.

## Authoring verification

`audit_reward_catalog.py` verifies all four 73-member pools and fallback IDs, all 296 preexisting modifier IDs, all eight hands and 25 stages, and the four cyclic layouts. It reconstructs the prior catalog text by removing inserted reward authoring and the catalog version change, then compares it to the earlier modifier authoring artifact with its already approved identity/version corrections. The original artifact is not modified. The catalog version is `standard.rewards.v1`.

The deployed file hashes are in `deployed-hashes.json`. Unity compilation and edit-mode integration tests are run by the parent task; this report does not claim their results.
