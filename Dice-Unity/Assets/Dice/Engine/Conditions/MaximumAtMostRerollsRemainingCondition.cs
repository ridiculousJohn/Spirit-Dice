namespace Dice.Engine
{
    public sealed class MaximumAtMostRerollsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.Maximum <= context.RerollsRemaining;
        }
    }
}
