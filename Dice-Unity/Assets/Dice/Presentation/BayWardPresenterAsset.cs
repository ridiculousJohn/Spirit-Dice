using System;
using System.Collections;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** The bay ward rule's presenter: the prop of the ward's table piece flares when the ward repels a disturbance. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Bay Ward")]
    public sealed class BayWardPresenterAsset : RulePresenterAsset<BayWardRuleDefinition>
    {
        /** Fails when the table shows no prop for the ward's piece, which the ward would have nothing to flare on. */
        protected override RulePresenter Create(BayWardRuleDefinition rule, RulePresenterContext context)
        {
            if (!context.ShowsPiece(rule.PieceId, rule.InteractionId))
                throw new InvalidOperationException("The ward presenter " + name + " presents rule " + rule.Id +
                    ", but the table shows no prop for its piece " + rule.PieceId + " (" + rule.InteractionId + ").");
            return new BayWardPresenter(rule, context);
        }
    }

    /** Flares the ward's own piece for each disturbance the ward repels and waits for the flare. */
    public sealed class BayWardPresenter : RulePresenter
    {
        private readonly BayWardRuleDefinition ward;
        private readonly RulePresenterContext context;

        internal BayWardPresenter(BayWardRuleDefinition ward, RulePresenterContext context)
        {
            this.ward = ward;
            this.context = context;
        }

        public override IEnumerator Present(RuleEvent ruleEvent)
            => ruleEvent is DisturbanceBlockedEvent blocked && blocked.ProtectingRuleId == ward.Id ? Flare() : null;

        private IEnumerator Flare()
        {
            float duration = context.FlarePiece(ward.PieceId, ward.InteractionId);
            if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
        }
    }
}
