namespace Dice.Engine
{
    public sealed class ExtremeEqualsHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.Minimum == context.HandsRemaining || context.CurrentHand.Maximum == context.HandsRemaining;
        }
    }
}
