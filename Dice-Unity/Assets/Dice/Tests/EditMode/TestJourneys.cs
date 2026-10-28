using System.Linq;
using Dice.Presentation;

namespace Dice.Tests
{
    /** Fills fabricated journey content through its serialized fields: catalogs, chapters and journey beats. */
    internal static class TestJourneys
    {
        public static (string Id, EncounterJourneyBeatKind Kind) Beat(string id, EncounterJourneyBeatKind kind) => (id, kind);

        /** Replaces the journey's beats with new default beats of the given ids and kinds; an arrival beat shows the host arriving. */
        public static void SetBeats(EncounterJourneyAsset journey, params (string Id, EncounterJourneyBeatKind Kind)[] beats)
        {
            SyntheticUi.Set(journey, "beats", beats.Select(beat => new EncounterJourneyBeat()).ToArray());
            for (int index = 0; index < beats.Length; ++index)
            {
                SetBeat(journey, index, "id", beats[index].Id);
                SetBeat(journey, index, "kind", beats[index].Kind);
                if (beats[index].Kind == EncounterJourneyBeatKind.Arrival)
                    SetBeat(journey, index, "hostState", SpiritPresentationState.Arrival);
            }
        }

        /** Sets an authored field of the journey's beat through the journey's serialized form. */
        public static void SetBeat(EncounterJourneyAsset journey, int beat, string field, object value)
            => SyntheticUi.Set(journey, "beats.Array.data[" + beat + "]." + field, value);

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
