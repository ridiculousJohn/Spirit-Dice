namespace Dice.Engine
{
    public sealed class SumLessThanInitialCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return dice.Sum < context.InitialDice.Sum;
        }
    }
}
