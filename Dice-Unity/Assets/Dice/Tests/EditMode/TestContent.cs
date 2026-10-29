using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    internal static class TestContent
    {
        internal static readonly EncounterDefinition.Id EncounterId = TestEncounters.IdOf("encounter.test");

        internal static HandDefinition[] Hands()
        {
            return new[]
            {
                new HandDefinition(HandCategory.HighDie, 5, 1, 5, 1, 6),
                new HandDefinition(HandCategory.Pair, 5, 2, 5, 1, 6),
                new HandDefinition(HandCategory.TwoPair, 10, 3, 5, 1, 6),
                new HandDefinition(HandCategory.ThreeOfAKind, 15, 4, 5, 1, 6),
                new HandDefinition(HandCategory.FullHouse, 20, 6, 5, 1, 6),
                new HandDefinition(HandCategory.FourOfAKind, 25, 8, 5, 1, 6),
                new HandDefinition(HandCategory.Straight, 25, 10, 5, 1, 6),
                new HandDefinition(HandCategory.FiveOfAKind, 30, 14, 5, 2, 6)
            };
        }

        internal static MechanicalCatalog Catalog(IEnumerable<StageDefinition> stages = null, int[] upgrades = null,
            IEnumerable<HandDefinition> hands = null)
        {
            var encounter = new EncounterDefinition(EncounterId,
                stages ?? new[] { new StageDefinition(1000000, 20, 20) }, upgrades);
            return new MechanicalCatalog("test.v1", hands ?? Hands(), new[] { encounter });
        }

        internal static EncounterSession Session(ulong seed = 0, IEnumerable<StageDefinition> stages = null)
            => EncounterSession.CreateEncounter(Catalog(stages), EncounterId, seed);

        /**
         * The one authored encounter table the scene-level tests run on: the first, by asset path, that the authored
         * journey catalog references whose encounter scene has scene lighting.
         */
        internal static string LitTablePath()
        {
            var config = AssetDatabase.LoadAssetAtPath<DiceApplicationConfig>("Assets/Dice/Content/DiceApplicationConfig.asset");
            Assert.That(config != null && config.JourneyCatalog != null, Is.True, "The application configuration has no journey catalog.");
            string path = config.JourneyCatalog.Entries
                .Where(journey => journey != null)
                .SelectMany(journey => journey.Beats.Select(beat => beat.Scene != null ? beat.Scene.Table : null))
                .Where(table => table != null && table.SceneLighting != null && table.Scene.RuntimeKeyIsValid())
                .Select(table => AssetDatabase.GetAssetPath(table))
                .OrderBy(value => value, System.StringComparer.Ordinal).FirstOrDefault();
            Assert.That(path, Is.Not.Null, "The authored journey catalog references no encounter table whose scene has scene lighting.");
            return path;
        }
    }
}
