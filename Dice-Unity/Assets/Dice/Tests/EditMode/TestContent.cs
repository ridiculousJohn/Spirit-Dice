using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.UnityContent;
using UnityEditor;

namespace Dice.Tests
{
    internal static class TestContent
    {
        internal const string EncounterId = "encounter.test";

        internal static ModifierAsset[] ModifierAssets() => AssetDatabase.FindAssets("t:ModifierAsset",
                new[] { "Assets/Dice/Content/Mechanical/Modifiers" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ModifierAsset>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(asset => asset.Id, System.StringComparer.Ordinal).ToArray();

        internal static MechanicalCatalog BuildStandardCatalog() => AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>(
            "Assets/Dice/Content/Mechanical/Standard.asset").Build(ModifierAssets());

        internal static HandDefinition[] Hands()
        {
            return new[]
            {
                new HandDefinition("hand.high_die", HandCategory.HighDie, 5, 1, 5, 1, 6),
                new HandDefinition("hand.pair", HandCategory.Pair, 5, 2, 5, 1, 6),
                new HandDefinition("hand.two_pair", HandCategory.TwoPair, 10, 3, 5, 1, 6),
                new HandDefinition("hand.three_of_a_kind", HandCategory.ThreeOfAKind, 15, 4, 5, 1, 6),
                new HandDefinition("hand.full_house", HandCategory.FullHouse, 20, 6, 5, 1, 6),
                new HandDefinition("hand.four_of_a_kind", HandCategory.FourOfAKind, 25, 8, 5, 1, 6),
                new HandDefinition("hand.straight", HandCategory.Straight, 25, 10, 5, 1, 6),
                new HandDefinition("hand.five_of_a_kind", HandCategory.FiveOfAKind, 30, 14, 5, 2, 6)
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
    }
}
