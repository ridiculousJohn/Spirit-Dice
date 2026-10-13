using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Cycling Favored Face Multiplier")]
    public sealed class CyclingFavoredFaceMultiplierRuleAsset : EncounterRuleAsset
    {
        public override EncounterRuleDefinition Build() => new CyclingFavoredFaceMultiplierRuleDefinition();
    }
}
