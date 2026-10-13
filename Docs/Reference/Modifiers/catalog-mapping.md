# Modifier mechanical catalog mapping

74 families, 296 variants; 73 functional families plus one no-op family. Effect triples are (base score, multiplier, rerolls). All source variants use OncePerHand, including the final-reroll family. Engine IDs contain only phase, condition mechanism, and effect role.

d[0..4] means the five dice in physical order. H means HandsRemaining after play consumption, R means remaining rerolls after reroll cost; source hand-resource predicates 33/35/39/52/62 use H+1. Rank comparison defaults to HighDie only for source triggers 10/11/12; initial rank comparison requires actual history in custom starts.

Native checked means the branch comparison was inspected. Enum/text means the extracted enum and localized description agree, but the native branch was not exhaustively reconstructed. All effect amounts and phases are asset-exact.

| Source family | ID family | Phase | Trigger | Explicit predicate | Four role effect triples | Evidence |
|---|---|---|---:|---|---|---|
| Ace Skull | modifier.play_hand.three_smallest_sum_at_most_maximum | Play hand | 57 | sum(three smallest faces) <= max(d) | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Native checked |
| Archer Skull | modifier.play_hand.range_less_than | Play hand | 4 | max(d) - min(d) < 3 | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Enum/text |
| Astronaut Skull | modifier.initial_roll.previous_stage_at_most_half_hands_played | Initial roll | 53 | first hand and previous stage exists and 2 * previous remaining hands >= previous starting hands | base_score:(30,0,0); multiplier:(0,6,0); combined:(15,3,0); rerolls:(0,0,3) | Native checked |
| Automaton Skull | modifier.player_reroll.selected_matches_hands_remaining | Player reroll | 22 | any(selected and face == HandsRemaining) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Bachelor Skull | modifier.play_hand.second_fourth_product_at_least_sum | Play hand | 58 | d[1] * d[3] >= sum(d) | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Bandaged Skull | modifier.play_hand.sum_less_than_initial | Play hand | 5 | sum(d) < sum(initial) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Bettor Skull | modifier.player_reroll.rank_equals_before_reroll | Player reroll | 13 | rank(d) == rank(beforeReroll) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Blank Skull | modifier.play_hand.never | Play hand | 56 | false | base_score:(0,0,0); multiplier:(0,0,0); combined:(0,0,0); rerolls:(0,0,0) | Enum/text |
| Blind Skull | modifier.play_hand.no_dice_rerolled | Play hand | 0 | RerollsSpentOnHand == 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Bolted Skull | modifier.play_hand.adjacent_matching_triple | Play hand | 19 | any three consecutive faces equal | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Enum/text |
| Bomb Skull | modifier.player_reroll.exactly_one_selected_matches_selected_count | Player reroll | 31 | count(selected and face == selected_count) == 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Candle Skull | modifier.play_hand.out_of_rerolls | Play hand | 3 | RerollsRemaining == 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Captain Skull | modifier.player_reroll.exactly_one_selected_face | Player reroll | 2 | count(selected and face == 1) == 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Carnival Skull | modifier.player_reroll.adjacent_difference_at_least | Player reroll | 23 | all(abs(adjacent difference) >= 2) | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Native checked |
| Chef Skull | modifier.play_hand.sum_less_than | Play hand | 40 | sum(d) < 15 | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Enum/text |
| Chieftain Skull | modifier.play_hand.interior_contains_pair | Play hand | 64 | d[1] == d[2] OR d[1] == d[3] OR d[2] == d[3] | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Choked Skull | modifier.play_hand.largest_group_equals_hands_or_rerolls | Play hand | 35 | max_frequency(d) == RerollsRemaining OR HandsRemaining + 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Clairvoyant Skull | modifier.player_reroll.selected_matching_group_at_least | Player reroll | 8 | max_frequency(selected dice) >= 3 | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Enum/text |
| Dj Skull | modifier.play_hand.edge_sum_equals_either | Play hand | 36 | d[0] + d[4] in {6,8} | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Native checked |
| Daredevil Skull | modifier.play_hand.rank_above_previous | Play hand | 11 | rank(d) > rank(previousPlayed, default HighDie) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,2) | Native checked |
| Dealer Skull | modifier.player_reroll.rank_below_before_reroll | Player reroll | 55 | rank(d) < rank(beforeReroll) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Demon Skull | modifier.initial_roll.initial_rank_above_previous | Initial roll | 47 | previousPlayed exists and rank(initial) > rank(previousPlayed) | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Doctor Skull | modifier.initial_roll.first_hand | Initial roll | 38 | HandsPlayedInStage == 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Drilled Skull | modifier.play_hand.interior_pair_matches_edge | Play hand | 72 | any face on either edge occurs at least twice among the three interior dice | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Native checked |
| Drinking Skull | modifier.play_hand.extreme_equals_hands_remaining | Play hand | 39 | min(d) == HandsRemaining + 1 OR max(d) == HandsRemaining + 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Executioner Skull | modifier.play_hand.adjacent_different | Play hand | 18 | all adjacent faces differ | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Fisher Skull | modifier.initial_roll.initial_pair | Initial roll | 46 | rank(initial) == Pair | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Florist Skull | modifier.play_hand.adjacent_difference_at_most | Play hand | 44 | all(abs(adjacent difference) <= 1) | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Enum/text |
| Fractured Skull | modifier.play_hand.high_die | Play hand | 17 | rank(d) == HighDie | base_score:(30,0,0); multiplier:(0,6,0); combined:(15,3,0); rerolls:(0,0,3) | Enum/text |
| Fungal Skull | modifier.player_reroll.high_die_or_straight | Player reroll | 26 | rank(d) in {HighDie,Straight} | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Gambler Skull | modifier.play_hand.rank_below_previous | Play hand | 10 | rank(d) < rank(previousPlayed, default HighDie) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,2) | Native checked |
| Gamer Skull | modifier.play_hand.edge_sum_equals_hands_or_rerolls | Play hand | 52 | d[0] + d[4] == RerollsRemaining OR HandsRemaining + 1 | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Geek Skull | modifier.player_reroll.selected_interior_equals_neighbor_sum | Player reroll | 29 | any selected i in 1..3: d[i] == d[i-1] + d[i+1] | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Guard Skull | modifier.initial_roll.last_hand | Initial roll | 6 | HandsRemaining == 1 | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Enum/text |
| Hacker Skull | modifier.play_hand.sum_prime | Play hand | 66 | sum(d) in {5,7,11,13,17,19,23,29} | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Half Skull | modifier.play_hand.edge_pair_differences_equal | Play hand | 63 | abs(d[0]-d[1]) == abs(d[3]-d[4]) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Inventor Skull | modifier.play_hand.interior_equals_neighbor_difference | Play hand | 59 | any i in 1..3: d[i] == abs(d[i-1] - d[i+1]) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Jester Skull | modifier.player_reroll.selected_face_unchanged | Player reroll | 7 | any(selected[i] and d[i] == beforeReroll[i]) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Jolly Skull | modifier.play_hand.minimum_equals_rerolls_spent | Play hand | 32 | min(d) == RerollsSpentOnHand | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Native checked |
| Judge Skull | modifier.play_hand.no_extreme_faces | Play hand | 69 | no face 1 and no face 6 | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Knight Skull | modifier.play_hand.hands_remaining_even | Play hand | 62 | (HandsRemaining + 1) % 2 == 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Machine Skull | modifier.play_hand.contains_rerolls_remaining | Play hand | 21 | any(face == RerollsRemaining) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Masque Skull | modifier.play_hand.rerolls_exceed_hands_remaining | Play hand | 33 | RerollsRemaining > HandsRemaining + 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Merchant Skull | modifier.player_reroll.ordered | Player reroll | 16 | all adjacent <= OR all adjacent >= | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Mercy Skull | modifier.player_reroll.last_reroll_on_last_hand | Player reroll | 54 | HandsRemaining == 1 and RerollsRemaining == 0 | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Native checked |
| Miner Skull | modifier.play_hand.same_parity | Play hand | 50 | all faces odd OR all faces even | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Enum/text |
| Musketeer Skull | modifier.initial_roll.middle_hand | Initial roll | 71 | HandsRemaining == (StartingHands + 1) / 2 using integer division | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Nailed Skull | modifier.play_hand.edges_match_interior | Play hand | 67 | d[0] == d[4] and any interior face equals d[0] | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Enum/text |
| Nerd Skull | modifier.play_hand.interior_equals_neighbor_sum | Play hand | 28 | any i in 1..3: d[i] == d[i-1] + d[i+1] | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Occult Skull | modifier.play_hand.sum_divisible_by | Play hand | 14 | sum(d) % 5 == 0 | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Enum/text |
| Officer Skull | modifier.play_hand.face_equals_position | Play hand | 37 | any d[i] == i+1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Opera Skull | modifier.play_hand.two_pair | Play hand | 41 | rank(d) == TwoPair | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Native checked |
| Party Skull | modifier.play_hand.alternating_parity | Play hand | 27 | all adjacent faces have different parity | base_score:(25,0,0); multiplier:(0,5,0); combined:(15,2,0); rerolls:(0,0,2) | Enum/text |
| Pilot Skull | modifier.play_hand.edge_sum_greater_than_interior | Play hand | 45 | d[0] + d[4] > d[1] + d[2] + d[3] | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Enum/text |
| Pirate Skull | modifier.play_hand.contains_face | Play hand | 1 | count(face == 1) > 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Pot Skull | modifier.play_hand.sum_greater_than | Play hand | 61 | sum(d) > 22 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Native checked |
| Princess Skull | modifier.play_hand.rerolls_spent_at_least | Play hand | 43 | RerollsSpentOnHand >= 3 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Queen Skull | modifier.play_hand.unique_count_less_than_rerolls_spent | Play hand | 68 | unique_face_count < RerollsSpentOnHand | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Enum/text |
| Ritual Skull | modifier.play_hand.interior_sum_divisible_by | Play hand | 51 | (d[1] + d[2] + d[3]) % 3 == 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Royal Skull | modifier.play_hand.contains_upper_face | Play hand | 9 | count(face == 6) > 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Scholar Skull | modifier.play_hand.ordered | Play hand | 16 | all adjacent <= OR all adjacent >= | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Native checked |
| Shaman Skull | modifier.play_hand.odd_sum_greater_than_even_sum | Play hand | 65 | sum(odd faces) > sum(even faces) | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Shot Skull | modifier.play_hand.middle_unique | Play hand | 25 | count(face == d[2]) == 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Spiked Skull | modifier.play_hand.middle_equals_odd_count | Play hand | 60 | d[2] == count(odd faces) | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Enum/text |
| Split Skull | modifier.play_hand.edge_pair_sums_equal | Play hand | 15 | d[0] + d[1] == d[3] + d[4] | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Enum/text |
| Swindler Skull | modifier.play_hand.rank_equals_previous | Play hand | 12 | rank(d) == rank(previousPlayed, default HighDie) | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Native checked |
| Tactical Skull | modifier.play_hand.range_greater_than | Play hand | 48 | max(d) - min(d) > 3 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Trench Skull | modifier.play_hand.maximum_at_most_rerolls_remaining | Play hand | 34 | max(d) <= RerollsRemaining | base_score:(15,0,0); multiplier:(0,3,0); combined:(5,2,0); rerolls:(0,0,2) | Enum/text |
| Trinity Skull | modifier.play_hand.positive_rerolls_divisible_by | Play hand | 24 | RerollsRemaining > 0 and RerollsRemaining % 3 == 0 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Vampire Skull | modifier.play_hand.exactly_one_face | Play hand | 20 | count(face == 2) == 1 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,2) | Enum/text |
| Virtual Skull | modifier.player_reroll.selected_sum_equals_either | Player reroll | 42 | sum(selected dice) in {8,10} | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Weeping Skull | modifier.play_hand.every_die_rerolled | Play hand | 30 | EverRerolledMask == 31 | base_score:(10,0,0); multiplier:(0,2,0); combined:(5,1,0); rerolls:(0,0,1) | Enum/text |
| Welder Skull | modifier.play_hand.middle_faces_count_at_least | Play hand | 70 | count(face 3 or 4) >= 4 | base_score:(15,0,0); multiplier:(0,3,0); combined:(10,1,0); rerolls:(0,0,2) | Native checked |
| Yule Skull | modifier.initial_roll.previous_stage_exhausted_hands | Initial roll | 49 | first hand and previous stage exists and previous stage remaining hands == 0 | base_score:(20,0,0); multiplier:(0,4,0); combined:(10,2,0); rerolls:(0,0,2) | Native checked |

## Fidelity notes

- Native enum names for 31,33,35,36,39,41,46,61 are stale relative to branch behavior. Preserve native comparisons, not enum wording.
- Ordered includes ties; adjacency is linear with no edge wrapping. Pair matching an edge requires a same-valued interior pair.
- Source first initial-rank comparison uses an earlier random hand; source Standard begins without modifiers so this is unobservable. Custom starting inventory uses false without a preceding played hand.
- Starting budgets 3/4/5 produce middle hand remaining budgets 2/2/3, selecting the later of two center hands for even hand counts.
- Four no-op definitions preserve 296 variant inventory identities; each has no effects and RewardEligible false. Their mechanical suffixes retain variant role identity without presentation labels.
