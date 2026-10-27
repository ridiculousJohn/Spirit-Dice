using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * The dice visuals of an encounter table's rules: one presenter per rule card that references a dice visual, created
     * when the table is configured and disposed with the encounter screen's presentation of that table. Score playback hands
     * it every event; it knows no rule or visual kind.
     */
    public sealed class EncounterRuleDiceVisuals : IDisposable
    {
        private readonly List<RuleDiceVisualPresenter> presenters;

        private EncounterRuleDiceVisuals(List<RuleDiceVisualPresenter> presenters) => this.presenters = presenters;

        public IReadOnlyList<RuleDiceVisualPresenter> Presenters => presenters;

        /**
         * Creates the presenters of the rule cards that reference a dice visual, or returns null when none does; the context
         * is only built when a card needs it.
         */
        public static EncounterRuleDiceVisuals Create(IReadOnlyList<EncounterRulePresentationAsset> rules,
            Func<RuleDiceVisualContext> context)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (context == null) throw new ArgumentNullException(nameof(context));
            List<RuleDiceVisualPresenter> presenters = null;
            RuleDiceVisualContext built = null;
            foreach (var rule in rules)
            {
                if (rule == null || rule.DiceVisual == null) continue;
                built ??= context() ?? throw new InvalidOperationException("Rule dice visuals require a context.");
                presenters ??= new List<RuleDiceVisualPresenter>();
                try { presenters.Add(rule.DiceVisual.Create(rule.RuleId, built)); }
                catch
                {
                    foreach (var created in presenters) created.Dispose();
                    throw;
                }
            }
            return presenters != null ? new EncounterRuleDiceVisuals(presenters) : null;
        }

        public void Sync(EncounterSnapshot snapshot, bool animate)
        {
            foreach (var presenter in presenters) presenter.Sync(snapshot, animate);
        }

        /** Each rule's presentation of the event in rule order, or null when no rule presents it. */
        public IEnumerator Present(RuleEvent ruleEvent)
        {
            List<IEnumerator> routines = null;
            foreach (var presenter in presenters)
            {
                var routine = presenter.Present(ruleEvent);
                if (routine == null) continue;
                routines ??= new List<IEnumerator>();
                routines.Add(routine);
            }
            return routines == null ? null : routines.Count == 1 ? routines[0] : Sequence(routines);
        }

        public void Dispose()
        {
            foreach (var presenter in presenters) presenter.Dispose();
            presenters.Clear();
        }

        private static IEnumerator Sequence(List<IEnumerator> routines)
        {
            foreach (var routine in routines) yield return routine;
        }
    }
}
