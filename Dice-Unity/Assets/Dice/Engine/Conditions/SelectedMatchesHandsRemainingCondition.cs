namespace Dice.Engine
{
    public sealed class SelectedMatchesHandsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.CountOf(context.HandsRemaining, context.SelectedMask) > 0;
        }
    }
}
