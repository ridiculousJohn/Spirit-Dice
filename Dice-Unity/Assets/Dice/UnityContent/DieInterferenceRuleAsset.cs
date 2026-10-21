using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Die Interference")]
    public sealed class DieInterferenceRuleAsset : EncounterRuleAsset
    {
        [SerializeField] private string ruleId;
        [SerializeField, Range(0, 100)] private int chancePercent = 20;
        [SerializeField, Min(0)] private int forcedRerollWeight = 1;
        [SerializeField, Min(0)] private int flipWeight = 1;
        [SerializeField] private bool afterPlayerReroll = true;
        [SerializeField] private bool afterPlayHand = true;

        public override EncounterRuleDefinition Build() => new DieInterferenceRuleDefinition(ruleId, chancePercent,
            forcedRerollWeight, flipWeight, afterPlayerReroll, afterPlayHand);
    }
}
