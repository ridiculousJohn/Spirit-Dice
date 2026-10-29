using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Die Binding")]
    public sealed class DieBindingRuleAsset : EncounterRuleAsset
    {
        [SerializeField] private bool marksAfterOpeningRoll = true;
        [SerializeField] private bool marksAfterPlayerReroll = true;

        public override EncounterRuleDefinition Build()
            => new DieBindingRuleDefinition(Identity, marksAfterOpeningRoll, marksAfterPlayerReroll);
    }
}
