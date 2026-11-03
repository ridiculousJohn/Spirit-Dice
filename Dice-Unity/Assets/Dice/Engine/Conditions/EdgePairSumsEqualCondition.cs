namespace Dice.Engine
{
    public sealed class EdgePairSumsEqualCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(DiceValues.AllMask & ~ModifierConditionHelpers.MiddleDie)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            return dice[0] + dice[1] == dice[3] + dice[4];
        }
    }
}
