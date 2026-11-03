namespace Dice.Engine
{
    public sealed class MiddleEqualsOddCountCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.Counts(ModifierConditionHelpers.MiddleDie) && context.CurrentHand.Faces[2] == ModifierConditionHelpers.OddCount(context);
        }
    }
}
