using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/First Reroll Free")]
    public sealed class FirstRerollFreeRuleAsset : EncounterRuleAsset
    {
        public override EncounterRuleDefinition Build() => new FirstRerollFreeRuleDefinition();
    }
}
