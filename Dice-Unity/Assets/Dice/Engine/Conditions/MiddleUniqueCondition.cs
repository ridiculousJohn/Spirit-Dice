namespace Dice.Engine
{
    public sealed class MiddleUniqueCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.Counts(ModifierConditionHelpers.MiddleDie) && context.CurrentHand.CountOf(context.CurrentHand.Faces[2]) == 1;
        }
    }
}
