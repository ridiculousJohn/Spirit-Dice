using System;
using System.Collections;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The presentation of a rule's activation on an action, such as a waived reroll cost: the HUD's rule badge shows the
     * rule's new state, the host's cue marks the action of the card's activation phase, the selection line names the
     * rule, the host reacts and the rule's spirit sweeps across the tray. A scored hand's rule bonus is presented with
     * the hand's scoring instead. The card supplies the rule's name, icon and action; this asset times the sweep.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Rule Activation")]
    public sealed class RuleActivationPresenterAsset : RulePresenterAsset
    {
        [Tooltip("Seconds the rule's spirit takes to sweep across the tray; playback waits this long.")]
        [SerializeField, Min(0)] private float sweepDuration = .65f;

        public float SweepDuration => sweepDuration;

        public override RulePresenter Create(EncounterRulePresentationAsset card, EncounterRuleDefinition rule,
            RulePresenterContext context)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!FiniteValue.Is(sweepDuration) || sweepDuration < 0f)
                throw new InvalidOperationException("The rule activation presenter " + name + " requires a nonnegative sweep duration.");
            return new RuleActivationPresenter(this, card, rule.Id, context);
        }
    }

    /** Presents its own rule's activations that carry no scored-hand bonus; every other event is left to others. */
    public sealed class RuleActivationPresenter : RulePresenter
    {
        private readonly RuleActivationPresenterAsset settings;
        private readonly EncounterRulePresentationAsset card;
        private readonly string ruleId;
        private readonly RulePresenterContext context;

        internal RuleActivationPresenter(RuleActivationPresenterAsset settings, EncounterRulePresentationAsset card,
            string ruleId, RulePresenterContext context)
        {
            this.settings = settings;
            this.card = card;
            this.ruleId = ruleId;
            this.context = context;
        }

        public override IEnumerator Present(RuleEvent ruleEvent)
            => ruleEvent is EncounterRuleActivatedEvent activated && activated.RuleId == ruleId && activated.MultiplierBonus == 0
                ? Activate() : null;

        private IEnumerator Activate()
        {
            context.ShowRuleBadge();
            context.CueAction(card.ActivationPhase, card.Icon);
            context.ShowSelection(card.DisplayName);
            context.ReactHost();
            yield return context.SweepSpirit(card.Icon, settings.SweepDuration);
        }
    }
}
