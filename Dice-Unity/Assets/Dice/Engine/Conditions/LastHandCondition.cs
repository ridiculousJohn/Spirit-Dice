namespace Dice.Engine
{
    public sealed class LastHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.HandsRemaining == 1;
        }
    }
}
