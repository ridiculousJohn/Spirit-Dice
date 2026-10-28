using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dice.Engine;

namespace Dice.App
{
    public sealed class PlayerProfile
    {
        public const int CurrentFormatVersion = 5;
        private readonly List<ModifierFamilyDefinition.Id> discoveredFamilyIds = new List<ModifierFamilyDefinition.Id>();
        private readonly List<string> unlockedModifierIds = new List<string>();
        private readonly List<string> completedEncounterIds = new List<string>();
        private readonly List<string> completedStoryIds = new List<string>();
        private readonly List<string> visitedEncounterIds = new List<string>();
        public ReadOnlyCollection<string> UnlockedModifierIds { get; }
        public ReadOnlyCollection<string> CompletedEncounterIds { get; }
        public ReadOnlyCollection<string> CompletedStoryIds { get; }
        public ReadOnlyCollection<string> VisitedEncounterIds { get; }
        public ReadOnlyCollection<ModifierFamilyDefinition.Id> DiscoveredFamilyIds { get; }

        public PlayerProfile()
        {
            UnlockedModifierIds = unlockedModifierIds.AsReadOnly();
            CompletedEncounterIds = completedEncounterIds.AsReadOnly();
            CompletedStoryIds = completedStoryIds.AsReadOnly();
            VisitedEncounterIds = visitedEncounterIds.AsReadOnly();
            DiscoveredFamilyIds = discoveredFamilyIds.AsReadOnly();
        }

        public bool UnlockModifier(string id) => Add(unlockedModifierIds, id);
        public bool CompleteEncounter(string id) => Add(completedEncounterIds, id);
        public bool CompleteStory(string id) => Add(completedStoryIds, id);
        public bool VisitEncounter(string id) => Add(visitedEncounterIds, id);
        public bool DiscoverFamily(ModifierFamilyDefinition.Id id)
        {
            if (discoveredFamilyIds.Contains(id)) return false;
            discoveredFamilyIds.Add(id);
            return true;
        }

        public ReadOnlyCollection<ModifierFamilyDefinition.Id> GetDiscoveredFamilyIds(MechanicalCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new List<ModifierFamilyDefinition.Id>(discoveredFamilyIds);
            var known = new HashSet<ModifierFamilyDefinition.Id>(result);
            foreach (var modifier in catalog.Modifiers)
                if ((modifier.IsDiscoveredFromStart ||
                     modifier.Availability.Access == ModifierAccess.PermanentUnlock && unlockedModifierIds.Contains(modifier.Id)) &&
                    known.Add(modifier.Family.Identity)) result.Add(modifier.Family.Identity);
            return result.AsReadOnly();
        }

        public bool ApplyReward(ProgressionRewardDefinition reward, MechanicalCatalog catalog)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            reward.Validate(catalog);
            bool changed = false;
            foreach (var id in reward.DiscoveredFamilyIds) changed |= DiscoverFamily(id);
            foreach (string id in reward.GrantedModifierIds)
            {
                changed |= UnlockModifier(id);
                changed |= DiscoverFamily(catalog.GetModifier(id).Family.Identity);
            }
            return changed;
        }

        public bool Merge(PlayerProfile other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            bool changed = false;
            foreach (var id in other.DiscoveredFamilyIds) changed |= DiscoverFamily(id);
            foreach (string id in other.UnlockedModifierIds) changed |= UnlockModifier(id);
            foreach (string id in other.CompletedEncounterIds) changed |= CompleteEncounter(id);
            foreach (string id in other.CompletedStoryIds) changed |= CompleteStory(id);
            foreach (string id in other.VisitedEncounterIds) changed |= VisitEncounter(id);
            return changed;
        }

        private static bool Add(List<string> ids, string id)
        {
            ValidateId(id);
            if (ids.Contains(id)) return false;
            ids.Add(id);
            return true;
        }

        internal static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > ProgressionRewardDefinition.MaximumIdLength || id.Trim() != id)
                throw new FormatException("Profile IDs must be nonempty and at most " + ProgressionRewardDefinition.MaximumIdLength + " characters.");
            foreach (char value in id)
                if (char.IsControl(value)) throw new FormatException("Profile IDs cannot contain control characters.");
        }
    }
}
