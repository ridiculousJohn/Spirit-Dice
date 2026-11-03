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
                EncounterId = definition.Identity,
                Revision = state.Revision, Phase = state.Phase, Outcome = state.Outcome,
                StageIndex = state.StageIndex, HandIndex = state.HandIndex,
                RemainingThreshold = state.RemainingThreshold, HandsRemaining = state.HandsRemaining,
                RerollsRemaining = state.RerollsRemaining,
                CurrentDice = state.CurrentDice.ToArray(), InitialHand = CaptureHand(state.InitialHand),
                HandBeforeLastReroll = CaptureHand(state.HandBeforeLastReroll),
                PreviousPlayedHand = CaptureHand(state.PreviousPlayedHand),
                LatestRerollMask = state.LatestRerollMask, LatestChangedMask = state.LatestChangedMask,
                EverRerolledMask = state.EverRerolledMask, RerollsSpentOnHand = state.RerollsSpentOnHand,
                RerollsSpentInStage = state.RerollsSpentInStage,
                HandsPlayedInStage = state.HandsPlayedInStage,
                HasPreviousStage = state.PreviousStageStartingHands.HasValue,
                PreviousStageStartingHands = state.PreviousStageStartingHands ?? 0,
                PreviousStageHandsRemaining = state.PreviousStageHandsRemaining ?? 0,
                TotalScore = state.TotalScore, TotalHandsPlayed = state.TotalHandsPlayed,
                TotalRerollsSpent = state.TotalRerollsSpent,
                HandUpgrades = (int[])state.HandUpgrades.Clone(),
                DiceRandomState = state.DiceRandom.State, RewardRandomState = state.RewardRandom.State,
                RuleRandomState = state.RuleRandom.State,
                Pieces = CapturePieces(definition, state),
                RuleStates = CaptureRuleStates(definition, state),
                RewardRefreshes = state.RewardRefreshes, NextOfferId = state.NextOfferId, NextOptionId = state.NextOptionId,
                ExcludedRewardFamilies = state.ExcludedRewardFamilies.ToArray(),
                RecentHandUpgrades = state.RecentHandUpgrades.ToArray(),
                PendingUpgradeCategories = state.PendingUpgradeCategories?.ToArray(),
                UnlockedModifierIds = state.UnlockedModifierIds.ToArray(),
                NewlyUnlockedModifierIds = state.NewlyUnlockedModifierIds.ToArray(),
                DiscoveredFamilyIds = state.DiscoveredFamilyIds.ToArray(),
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
                    InstanceId = value.InstanceId, DefinitionId = value.DefinitionId,
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

        /**
         * Maps a save onto encounter state, checking only that the state can be used safely: required data is present,
         * values are in range, ids resolve in the catalog and each rule state restores. A save is not checked for having
         * been produced by play; the file seal guards it against corruption.
         */
        private static EncounterState ValidateAndMap(MechanicalCatalog catalog, EncounterDefinition definition, EncounterSaveData save)
        {
            Require(Enum.IsDefined(typeof(EncounterPhase), save.Phase) && Enum.IsDefined(typeof(EncounterOutcome), save.Outcome),
                "Phase or outcome is invalid.");
            Require(save.Revision >= 0 && save.StageIndex >= 0 && save.StageIndex < definition.Stages.Count,
                "Revision or stage index is invalid.");
            Require(save.RemainingThreshold >= 0 && save.HandIndex >= 0 && save.HandsRemaining >= 0 && save.HandsPlayedInStage >= 0 &&
                save.RerollsRemaining >= 0 && save.RerollsSpentOnHand >= 0 && save.RerollsSpentInStage >= 0 &&
                save.RewardRefreshes >= 0 && save.TotalScore >= 0 && save.TotalHandsPlayed >= 0 && save.TotalRerollsSpent >= 0,
                "A saved count is negative.");
            Require(!save.HasPreviousStage || (save.PreviousStageStartingHands >= 0 && save.PreviousStageHandsRemaining >= 0),
                "Previous stage hand counts are negative.");
            Require(save.NextOfferId > 0 && save.NextOptionId > 0, "Reward identity counters are invalid.");
            // Choosing a modifier or hand upgrade leads into the next stage, which must exist.
            Require((save.Phase != EncounterPhase.ChoosingModifier && save.Phase != EncounterPhase.ChoosingHandUpgrade) ||
                save.StageIndex < definition.Stages.Count - 1, "A stage reward requires a following stage.");
            Require(IsBayMask(save.LatestRerollMask) && IsBayMask(save.LatestChangedMask) && IsBayMask(save.EverRerolledMask),
                "Dice masks name dice outside the five bays.");
            Require(save.HandUpgrades != null && save.HandUpgrades.Length == HandDefinition.CategoryCount &&
                save.Modifiers != null && save.ModifierContributions != null && save.ExcludedRewardFamilies != null &&
                save.RecentHandUpgrades != null, "A required save array is missing.");
            // Scoring reads each hand at its upgrade level, which the catalog bounds.
            for (int i = 0; i < save.HandUpgrades.Length; ++i)
                Require(save.HandUpgrades[i] >= 0 && save.HandUpgrades[i] <= catalog.Hands[i].MaximumUpgrades,
                    "A hand upgrade lies outside its catalog range.");
            var state = new EncounterState
            {
                Revision = save.Revision, Phase = save.Phase, Outcome = save.Outcome,
                StageIndex = save.StageIndex, HandIndex = save.HandIndex,
                RemainingThreshold = save.RemainingThreshold, HandsRemaining = save.HandsRemaining,
                RerollsRemaining = save.RerollsRemaining,
                CurrentDice = ReadDice(save.CurrentDice), InitialHand = ReadRequiredHand(save.InitialHand),
                HandBeforeLastReroll = ReadHand(save.HandBeforeLastReroll),
                PreviousPlayedHand = ReadHand(save.PreviousPlayedHand),
                LatestRerollMask = save.LatestRerollMask, LatestChangedMask = save.LatestChangedMask,
                EverRerolledMask = save.EverRerolledMask, RerollsSpentOnHand = save.RerollsSpentOnHand,
                RerollsSpentInStage = save.RerollsSpentInStage,
                HandsPlayedInStage = save.HandsPlayedInStage,
                PreviousStageStartingHands = save.HasPreviousStage ? (int?)save.PreviousStageStartingHands : null,
                PreviousStageHandsRemaining = save.HasPreviousStage ? (int?)save.PreviousStageHandsRemaining : null,
                TotalScore = save.TotalScore, TotalHandsPlayed = save.TotalHandsPlayed, TotalRerollsSpent = save.TotalRerollsSpent,
                HandUpgrades = (int[])save.HandUpgrades.Clone(), DiceRandom = new DeterministicRandom(save.DiceRandomState),
                RewardRandom = new DeterministicRandom(save.RewardRandomState), RewardRefreshes = save.RewardRefreshes,
                RuleRandom = new DeterministicRandom(save.RuleRandomState),
                NextOfferId = save.NextOfferId, NextOptionId = save.NextOptionId,
                Modifiers = new ModifierInstanceState[save.Modifiers.Length],
                ExcludedRewardFamilies = ReadFamilies(catalog, save.ExcludedRewardFamilies, "Excluded reward families"),
                RecentHandUpgrades = new List<HandCategory>(save.RecentHandUpgrades),
                PendingUpgradeCategories = save.PendingUpgradeCategories == null ? null : new List<HandCategory>(save.PendingUpgradeCategories)
            };
            foreach (var category in state.RecentHandUpgrades) RequireCategory(category);
            var definitions = new Dictionary<ModifierDefinition.Id, ModifierDefinition>();
            foreach (var modifier in catalog.Modifiers) definitions.Add(modifier.Identity, modifier);
            state.UnlockedModifierIds = ReadModifierIds(definitions, save.UnlockedModifierIds, "Unlocked modifiers");
            state.NewlyUnlockedModifierIds = ReadModifierIds(definitions, save.NewlyUnlockedModifierIds, "Newly unlocked modifiers");
            state.DiscoveredFamilyIds = ReadFamilies(catalog, save.DiscoveredFamilyIds, "Discovered families");
            RestoreRuleStates(definition, save, state);
            state.PieceStates = RestorePieces(definition, save);
            RestoreModifiers(save, state, definitions);
            RestoreReward(catalog, definition, save, state, definitions);
            return state;
        }

        /** Each stateful rule restores its own saved state; together the rules must leave a die counted. */
        private static void RestoreRuleStates(EncounterDefinition definition, EncounterSaveData save, EncounterState state)
        {
            Require(save.RuleStates != null, "Rule states are missing.");
            var rules = definition.Rules;
            var sources = new object[rules.Count];
            foreach (var entry in save.RuleStates)
            {
                int slot = -1;
                for (int i = 0; entry != null && i < rules.Count; ++i)
                    if (rules[i].Identity == entry.RuleId) slot = i;
                Require(slot >= 0 && sources[slot] == null && rules[slot].SaveDataType != null && entry.State != null &&
                    entry.State.GetType() == rules[slot].SaveDataType, "A rule state disagrees with the encounter's rules.");
                sources[slot] = entry.State;
            }
            state.RuleStates = new EncounterRuleState[rules.Count];
            for (int i = 0; i < rules.Count; ++i)
            {
                if (rules[i].SaveDataType == null) continue;
                Require(sources[i] != null, "A rule state is missing.");
                Require(rules[i].TryRestoreState(sources[i], out state.RuleStates[i], out string detail), detail);
            }
            // The current hand is scored from its counted dice, so the rules cannot leave every die uncounted.
            Require(HandDice.IsCountedMask(DiceValues.AllMask & ~EncounterRuleResolver.UncountedDice(definition, state)),
                "The rules leave no die counted.");
        }

        private static EncounterSaveRuleData[] CaptureRuleStates(EncounterDefinition definition, EncounterState state)
        {
            var values = new List<EncounterSaveRuleData>();
            for (int i = 0; i < definition.Rules.Count; ++i)
                if (definition.Rules[i].SaveDataType != null)
                    values.Add(new EncounterSaveRuleData
                    {
                        RuleId = definition.Rules[i].Identity, State = definition.Rules[i].CaptureState(state.RuleStates[i])
                    });
            return values.ToArray();
        }

        private static EncounterSavePieceData[] CapturePieces(EncounterDefinition definition, EncounterState state)
        {
            var values = new List<EncounterSavePieceData>();
            int slot = 0;
            foreach (var piece in definition.Pieces)
            {
                var interactions = new int[piece.Interactions.Count][];
                for (int i = 0; i < interactions.Length; ++i) interactions[i] = piece.Interactions[i].Capture(state.PieceStates[slot++]);
                values.Add(new EncounterSavePieceData { PieceId = piece.Identity, Interactions = interactions });
            }
            return values.ToArray();
        }

        private static PieceInteractionState[] RestorePieces(EncounterDefinition definition, EncounterSaveData save)
        {
            Require(save.Pieces != null && save.Pieces.Length == definition.Pieces.Count, "Table piece states disagree with the encounter.");
            var states = new PieceInteractionState[TablePieceStates.Initial(definition).Length];
            int slot = 0;
            for (int index = 0; index < save.Pieces.Length; ++index)
            {
                var piece = definition.Pieces[index];
                var source = save.Pieces[index];
                Require(source != null && source.PieceId == piece.Identity && source.Interactions != null &&
                    source.Interactions.Length == piece.Interactions.Count, "A table piece state is invalid.");
                for (int i = 0; i < piece.Interactions.Count; ++i, ++slot)
                    Require(piece.Interactions[i].TryRestore(source.Interactions[i], out states[slot]), "A table piece state is invalid.");
            }
            return states;
        }

        private static void RestoreModifiers(EncounterSaveData save, EncounterState state,
            Dictionary<ModifierDefinition.Id, ModifierDefinition> definitions)
        {
            for (int i = 0; i < state.Modifiers.Length; ++i)
            {
                var source = save.Modifiers[i];
                // A contribution names its instance by its place in the inventory.
                Require(source != null && source.InstanceId == i + 1L, "Modifier instance identity or order is invalid.");
                GetModifier(definitions, source.DefinitionId);
                Require(source.ActivationsThisHand >= 0 && source.ActivationsThisStage >= 0 && source.TotalActivations >= 0,
                    "Modifier counters are negative.");
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
                var modifier = GetModifier(definitions, source.DefinitionId);
                baseScore = checked(baseScore + source.BaseScore);
                multiplier = checked(multiplier + source.Multiplier);
                state.ModifierContributions.Add(new ModifierContribution(source.InstanceId, modifier,
                    source.BaseScore, source.Multiplier, source.Rerolls));
            }
            state.ModifierBaseScore = baseScore;
            state.ModifierMultiplier = multiplier;
        }

        private static void RestoreReward(MechanicalCatalog catalog, EncounterDefinition definition, EncounterSaveData save,
            EncounterState state, Dictionary<ModifierDefinition.Id, ModifierDefinition> definitions)
        {
            bool choosingStageReward = state.Phase == EncounterPhase.ChoosingModifier || state.Phase == EncounterPhase.ChoosingHandUpgrade;
            bool choosing = choosingStageReward || state.Phase == EncounterPhase.ChoosingUnlock;
            // A reward phase waits on its offer, and selecting a stage reward reads the encounter's reward policy.
            Require(!choosing || save.PendingReward != null, "A reward phase requires its pending offer.");
            Require(!choosingStageReward || definition.RewardPolicy != null, "Reward selection requires a policy.");
            // Selecting a modifier goes on to offer the cached hand upgrades.
            Require(state.Phase != EncounterPhase.ChoosingModifier || state.PendingUpgradeCategories != null,
                "A modifier offer requires its cached upgrade choices.");
            if (state.PendingUpgradeCategories != null)
                foreach (var category in state.PendingUpgradeCategories) RequireUpgradable(catalog, state, category);
            if (save.PendingReward == null) return;
            var source = save.PendingReward;
            Require(source.Kind != null && (!choosing || source.Kind.WaitingPhase == state.Phase) && source.OfferId > 0 &&
                source.Options != null && (source.Options.Length > 0 || source.Kind.OfferedWhenEmpty),
                "The pending offer is invalid for the phase.");
            // Selecting a hand upgrade reads each option's category; selecting a charm or an unlock reads its definition.
            bool upgradeOffer = source.Kind == RewardKind.HandUpgrade;
            var options = new List<RewardOption>();
            foreach (var option in source.Options)
            {
                Require(option != null, "A reward option is missing.");
                if (upgradeOffer)
                {
                    Require(option.HasCategory && option.DefinitionId == null, "An upgrade option names its hand by its category alone.");
                    RequireUpgradable(catalog, state, option.Category);
                }
                else
                {
                    Require(!option.HasCategory && option.Category == default, "A modifier option cannot contain a hand category.");
                    GetModifier(definitions, option.DefinitionId);
                }
                options.Add(new RewardOption(option.OptionId, option.DefinitionId, option.HasCategory ? (HandCategory?)option.Category : null));
            }
            state.PendingReward = new RewardOffer(source.OfferId, source.Kind, source.TargetStageIndex, options, definition);
        }

        /** A hand offered for an upgrade is a defined category below its cap, which selecting it would exceed. */
        private static void RequireUpgradable(MechanicalCatalog catalog, EncounterState state, HandCategory category)
        {
            RequireCategory(category);
            Require(state.HandUpgrades[(int)category] < catalog.Hands[(int)category].MaximumUpgrades,
                "An offered hand is already at its upgrade cap.");
        }

        private static List<ModifierDefinition.Id> ReadModifierIds(Dictionary<ModifierDefinition.Id, ModifierDefinition> definitions,
            ModifierDefinition.Id[] ids, string subject)
        {
            Require(ids != null, subject + " are missing.");
            var unique = new HashSet<ModifierDefinition.Id>();
            foreach (var id in ids)
            {
                Require(unique.Add(id), subject + " must be unique.");
                GetModifier(definitions, id);
            }
            return new List<ModifierDefinition.Id>(ids);
        }

        private static List<ModifierFamilyDefinition.Id> ReadFamilies(MechanicalCatalog catalog, ModifierFamilyDefinition.Id[] ids,
            string subject)
        {
            Require(ids != null, subject + " are missing.");
            var unique = new HashSet<ModifierFamilyDefinition.Id>();
            foreach (var id in ids)
            {
                Require(unique.Add(id), subject + " must be unique.");
                if (!catalog.HasFamily(id))
                    throw new InvalidSaveException(EncounterRestoreError.InvalidDefinition, subject + " name a family missing from the catalog.");
            }
            return new List<ModifierFamilyDefinition.Id>(ids);
        }

        private static bool IsBayMask(int mask) => (mask & ~DiceValues.AllMask) == 0;

        private static ModifierDefinition GetModifier(Dictionary<ModifierDefinition.Id, ModifierDefinition> definitions, ModifierDefinition.Id? id)
        {
            if (!id.HasValue || !definitions.TryGetValue(id.Value, out var definition))
                throw new InvalidSaveException(EncounterRestoreError.InvalidDefinition, "Modifier definition is missing from the catalog.");
            return definition;
        }

        private static DiceValues ReadDice(int[] values)
        {
            Require(values != null && values.Length == DiceValues.Count, "Dice must have five faces.");
            foreach (int face in values) Require(DiceValues.IsFace(face), "A saved die face is invalid.");
            return new DiceValues(values[0], values[1], values[2], values[3], values[4]);
        }

        private static EncounterSaveHandData CaptureHand(HandDice? hand)
            => hand.HasValue ? new EncounterSaveHandData { Faces = hand.Value.Faces.ToArray(), CountedMask = hand.Value.CountedMask } : null;

        private static HandDice? ReadHand(EncounterSaveHandData saved)
            => saved == null ? (HandDice?)null : ReadRequiredHand(saved);

        private static HandDice ReadRequiredHand(EncounterSaveHandData saved)
        {
            Require(saved != null, "A required saved hand is missing.");
            DiceValues faces = ReadDice(saved.Faces);
            Require(HandDice.IsCountedMask(saved.CountedMask), "A saved hand's counted dice are invalid.");
            return new HandDice(faces, saved.CountedMask);
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
