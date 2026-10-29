using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * The presenters of an encounter table's rules: one per rule card that references a presenter, created for the
     * encounter the table presents and disposed with the encounter screen's presentation of it. Score playback asks it for
     * each event's lead-in and presentation; it knows no rule or presenter kind.
     */
    public sealed class EncounterRulePresenters : IDisposable
    {
        private readonly List<RulePresenter> presenters;

        private EncounterRulePresenters(List<RulePresenter> presenters) => this.presenters = presenters;

        public IReadOnlyList<RulePresenter> Presenters => presenters;

        /**
         * Creates the presenters of the rule cards that reference one, each for its rule's definition in the encounter, or
         * returns null when no card does; the context is only built when a card needs it. Fails when a card's rule is not
         * one of the encounter's rules.
         */
        public static EncounterRulePresenters Create(IReadOnlyList<EncounterRulePresentationAsset> rules,
            EncounterDefinition encounter, Func<RulePresenterContext> context)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (context == null) throw new ArgumentNullException(nameof(context));
            List<RulePresenter> presenters = null;
            RulePresenterContext built = null;
            try
            {
                foreach (var card in rules)
                {
                    if (card == null || card.Presenter == null) continue;
                    var rule = Definition(encounter, card.RuleId) ?? throw new InvalidOperationException("The rule card " +
                        card.name + " presents rule " + card.Rule.name + ", which encounter " + encounter.Identity + " does not have.");
                    built ??= context() ?? throw new InvalidOperationException("Rule presenters require a context.");
                    presenters ??= new List<RulePresenter>();
                    presenters.Add(card.Presenter.Create(card, rule, built));
                }
            }
            catch
            {
                if (presenters != null)
                    foreach (var created in presenters) created.Dispose();
                throw;
            }
            return presenters != null ? new EncounterRulePresenters(presenters) : null;
        }

        public void Sync(EncounterSnapshot snapshot, bool animate)
        {
            foreach (var presenter in presenters) presenter.Sync(snapshot, animate);
        }

        /** The longest pause any presenter asks for before the event is presented. */
        public float LeadIn(RuleEvent ruleEvent)
        {
            float leadIn = 0f;
            foreach (var presenter in presenters)
            {
                float requested = presenter.LeadIn(ruleEvent);
                if (float.IsNaN(requested) || requested < 0f)
                    throw new InvalidOperationException("A rule presenter's lead-in must be a nonnegative number of seconds.");
                leadIn = Math.Max(leadIn, requested);
            }
            return leadIn;
        }

        /**
         * Every rule's presentation of the event, started in card order and run side by side; the routine ends when all
         * have ended. Null when no presenter presents the event.
         */
        public ConcurrentRoutines Present(RuleEvent ruleEvent)
        {
            List<IEnumerator> routines = null;
            foreach (var presenter in presenters)
            {
                var routine = presenter.Present(ruleEvent);
                if (routine == null) continue;
                routines ??= new List<IEnumerator>();
                routines.Add(routine);
            }
            return routines != null ? new ConcurrentRoutines(routines) : null;
        }

        public void Dispose()
        {
            foreach (var presenter in presenters) presenter.Dispose();
            presenters.Clear();
        }

        private static EncounterRuleDefinition Definition(EncounterDefinition encounter, EncounterRuleDefinition.Id ruleId)
        {
            foreach (var rule in encounter.Rules)
                if (rule.Identity == ruleId) return rule;
            return null;
        }
    }
}
