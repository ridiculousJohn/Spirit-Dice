namespace Dice.Engine
{
    public sealed class MiddleHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.HandsRemaining == (context.StartingHands + 1L) / 2;
        }
    }
}
