namespace Dice.Engine
{
    public sealed class SecondFourthProductAtLeastSumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentHand.Faces;
            return context.CurrentHand.Counts(ModifierConditionHelpers.SecondAndFourthDice) && dice[1] * dice[3] >= context.CurrentHand.Sum;
        }
    }
}
