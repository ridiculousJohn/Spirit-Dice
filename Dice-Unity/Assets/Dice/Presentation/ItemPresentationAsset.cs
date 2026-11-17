using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * How an item shows: its name, icon and glow colour, the rule cards that present exactly the rules the item provides,
     * and for an item with a table piece, how the piece shows where the scene gives it no prop. The item skin lists it.
     */
    [CreateAssetMenu(menuName = "Dice/Item Presentation")]
    public sealed class ItemPresentationAsset : ScriptableObject
    {
        [Tooltip("The item this presentation presents.")]
        [SerializeField] private ItemAsset item;
        [Tooltip("The item's name as the player reads it.")]
        [SerializeField] private string displayName;
        [Tooltip("The item's icon: its emblem on the bay its piece rests on and its picture in the item slot.")]
        [SerializeField] private Sprite icon;
        [Tooltip("The colour of the item's glow on the bay its piece rests on.")]
        [SerializeField] private Color glowColor = Color.white;
        [Tooltip("How the item's table piece shows where the scene gives it no prop; required exactly when the item places a piece, and of a kind the piece has an interaction of.")]
        [SerializeField] private ItemPiecePresenterAsset piecePresenter;
        [Tooltip("One card for each rule the item provides; they show wherever the item is in play, beside the table's own cards.")]
        [SerializeField] private EncounterRulePresentationAsset[] ruleCards = Array.Empty<EncounterRulePresentationAsset>();

        public ItemAsset Item => item;
        public IReadOnlyList<EncounterRulePresentationAsset> RuleCards => ruleCards;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Color GlowColor => glowColor;
        /** How the item's piece shows where the scene gives it no prop; null for an item without a piece. */
        public ItemPiecePresenterAsset PiecePresenter => piecePresenter;

        /**
         * Fails, naming the presentation, unless it has a name and an icon, its cards present exactly the rules of its
         * item's definition, each once, each card's text uses only the placeholders its rule supplies, and it has a piece
         * presenter exactly when the item places a piece, of a kind the piece has an interaction of and with valid settings
         * of its own.
         */
        internal void Validate(ItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(displayName)) throw new InvalidOperationException("The item presentation " + name + " has no name.");
            if (icon == null) throw new InvalidOperationException("The item presentation " + name + " has no icon.");
            if (ruleCards == null) throw new InvalidOperationException("The item presentation " + name + " has no rule card list.");
            var presented = new HashSet<EncounterRuleDefinition.Id>();
            foreach (var card in ruleCards)
            {
                if (card == null) throw new InvalidOperationException("The item presentation " + name + " has a missing rule card.");
                if (card.Rule == null)
                    throw new InvalidOperationException("The rule card " + card.name + " of the item presentation " + name + " presents no rule.");
                if (!presented.Add(card.RuleId))
                    throw new InvalidOperationException("The item presentation " + name + " has more than one card for the rule " +
                        card.Rule.name + ".");
                EncounterRuleDefinition rule = null;
                foreach (var candidate in definition.Rules)
                    if (candidate.Identity == card.RuleId) rule = candidate;
                if (rule == null)
                    throw new InvalidOperationException("The rule card " + card.name + " of the item presentation " + name +
                        " presents the rule " + card.Rule.name + ", which the item " + item.name + " does not provide.");
                try { card.ValidateText(rule); }
                catch (FormatException exception)
                {
                    throw new InvalidOperationException("The item presentation " + name + " is invalid: " + exception.Message, exception);
                }
            }
            if (presented.Count != definition.Rules.Count)
                throw new InvalidOperationException("The item presentation " + name + " has no card for some rule of the item " +
                    item.name + ".");
            ValidatePiecePresenter(definition.Piece);
        }

        private void ValidatePiecePresenter(TablePieceDefinition piece)
        {
            if (piece == null)
            {
                if (piecePresenter != null)
                    throw new InvalidOperationException("The item presentation " + name + " has the piece presenter " + piecePresenter.name +
                        ", but its item " + item.name + " places no table piece.");
                return;
            }
            if (piecePresenter == null)
                throw new InvalidOperationException("The item presentation " + name + " has no piece presenter for the table piece " +
                    piece.Identity + " of its item " + item.name + ".");
            if (piecePresenter.InteractionIndex(piece) < 0)
                throw new InvalidOperationException("The piece presenter " + piecePresenter.name + " of the item presentation " + name +
                    " presents " + piecePresenter.InteractionKind.Name + " interactions, but the item " + item.name +
                    " gives its table piece " + piece.Identity + " none.");
            piecePresenter.Validate(this);
        }
    }
}
