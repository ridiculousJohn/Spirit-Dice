using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;

namespace Dice.Editor
{
    /**
     * The game content the Save Games window reads saves against, loaded from the project's application configuration as
     * startup loads it: the mechanical catalog, the charm presentation and the journey catalog. It names the content a
     * profile stores and lists what a profile can store, in the order the game lists it.
     */
    internal sealed class SaveGameContent
    {
        /** One thing a profile can store: its id, the heading it is listed under and its display name. */
        internal readonly struct Entry<TId>
        {
            public TId Id { get; }
            public string Group { get; }
            public string Name { get; }

            public Entry(TId id, string group, string name)
            {
                Id = id;
                Group = group;
                Name = name;
            }
        }

        public string ConfigurationPath { get; }
        public MechanicalCatalog Catalog { get; }
        public ModifierPresentationCatalog Presentation { get; }
        public EncounterJourneyCatalogAsset Journeys { get; }
        /** The journeys in chapter order, each under its chapter's title. */
        public IReadOnlyList<Entry<EncounterJourneyAsset.Id>> JourneyEntries { get; }
        /** The catalog's encounters in catalog order, grouped under the chapter of the journey that plays each. */
        public IReadOnlyList<Entry<EncounterDefinition.Id>> EncounterEntries { get; }
        public IReadOnlyList<Entry<ModifierFamilyDefinition.Id>> FamilyEntries { get; }
        /** The permanent unlock charms in catalog order, grouped under their family's name. */
        public IReadOnlyList<Entry<ModifierDefinition.Id>> CharmEntries { get; }

        private SaveGameContent(string configurationPath, MechanicalContent content, ModifierPresentationCatalog presentation,
            EncounterJourneyCatalogAsset journeys)
        {
            ConfigurationPath = configurationPath;
            Catalog = content.Catalog;
            Presentation = presentation;
            Journeys = journeys;
            JourneyEntries = journeys.Chapters
                .SelectMany(chapter => chapter.Entries.Select(journey =>
                    new Entry<EncounterJourneyAsset.Id>(journey.Identity, chapter.Title, journey.LocationTitle)))
                .ToList();
            EncounterEntries = Catalog.Encounters
                .Select(encounter => new Entry<EncounterDefinition.Id>(encounter.Identity, EncounterGroup(encounter.Identity),
                    EncounterName(encounter.Identity)))
                .GroupBy(entry => entry.Group)
                .SelectMany(group => group)
                .ToList();
            FamilyEntries = Catalog.Families
                .Select(family => new Entry<ModifierFamilyDefinition.Id>(family.Identity, "", FamilyName(family.Identity)))
                .ToList();
            CharmEntries = Catalog.Modifiers
                .Where(modifier => modifier.Availability.Access == ModifierAccess.PermanentUnlock)
                .Select(modifier => new Entry<ModifierDefinition.Id>(modifier.Identity, Presentation.GetFamilyName(modifier),
                    Presentation.GetName(modifier)))
                .GroupBy(entry => entry.Group)
                .SelectMany(group => group)
                .ToList();
        }

        /** Loads the project's one application configuration and validates its content as startup does. */
        internal static SaveGameContent Load()
        {
            var configurations = ApplicationConfigurationAssets.FindAll();
            if (configurations.Count != 1)
                throw new InvalidOperationException("The project requires exactly one application configuration (DiceApplicationConfig); it has " +
                    configurations.Count + ".");
            var (path, configuration) = configurations[0];
            configuration.Validate();
            var journeys = configuration.JourneyCatalog ??
                throw new InvalidOperationException("The application configuration " + path + " has no journey catalog.");
            var content = configuration.CatalogAsset.Build();
            var presentation = new ModifierPresentationCatalog(configuration.ModifierSkin, content);
            journeys.Validate(content.Catalog);
            return new SaveGameContent(path, content, presentation, journeys);
        }

        public string JourneyName(EncounterJourneyAsset.Id id)
            => Journeys.Entries.FirstOrDefault(journey => journey.Identity == id)?.LocationTitle ?? Unlisted(id.Value);

        /** The location title of the journey that plays the encounter, or the encounter asset's name when no journey plays it. */
        public string EncounterName(EncounterDefinition.Id id) => Journeys.Find(id)?.LocationTitle ?? Unlisted(id.Value);

        public string FamilyName(ModifierFamilyDefinition.Id id)
        {
            var modifier = Catalog.Modifiers.FirstOrDefault(candidate => candidate.Family.Identity == id);
            return modifier != null ? Presentation.GetFamilyName(modifier) : Unlisted(id.Value);
        }

        public string CharmName(ModifierDefinition.Id id)
        {
            var modifier = Catalog.Modifiers.FirstOrDefault(candidate => candidate.Identity == id);
            return modifier != null ? Presentation.GetName(modifier) : Unlisted(id.Value);
        }

        private string EncounterGroup(EncounterDefinition.Id id)
        {
            var journey = Journeys.Find(id);
            return journey == null ? "Played by no journey" :
                Journeys.Chapters.First(chapter => chapter.Entries.Contains(journey)).Title;
        }

        /** Content ids are asset GUIDs, so content without a display name is named by its asset, or by its id when no asset has it. */
        private static string Unlisted(Guid id)
        {
            string path = AssetDatabase.GUIDToAssetPath(id.ToString("N"));
            return string.IsNullOrEmpty(path) ? "Unknown id " + id.ToString("N") : Path.GetFileNameWithoutExtension(path);
        }
    }
}
