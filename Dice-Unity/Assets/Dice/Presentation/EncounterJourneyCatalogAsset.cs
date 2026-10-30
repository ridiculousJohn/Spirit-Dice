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
        [SerializeField] private EncounterChapterAsset[] chapters = Array.Empty<EncounterChapterAsset>();

        public string Title => title;
        public string Description => description;
        public IReadOnlyList<EncounterChapterAsset> Chapters => chapters;
        public IReadOnlyList<EncounterJourneyAsset> Entries
        {
            get
            {
                var result = new List<EncounterJourneyAsset>();
                foreach (var chapter in chapters)
                    if (chapter?.Entries != null) result.AddRange(chapter.Entries);
                return result;
            }
        }

        /** The journey with the Encounter beat that plays the encounter, or null. */
        public EncounterJourneyAsset Find(EncounterDefinition.Id encounterId)
        {
            foreach (var entry in Entries)
                if (entry != null && entry.IndexOfEncounter(encounterId) >= 0) return entry;
            return null;
        }

        public void Validate(MechanicalCatalog catalog)
        {
            var listedChapters = new HashSet<EncounterChapterAsset>();
            if (chapters != null)
                foreach (var chapter in chapters)
                {
                    if (chapter == null) throw new FormatException("Chapters must be present.");
                    if (!listedChapters.Add(chapter))
                        throw new FormatException("The journey catalog lists the chapter " + chapter.name + " twice.");
                    if (string.IsNullOrWhiteSpace(chapter.Title) || chapter.Entries == null || chapter.Entries.Count == 0)
                        throw new FormatException("The chapter " + chapter.name + " requires a title and journey entries.");
                }
            var journeys = Entries;
            if (journeys == null || journeys.Count == 0) throw new FormatException("An investigation requires encounter journeys.");
            var journeyIds = new HashSet<EncounterJourneyAsset.Id>();
            var encounterJourneys = new Dictionary<EncounterDefinition.Id, EncounterJourneyAsset>();
            foreach (var entry in journeys)
            {
                if (entry == null) throw new FormatException("Journey entries must be present.");
                entry.Validate(catalog);
                if (!journeyIds.Add(entry.Identity))
                    throw new FormatException("The journey catalog lists the journey " + entry.name + " twice.");
                foreach (var encounter in entry.Encounters)
                {
                    if (encounterJourneys.TryGetValue(encounter.Identity, out var other))
                        throw new FormatException("The encounter " + encounter.name + " is played by more than one Encounter beat, " +
                            "in the journeys " + other.name + " and " + entry.name + ".");
                    encounterJourneys.Add(encounter.Identity, entry);
                }
            }
            foreach (var entry in journeys)
            {
                foreach (var followUp in entry.FollowUpJourneys)
                    if (!journeyIds.Contains(followUp.Identity))
                        throw new FormatException("The follow-up journey " + followUp.name + " of the journey " + entry.name +
                            " is not in the journey catalog.");
                foreach (var required in entry.RequiredCompletedJourneys)
                    if (!journeyIds.Contains(required.Identity))
                        throw new FormatException("The required completed journey " + required.name + " of the journey " +
                            entry.name + " is not in the journey catalog.");
            }
            var visiting = new HashSet<EncounterJourneyAsset>();
            var visited = new HashSet<EncounterJourneyAsset>();
            foreach (var entry in journeys) ValidatePrerequisiteCycles(entry, visiting, visited);
        }

        private void ValidatePrerequisiteCycles(EncounterJourneyAsset entry, HashSet<EncounterJourneyAsset> visiting,
            HashSet<EncounterJourneyAsset> visited)
        {
            if (visited.Contains(entry)) return;
            if (!visiting.Add(entry))
                throw new FormatException("Journey completion prerequisites cannot contain a cycle: " + entry.name + ".");
            foreach (var required in entry.RequiredCompletedJourneys) ValidatePrerequisiteCycles(required, visiting, visited);
            visiting.Remove(entry);
            visited.Add(entry);
        }
    }
}
