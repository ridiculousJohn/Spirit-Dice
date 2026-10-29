using System;

namespace Dice.Engine
{
    /** A charm family: the charms that share a trigger and are discovered, offered and listed together. */
    public sealed class ModifierFamilyDefinition
    {
        /** Identifies a charm family where an identity is stored, as in saves and the player profile. */
        public readonly struct Id : IEquatable<Id>, IContentId
        {
            public Guid Value { get; }

            public Id(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentException("A charm family id requires a nonempty GUID.", nameof(value));
                Value = value;
            }

            public bool Equals(Id other) => Value == other.Value;
            public override bool Equals(object obj) => obj is Id other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            /** The 32 hexadecimal digits, as the family's asset and its .meta file write its GUID. */
            public override string ToString() => Value.ToString("N");
            public static bool operator ==(Id left, Id right) => left.Equals(right);
            public static bool operator !=(Id left, Id right) => !left.Equals(right);
        }

        // A nested type and a member cannot share a name, so the id property is not called Id.
        public Id Identity { get; }

        public ModifierFamilyDefinition(Id identity)
        {
            if (identity.Value == Guid.Empty) throw new ArgumentException("A charm family requires an id.", nameof(identity));
            Identity = identity;
        }
    }
}
