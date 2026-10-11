using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Distinct Faces Multiplier")]
    public sealed class DistinctFacesMultiplierRuleAsset : EncounterRuleAsset
    {
        public override EncounterRuleDefinition Build() => new DistinctFacesMultiplierRuleDefinition();
    }
}
