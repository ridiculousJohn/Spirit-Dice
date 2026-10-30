namespace Dice.Engine
{
    public sealed class NoDiceRerolledCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.RerollsSpentOnHand == 0;
        }
    }
}
