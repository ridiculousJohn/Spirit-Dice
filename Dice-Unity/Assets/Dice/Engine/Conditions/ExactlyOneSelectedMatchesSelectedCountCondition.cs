namespace Dice.Engine
{
    public sealed class ExactlyOneSelectedMatchesSelectedCountCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.CountOf(DiceValues.CountBays(context.SelectedMask), context.SelectedMask) == 1;
        }
    }
}
