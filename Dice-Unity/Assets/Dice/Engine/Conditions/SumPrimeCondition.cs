namespace Dice.Engine
{
    public sealed class SumPrimeCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            int sum = context.CurrentHand.Sum;
            // Dice that do not count can leave a sum below the smallest prime.
            if (sum < 2) return false;
            for (int divisor = 2; divisor * divisor <= sum; ++divisor) if (sum % divisor == 0) return false;
            return true;
        }
    }
}
