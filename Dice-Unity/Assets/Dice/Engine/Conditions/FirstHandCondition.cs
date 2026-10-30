namespace Dice.Engine
{
    public sealed class FirstHandCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.HandsPlayedInStage == 0;
        }
    }
}
