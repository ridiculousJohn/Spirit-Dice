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

        /** The encounter journey of the encounter, or null. */
        public EncounterJourneyAsset Find(EncounterDefinition.Id encounterId)
        {
            foreach (var entry in Entries)
                if (entry != null && !entry.IsStoryOnly && entry.Encounter != null && entry.Encounter.Identity == encounterId) return entry;
            return null;
        }

        /** The story journey of the story id, or null. */
        public EncounterJourneyAsset FindStory(string storyId)
        {
            foreach (var entry in Entries)
                if (entry != null && entry.IsStoryOnly && entry.StoryId == storyId) return entry;
            return null;
        }

        public void Validate(MechanicalCatalog catalog)
        {
            var chapterIds = new HashSet<string>(StringComparer.Ordinal);
            if (chapters != null)
                foreach (var chapter in chapters)
                    if (chapter == null || string.IsNullOrWhiteSpace(chapter.Id) || !chapterIds.Add(chapter.Id) ||
                        string.IsNullOrWhiteSpace(chapter.Title) || chapter.Entries == null || chapter.Entries.Count == 0)
                        throw new FormatException("Chapters require unique nonempty IDs, titles, and journey entries.");
            var journeys = Entries;
            if (journeys == null || journeys.Count == 0) throw new FormatException("An investigation requires encounter journeys.");
            var encounterIds = new HashSet<EncounterDefinition.Id>();
            var storyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in journeys)
            {
                if (entry == null) throw new FormatException("Journey entries must be present.");
                entry.Validate(catalog);
                if (entry.IsStoryOnly ? !storyIds.Add(entry.StoryId) : !encounterIds.Add(entry.Encounter.Identity))
                    throw new FormatException("Two journeys lead to the same encounter or story: " + entry.name + ".");
            }
            foreach (var entry in journeys)
            {
                foreach (var encounter in entry.FollowUpEncounters)
                    if (!encounterIds.Contains(encounter.Identity))
                        throw new FormatException("A follow-up encounter requires an investigation lead in the catalog.");
                foreach (string id in entry.FollowUpStoryIds)
                    if (!storyIds.Contains(id)) throw new FormatException("A follow-up story requires an investigation lead in the catalog.");
                foreach (string id in entry.RequiredCompletedStoryIds)
                    if (!storyIds.Contains(id)) throw new FormatException("A required completed story must exist in the journey catalog.");
            }
            var visiting = new HashSet<EncounterJourneyAsset>();
            var visited = new HashSet<EncounterJourneyAsset>();
            foreach (var entry in journeys) ValidatePrerequisiteCycles(entry, visiting, visited);
        }

        private void ValidatePrerequisiteCycles(EncounterJourneyAsset entry, HashSet<EncounterJourneyAsset> visiting,
            HashSet<EncounterJourneyAsset> visited)
        {
            if (visited.Contains(entry)) return;
            if (!visiting.Add(entry)) throw new FormatException("Journey completion prerequisites cannot contain a cycle.");
            foreach (var encounter in entry.RequiredCompletedEncounters)
            {
                var required = Find(encounter.Identity);
                if (required != null) ValidatePrerequisiteCycles(required, visiting, visited);
            }
            foreach (string id in entry.RequiredCompletedStoryIds) ValidatePrerequisiteCycles(FindStory(id), visiting, visited);
            visiting.Remove(entry);
            visited.Add(entry);
        }
    }
}
