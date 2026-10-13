# Standard reward and hand-upgrade reference findings

Scope: native ARM64 evidence in Docs/Reference/DiceOfKalma. Read-only game analysis. The separate catalog agent owns exact modifier offer roles, candidate filtering, and weighted selection. Addresses below are native virtual addresses.

## Flow and depth

- Fresh Standard starts with no modifier reward. Start.MoveNext's sole OpenLootView call at 0x18eb548 is guarded by BattleHandler.cursedInUse (+0x313) at 0x18eb4b4-0x18eb4b8. Standard bypasses it. Normal fresh gameplay calls RerollDiceCycle(initialRoll=true) at 0x18e9ec0-0x18e9ecc.
- Start resets inventory at 0x18e97e8 via Inventory.ResetAllSlots. That clears item pointers and IDs at 0x19288b0-0x19288c4. The serialized PlayerInv (design-assets.json, sharedassets1.assets, id652) has 200 empty inventory slots and four loot slots. Standard adds selected loot into the first empty inventory slot: Inventory.AddItemFromGround scans at 0x19282cc-0x1928314 and assigns at 0x1928330-0x1928398. It does not replace or merge families. No graceful full-capacity path is visible in this function; 200 slots exceed the 24 Standard interstage acquisitions.
- A successful stage increments RunSaveData.levelDepth (+0x5c) at 0x18ddfec-0x18ddffc BEFORE reward generation. OpenLootView loads that incremented depth at 0x18df0dc and calls at 0x18df108. SelectHandsForUpgrade loads the same depth at 0x18e2098 and calls at 0x18e20ac.
- GenerateLoot indexes loopingLootOptions with (depth-1)%length at 0x190422c-0x190424c. The catalog agent verified the array [3,6,6,6]. Therefore first interstage reward after clearing stage1 uses incoming depth2, and first reroll-role offer layout after clearing stage4 uses incoming depth5. There is no Standard depth1 opening reward.
- Modifier selection precedes hand upgrade selection. The coroutine waits for InventoryVisualizer.lootViewIsOpen (+0x70) to clear at 0x18ddcc0-0x18ddce8. It then records GameState.LootingHands=2 at 0x18ddd78-0x18ddd80. It waits for HandsSyncToUI.handsLootViewIsOpen (+0xa0) at 0x18db528-0x18db548. Both offer sets are generated before user selection; upgrade generation follows modifier generation at 0x18e2038-0x18e20ac.
- Final Standard clear bypasses ordinary rewards and refresh grant. After depth increment, 0x18e1538-0x18e1580 checks currentDepth >= standardGameLength and prevDepth == standardGameLength. Standard, non-endless takes 0x18e1608-0x18e160c to the end-run branch at 0x18e24a0, before the grant and offer blocks. Thus clearing stage25 produces no stage26 modifier offer, hand upgrade, or refresh token.

## Refresh economy

- RunSaveData.skullRerolls is +0x74; prevSkullRerolls is +0x78; spent is +0xe0.
- ResetRunSaveDataToDefaults assigns 1 token at 0x18aa050-0x18aa054. Tutorial/cursed override paths set zero; they are out of scope. Previous token display balance initializes1 at 0x18aa0d8-0x18aa0e0.
- Each nonfinal Standard stage clear adds exactly1 to the existing balance at 0x18e1b74-0x18e1b80. The balance carries; there is no replacement or cap there. Previous balance is copied at 0x18e1970-0x18e1974.
- The apparent legacy unused-hands reward block at 0x18e1a24-0x18e1a38 is gated by coroutine.remainingHandsAreRewarded (+0x4c), explicitly set false at 0x18e1934. The current branch therefore adds one irrespective of unused hands.
- SkullRollBtnPress requires balance >0 at 0x18aba20-0x18aba30 (debug override omitted), subtracts1 at 0x18aba7c-0x18aba88, increments spent at 0x18abab0-0x18abab8, then calls DoSkullReroll at 0x18abb78. Its coroutine calls GenerateLoot(fromReroll=true) at 0x190a3ac. Refresh price does not increase.

## Hand upgrade eligibility, choices, and application

- Hand levels reside in RunSaveData.handUpgrades (+0x60). Last chosen indices are +0x68,+0x6c,+0x70, initially -1 at 0x18aa0d4-0x18aa0f0.
- Constant maxHandUpgrades is [7,7,7,7,0,7,0,7,7,7]. These are counts of levels, not maximum stored indices. SelectHandsForUpgrade rejects currentLevel >= maxHandUpgrades[index]-1 at 0x1900214-0x1900220. Eligible ordinary categories have stored levels0..5, and application reaches maximum6. Categories4/6 also explicitly excluded by (index|2)==6 at 0x1900224-0x190022c.
- Selection depth is incoming stage depth. Depth <=16 excludes all last three chosen indices (0x19002d0,0x1900300,0x1900330 comparisons). Depth17..21 excludes the latest two (0x1900270-0x19002a4). Depth>=22 excludes none (0x1900238-0x1900244).
- If candidate count <=3, discard history exclusions and rebuild using caps and category exclusions only: 0x19003a8-0x19003b0 routes to 0x1900420-0x1900534. Thus relax history whenever fewer than four candidates remain, not merely when pool is empty.
- Target offer count is4. With >=4 candidates, draw four independent uniform candidate indices with System.Random.Next(0,count), reject the entire tuple if any pair matches, and redraw until distinct (0x190068c-0x19008c8). Sort indices at 0x1900b20 before displaying. This is uniform sampling without replacement, displayed in category order.
- With <=3 capped-eligible candidates, indices0,1,2,3 are used and unavailable rows blocked (0x19008d0-0x1900b08). Valid displayed choices are the existing candidates in category order. Native code still attempts out-of-range slots in its display loop and catches/logs them; it retries recursively up to errorLimit3 (0x1900e90-0x1900ed0; constructor0x1901d84-0x1901d90). This UI artifact should not be interpreted as extra valid offers.
- All caps: PointsCountCycle scans for any level < max-1 at 0x18dc07c-0x18dc08c, and clears/hides the upgrade window when none qualify at 0x18e2114-0x18e2148. Resolver semantics: no upgrade selection required when capped pool is empty.
- Choosing an upgrade calls BattleHandler.UpgradeHand. It shifts history: latest into second, second into third, new index into latest (0x18aef4c-0x18aef58), then increments the chosen level by1 at 0x18aef74-0x18aef7c. Application itself lacks cap validation; offer eligibility enforces it in source.
- Scoring effect is +5 base and +1 multiplier per stored level, except FiveOfAKind (category9), which gets +2 multiplier. Base: 0x18ddac8-0x18ddad0. PointsCountCycle checks category9 at 0x18ddb74-0x18ddb78, uses doubled level at 0x18ddc18 only for that branch, and adds one level at 0x18de7d0 for all other categories. Preview independently checks9 at 0x18c1928, doubles at 0x18c19a0, and adds one level at 0x18c1a24 otherwise. UpgradeHand changes only that category's level; no separate currency cost.
- UpgradeHandBtnPress with negative selected index skips the UpgradeHand call at 0x1901118-0x190111c and closes selection at 0x1901168-0x1901178. This is the source handler's skip mechanism; whether the authored UI exposes voluntary skip for nonempty choices has not been independently verified.

## Exclusion history coordination

- TakeLoot clears transient RunSaveData.excludedLoot (+0xa0) at 0x1908bc0-0x1908bd0. This does NOT mean all offer history resets on acceptance.
- ExpandedSaveData.vacantIntList2 (+0x50) is copied into transient exclusion by GenerateLoot and reset by low-candidate fallback. Subsequent catalog-agent verification found no Standard append of shown offers to this Expanded list. Standard TakeLoot leaves it intact, and its Expanded writes are guarded by cursedInUse at 0x1908c3c-0x1908c40. Therefore ordinary Standard shown-family history persists across refreshes within one reward, then clears on acceptance. Existing account state from other modes is outside scope. Exact filtering/weighting belongs to the catalog agent's report.

## Explicit limits

- This is Standard-mode mechanical reconstruction. Tutorial, daily, cursed, challenge, ads, cloud persistence, and UI behavior are not engine requirements here.
- Ordinary Standard cannot reach inventory capacity. Native full-inventory behavior is not a robust gameplay contract.
- Exhausted upgrade pool mechanically skips; the native renderer has exception recovery for missing rows rather than clean shortened lists. A compact eligible offer list preserves meaningful choices without reproducing UI faults.
- Modifier role counts/weights, owned exclusions, history fallback, and no-op fallback are covered by the catalog agent, not re-derived in this report.


