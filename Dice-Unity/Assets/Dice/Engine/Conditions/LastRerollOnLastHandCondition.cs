namespace Dice.Engine
{
    public sealed class LastRerollOnLastHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.HandsRemaining == 1 && context.RerollsRemaining == 0;
        }
    }
}
