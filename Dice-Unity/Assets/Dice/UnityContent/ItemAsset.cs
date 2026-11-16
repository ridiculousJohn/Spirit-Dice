using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * An item: effects one provider gives, its rules and optionally one table piece with its interactions, authored as an
     * encounter's pieces entry is. Its identity is its asset GUID. It holds no name, art or text; an item presentation
     * references it. An encounter lends it by listing it among its lent items.
     */
    [CreateAssetMenu(menuName = "Dice/Item")]
    public sealed class ItemAsset : ContentAsset
    {
        [SerializeField] private EncounterRuleAsset[] rules = Array.Empty<EncounterRuleAsset>();
        [Tooltip("The table piece the item places and its interactions; an item without a piece leaves the piece empty.")]
        [SerializeField] private PieceData piece = new PieceData();

        public ItemDefinition.Id Identity => new ItemDefinition.Id(AssetGuid);

        /** The item's definition; a failure names the item. */
        internal ItemDefinition Build()
        {
            string owner = "item " + name;
            EncounterRuleDefinition[] ruleDefinitions = EncounterRuleAsset.BuildAll(rules, owner);
            TablePieceDefinition pieceDefinition = null;
            if (piece.piece != null) pieceDefinition = piece.Build(owner);
            else if (piece.interactions != null && piece.interactions.Length > 0)
                throw new InvalidOperationException("The item " + name + " authors piece interactions without a piece.");
            try { return new ItemDefinition(Identity, ruleDefinitions, pieceDefinition); }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("The item " + name + " is invalid: " + exception.Message, exception);
            }
        }
    }
}
