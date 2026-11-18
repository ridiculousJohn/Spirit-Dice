using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dice.Engine;
using Dice.Presentation;

namespace Dice.App
{
    public sealed class PlayerProfile
    {
        public const int CurrentFormatVersion = 10;
        private readonly List<ModifierFamilyDefinition.Id> discoveredFamilyIds = new List<ModifierFamilyDefinition.Id>();
        private readonly List<ModifierDefinition.Id> unlockedModifierIds = new List<ModifierDefinition.Id>();
        private readonly List<EncounterDefinition.Id> completedEncounterIds = new List<EncounterDefinition.Id>();
        private readonly List<EncounterJourneyAsset.Id> completedJourneyIds = new List<EncounterJourneyAsset.Id>();
        private readonly List<EncounterDefinition.Id> visitedEncounterIds = new List<EncounterDefinition.Id>();
        private readonly List<ItemDefinition.Id> ownedItemIds = new List<ItemDefinition.Id>();
        public ReadOnlyCollection<ModifierDefinition.Id> UnlockedModifierIds { get; }
        public ReadOnlyCollection<EncounterDefinition.Id> CompletedEncounterIds { get; }
        public ReadOnlyCollection<EncounterJourneyAsset.Id> CompletedJourneyIds { get; }
        public ReadOnlyCollection<EncounterDefinition.Id> VisitedEncounterIds { get; }
        public ReadOnlyCollection<ModifierFamilyDefinition.Id> DiscoveredFamilyIds { get; }
        public ReadOnlyCollection<ItemDefinition.Id> OwnedItemIds { get; }

        public PlayerProfile()
        {
            UnlockedModifierIds = unlockedModifierIds.AsReadOnly();
            CompletedEncounterIds = completedEncounterIds.AsReadOnly();
            CompletedJourneyIds = completedJourneyIds.AsReadOnly();
            VisitedEncounterIds = visitedEncounterIds.AsReadOnly();
            DiscoveredFamilyIds = discoveredFamilyIds.AsReadOnly();
            OwnedItemIds = ownedItemIds.AsReadOnly();
        }

        public bool UnlockModifier(ModifierDefinition.Id id) => Add(unlockedModifierIds, id);
        public bool CompleteEncounter(EncounterDefinition.Id id) => Add(completedEncounterIds, id);
        public bool CompleteJourney(EncounterJourneyAsset.Id id) => Add(completedJourneyIds, id);
        public bool VisitEncounter(EncounterDefinition.Id id) => Add(visitedEncounterIds, id);
        public bool DiscoverFamily(ModifierFamilyDefinition.Id id) => Add(discoveredFamilyIds, id);
        public bool OwnItem(ItemDefinition.Id id) => Add(ownedItemIds, id);

        public ReadOnlyCollection<ModifierFamilyDefinition.Id> GetDiscoveredFamilyIds(MechanicalCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new List<ModifierFamilyDefinition.Id>(discoveredFamilyIds);
            var known = new HashSet<ModifierFamilyDefinition.Id>(result);
            foreach (var modifier in catalog.Modifiers)
                if ((catalog.IsDiscoveredFromStart(modifier) ||
                     modifier.Availability.RequiresUnlock && unlockedModifierIds.Contains(modifier.Identity)) &&
                    known.Add(modifier.Family.Identity)) result.Add(modifier.Family.Identity);
            return result.AsReadOnly();
        }

        public bool ApplyReward(ProgressionRewardDefinition reward, MechanicalCatalog catalog)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            reward.Validate(catalog);
            bool changed = false;
            foreach (var id in reward.DiscoveredFamilyIds) changed |= DiscoverFamily(id);
            foreach (var id in reward.GrantedModifierIds)
            {
                changed |= UnlockModifier(id);
                changed |= DiscoverFamily(catalog.GetModifier(id).Family.Identity);
            }
            foreach (var id in reward.GrantedItemIds) changed |= OwnItem(id);
            return changed;
        }

        /**
         * Whether applying the reward would add anything to the profile, as ApplyReward adds it; lackedItems, when given,
         * receives each item of the reward the profile does not own and does not hold yet.
         */
        public bool Lacks(ProgressionRewardDefinition reward, MechanicalCatalog catalog, ICollection<ItemDefinition.Id> lackedItems = null)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            reward.Validate(catalog);
            bool lacks = false;
            foreach (var id in reward.DiscoveredFamilyIds) lacks |= !discoveredFamilyIds.Contains(id);
            foreach (var id in reward.GrantedModifierIds)
                lacks |= !unlockedModifierIds.Contains(id) || !discoveredFamilyIds.Contains(catalog.GetModifier(id).Family.Identity);
            foreach (var id in reward.GrantedItemIds)
            {
                if (ownedItemIds.Contains(id)) continue;
                lacks = true;
                if (lackedItems != null && !lackedItems.Contains(id)) lackedItems.Add(id);
            }
            return lacks;
        }

        public bool Merge(PlayerProfile other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            bool changed = false;
            foreach (var id in other.DiscoveredFamilyIds) changed |= DiscoverFamily(id);
            foreach (var id in other.UnlockedModifierIds) changed |= UnlockModifier(id);
            foreach (var id in other.CompletedEncounterIds) changed |= CompleteEncounter(id);
            foreach (var id in other.CompletedJourneyIds) changed |= CompleteJourney(id);
            foreach (var id in other.VisitedEncounterIds) changed |= VisitEncounter(id);
            foreach (var id in other.OwnedItemIds) changed |= OwnItem(id);
            return changed;
        }

        private static bool Add<T>(List<T> ids, T id)
        {
            if (ids.Contains(id)) return false;
            ids.Add(id);
            return true;
        }
    }
}
