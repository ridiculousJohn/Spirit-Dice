namespace Dice.Engine
{
    public sealed class PreviousStageExhaustedHandsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.HandsPlayedInStage == 0 && context.PreviousStageHandsRemaining == 0;
        }
    }
}
