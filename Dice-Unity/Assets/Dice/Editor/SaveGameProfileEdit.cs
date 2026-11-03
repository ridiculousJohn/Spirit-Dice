using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;

namespace Dice.Editor
{
    /** The ids one list of a profile stores, editable; it keeps the stored order and adds new ids last. */
    internal sealed class SaveGameIdSelection<TId> where TId : struct, IEquatable<TId>
    {
        private readonly List<TId> stored;
        private readonly List<TId> ids;

        public IReadOnlyList<TId> Ids => ids;
        public bool IsChanged => !ids.SequenceEqual(stored);

        public SaveGameIdSelection(IEnumerable<TId> storedIds)
        {
            stored = storedIds.ToList();
            ids = new List<TId>(stored);
        }

        public bool Contains(TId id) => ids.Contains(id);

        public void Set(TId id, bool selected)
        {
            if (!selected) ids.Remove(id);
            else if (!ids.Contains(id)) ids.Add(id);
        }
    }

    /** How the game treats a charm family as discovered without the profile storing it. */
    internal enum SaveGameImplicitDiscovery { Starter, UnlockedCharm }

    /** An editable copy of the entries a player profile stores; Build makes the profile they describe. */
    internal sealed class SaveGameProfileEdit
    {
        public SaveGameIdSelection<EncounterJourneyAsset.Id> CompletedJourneys { get; }
        public SaveGameIdSelection<EncounterDefinition.Id> CompletedEncounters { get; }
        public SaveGameIdSelection<EncounterDefinition.Id> VisitedEncounters { get; }
        public SaveGameIdSelection<ModifierFamilyDefinition.Id> DiscoveredFamilies { get; }
        public SaveGameIdSelection<ModifierDefinition.Id> UnlockedCharms { get; }

        public bool IsChanged => CompletedJourneys.IsChanged || CompletedEncounters.IsChanged || VisitedEncounters.IsChanged ||
            DiscoveredFamilies.IsChanged || UnlockedCharms.IsChanged;

        public SaveGameProfileEdit(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            CompletedJourneys = new SaveGameIdSelection<EncounterJourneyAsset.Id>(profile.CompletedJourneyIds);
            CompletedEncounters = new SaveGameIdSelection<EncounterDefinition.Id>(profile.CompletedEncounterIds);
            VisitedEncounters = new SaveGameIdSelection<EncounterDefinition.Id>(profile.VisitedEncounterIds);
            DiscoveredFamilies = new SaveGameIdSelection<ModifierFamilyDefinition.Id>(profile.DiscoveredFamilyIds);
            UnlockedCharms = new SaveGameIdSelection<ModifierDefinition.Id>(profile.UnlockedModifierIds);
        }

        public PlayerProfile Build()
        {
            var profile = new PlayerProfile();
            foreach (var id in DiscoveredFamilies.Ids) profile.DiscoverFamily(id);
            foreach (var id in UnlockedCharms.Ids) profile.UnlockModifier(id);
            foreach (var id in CompletedEncounters.Ids) profile.CompleteEncounter(id);
            foreach (var id in CompletedJourneys.Ids) profile.CompleteJourney(id);
            foreach (var id in VisitedEncounters.Ids) profile.VisitEncounter(id);
            return profile;
        }

        /**
         * How the game treats each family as discovered apart from the stored entries, read from the game's own rule
         * (PlayerProfile.GetDiscoveredFamilyIds): a starter charm's family for every player, an unlocked charm's family for
         * this profile's unlocked charms.
         */
        public IReadOnlyDictionary<ModifierFamilyDefinition.Id, SaveGameImplicitDiscovery> ImplicitDiscoveries(MechanicalCatalog catalog)
        {
            var starters = new PlayerProfile().GetDiscoveredFamilyIds(catalog);
            var owned = new PlayerProfile();
            foreach (var id in UnlockedCharms.Ids) owned.UnlockModifier(id);
            var result = new Dictionary<ModifierFamilyDefinition.Id, SaveGameImplicitDiscovery>();
            foreach (var id in owned.GetDiscoveredFamilyIds(catalog))
                result[id] = starters.Contains(id) ? SaveGameImplicitDiscovery.Starter : SaveGameImplicitDiscovery.UnlockedCharm;
            return result;
        }
    }
}
