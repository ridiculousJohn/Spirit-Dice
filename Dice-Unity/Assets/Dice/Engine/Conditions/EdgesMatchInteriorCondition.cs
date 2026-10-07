namespace Dice.Engine
{
    public sealed class EdgesMatchInteriorCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return dice[0] == dice[4] && (dice[0] == dice[1] || dice[0] == dice[2] || dice[0] == dice[3]);
        }
    }
}
