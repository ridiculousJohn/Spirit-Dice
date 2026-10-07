namespace Dice.Engine
{
    public sealed class InitialPairCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return HandEvaluator.Evaluate(context.InitialDice) == HandCategory.Pair;
        }
    }
}
