using System.Collections.Generic;

namespace Dice.Presentation
{
    /**
     * The rule cards in play at an encounter table: the table's own cards, which present the encounter's own rules, then
     * the cards of the presentation of each item in play, in item order (ItemPresentationCatalog.CardsInPlay). The
     * invitation, the HUD's rule badge, score playback and the rule presenters all take their cards from here.
     */
    public sealed class RuleCardsInPlay
    {
        public IReadOnlyList<EncounterRulePresentationAsset> Cards { get; }
        /** The card in play flagged as shown on the HUD, or null when none is; more than one flagged card fails on creation. */
        public EncounterRulePresentationAsset HudCard { get; }

        internal RuleCardsInPlay(EncounterTablePresentationAsset table, List<EncounterRulePresentationAsset> cards)
        {
            Cards = cards.AsReadOnly();
            HudCard = EncounterRulePresentationAsset.HudCardOf(Cards, "The rule cards in play at encounter table '" + table.name + "'");
        }
    }
}
