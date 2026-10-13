# Modifier reference findings

Source: Dice of Kalma ARM64 IL2CPP binary and metadata extracted from the APKs in `Docs/Reference/DiceOfKalma/Inputs`; native evidence is preserved in `Docs/Reference/DiceOfKalma`. Addresses below are native virtual addresses. The integration intentionally supports neutral/custom encounters; this is not a claim of reproducing the reference's complete initialization RNG sequence or challenge modes.

## Resolver sequence and resources

- `BattleHandler.CheckSkullEffects` is `0x18af3e0`. It scans inventory slots in ascending index: index initialization `0x18afa80`, slot fetch `0x18afab4-0x18afacc`, increment `0x18bfa68`.
- It accumulates effects in local totals and applies them after the full scan (`0x18bfcac-0x18bfccc`). Every predicate sees the same resources before modifier grants. Earlier reroll grants cannot change later eligibility in that phase.
- `RerollBtnPress` increments hand rerolls spent and decrements remaining rerolls at `0x18ab6d0-0x18ab6f4`; modifier evaluation occurs later at `0x18e47b4`. Reroll predicates use post-cost rerolls and the incremented spent count.
- `PlayHand` starts the points coroutine, whose state-zero jump goes to `0x18db9e0` (jump table `0x9af820`). It yields at `0x18dba44-0x18dba78`. `PlayHand` then decrements hands at `0x18ab43c-0x18ab444`; play modifiers are evaluated later at `0x18e2ff0`.
- The resolver defines local H = saved hands remaining, subtracting one only for a play preview, and Hplus1 = H + 1 (`0x18af9f4-0x18afa9c`). Thus H is post-play hands during play, current hands during initial/reroll phases.
- Triggers 33, 35, 39 and 52 use Hplus1: `0x18b5734`, `0x18b5d04/0x18b5ecc`, `0x18b6d70/0x18b6db0/0x18b6df4`, `0x18b9498`.
- Trigger 62 checks H odd: load H `0x18bb468`, branch to failure if bit zero at `0x18bb46c`. This means pre-play hands even.

## Activation identity and lifetime

- `RunSaveData.rerollSkullsActivatedThisTurn` is a saved list at offset `0x80`. The resolver tests Contains(slotIndex) at `0x18afb50-0x18afb70` and adds the slot index on successful actual activation at `0x18bdf3c-0x18bdf80`.
- This is owned-slot identity, not definition or family identity. All actual effects share this once-per-hand gate.
- `RerollDiceCycle` replaces the list at `0x18e4890` only on an initial roll. The ordinary-reroll branch at `0x18e45b4-0x18e45b8` skips to `0x18e48d4`, retaining the list.
- No separate native stage activation counter was found. Mercy's once-per-level behavior follows from its final-hand predicate plus the hand limit. Generic engine stage limits are an explicit extension.

## Score accumulators

- Saved bones/multiplier offsets are `0x30/0x34`. Initial and reroll contributions are added at `0x18bfcac-0x18bfcc0`, and ordinary rerolls retain them.
- Play adds die faces to existing bones at `0x18dbbd4-0x18dbbe4` and category/upgrades at `0x18ddac8-0x18ddad4` plus multiplier paths.
- `ResetScoreCounting` (`0x18abe50`) zeroes bones, multiplier and hand rerolls spent (`0xd0`) at `0x18abeb8-0x18abebc`. The scoring cycle calls it at `0x18dc46c` after score/statistic processing. New run defaults also clear bones/multiplier at `0x18a9ea4`.

## First history and initial predicates

- New-run `prevPlayedDice` (`0x88`) is explicitly `[1,2,3,4,6]`: array load `0x18aa0dc`, writes `0x18aa108/118/128/138/148`. Triggers 10/11/12 compare against that history without a has-previous guard; trigger 10 evidence is `0x18b1188-0x18b11b8`. The initial comparison category is HighDie.
- Demon 47 instead compares current dice against `prevDice` (`0x38`) at `0x18b84b8-0x18b84e8`. On subsequent initial rolls, that is the preceding played hand: reroll-cycle setup copies old current dice to prevDice at `0x18e4c28-0x18e4c60`.
- New-run initialization generates random current dice and copies them into initial/previous dice (`0x18a9f04-0x18a9fe8`). This is not the fixed previous-play array. Daily/cursed modes additionally suppress Demon until aggregate hands played is positive (`0x18b84ec-0x18b852c`).
- **Custom first-hand Demon policy:** when no prior played hand exists, condition 47 returns false. This is a deliberate custom-encounter definition, not a claim that native Standard compares its first initial hand against an absent history. Native Standard ordinarily starts without owned modifiers.
- Initial-roll families are Guard 6, Doctor 38, Fisher 46, Demon 47, Yule 49, Astronaut 53 and Musketeer 71.
- Guard: H == 1 (`0x18b08e0-0x18b08f4`).
- Doctor: first hand of the stage; Standard compares H with that stage's initial hand budget (`0x18b6aa0-0x18b6aac`).
- Fisher: Pair only, not HighDie or Pair (`0x18b8354-0x18b8360`).
- Yule: stage depth >= 2 (`0x18b89b4-0x18b89bc`), previous remaining hands <= 0 (`0x18b89e4-0x18b89ec`), and first hand of the new stage (`0x18b8a5c-0x18b8a70`). It cannot activate in the first stage.
- Astronaut: stage depth >= 2 (`0x18b9958-0x18b9960`), previous unused hands >= half the previous starting budget (`0x18b996c-0x18b99f0`), and first hand of the new stage (`0x18b9a60-0x18b9a74`). It cannot activate in the first stage.
- Musketeer: Standard uses H == 2 in stages 1-20 and H == 3 from stage 21 (`0x18bd710-0x18bd840`). The extracted starting budgets are 3, 4 and 5 for stages 1-10, 11-20 and 21 onward. Generalized neutral behavior is H == (startingHands + 1) / 2, integer division. With four starting hands, this is the third hand.
- Mercy 54: H == 1 and post-cost rerolls == 0 (`0x18b9e04-0x18b9e48`), evaluated after a player reroll. The native hand activation list prevents another activation during that last hand.

## Scope of independent review

Reviewed staged session, contracts, scoring and resolver plus deployed modifier definitions/evaluator. Frozen resource facts, transactional previews, contribution persistence, owned-instance attribution and hand/stage reset boundaries were consistent. Reported one concrete mismatch: trigger 62 initially tested post-play H even; native requires H odd. Catalog author was notified to correct it and its fixtures.

Challenge-specific branch behavior and complete initialization RNG fidelity are not established by this report. Native evidence distinguishes intentional custom-first-47 behavior from reference behavior.
