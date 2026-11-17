using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    internal static class EncounterPersistence
    {
        internal static EncounterSaveData Capture(MechanicalCatalog catalog, EncounterInPlay inPlay, EncounterState state)
        {
            var save = new EncounterSaveData
            {
                SaveFormatVersion = EncounterSaveData.CurrentFormatVersion,
                RulesVersion = EncounterSession.RulesVersion,
                RandomAlgorithmVersion = DeterministicRandom.AlgorithmVersion,
                CatalogVersion = catalog.Version,
                EncounterId = inPlay.Encounter.Identity,
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
                CarriedItems = ItemList.Save(state.CarriedItems),
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
            inPlay.Encounter.Save(state, save);
            return save;
        }

        internal static bool TryRestore(MechanicalCatalog catalog, EncounterSaveData save,
            out EncounterInPlay inPlay, out EncounterState state, out EncounterRestoreError error, out string detail)
        {
            inPlay = null;
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
            try { catalog.GetEncounter(save.EncounterId); }
            catch (ArgumentException) { return Fail(EncounterRestoreError.EncounterNotFound, "Encounter ID is invalid.", out error, out detail); }
            catch (KeyNotFoundException) { return Fail(EncounterRestoreError.EncounterNotFound, "Encounter ID is missing from the catalog.", out error, out detail); }
            if (save.CarriedItems == null)
                return Fail(EncounterRestoreError.InvalidState, "Carried items are missing.", out error, out detail);
            var carriedItemIds = new List<ItemDefinition.Id>();
            foreach (var entry in save.CarriedItems)
            {
                if (entry == null) return Fail(EncounterRestoreError.InvalidState, "A carried item entry is missing.", out error, out detail);
                try { catalog.GetItem(entry.ItemId); }
                catch (Exception exception) when (exception is ArgumentException || exception is KeyNotFoundException)
                {
                    return Fail(EncounterRestoreError.InvalidDefinition, "The carried item " + entry.ItemId + " is missing from the catalog.",
                        out error, out detail);
                }
                carriedItemIds.Add(entry.ItemId);
            }
            // What the run plays is rebuilt from the save alone, never from the profile.
            try { inPlay = catalog.GetEncounterInPlay(save.EncounterId, carriedItemIds); }
            catch (ArgumentException exception)
            {
                return Fail(EncounterRestoreError.InvalidDefinition, "The carried items cannot be played with the encounter: " + exception.Message,
                    out error, out detail);
            }
            try
            {
                state = ValidateAndMap(catalog, inPlay, save);
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
         * values are in range, ids resolve in the catalog and each installed rule's state is usable. A save is not checked
         * for having been produced by play; the file seal guards it against corruption.
         */
        private static EncounterState ValidateAndMap(MechanicalCatalog catalog, EncounterInPlay inPlay, EncounterSaveData save)
        {
            Require(Enum.IsDefined(typeof(EncounterPhase), save.Phase) && Enum.IsDefined(typeof(EncounterOutcome), save.Outcome),
                "Phase or outcome is invalid.");
            Require(save.Revision >= 0 && save.StageIndex >= 0 && save.StageIndex < inPlay.Encounter.Stages.Count,
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
                save.StageIndex < inPlay.Encounter.Stages.Count - 1, "A stage reward requires a following stage.");
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
            // Each owner loads what it creates, a rule its state as a copy the save does not share and a piece its
            // interactions' states: the encounter its own rules and pieces and the items it lends, the run the carried items.
            inPlay.Encounter.Load(save, state);
            state.CarriedItems = ItemList.Load("run", inPlay.CarriedItems, save.CarriedItems);
            // The current hand is scored from its counted dice, so the rules cannot leave every die uncounted.
            Require(HandDice.IsCountedMask(DiceValues.AllMask & ~EncounterRuleResolver.UncountedDice(state)),
                "The rules leave no die counted.");
            RestoreModifiers(save, state, definitions);
            RestoreReward(catalog, inPlay, save, state, definitions);
            return state;
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

        private static void RestoreReward(MechanicalCatalog catalog, EncounterInPlay inPlay, EncounterSaveData save,
            EncounterState state, Dictionary<ModifierDefinition.Id, ModifierDefinition> definitions)
        {
            bool choosingStageReward = state.Phase == EncounterPhase.ChoosingModifier || state.Phase == EncounterPhase.ChoosingHandUpgrade;
            bool choosing = choosingStageReward || state.Phase == EncounterPhase.ChoosingUnlock;
            // A reward phase waits on its offer, and selecting a stage reward reads the encounter's reward policy.
            Require(!choosing || save.PendingReward != null, "A reward phase requires its pending offer.");
            Require(!choosingStageReward || inPlay.Encounter.RewardPolicy != null, "Reward selection requires a policy.");
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
            state.PendingReward = new RewardOffer(source.OfferId, source.Kind, source.TargetStageIndex, options, inPlay);
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

        private static void Require(bool condition, string detail) => SavedEntries.Require(condition, detail);

        private static bool Fail(EncounterRestoreError value, string message, out EncounterRestoreError error, out string detail)
        {
            error = value;
            detail = message;
            return false;
        }
    }

    /** A save restore rejects; Error says why and the message describes it. */
    internal sealed class InvalidSaveException : Exception
    {
        internal EncounterRestoreError Error { get; }
        internal InvalidSaveException(EncounterRestoreError error, string detail) : base(detail) => Error = error;
    }

    /**
     * Reads the saved entries of the things one owner creates, by id: an owner takes the entry of each thing it creates,
     * and a missing entry, an entry for a thing it does not create or a repeated id rejects the save, naming the owner and
     * the id.
     */
    internal static class SavedEntries
    {
        internal static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidSaveException(EncounterRestoreError.InvalidState, detail);
        }

        /** The owner's saved entries by id; kind names what they are, such as "rule". */
        internal static Dictionary<TId, TEntry> ById<TId, TEntry>(string owner, TEntry[] entries, Func<TEntry, TId> idOf, string kind)
            where TEntry : class
        {
            Require(entries != null, "The " + owner + " has no saved " + kind + " list.");
            var byId = new Dictionary<TId, TEntry>();
            foreach (var entry in entries)
            {
                Require(entry != null, "The " + owner + " has a missing saved " + kind + " entry.");
                TId id = idOf(entry);
                Require(!byId.ContainsKey(id), "The " + owner + " has more than one saved entry for the " + kind + " " + id + ".");
                byId.Add(id, entry);
            }
            return byId;
        }

        /** Takes the entry of a thing the owner creates, which must be saved. */
        internal static TEntry Take<TId, TEntry>(string owner, Dictionary<TId, TEntry> entries, TId id, string kind)
        {
            Require(entries.TryGetValue(id, out var entry), "The " + owner + " has no saved entry for its " + kind + " " + id + ".");
            entries.Remove(id);
            return entry;
        }

        /** Rejects an entry left once the owner has taken its own, which names a thing the owner does not create. */
        internal static void RequireNoneLeft<TId, TEntry>(string owner, Dictionary<TId, TEntry> entries, string kind)
        {
            foreach (var id in entries.Keys)
                Require(false, "The " + owner + " has a saved entry for the " + kind + " " + id + ", which it does not create.");
        }
    }
}
