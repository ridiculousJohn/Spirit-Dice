namespace Dice.Engine
{
    public sealed class PreviousStageExhaustedHandsCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.HandsPlayedInStage == 0 && context.PreviousStageHandsRemaining == 0;
        }
    }
}
