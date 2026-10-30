namespace Dice.Engine
{
    public sealed class NeverCondition : IModifierCondition
    {
        public bool CanEverActivate => false;

        public bool Evaluate(in ModifierContext context)
        {
            return false;
        }
    }
}
