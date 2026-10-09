using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    [CreateAssetMenu(menuName = "Dice/Encounter Journey Catalog")]
    public sealed class EncounterJourneyCatalogAsset : ScriptableObject
    {
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private EncounterJourneyAsset[] entries = Array.Empty<EncounterJourneyAsset>();

        public string Title => title;
        public string Description => description;
        public IReadOnlyList<EncounterJourneyAsset> Entries => entries;

        public EncounterJourneyAsset Find(string encounterId)
        {
            foreach (var entry in entries)
                if (entry != null && entry.EncounterId == encounterId) return entry;
            return null;
        }

        public void Validate(MechanicalCatalog catalog)
        {
            if (entries == null || entries.Length == 0) throw new FormatException("An investigation requires encounter journeys.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.EncounterId) || !ids.Add(entry.EncounterId))
                    throw new FormatException("Journey encounter IDs must be nonempty and unique.");
                entry.Validate(catalog);
            }
        }
    }
}
