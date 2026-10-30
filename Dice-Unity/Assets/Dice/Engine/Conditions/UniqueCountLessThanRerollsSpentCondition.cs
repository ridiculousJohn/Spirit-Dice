namespace Dice.Engine
{
    public sealed class UniqueCountLessThanRerollsSpentCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return dice.DistinctFaceCount < context.RerollsSpentOnHand;
        }
    }
}
