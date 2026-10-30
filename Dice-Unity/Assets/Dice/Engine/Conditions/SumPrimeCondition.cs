namespace Dice.Engine
{
    public sealed class SumPrimeCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            for (int divisor = 2; divisor * divisor <= dice.Sum; ++divisor) if (dice.Sum % divisor == 0) return false;
            return true;
        }
    }
}
