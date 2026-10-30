using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.UnityContent;
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

        /** Executes the action at the session's current revision and asserts the session accepted it. */
        internal static Resolution Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result;
        }

        /** Restores the save into a session of the catalog and asserts the restore succeeded. */
        internal static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            return result.Session;
        }

        /** A fresh session of the catalog's test encounter from the seed, whose opening dice are replaced with the given faces. */
        internal static EncounterSession WithDice(MechanicalCatalog catalog, ulong seed, params int[] dice)
        {
            var save = EncounterSession.CreateEncounter(catalog, EncounterId, seed).CaptureSave();
            save.CurrentDice = dice;
            save.InitialDice = (int[])dice.Clone();
            return Restore(catalog, save);
        }

        /**
         * The one authored encounter table the scene-level tests run on: the first, by asset path, that the authored
         * journey catalog references whose encounter scene has scene lighting.
         */
        internal static string LitTablePath()
        {
            var config = Configuration();
            Assert.That(config.JourneyCatalog, Is.Not.Null, "The application configuration has no journey catalog.");
            string path = config.JourneyCatalog.Entries
                .Where(journey => journey != null)
                .SelectMany(journey => journey.Beats.Select(beat => beat.Scene != null ? beat.Scene.Table : null))
                .Where(table => table != null && table.SceneLighting != null && table.Scene.RuntimeKeyIsValid())
                .Select(table => AssetDatabase.GetAssetPath(table))
                .OrderBy(value => value, System.StringComparer.Ordinal).FirstOrDefault();
            Assert.That(path, Is.Not.Null, "The authored journey catalog references no encounter table whose scene has scene lighting.");
            return path;
        }

        /** One charm the authored content uses; which one does not matter. */
        internal static ModifierAsset AuthoredCharm()
        {
            var config = Configuration();
            Assert.That(config.CatalogAsset, Is.Not.Null, "The application configuration has no catalog.");
            var charm = config.CatalogAsset.Build().Modifiers.FirstOrDefault();
            Assert.That(charm, Is.Not.Null, "The application's catalog references no charm.");
            return charm;
        }

        private static DiceApplicationConfig Configuration()
        {
            var config = AssetDatabase.LoadAssetAtPath<DiceApplicationConfig>("Assets/Dice/Content/DiceApplicationConfig.asset");
            Assert.That(config, Is.Not.Null, "The application configuration is missing.");
            return config;
        }
    }
}
