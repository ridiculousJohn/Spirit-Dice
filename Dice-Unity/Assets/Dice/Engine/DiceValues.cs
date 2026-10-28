using System;

namespace Dice.Engine
{
    public readonly struct DiceValues : IEquatable<DiceValues>
    {
        public const int Count = 5;
        public const int AllMask = (1 << Count) - 1;
        public const int MinimumFace = 1;
        public const int MaximumFace = 6;
        public const int FaceCount = MaximumFace - MinimumFace + 1;
        /** The largest difference between two faces. */
        public const int MaximumFaceDifference = MaximumFace - MinimumFace;
        public const int MaximumSum = Count * MaximumFace;
        private readonly int first;
        private readonly int second;
        private readonly int third;
        private readonly int fourth;
        private readonly int fifth;

        public DiceValues(int first, int second, int third, int fourth, int fifth)
        {
            ValidateFace(first);
            ValidateFace(second);
            ValidateFace(third);
            ValidateFace(fourth);
            ValidateFace(fifth);
            this.first = first;
            this.second = second;
            this.third = third;
            this.fourth = fourth;
            this.fifth = fifth;
        }

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return first;
                    case 1: return second;
                    case 2: return third;
                    case 3: return fourth;
                    case 4: return fifth;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public int Sum => first + second + third + fourth + fifth;
        public bool IsValid => IsFace(first) && IsFace(second) && IsFace(third) && IsFace(fourth) && IsFace(fifth);
        public int[] ToArray() => new[] { first, second, third, fourth, fifth };

        /** The number of different faces among the dice. */
        public int DistinctFaceCount
        {
            get
            {
                int seen = 0, distinct = 0;
                for (int i = 0; i < Count; ++i)
                {
                    int bit = 1 << this[i];
                    if ((seen & bit) == 0) ++distinct;
                    seen |= bit;
                }
                return distinct;
            }
        }

        /** The dice whose faces differ from the other dice in the same bays. */
        public int DifferingMask(DiceValues other)
        {
            int mask = 0;
            for (int i = 0; i < Count; ++i)
                if (this[i] != other[i]) mask |= 1 << i;
            return mask;
        }

        /** The number of bays a dice mask selects. */
        public static int CountBays(int mask)
        {
            int count = 0;
            for (int i = 0; i < Count; ++i)
                if ((mask & (1 << i)) != 0) ++count;
            return count;
        }

        /** The bay at the given zero-based position among the bays a dice mask selects, counting from the first bay. */
        public static int NthBay(int mask, int index)
        {
            for (int i = 0; i < Count; ++i)
                if ((mask & (1 << i)) != 0 && index-- == 0) return i;
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        internal DiceValues With(int index, int face)
        {
            int[] faces = ToArray();
            if ((uint)index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            faces[index] = face;
            return new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]);
        }

        public bool Equals(DiceValues other) => first == other.first && second == other.second &&
            third == other.third && fourth == other.fourth && fifth == other.fifth;
        public override bool Equals(object obj) => obj is DiceValues other && Equals(other);
        public override int GetHashCode() => ((((first * 7 + second) * 7 + third) * 7 + fourth) * 7 + fifth);
        public static bool operator ==(DiceValues left, DiceValues right) => left.Equals(right);
        public static bool operator !=(DiceValues left, DiceValues right) => !left.Equals(right);

        public static bool IsFace(int value) => value >= MinimumFace && value <= MaximumFace;

        /** The face on the opposite side of a die. */
        internal static int OppositeFace(int face) => MinimumFace + MaximumFace - face;

        internal static int RollFace(ref DeterministicRandom random) => random.NextInt(FaceCount) + MinimumFace;

        private static void ValidateFace(int value)
        {
            if (!IsFace(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
