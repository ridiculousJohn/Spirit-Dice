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

        public EncounterJourneyAsset Find(string encounterId)
        {
            foreach (var entry in Entries)
                if (entry != null && entry.Id == encounterId) return entry;
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
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in journeys)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
                    throw new FormatException("Journey IDs must be nonempty and unique.");
                entry.Validate(catalog);
            }
            foreach (var entry in journeys)
            {
                foreach (string id in entry.FollowUpEncounterIds)
                    if (!ids.Contains(id)) throw new FormatException("A follow-up encounter requires an investigation lead in the catalog.");
                foreach (string id in entry.RequiredCompletedStoryIds)
                    if (Find(id)?.IsStoryOnly != true)
                        throw new FormatException("A required completed story must exist in the journey catalog.");
            }
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in journeys) ValidatePrerequisiteCycles(entry, visiting, visited);
        }

        private void ValidatePrerequisiteCycles(EncounterJourneyAsset entry, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(entry.Id)) return;
            if (!visiting.Add(entry.Id)) throw new FormatException("Journey completion prerequisites cannot contain a cycle.");
            foreach (string id in entry.RequiredCompletedEncounterIds)
            {
                var required = Find(id);
                if (required != null && !required.IsStoryOnly) ValidatePrerequisiteCycles(required, visiting, visited);
            }
            foreach (string id in entry.RequiredCompletedStoryIds) ValidatePrerequisiteCycles(Find(id), visiting, visited);
            visiting.Remove(entry.Id);
            visited.Add(entry.Id);
        }
    }
}
