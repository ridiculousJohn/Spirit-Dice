namespace Dice.Engine
{
    public sealed class UniqueCountLessThanRerollsSpentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.DistinctFaceCount < context.RerollsSpentOnHand;
        }
    }
}
