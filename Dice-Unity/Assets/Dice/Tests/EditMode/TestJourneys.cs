using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dice.Presentation;
using Dice.UnityContent;

namespace Dice.Tests
{
    /**
     * Fills fabricated journey content through its serialized fields: identities, catalogs, chapters and journey beats.
     */
    internal static class TestJourneys
    {
        /** The test journey id named by the text: each name has one id, derived from the name. */
        public static EncounterJourneyAsset.Id IdOf(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            using (var hash = MD5.Create())
                return new EncounterJourneyAsset.Id(new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("journey:" + name))));
        }

        /** Names the journey and stores the test journey id of the name as its asset GUID. */
        public static EncounterJourneyAsset Identify(EncounterJourneyAsset journey, string name)
        {
            journey.name = name;
            SyntheticUi.Set(journey, ContentAsset.AssetGuidField, IdOf(name).Value.ToString("N"));
            return journey;
        }

        /**
         * Replaces the journey's beats with the given new beats; the first beat sets the scene, and an arrival beat shows the
         * host arriving.
         */
        public static void SetBeats(EncounterJourneyAsset journey, JourneySceneAsset scene, params EncounterJourneyBeat[] beats)
        {
            SyntheticUi.Set(journey, "beats", beats);
            if (beats.Length > 0) SetBeat(journey, 0, "scene", scene);
            for (int index = 0; index < beats.Length; ++index)
                if (beats[index] is ArrivalBeat) SetBeat(journey, index, "hostState", SpiritPresentationState.Arrival);
        }

        /**
         * Replaces the journey's beats with the intro beats, then an invitation and the Encounter beat that plays the
         * encounter; the first beat sets the scene.
         */
        public static void SetEncounterBeats(EncounterJourneyAsset journey, JourneySceneAsset scene, EncounterAsset encounter,
            params EncounterJourneyBeat[] intro)
        {
            SetBeats(journey, scene, intro.Concat(new EncounterJourneyBeat[] { new InvitationBeat(), new EncounterBeat() }).ToArray());
            SetBeat(journey, journey.Beats.Count - 1, "encounter", encounter);
        }

        /** Authors the tray of the table scene, which every table scene must author to validate. */
        public static EncounterTablePresentationAsset WithTray(EncounterTablePresentationAsset table, SpiritTrayPresentationAsset tray)
        {
            SyntheticUi.Set(table, "trayPresentation", tray);
            return table;
        }

        /** Sets an authored field of the journey's beat through the journey's serialized form. */
        public static void SetBeat(EncounterJourneyAsset journey, int beat, string field, object value)
            => SyntheticUi.Set(journey, "beats.Array.data[" + beat + "]." + field, value);

        /** Sets an authored field of the journey's Encounter beat of the encounter. */
        public static void SetEncounterBeat(EncounterJourneyAsset journey, EncounterAsset encounter, string field, object value)
            => SetBeat(journey, journey.IndexOfEncounter(encounter.Identity), field, value);

        /** Fills the chapter with the given id, used as its title too, and journeys. */
        public static EncounterChapterAsset Chapter(EncounterChapterAsset chapter, string id, params EncounterJourneyAsset[] entries)
        {
            SyntheticUi.Set(chapter, "id", id);
            SyntheticUi.Set(chapter, "title", id);
            SyntheticUi.Set(chapter, "entries", entries);
            return chapter;
        }

        /** Fills the catalog with the given chapters. */
        public static EncounterJourneyCatalogAsset Catalog(EncounterJourneyCatalogAsset catalog, params EncounterChapterAsset[] chapters)
        {
            SyntheticUi.Set(catalog, "chapters", chapters);
            return catalog;
        }
    }
}
