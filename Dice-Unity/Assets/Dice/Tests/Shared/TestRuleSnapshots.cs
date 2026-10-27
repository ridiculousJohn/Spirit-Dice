using System.Linq;
using Dice.Engine;

namespace Dice.Tests
{
    /** Reads a rule's own snapshot out of an encounter snapshot. */
    public static class TestRuleSnapshots
    {
        /** The encounter's single binding rule. */
        public static DieBindingRuleSnapshot Binding(this EncounterSnapshot snapshot)
            => snapshot.EncounterRules.OfType<DieBindingRuleSnapshot>().Single();

        /** The encounter's single interference rule. */
        public static DieInterferenceRuleSnapshot Interference(this EncounterSnapshot snapshot)
            => snapshot.EncounterRules.OfType<DieInterferenceRuleSnapshot>().Single();
    }
}
