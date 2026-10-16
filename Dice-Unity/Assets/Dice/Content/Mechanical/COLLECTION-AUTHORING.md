# Collection content

`Standard.asset` authors each spirit's six `unlockRewardModifierIds` and its stage
3, 6, and 10 unlock opportunities. Each opportunity chooses one still-locked entry
from that same list. Fox uses Base and Mult variants of Open Gate, Still Reflection,
and Rising Sun. Owl and moth use Base and Mult variants of their three established
milestone families; these lists are provisional.

Each milestone retains its four legacy `modifierIds` for validating existing run
saves. Those IDs do not select the contents of new unlock offers.

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
