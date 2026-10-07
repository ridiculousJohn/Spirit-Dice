namespace Dice.Engine
{
    public sealed class EdgePairSumsEqualCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return dice[0] + dice[1] == dice[3] + dice[4];
        }
    }
}
