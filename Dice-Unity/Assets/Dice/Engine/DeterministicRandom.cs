using System;

namespace Dice.Engine
{
    public struct DeterministicRandom
    {
        public const int AlgorithmVersion = 1;
        public ulong State { get; private set; }

        public DeterministicRandom(ulong state) => State = state;

        // SplitMix64 uses explicit wrapping arithmetic so runtime overflow settings cannot change a stream.
        public ulong NextUInt64()
        {
            unchecked
            {
                State += 0x9e3779b97f4a7c15UL;
                ulong value = State;
                value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
                value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
                return value ^ (value >> 31);
            }
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            ulong bound = (ulong)exclusiveMaximum;
            ulong threshold = unchecked(0UL - bound) % bound;
            ulong value;
            do { value = NextUInt64(); } while (value < threshold);
            return (int)(value % bound);
        }
    }
}
