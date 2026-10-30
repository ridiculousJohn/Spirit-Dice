namespace Dice.Engine
{
    public sealed class RerollsExceedHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.RerollsRemaining > context.HandsRemaining;
        }
    }
}
