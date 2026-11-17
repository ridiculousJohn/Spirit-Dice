using System.Collections;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The bay ward rule's presenter: when the ward repels a disturbance, whatever shows the ward's table piece reacts in its
     * own way, such as a scene prop's flare; a piece shown without a reaction, or not shown, has none.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Bay Ward")]
    public sealed class BayWardPresenterAsset : RulePresenterAsset<BayWardRuleDefinition>
    {
        protected override RulePresenter Create(BayWardRuleDefinition rule, RulePresenterContext context)
            => new BayWardPresenter(rule, context);
    }

    /** Has the ward's own piece react for each disturbance the ward repels and waits for the reaction. */
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
            => ruleEvent is DisturbanceBlockedEvent blocked && blocked.ProtectingRuleId == ward.Identity ? React() : null;

        private IEnumerator React()
        {
            float duration = context.ReactPiece(ward.PieceId);
            if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
        }
    }
}
