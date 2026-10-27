using System;

namespace Dice.Engine
{
    public readonly struct DiceValues : IEquatable<DiceValues>
    {
        public const int Count = 5;
        public const int AllMask = (1 << Count) - 1;
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

        private static bool IsFace(int value) => value >= 1 && value <= 6;
        private static void ValidateFace(int value)
        {
            if (!IsFace(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
