using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Encounter Rules/Die Carry Over")]
    public sealed class DieCarryOverRuleAsset : EncounterRuleAsset
    {
        [Tooltip("The table piece whose bay carries a die into the next hand; wherever the rule is in play, the piece must be in play with a position interaction.")]
        [SerializeField] private TablePieceAsset piece;
        [Tooltip("The charms offered while this rule is in play.")]
        [SerializeField] private ModifierAsset[] modifiers = Array.Empty<ModifierAsset>();

        public override IReadOnlyList<ModifierAsset> AddedModifiers => modifiers;

        public override EncounterRuleDefinition Build()
        {
            if (piece == null) throw new InvalidOperationException("The carry-over rule " + name + " references no table piece.");
            if (modifiers == null) throw new InvalidOperationException("The carry-over rule " + name + " has no charm list.");
            var ids = new ModifierDefinition.Id[modifiers.Length];
            for (int i = 0; i < ids.Length; ++i)
                ids[i] = (modifiers[i] ?? throw new InvalidOperationException("A charm reference in the charm list of the rule " + name +
                    " is missing.")).Identity;
            return new DieCarryOverRuleDefinition(Identity, piece.Identity, ids);
        }
    }
}
