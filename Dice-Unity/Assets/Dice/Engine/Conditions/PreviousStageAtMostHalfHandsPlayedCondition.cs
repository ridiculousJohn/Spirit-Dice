namespace Dice.Engine
{
    public sealed class PreviousStageAtMostHalfHandsPlayedCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.HandsPlayedInStage == 0 && context.PreviousStageStartingHands.HasValue &&
                context.PreviousStageHandsRemaining.HasValue &&
                (long)context.PreviousStageHandsRemaining.Value * 2 >= context.PreviousStageStartingHands.Value;
        }
    }
}
