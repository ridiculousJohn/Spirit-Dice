namespace Dice.Engine
{
    public sealed class AlwaysCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            return true;
        }
    }
}
