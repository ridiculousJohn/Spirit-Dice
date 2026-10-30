namespace Dice.Engine
{
    public sealed class EdgeSumGreaterThanInteriorCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return (dice[0] + dice[4]) > dice[1] + dice[2] + dice[3];
        }
    }
}
