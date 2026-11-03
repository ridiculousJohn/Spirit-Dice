namespace Dice.Engine
{
    public sealed class MinimumEqualsRerollsSpentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.Minimum == context.RerollsSpentOnHand;
        }
    }
}
