namespace Dice.Engine
{
    public sealed class ContainsRerollsRemainingCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return DiceValues.IsFace(context.RerollsRemaining) && context.CurrentHand.CountOf(context.RerollsRemaining) > 0;
        }
    }
}
