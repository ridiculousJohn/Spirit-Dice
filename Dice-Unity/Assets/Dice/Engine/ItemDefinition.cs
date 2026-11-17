using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    /**
     * Something that provides effects to an encounter: encounter rules and optionally one table piece with its
     * interactions. An effect is the same whatever provides it; an encounter in which the item is in play has the item's
     * rules and piece in play beside its own.
     */
    public sealed class ItemDefinition
    {
        /** Identifies an item where its identity is needed, as the item presentations are found by it. */
        public readonly struct Id : IEquatable<Id>, IContentId
        {
            public Guid Value { get; }

            public Id(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentException("An item id requires a nonempty GUID.", nameof(value));
                Value = value;
            }

            public bool Equals(Id other) => Value == other.Value;
            public override bool Equals(object obj) => obj is Id other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            /** The 32 hexadecimal digits, as the item's asset and its .meta file write its GUID. */
            public override string ToString() => Value.ToString("N");
            public static bool operator ==(Id left, Id right) => left.Equals(right);
            public static bool operator !=(Id left, Id right) => !left.Equals(right);
        }

        // A nested type and a member cannot share a name, so the id property is not called Id.
        public Id Identity { get; }
        /** The rules the item provides, zero or more. */
        public ReadOnlyCollection<EncounterRuleDefinition> Rules { get; }
        /** The table piece the item places with its interactions, or null for an item without a piece. */
        public TablePieceDefinition Piece { get; }

        /** An item provides at least one rule or a piece; its rules are validated against the encounter it is in play in. */
        public ItemDefinition(Id identity, IEnumerable<EncounterRuleDefinition> rules = null, TablePieceDefinition piece = null)
        {
            if (identity.Value == Guid.Empty) throw new ArgumentException("An item requires an id.", nameof(identity));
            var ruleList = rules == null ? new List<EncounterRuleDefinition>() : new List<EncounterRuleDefinition>(rules);
            var ruleIds = new HashSet<EncounterRuleDefinition.Id>();
            foreach (var rule in ruleList)
                if (rule == null || !ruleIds.Add(rule.Identity))
                    throw new ArgumentException("Item rule IDs must be unique and nonnull.", nameof(rules));
            if (ruleList.Count == 0 && piece == null)
                throw new ArgumentException("An item must provide at least one rule or a piece.", nameof(rules));
            Identity = identity;
            Rules = ruleList.AsReadOnly();
            Piece = piece;
        }

        /** The item installed at the start of a run: its rules and its piece, each with its starting state. */
        internal InstalledItem Install() => new InstalledItem(this, RuleList.Install(Rules), Piece == null ? null : PieceList.Install(Piece));

        /** The item's saved entry: its id, its rules' entries and its piece's entry, null for an item without a piece. */
        internal EncounterSaveItemData Save(InstalledItem installed) => new EncounterSaveItemData
        {
            ItemId = Identity, Rules = RuleList.Save(installed.Rules), Piece = installed.Piece == null ? null : PieceList.Save(installed.Piece)
        };

        /**
         * The item installed from its saved entry, which holds exactly its rules and its piece, the piece's entry present
         * exactly when the item has a piece; a failure rejects the save.
         */
        internal InstalledItem Load(EncounterSaveItemData saved)
        {
            string owner = "item " + Identity;
            var rules = RuleList.Load(owner, Rules, saved.Rules);
            if (Piece == null)
            {
                SavedEntries.Require(saved.Piece == null, "The " + owner + " has a saved entry for the table piece " + saved.Piece?.PieceId +
                    ", which it does not create.");
                return new InstalledItem(this, rules, null);
            }
            SavedEntries.Require(saved.Piece != null, "The " + owner + " has no saved entry for its table piece " + Piece.Identity + ".");
            SavedEntries.Require(saved.Piece.PieceId == Piece.Identity, "The " + owner + " has a saved entry for the table piece " +
                saved.Piece.PieceId + ", which it does not create.");
            return new InstalledItem(this, rules, PieceList.Load(owner, Piece, saved.Piece));
        }
    }

    /** An item installed in a run: its installed rules, in the item's rule order, and its installed piece, null without one. */
    internal sealed class InstalledItem
    {
        internal ItemDefinition Item { get; }
        internal List<InstalledRule> Rules { get; }
        internal InstalledPiece Piece { get; }

        internal InstalledItem(ItemDefinition item, List<InstalledRule> rules, InstalledPiece piece)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Piece = piece;
        }

        internal InstalledItem Copy() => new InstalledItem(Item, Rules.ConvertAll(rule => rule.Copy()), Piece?.Copy());
    }

    /** Installs, saves and loads a list of items, as an encounter does the items it lends and a run the items it carries. */
    internal static class ItemList
    {
        internal static List<InstalledItem> Install(IReadOnlyList<ItemDefinition> items)
        {
            var installed = new List<InstalledItem>(items.Count);
            foreach (var item in items) installed.Add(item.Install());
            return installed;
        }

        /** One entry per installed item, in the owner's order. */
        internal static EncounterSaveItemData[] Save(List<InstalledItem> installed)
            => installed.ConvertAll(item => item.Item.Save(item)).ToArray();

        /**
         * The owner's items installed from its saved entries, each read by its item's id; owner names the encounter or the
         * run in failures, such as "encounter X".
         */
        internal static List<InstalledItem> Load(string owner, IReadOnlyList<ItemDefinition> items, EncounterSaveItemData[] saved)
        {
            var entries = SavedEntries.ById(owner, saved, entry => entry.ItemId, "item");
            var installed = new List<InstalledItem>(items.Count);
            foreach (var item in items) installed.Add(item.Load(SavedEntries.Take(owner, entries, item.Identity, "item")));
            SavedEntries.RequireNoneLeft(owner, entries, "item");
            return installed;
        }
    }
}
