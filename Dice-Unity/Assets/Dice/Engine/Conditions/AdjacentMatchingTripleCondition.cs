namespace Dice.Engine
{
    public sealed class AdjacentMatchingTripleCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            for (int i = 0; i < DiceValues.Count - 2; ++i)
                if (dice[i] == dice[i + 1] && dice[i] == dice[i + 2]) return true;
            return false;
        }
    }
}
