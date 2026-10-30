namespace Dice.Engine
{
    public sealed class InteriorContainsPairCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return dice[1] == dice[2] || dice[1] == dice[3] || dice[2] == dice[3];
        }
    }
}
