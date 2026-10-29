using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Bay Ward")]
    public sealed class BayWardRuleAsset : EncounterRuleAsset
    {
        [Tooltip("The table piece whose bay the ward protects; its encounter must place it with a position interaction.")]
        [SerializeField] private TablePieceAsset piece;
        [SerializeField] private DieInterferenceKind[] blockedEffects = { DieInterferenceKind.ForcedReroll, DieInterferenceKind.Flip };

        public override EncounterRuleDefinition Build()
        {
            if (piece == null) throw new InvalidOperationException("The ward rule " + name + " references no table piece.");
            return new BayWardRuleDefinition(Identity, piece.Identity, blockedEffects);
        }
    }
}
