using System;

namespace Dice.Engine
{
    /**
     * A hand: its five dice and the dice that count toward it. Scoring, hand ranks and charm conditions read only the
     * counted dice; a full hand counts every die. A hand counts at least one die.
     */
    public readonly struct HandDice : IEquatable<HandDice>
    {
        public DiceValues Faces { get; }
        /** The dice that count, as a mask of die slots. */
        public int CountedMask { get; }

        public HandDice(DiceValues faces, int countedMask)
        {
            if (!faces.IsValid) throw new ArgumentException("Five valid die faces are required.", nameof(faces));
            if (!IsCountedMask(countedMask))
                throw new ArgumentOutOfRangeException(nameof(countedMask), "A hand counts at least one of its five dice.");
            Faces = faces;
            CountedMask = countedMask;
        }

        /** The hand that counts every die. */
        public static HandDice Full(DiceValues faces) => new HandDice(faces, DiceValues.AllMask);

        /** Whether a mask names only the five bays and at least one of them, as a hand's counted dice must. */
        public static bool IsCountedMask(int mask) => mask != 0 && (mask & ~DiceValues.AllMask) == 0;

        public bool IsValid => Faces.IsValid && IsCountedMask(CountedMask);
        public int CountedCount => DiceValues.CountBays(CountedMask);

        /** Whether every die at the positions counts. */
        public bool Counts(int positions) => (CountedMask & positions) == positions;

        /** The sum of the counted dice. */
        public int Sum => SumOf(DiceValues.AllMask);

        /** The sum of the counted dice among those a mask selects. */
        public int SumOf(int mask)
        {
            int sum = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((CountedMask & mask & (1 << i)) != 0) sum += Faces[i];
            return sum;
        }

        /** The number of counted dice showing the face. */
        public int CountOf(int face) => CountOf(face, DiceValues.AllMask);

        /** The number of counted dice among those a mask selects that show the face. */
        public int CountOf(int face, int mask)
        {
            int count = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((CountedMask & mask & (1 << i)) != 0 && Faces[i] == face) ++count;
            return count;
        }

        /** The number of different faces among the counted dice. */
        public int DistinctFaceCount
        {
            get
            {
                int seen = 0, distinct = 0;
                for (int i = 0; i < DiceValues.Count; ++i)
                {
                    if ((CountedMask & (1 << i)) == 0) continue;
                    int bit = 1 << Faces[i];
                    if ((seen & bit) == 0) ++distinct;
                    seen |= bit;
                }
                return distinct;
            }
        }

        /** The lowest face among the counted dice. */
        public int Minimum
        {
            get
            {
                int minimum = DiceValues.MaximumFace;
                for (int i = 0; i < DiceValues.Count; ++i)
                    if ((CountedMask & (1 << i)) != 0) minimum = Math.Min(minimum, Faces[i]);
                return minimum;
            }
        }

        /** The highest face among the counted dice. */
        public int Maximum
        {
            get
            {
                int maximum = DiceValues.MinimumFace;
                for (int i = 0; i < DiceValues.Count; ++i)
                    if ((CountedMask & (1 << i)) != 0) maximum = Math.Max(maximum, Faces[i]);
                return maximum;
            }
        }

        public bool Equals(HandDice other) => Faces == other.Faces && CountedMask == other.CountedMask;
        public override bool Equals(object obj) => obj is HandDice other && Equals(other);
        public override int GetHashCode() => unchecked(Faces.GetHashCode() * 397 ^ CountedMask);
        public static bool operator ==(HandDice left, HandDice right) => left.Equals(right);
        public static bool operator !=(HandDice left, HandDice right) => !left.Equals(right);
    }
}
