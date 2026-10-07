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
