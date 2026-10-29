using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Cycling Favored Face Multiplier")]
    public sealed class CyclingFavoredFaceMultiplierRuleAsset : EncounterRuleAsset
    {
        [SerializeField, Min(1)] private int multiplierPerFavoredDie = 2;

        public override EncounterRuleDefinition Build()
            => new CyclingFavoredFaceMultiplierRuleDefinition(Identity, multiplierPerFavoredDie);
    }
}
