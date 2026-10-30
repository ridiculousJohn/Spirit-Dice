namespace Dice.Engine
{
    public sealed class EveryDieRerolledCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.EverRerolledMask == DiceValues.AllMask;
        }
    }
}
