namespace Dice.Engine
{
    public sealed class HandsRemainingEvenCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return (context.HandsRemaining & 1) == 0;
        }
    }
}
