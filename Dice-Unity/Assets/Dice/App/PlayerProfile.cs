using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.App
{
    public sealed class PlayerProfile
    {
        public const int CurrentFormatVersion = 1;
        private readonly List<string> unlockedModifierIds = new List<string>();
        private readonly List<string> completedEncounterIds = new List<string>();
        private readonly List<string> visitedEncounterIds = new List<string>();
        public ReadOnlyCollection<string> UnlockedModifierIds { get; }
        public ReadOnlyCollection<string> CompletedEncounterIds { get; }
        public ReadOnlyCollection<string> VisitedEncounterIds { get; }

        public PlayerProfile()
        {
            UnlockedModifierIds = unlockedModifierIds.AsReadOnly();
            CompletedEncounterIds = completedEncounterIds.AsReadOnly();
            VisitedEncounterIds = visitedEncounterIds.AsReadOnly();
        }

        public bool UnlockModifier(string id) => Add(unlockedModifierIds, id);
        public bool CompleteEncounter(string id) => Add(completedEncounterIds, id);
        public bool VisitEncounter(string id) => Add(visitedEncounterIds, id);

        public bool Merge(PlayerProfile other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            bool changed = false;
            foreach (string id in other.UnlockedModifierIds) changed |= UnlockModifier(id);
            foreach (string id in other.CompletedEncounterIds) changed |= CompleteEncounter(id);
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
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Trim() != id)
                throw new FormatException("Profile IDs must be nonempty and at most 256 characters.");
            foreach (char value in id)
                if (char.IsControl(value)) throw new FormatException("Profile IDs cannot contain control characters.");
        }
    }
}
