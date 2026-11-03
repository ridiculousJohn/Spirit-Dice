namespace Dice.Engine
{
    public sealed class SumLessThanInitialCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.Sum < context.InitialHand.Sum;
        }
    }
}
