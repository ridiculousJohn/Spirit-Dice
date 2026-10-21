using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Bay Ward")]
    public sealed class BayWardRuleAsset : EncounterRuleAsset
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string pieceId;
        [SerializeField] private string interactionId = "position";
        [SerializeField] private DieInterferenceKind[] blockedEffects = { DieInterferenceKind.ForcedReroll, DieInterferenceKind.Flip };

        public override EncounterRuleDefinition Build() => new BayWardRuleDefinition(ruleId, pieceId, interactionId, blockedEffects);
    }
}
