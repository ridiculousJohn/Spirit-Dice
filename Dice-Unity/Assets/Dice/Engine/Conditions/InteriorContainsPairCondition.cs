namespace Dice.Engine
{
    public sealed class InteriorContainsPairCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(ModifierConditionHelpers.InteriorDice)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            return dice[1] == dice[2] || dice[1] == dice[3] || dice[2] == dice[3];
        }
    }
}
