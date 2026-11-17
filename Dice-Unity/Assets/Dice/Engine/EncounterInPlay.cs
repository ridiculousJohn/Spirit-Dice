using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    /** What a run plays: a catalog's encounter and the items the player carries into it. */
    public sealed class EncounterInPlay
    {
        /** The catalog's encounter: its stages, policy, own rules and pieces and the items it lends. */
        public EncounterDefinition Encounter { get; }
        /**
         * The items the player carries, those the encounter does not lend; an item the encounter lends is in play once, as
         * lent, and is not carried.
         */
        public ReadOnlyCollection<ItemDefinition> CarriedItems { get; }
        /** The items in play: the items the encounter lends, then CarriedItems. */
        public ReadOnlyCollection<ItemDefinition> Items { get; }
        /** The rules in play: the encounter's rules, then the rules of each carried item, in item order. */
        public ReadOnlyCollection<EncounterRuleDefinition> Rules { get; }
        /** The table pieces in play: the encounter's pieces, then the piece of each carried item that has one, in item order. */
        public ReadOnlyCollection<TablePieceDefinition> Pieces { get; }
        /**
         * The charms the carried items' rules bring, in rule order: candidates in every charm offer from the first, and never
         * the encounter's charm additions.
         */
        public ReadOnlyCollection<ModifierDefinition.Id> CarriedModifierIds { get; }

        /**
         * Validates what is in play: no item, rule, piece or charm in play twice, and every rule checked against everything
         * in play, so a rule may rely on what an item provides.
         */
        public EncounterInPlay(EncounterDefinition encounter, IEnumerable<ItemDefinition> carriedItems)
        {
            if (carriedItems == null) throw new ArgumentNullException(nameof(carriedItems));
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            var lentIds = new HashSet<ItemDefinition.Id>();
            foreach (var item in encounter.Items) lentIds.Add(item.Identity);
            var carried = new List<ItemDefinition>();
            foreach (var item in carriedItems)
            {
                if (item == null) throw new ArgumentException("Carried items must be nonnull.", nameof(carriedItems));
                if (!lentIds.Contains(item.Identity)) carried.Add(item);
            }
            var itemList = new List<ItemDefinition>(encounter.Items);
            itemList.AddRange(carried);
            var itemIds = new HashSet<ItemDefinition.Id>();
            foreach (var item in itemList)
                if (!itemIds.Add(item.Identity))
                    throw new ArgumentException("The item " + item.Identity + " is in play more than once.", nameof(carriedItems));
            CarriedItems = carried.AsReadOnly();
            Items = itemList.AsReadOnly();
            var ruleList = new List<EncounterRuleDefinition>(encounter.Rules);
            var carriedModifierIds = new List<ModifierDefinition.Id>();
            foreach (var item in carried)
                foreach (var rule in item.Rules)
                {
                    ruleList.Add(rule);
                    carriedModifierIds.AddRange(rule.AddedModifierIds);
                }
            var ruleIds = new HashSet<EncounterRuleDefinition.Id>();
            foreach (var rule in ruleList)
                if (!ruleIds.Add(rule.Identity))
                    throw new ArgumentException("The rule " + rule.Identity + " is in play more than once.", nameof(carriedItems));
            Rules = ruleList.AsReadOnly();
            var modifierIds = new List<ModifierDefinition.Id>(encounter.EncounterModifierIds);
            modifierIds.AddRange(carriedModifierIds);
            var uniqueModifierIds = new HashSet<ModifierDefinition.Id>();
            foreach (var modifierId in modifierIds)
                if (!uniqueModifierIds.Add(modifierId))
                    throw new ArgumentException("The charm " + modifierId + " is in play more than once.", nameof(carriedItems));
            CarriedModifierIds = carriedModifierIds.AsReadOnly();
            var pieceList = new List<TablePieceDefinition>(encounter.Pieces);
            foreach (var item in carried)
                if (item.Piece != null) pieceList.Add(item.Piece);
            var pieceIds = new HashSet<TablePieceDefinition.Id>();
            foreach (var piece in pieceList)
                if (!pieceIds.Add(piece.Identity))
                    throw new ArgumentException("The table piece " + piece.Identity + " is in play more than once.", nameof(carriedItems));
            Pieces = pieceList.AsReadOnly();
            foreach (var rule in Rules) rule.ValidateDefinition(this);
        }
    }
}
