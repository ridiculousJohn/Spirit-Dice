using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    internal static class EncounterPersistence
    {
        internal static EncounterSaveData Capture(MechanicalCatalog catalog, EncounterDefinition definition, EncounterState state)
        {
            var save = new EncounterSaveData
            {
                SaveFormatVersion = EncounterSaveData.CurrentFormatVersion,
                RulesVersion = EncounterSession.RulesVersion,
                RandomAlgorithmVersion = DeterministicRandom.AlgorithmVersion,
                CatalogVersion = catalog.Version,
                EncounterId = definition.Id,
                Revision = state.Revision, Phase = state.Phase, Outcome = state.Outcome,
                StageIndex = state.StageIndex, HandIndex = state.HandIndex,
                RemainingThreshold = state.RemainingThreshold, HandsRemaining = state.HandsRemaining,
                RerollsRemaining = state.RerollsRemaining,
                CurrentDice = state.CurrentDice.ToArray(), InitialDice = state.InitialDice.ToArray(),
                DiceBeforeLastReroll = state.DiceBeforeLastReroll?.ToArray(),
                PreviousPlayedDice = state.PreviousPlayedDice?.ToArray(),
                LatestRerollMask = state.LatestRerollMask, LatestChangedMask = state.LatestChangedMask,
                EverRerolledMask = state.EverRerolledMask, RerollsSpentOnHand = state.RerollsSpentOnHand,
                HandsPlayedInStage = state.HandsPlayedInStage,
                HasPreviousStage = state.PreviousStageStartingHands.HasValue,
                PreviousStageStartingHands = state.PreviousStageStartingHands ?? 0,
                PreviousStageHandsRemaining = state.PreviousStageHandsRemaining ?? 0,
                TotalScore = state.TotalScore, TotalHandsPlayed = state.TotalHandsPlayed,
                TotalRerollsSpent = state.TotalRerollsSpent,
                HandUpgrades = (int[])state.HandUpgrades.Clone(),
                DiceRandomState = state.DiceRandom.State, RewardRandomState = state.RewardRandom.State,
                ModifierBaseScore = state.ModifierBaseScore, ModifierMultiplier = state.ModifierMultiplier,
                RewardRefreshes = state.RewardRefreshes, NextOfferId = state.NextOfferId, NextOptionId = state.NextOptionId,
                ExcludedRewardFamilies = state.ExcludedRewardFamilies.ToArray(),
                RecentHandUpgrades = state.RecentHandUpgrades.ToArray(),
                PendingUpgradeCategories = state.PendingUpgradeCategories?.ToArray(),
                Modifiers = new EncounterSaveModifierData[state.Modifiers.Length],
                ModifierContributions = new EncounterSaveContributionData[state.ModifierContributions.Count]
            };
            for (int i = 0; i < save.Modifiers.Length; ++i)
            {
                var value = state.Modifiers[i];
                save.Modifiers[i] = new EncounterSaveModifierData
                {
                    InstanceId = value.InstanceId, DefinitionId = value.DefinitionId,
                    ActivationsThisHand = value.ActivationsThisHand, ActivationsThisStage = value.ActivationsThisStage,
                    TotalActivations = value.TotalActivations
                };
            }
            for (int i = 0; i < save.ModifierContributions.Length; ++i)
            {
                var value = state.ModifierContributions[i];
                save.ModifierContributions[i] = new EncounterSaveContributionData
                {
                    InstanceId = value.InstanceId, DefinitionId = value.DefinitionId, Phase = value.Phase,
                    BaseScore = value.BaseScore, Multiplier = value.Multiplier, Rerolls = value.Rerolls
                };
            }
            if (state.PendingReward != null)
            {
                var offer = state.PendingReward;
                save.PendingReward = new EncounterSaveRewardData
                {
                    OfferId = offer.OfferId, Kind = offer.Kind, TargetStageIndex = offer.TargetStageIndex,
                    Options = new EncounterSaveRewardOptionData[offer.Options.Count]
                };
                for (int i = 0; i < offer.Options.Count; ++i)
                {
                    var option = offer.Options[i];
                    save.PendingReward.Options[i] = new EncounterSaveRewardOptionData
                    {
                        OptionId = option.OptionId, DefinitionId = option.DefinitionId,
                        HasCategory = option.Category.HasValue, Category = option.Category ?? default
                    };
                }
            }
            return save;
        }

        internal static bool TryRestore(MechanicalCatalog catalog, EncounterSaveData save,
            out EncounterDefinition definition, out EncounterState state, out EncounterRestoreError error, out string detail)
        {
            definition = null;
            state = null;
            error = EncounterRestoreError.None;
            detail = null;
            if (save == null) return Fail(EncounterRestoreError.NullSave, "The save is missing.", out error, out detail);
            if (save.SaveFormatVersion != EncounterSaveData.CurrentFormatVersion)
                return Fail(EncounterRestoreError.SaveFormatVersionMismatch, "Save format version mismatch.", out error, out detail);
            if (save.RulesVersion != EncounterSession.RulesVersion)
                return Fail(EncounterRestoreError.RulesVersionMismatch, "Rules version mismatch.", out error, out detail);
            if (save.RandomAlgorithmVersion != DeterministicRandom.AlgorithmVersion)
                return Fail(EncounterRestoreError.RandomAlgorithmVersionMismatch, "Random algorithm version mismatch.", out error, out detail);
            if (!string.Equals(save.CatalogVersion, catalog.Version, StringComparison.Ordinal))
                return Fail(EncounterRestoreError.CatalogVersionMismatch, "Catalog version mismatch.", out error, out detail);
            try { definition = catalog.GetEncounter(save.EncounterId); }
            catch (ArgumentException) { return Fail(EncounterRestoreError.EncounterNotFound, "Encounter ID is invalid.", out error, out detail); }
            catch (KeyNotFoundException) { return Fail(EncounterRestoreError.EncounterNotFound, "Encounter ID is missing from the catalog.", out error, out detail); }
            try
            {
                state = ValidateAndMap(catalog, definition, save);
                return true;
            }
            catch (InvalidSaveException exception)
            {
                return Fail(exception.Error, exception.Message, out error, out detail);
            }
            catch (OverflowException)
            {
                return Fail(EncounterRestoreError.InvalidState, "Saved arithmetic totals overflow.", out error, out detail);
            }
        }

        private static EncounterState ValidateAndMap(MechanicalCatalog catalog, EncounterDefinition definition, EncounterSaveData save)
        {
            Require(Enum.IsDefined(typeof(EncounterPhase), save.Phase) && Enum.IsDefined(typeof(EncounterOutcome), save.Outcome),
                "Phase or outcome is invalid.");
            Require(save.Revision >= 0 && save.StageIndex >= 0 && save.StageIndex < definition.Stages.Count,
                "Revision or stage index is invalid.");
            var stage = definition.Stages[save.StageIndex];
            Require(save.RemainingThreshold >= 0 && save.RemainingThreshold <= stage.Threshold,
                "Remaining threshold is invalid.");
            Require(save.HandIndex >= 0 && save.HandIndex < stage.StartingHands && save.HandsRemaining >= 0 &&
                save.HandsPlayedInStage >= 0 && (long)save.HandsRemaining + save.HandsPlayedInStage == stage.StartingHands,
                "Hand counters or resources are inconsistent.");
            Require(save.RerollsRemaining >= 0 && save.RewardRefreshes >= 0 && save.RerollsSpentOnHand >= 0 &&
                save.TotalScore >= 0 && save.TotalHandsPlayed >= save.HandsPlayedInStage &&
                save.TotalRerollsSpent >= save.RerollsSpentOnHand && save.TotalHandsPlayed <= save.Revision &&
                save.TotalRerollsSpent <= save.Revision - save.TotalHandsPlayed,
                "Cumulative counters or resources are invalid.");
            Require(save.NextOfferId > 0 && save.NextOptionId > 0, "Reward identity counters are invalid.");
            bool completed = save.Phase == EncounterPhase.Completed;
            Require(completed == (save.Outcome != EncounterOutcome.None), "Phase and outcome disagree.");
            bool postPlay = save.Phase == EncounterPhase.AwaitingRewards || save.Phase == EncounterPhase.ChoosingModifier ||
                save.Phase == EncounterPhase.ChoosingHandUpgrade || (completed && save.Outcome != EncounterOutcome.Abandoned) ||
                (completed && save.RemainingThreshold == 0);
            Require(save.HandIndex == save.HandsPlayedInStage - (postPlay ? 1 : 0), "Hand index disagrees with played hands.");
            Require(save.HandsPlayedInStage == 0 ? save.RemainingThreshold == stage.Threshold : save.RemainingThreshold < stage.Threshold,
                "Threshold disagrees with stage hand history.");
            if ((!completed && save.Phase == EncounterPhase.Playing) || (completed && save.Outcome == EncounterOutcome.Abandoned && !postPlay))
                Require(save.HandsRemaining > 0 && save.RemainingThreshold > 0, "An active hand requires hands and threshold.");
            if (save.Outcome == EncounterOutcome.Won)
                Require(save.RemainingThreshold == 0 && save.StageIndex == definition.Stages.Count - 1, "Winning state is inconsistent.");
            if (save.Outcome == EncounterOutcome.Lost)
                Require(save.RemainingThreshold > 0 && save.HandsRemaining == 0, "Losing state is inconsistent.");
            if (postPlay && save.Outcome != EncounterOutcome.Won && save.Outcome != EncounterOutcome.Lost)
                Require(save.RemainingThreshold == 0 && save.StageIndex < definition.Stages.Count - 1 &&
                    stage.ExitBehavior == StageExitBehavior.AwaitRewards, "Reward boundary is inconsistent.");
            Require(save.HandUpgrades != null && save.HandUpgrades.Length == HandDefinition.CategoryCount &&
                save.Modifiers != null && save.ModifierContributions != null && save.ExcludedRewardFamilies != null &&
                save.RecentHandUpgrades != null, "A required save array is missing.");
            var state = new EncounterState
            {
                Revision = save.Revision, Phase = save.Phase, Outcome = save.Outcome,
                StageIndex = save.StageIndex, HandIndex = save.HandIndex,
                RemainingThreshold = save.RemainingThreshold, HandsRemaining = save.HandsRemaining,
                RerollsRemaining = save.RerollsRemaining,
                CurrentDice = ReadDice(save.CurrentDice), InitialDice = ReadDice(save.InitialDice),
                DiceBeforeLastReroll = save.DiceBeforeLastReroll == null ? (DiceValues?)null : ReadDice(save.DiceBeforeLastReroll),
                PreviousPlayedDice = save.PreviousPlayedDice == null ? (DiceValues?)null : ReadDice(save.PreviousPlayedDice),
                LatestRerollMask = save.LatestRerollMask, LatestChangedMask = save.LatestChangedMask,
                EverRerolledMask = save.EverRerolledMask, RerollsSpentOnHand = save.RerollsSpentOnHand,
                HandsPlayedInStage = save.HandsPlayedInStage,
                PreviousStageStartingHands = save.HasPreviousStage ? (int?)save.PreviousStageStartingHands : null,
                PreviousStageHandsRemaining = save.HasPreviousStage ? (int?)save.PreviousStageHandsRemaining : null,
                TotalScore = save.TotalScore, TotalHandsPlayed = save.TotalHandsPlayed, TotalRerollsSpent = save.TotalRerollsSpent,
                HandUpgrades = (int[])save.HandUpgrades.Clone(), DiceRandom = new DeterministicRandom(save.DiceRandomState),
                RewardRandom = new DeterministicRandom(save.RewardRandomState), RewardRefreshes = save.RewardRefreshes,
                NextOfferId = save.NextOfferId, NextOptionId = save.NextOptionId,
                Modifiers = new ModifierInstanceState[save.Modifiers.Length],
                ExcludedRewardFamilies = new List<string>(save.ExcludedRewardFamilies),
                RecentHandUpgrades = new List<HandCategory>(save.RecentHandUpgrades),
                PendingUpgradeCategories = save.PendingUpgradeCategories == null ? null : new List<HandCategory>(save.PendingUpgradeCategories)
            };
            ValidateHistory(definition, save, state, postPlay);
            ValidateDiceHistory(state, postPlay);
            var definitions = new Dictionary<string, ModifierDefinition>(StringComparer.Ordinal);
            var families = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers) { definitions.Add(modifier.Id, modifier); families.Add(modifier.FamilyId); }
            ValidateModifiers(catalog, definition, save, state, definitions, postPlay);
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (string family in state.ExcludedRewardFamilies)
            {
                Require(!string.IsNullOrWhiteSpace(family) && excluded.Add(family), "Reward family history is invalid.");
                if (!families.Contains(family))
                    throw new InvalidSaveException(EncounterRestoreError.InvalidDefinition, "Reward family is missing from the catalog.");
            }
            ValidateRewards(catalog, definition, save, state, definitions, postPlay);
            return state;
        }

        private static void ValidateHistory(EncounterDefinition definition, EncounterSaveData save, EncounterState state, bool postPlay)
        {
            long previousMaximumHands = 0;
            long minimumScore = definition.Stages[state.StageIndex].Threshold - state.RemainingThreshold;
            for (int i = 0; i < state.StageIndex; ++i)
            {
                previousMaximumHands += definition.Stages[i].StartingHands;
                minimumScore = checked(minimumScore + definition.Stages[i].Threshold);
            }
            long previousHandsPlayed = state.TotalHandsPlayed - state.HandsPlayedInStage;
            Require(previousHandsPlayed >= state.StageIndex && previousHandsPlayed <= previousMaximumHands &&
                state.TotalScore >= minimumScore && (state.TotalHandsPlayed == 0 ? state.TotalScore == 0 : state.TotalScore > 0),
                "Cumulative hand or score history is inconsistent.");
            Require(save.HasPreviousStage == (state.StageIndex > 0), "Previous stage presence is inconsistent.");
            if (save.HasPreviousStage)
            {
                int starting = definition.Stages[state.StageIndex - 1].StartingHands;
                Require(save.PreviousStageStartingHands == starting && save.PreviousStageHandsRemaining >= 0 &&
                    save.PreviousStageHandsRemaining < starting, "Previous stage hand resources are invalid.");
                long previousPlayed = starting - save.PreviousStageHandsRemaining;
                Require(previousHandsPlayed >= previousPlayed + state.StageIndex - 1 &&
                    previousHandsPlayed <= previousMaximumHands - starting + previousPlayed,
                    "Previous stage resources disagree with total played hands.");
            }
            else Require(save.PreviousStageStartingHands == 0 && save.PreviousStageHandsRemaining == 0,
                "Absent previous stage resources must be empty.");
            long earnedUpgrades = 0;
            for (int i = 0; i < state.HandUpgrades.Length; ++i)
            {
                Require(state.HandUpgrades[i] >= definition.StartingUpgrades[i], "Upgrade counts cannot precede starting upgrades.");
                earnedUpgrades += state.HandUpgrades[i] - definition.StartingUpgrades[i];
            }
            Require(earnedUpgrades <= state.StageIndex && earnedUpgrades <= state.Revision - state.TotalHandsPlayed - state.TotalRerollsSpent,
                "Earned upgrade counts exceed selection history.");
        }

        private static void ValidateDiceHistory(EncounterState state, bool postPlay)
        {
            Require((state.LatestRerollMask & ~DiceValues.AllMask) == 0 &&
                (state.LatestChangedMask & ~state.LatestRerollMask) == 0 &&
                (state.EverRerolledMask & ~DiceValues.AllMask) == 0 &&
                (state.LatestRerollMask & ~state.EverRerolledMask) == 0, "Dice masks are inconsistent.");
            Require(state.PreviousPlayedDice.HasValue == (state.TotalHandsPlayed != 0), "Previous played dice presence is inconsistent.");
            if (postPlay) Require(state.PreviousPlayedDice.HasValue && state.PreviousPlayedDice.Value == state.CurrentDice,
                "Played dice do not match the last hand.");
            if (state.RerollsSpentOnHand == 0)
                Require(!state.DiceBeforeLastReroll.HasValue && state.LatestRerollMask == 0 && state.LatestChangedMask == 0 &&
                    state.EverRerolledMask == 0 && state.CurrentDice == state.InitialDice, "Initial hand dice history is inconsistent.");
            else
            {
                Require(state.DiceBeforeLastReroll.HasValue && state.LatestRerollMask != 0, "Rerolled hand history is missing.");
                int changed = 0;
                for (int i = 0; i < DiceValues.Count; ++i)
                    if (state.DiceBeforeLastReroll.Value[i] != state.CurrentDice[i]) changed |= 1 << i;
                Require(changed == state.LatestChangedMask, "Changed mask disagrees with rerolled dice.");
            }
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((state.EverRerolledMask & (1 << i)) == 0)
                    Require(state.InitialDice[i] == state.CurrentDice[i], "An untouched die differs from its initial face.");
        }

        private static void ValidateModifiers(MechanicalCatalog catalog, EncounterDefinition definition, EncounterSaveData save,
            EncounterState state, Dictionary<string, ModifierDefinition> definitions, bool postPlay)
        {
            Require(state.Modifiers.Length >= definition.StartingModifiers.Count, "Starting modifier inventory is missing.");
            var contributionCounts = new int[state.Modifiers.Length];
            decimal stageRerollGrants = 0;
            for (int i = 0; i < state.Modifiers.Length; ++i)
            {
                var source = save.Modifiers[i];
                Require(source != null && source.InstanceId == i + 1L, "Modifier instance identity or order is invalid.");
                var modifier = GetModifier(definitions, source.DefinitionId);
                if (i < definition.StartingModifiers.Count)
                    Require(source.DefinitionId == definition.StartingModifiers[i], "Starting modifier order is inconsistent.");
                Require(source.ActivationsThisHand >= 0 && source.ActivationsThisStage >= source.ActivationsThisHand &&
                    source.TotalActivations >= source.ActivationsThisStage, "Modifier counters are inconsistent.");
                if (modifier.ActivationLimit == ModifierActivationLimit.OncePerHand)
                    Require(source.ActivationsThisHand <= 1 && source.ActivationsThisStage <= (long)state.HandIndex + 1 &&
                        source.TotalActivations <= state.TotalHandsPlayed + 1, "Modifier exceeds its hand activation limit.");
                if (modifier.ActivationLimit == ModifierActivationLimit.OncePerStage)
                    Require(source.ActivationsThisStage <= 1 && source.TotalActivations <= (long)state.StageIndex + 1,
                        "Modifier exceeds its stage activation limit.");
                if (modifier.Condition.Kind == ModifierConditionKind.Never)
                    Require(source.TotalActivations == 0, "An inactive modifier has activation history.");
                if (modifier.Phase == ModifierPhase.InitialRoll)
                    Require(source.ActivationsThisHand <= 1 && source.ActivationsThisStage <= (long)state.HandIndex + 1 &&
                        source.TotalActivations <= state.TotalHandsPlayed + 1, "Initial-roll modifier counters are invalid.");
                else if (modifier.Phase == ModifierPhase.PlayHand)
                    Require(source.ActivationsThisHand <= (postPlay ? 1 : 0) && source.ActivationsThisStage <= state.HandsPlayedInStage &&
                        source.TotalActivations <= state.TotalHandsPlayed, "Played-hand modifier counters are invalid.");
                else Require(source.ActivationsThisHand <= state.RerollsSpentOnHand && source.ActivationsThisStage <= state.TotalRerollsSpent &&
                    source.TotalActivations <= state.TotalRerollsSpent, "Reroll modifier counters are invalid.");
                if (source.ActivationsThisStage != 0)
                {
                    GetEffectTotals(modifier, out _, out _, out int rerolls);
                    stageRerollGrants += (decimal)rerolls * source.ActivationsThisStage;
                }
                state.Modifiers[i] = new ModifierInstanceState
                {
                    InstanceId = source.InstanceId, DefinitionId = source.DefinitionId,
                    ActivationsThisHand = source.ActivationsThisHand, ActivationsThisStage = source.ActivationsThisStage,
                    TotalActivations = source.TotalActivations
                };
            }
            long baseScore = 0, multiplier = 0;
            foreach (var source in save.ModifierContributions)
            {
                Require(source != null && source.InstanceId > 0 && source.InstanceId <= state.Modifiers.Length,
                    "A modifier contribution instance is invalid.");
                int index = (int)(source.InstanceId - 1);
                var instance = state.Modifiers[index];
                Require(source.DefinitionId == instance.DefinitionId, "Contribution definition disagrees with its instance.");
                var modifier = GetModifier(definitions, source.DefinitionId);
                GetEffectTotals(modifier, out long expectedBase, out long expectedMultiplier, out int expectedRerolls);
                Require(source.Phase == modifier.Phase && source.BaseScore == expectedBase &&
                    source.Multiplier == expectedMultiplier && source.Rerolls == expectedRerolls,
                    "Contribution effects disagree with the catalog.");
                ++contributionCounts[index];
                baseScore = checked(baseScore + source.BaseScore);
                multiplier = checked(multiplier + source.Multiplier);
                state.ModifierContributions.Add(new ModifierContribution(source.InstanceId, modifier,
                    source.BaseScore, source.Multiplier, source.Rerolls));
            }
            for (int i = 0; i < state.Modifiers.Length; ++i)
                Require(contributionCounts[i] == state.Modifiers[i].ActivationsThisHand,
                    "Contribution count disagrees with modifier hand activations.");
            Require(save.ModifierBaseScore == baseScore && save.ModifierMultiplier == multiplier,
                "Modifier accumulated totals are inconsistent.");
            state.ModifierBaseScore = baseScore;
            state.ModifierMultiplier = multiplier;
            decimal stageRerollsSpent = definition.Stages[state.StageIndex].StartingRerolls + stageRerollGrants - state.RerollsRemaining;
            Require(stageRerollsSpent >= state.RerollsSpentOnHand && stageRerollsSpent <= state.TotalRerollsSpent,
                "Stage reroll resources disagree with activation and spending history.");
            for (int i = 0; i < state.HandUpgrades.Length; ++i)
                Require(state.HandUpgrades[i] <= catalog.Hands[i].MaximumUpgrades, "A hand upgrade exceeds its catalog cap.");
        }

        private static void ValidateRewards(MechanicalCatalog catalog, EncounterDefinition definition, EncounterSaveData save,
            EncounterState state, Dictionary<string, ModifierDefinition> definitions, bool postPlay)
        {
            var policy = definition.RewardPolicy;
            bool choosingModifier = state.Phase == EncounterPhase.ChoosingModifier;
            bool choosingUpgrade = state.Phase == EncounterPhase.ChoosingHandUpgrade;
            Require((save.PendingReward != null) == (choosingModifier || choosingUpgrade), "Pending offer disagrees with phase.");
            Require((state.PendingUpgradeCategories != null) == choosingModifier, "Cached upgrade choices disagree with phase.");
            Require(state.Phase != EncounterPhase.AwaitingRewards || policy == null, "A reward policy cannot await external rewards.");
            Require(!(choosingModifier || choosingUpgrade) || policy != null, "Reward selection requires a policy.");
            long previousRewardStages = 0;
            for (int i = 0; i < state.StageIndex; ++i)
                if (definition.Stages[i].ExitBehavior == StageExitBehavior.AwaitRewards) ++previousRewardStages;
            bool currentRewardStage = postPlay && state.StageIndex < definition.Stages.Count - 1 && state.RemainingThreshold == 0;
            long maximumAcquisitions = policy == null ? 0 : previousRewardStages +
                (currentRewardStage && (choosingUpgrade || state.Outcome == EncounterOutcome.Abandoned) ? 1 : 0);
            Require(state.Modifiers.Length - definition.StartingModifiers.Count <= maximumAcquisitions,
                "Acquired inventory exceeds reward stage history.");
            long earnedUpgrades = 0;
            for (int i = 0; i < state.HandUpgrades.Length; ++i) earnedUpgrades += state.HandUpgrades[i] - definition.StartingUpgrades[i];
            Require(earnedUpgrades <= (policy == null ? 0 : previousRewardStages), "Upgrades exceed reward stage history.");
            int historyCount = 0;
            if (policy != null)
                foreach (var rule in policy.UpgradeHistory) historyCount = Math.Max(historyCount, rule.ExcludedRecentChoices);
            Require(state.RecentHandUpgrades.Count == Math.Min(historyCount, earnedUpgrades),
                "Recent upgrade history length is inconsistent.");
            var recentCounts = new int[HandDefinition.CategoryCount];
            foreach (var category in state.RecentHandUpgrades)
            {
                RequireCategory(category);
                ++recentCounts[(int)category];
            }
            for (int i = 0; i < recentCounts.Length; ++i)
                Require(recentCounts[i] <= state.HandUpgrades[i] - definition.StartingUpgrades[i],
                    "Recent upgrade history disagrees with upgrade counts.");
            if (policy == null)
                Require(state.RewardRefreshes == 0 && state.NextOfferId == 1 && state.NextOptionId == 1 &&
                    state.ExcludedRewardFamilies.Count == 0, "Reward data requires a policy.");
            else
            {
                long grants = previousRewardStages + (currentRewardStage ? 1 : 0);
                long availableRefreshes = policy.StartingRefreshes + grants * policy.RefreshesPerClear;
                Require(state.RewardRefreshes <= availableRefreshes &&
                    (availableRefreshes - state.RewardRefreshes) % policy.RefreshCost == 0,
                    "Refresh resources disagree with reward economy.");
                var acquiredIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pool in catalog.RewardPools)
                {
                    foreach (string id in pool.ModifierIds) acquiredIds.Add(id);
                    if (pool.FallbackModifierId != null) acquiredIds.Add(pool.FallbackModifierId);
                }
                for (int i = definition.StartingModifiers.Count; i < state.Modifiers.Length; ++i)
                    Require(acquiredIds.Contains(state.Modifiers[i].DefinitionId), "Acquired modifier is absent from reward pools.");
            }
            if (!choosingModifier)
                Require(state.Outcome == EncounterOutcome.Abandoned || state.ExcludedRewardFamilies.Count == 0,
                    "Reward family history exists outside modifier selection.");
            if (choosingModifier) ValidateUpgradeCategories(catalog, definition, state, state.PendingUpgradeCategories);
            if (save.PendingReward == null) return;
            var source = save.PendingReward;
            Require(source.Kind == (choosingModifier ? RewardKind.Modifier : RewardKind.HandUpgrade) &&
                source.TargetStageIndex == state.StageIndex + 1 && source.OfferId > 0 &&
                source.OfferId == state.NextOfferId - 1 && source.Options != null && source.Options.Length > 0 &&
                state.NextOptionId > source.Options.Length, "Pending offer metadata is inconsistent.");
            var options = new List<RewardOption>();
            foreach (var option in source.Options)
            {
                Require(option != null && option.OptionId == state.NextOptionId - source.Options.Length + options.Count,
                    "Reward option identity or order is invalid.");
                if (choosingModifier)
                {
                    Require(!option.HasCategory && option.Category == default, "A modifier option cannot contain a hand category.");
                    GetModifier(definitions, option.DefinitionId);
                }
                else
                {
                    Require(option.HasCategory, "An upgrade option requires a hand category.");
                    RequireCategory(option.Category);
                    Require(option.DefinitionId == catalog.GetHand(option.Category).Id, "Upgrade option definition is inconsistent.");
                }
                options.Add(new RewardOption(option.OptionId, option.DefinitionId, option.HasCategory ? (HandCategory?)option.Category : null));
            }
            if (choosingModifier) ValidateModifierOptions(catalog, definition, state, options);
            else
            {
                var categories = new List<HandCategory>();
                foreach (var option in options) categories.Add(option.Category.Value);
                ValidateUpgradeCategories(catalog, definition, state, categories);
            }
            state.PendingReward = new RewardOffer(source.OfferId, source.Kind, source.TargetStageIndex, options);
        }

        private static void ValidateModifierOptions(MechanicalCatalog catalog, EncounterDefinition definition,
            EncounterState state, List<RewardOption> options)
        {
            var policy = definition.RewardPolicy;
            var layout = policy.ModifierLayouts[(state.StageIndex + 1) % policy.ModifierLayouts.Count];
            Require(options.Count <= layout.PoolIds.Count, "Modifier offer has too many options.");
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instance in state.Modifiers) owned.Add(instance.DefinitionId);
            var offered = new HashSet<string>(StringComparer.Ordinal);
            var history = new HashSet<string>(state.ExcludedRewardFamilies, StringComparer.Ordinal);
            int nextOption = 0;
            foreach (string poolId in layout.PoolIds)
            {
                var pool = catalog.GetRewardPool(poolId);
                bool hasCandidate = false;
                foreach (string id in pool.ModifierIds)
                    if (!owned.Contains(id) && !offered.Contains(catalog.GetModifier(id).FamilyId)) { hasCandidate = true; break; }
                if (!hasCandidate && pool.FallbackModifierId == null) continue;
                Require(nextOption < options.Count, "A nonempty reward slot is missing from the offer.");
                var option = options[nextOption++];
                var modifier = catalog.GetModifier(option.DefinitionId);
                if (!hasCandidate)
                    Require(option.DefinitionId == pool.FallbackModifierId, "An exhausted reward slot requires its fallback.");
                else
                    Require(pool.ModifierIds.Contains(option.DefinitionId) && !owned.Contains(option.DefinitionId) &&
                        !offered.Contains(modifier.FamilyId), "Modifier option is inconsistent with its pool.");
                Require(history.Contains(modifier.FamilyId), "An offered family is absent from reward history.");
                offered.Add(modifier.FamilyId);
            }
            Require(nextOption == options.Count, "Modifier offer contains an extra option.");
        }

        private static void ValidateUpgradeCategories(MechanicalCatalog catalog, EncounterDefinition definition,
            EncounterState state, IList<HandCategory> categories)
        {
            var policy = definition.RewardPolicy;
            int window = 0;
            foreach (var rule in policy.UpgradeHistory)
                if ((long)state.StageIndex + 2 <= rule.MaxIncomingStageNumber) { window = rule.ExcludedRecentChoices; break; }
            var excluded = new HashSet<HandCategory>();
            for (int i = 0; i < Math.Min(window, state.RecentHandUpgrades.Count); ++i) excluded.Add(state.RecentHandUpgrades[i]);
            int available = 0;
            for (int i = 0; i < HandDefinition.CategoryCount; ++i)
                if (state.HandUpgrades[i] < catalog.Hands[i].MaximumUpgrades && !excluded.Contains((HandCategory)i)) ++available;
            if (available < policy.HandUpgradeOptionCount)
            {
                excluded.Clear();
                available = 0;
                for (int i = 0; i < HandDefinition.CategoryCount; ++i)
                    if (state.HandUpgrades[i] < catalog.Hands[i].MaximumUpgrades) ++available;
            }
            Require(categories.Count == Math.Min(available, policy.HandUpgradeOptionCount), "Upgrade offer has an incorrect option count.");
            int previous = -1;
            foreach (var category in categories)
            {
                RequireCategory(category);
                Require((int)category > previous && !excluded.Contains(category) &&
                    state.HandUpgrades[(int)category] < catalog.GetHand(category).MaximumUpgrades,
                    "Upgrade categories are unordered, repeated, or ineligible.");
                previous = (int)category;
            }
        }

        private static ModifierDefinition GetModifier(Dictionary<string, ModifierDefinition> definitions, string id)
        {
            if (id == null || !definitions.TryGetValue(id, out var definition))
                throw new InvalidSaveException(EncounterRestoreError.InvalidDefinition, "Modifier definition is missing from the catalog.");
            return definition;
        }

        private static void GetEffectTotals(ModifierDefinition modifier, out long baseScore, out long multiplier, out int rerolls)
        {
            baseScore = 0;
            multiplier = 0;
            rerolls = 0;
            checked
            {
                foreach (var effect in modifier.Effects)
                    switch (effect.Kind)
                    {
                        case ModifierEffectKind.AddBaseScore: baseScore += effect.Amount; break;
                        case ModifierEffectKind.AddMultiplier: multiplier += effect.Amount; break;
                        case ModifierEffectKind.AddRerolls: rerolls += (int)effect.Amount; break;
                    }
            }
        }

        private static DiceValues ReadDice(int[] values)
        {
            Require(values != null && values.Length == DiceValues.Count, "Dice must have five faces.");
            foreach (int face in values) Require(face >= 1 && face <= 6, "A saved die face is invalid.");
            return new DiceValues(values[0], values[1], values[2], values[3], values[4]);
        }

        private static void RequireCategory(HandCategory category)
            => Require((uint)category < HandDefinition.CategoryCount, "A hand category is invalid.");

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidSaveException(EncounterRestoreError.InvalidState, detail);
        }

        private static bool Fail(EncounterRestoreError value, string message, out EncounterRestoreError error, out string detail)
        {
            error = value;
            detail = message;
            return false;
        }

        private sealed class InvalidSaveException : Exception
        {
            internal EncounterRestoreError Error { get; }
            internal InvalidSaveException(EncounterRestoreError error, string detail) : base(detail) => Error = error;
        }
    }
}
