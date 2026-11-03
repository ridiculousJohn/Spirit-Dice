using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Rot")]
    public sealed class RotRuleAsset : EncounterRuleAsset
    {
        /** What a die that rots from its lowest face becomes. */
        [SerializeField] private RotEndpoint endpoint = RotEndpoint.Wither;

        public override EncounterRuleDefinition Build() => new RotRuleDefinition(Identity, endpoint);
    }
}
