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
    }
}
