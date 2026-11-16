namespace Dice.Engine
{
    /** What the carried-dice conditions share: the carried dice and the dice matching one of them. */
    internal static class CarriedDice
    {
        /** The dice every carry-over rule has carried into the hand and that keep their carried status. */
        internal static int Mask(in ModifierContext context)
        {
            int mask = 0;
            foreach (var rule in context.EncounterRules)
                if (rule is DieCarryOverRuleSnapshot carry) mask |= carry.CarriedMask;
            return mask;
        }

        /** The counted dice other than the counted die in the bay that show its face. */
        internal static int OtherMatches(HandDice hand, int bay) => hand.CountOf(hand.Faces[bay]) - 1;
    }
}
