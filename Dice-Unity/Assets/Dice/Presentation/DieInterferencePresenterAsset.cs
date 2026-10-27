using System;
using System.Collections;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The die interference rule's presenter: the host reacts and a sound plays when a die is disturbed or the
     * disturbance is repelled; a disturbed die visibly turns over or rerolls, the room's ambient activity jolts and the
     * table holds a moment. A disturbance that follows Play Hand waits a short beat first, so it reads as interrupting the
     * committed hand.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Die Interference")]
    public sealed class DieInterferencePresenterAsset : RulePresenterAsset<DieInterferenceRuleDefinition>
    {
        [Tooltip("Sounds when a die is disturbed, such as a giggle; chosen randomly without immediate repeats.")]
        [SerializeField] private AudioClip[] disturbanceClips = Array.Empty<AudioClip>();
        [Tooltip("Sounds when protection repels a disturbance, such as a grumble; chosen randomly without immediate repeats.")]
        [SerializeField] private AudioClip[] blockedClips = Array.Empty<AudioClip>();
        [Tooltip("Seconds the table holds after a disturbed die lands or a disturbance is repelled.")]
        [SerializeField, Min(0)] private float hold = .3f;
        [Tooltip("Seconds a committed Play Hand pauses before a disturbance that follows it.")]
        [SerializeField, Min(0)] private float playHandBeat = .25f;

        public AudioClip[] DisturbanceClips => disturbanceClips;
        public AudioClip[] BlockedClips => blockedClips;
        public float Hold => hold;
        public float PlayHandBeat => playHandBeat;

        protected override RulePresenter Create(DieInterferenceRuleDefinition rule, RulePresenterContext context)
        {
            if (!SceneLightingAsset.Finite(hold) || hold < 0f || !SceneLightingAsset.Finite(playHandBeat) || playHandBeat < 0f)
                throw new InvalidOperationException("The interference presenter " + name + " requires nonnegative timings.");
            return new DieInterferencePresenter(this, rule.Id, context);
        }
    }

    /** Presents its own interference rule's disturbances and repelled attempts; every other event is left to others. */
    public sealed class DieInterferencePresenter : RulePresenter
    {
        private readonly DieInterferencePresenterAsset settings;
        private readonly string ruleId;
        private readonly RulePresenterContext context;
        // Sound choices are cosmetic and never touch the engine's random streams.
        private readonly System.Random cosmeticRandom = new System.Random();
        private AudioClip lastDisturbanceClip;
        private AudioClip lastBlockedClip;

        internal DieInterferencePresenter(DieInterferencePresenterAsset settings, string ruleId, RulePresenterContext context)
        {
            this.settings = settings;
            this.ruleId = ruleId;
            this.context = context;
        }

        public override float LeadIn(RuleEvent ruleEvent)
        {
            switch (ruleEvent)
            {
                case DieDisturbedEvent disturbed when disturbed.RuleId == ruleId:
                    return BeatAfter(disturbed.Trigger);
                case DisturbanceBlockedEvent blocked when blocked.RuleId == ruleId:
                    return BeatAfter(blocked.Trigger);
                default:
                    return 0f;
            }
        }

        public override IEnumerator Present(RuleEvent ruleEvent)
        {
            switch (ruleEvent)
            {
                case DieDisturbedEvent disturbed when disturbed.RuleId == ruleId:
                    return Disturb(disturbed);
                case DisturbanceBlockedEvent blocked when blocked.RuleId == ruleId:
                    return Repelled();
                default:
                    return null;
            }
        }

        private float BeatAfter(InterferenceTrigger trigger) => trigger == InterferenceTrigger.PlayHand ? settings.PlayHandBeat : 0f;

        /** The die visibly moves even when its face returns to the one it showed. */
        private IEnumerator Disturb(DieDisturbedEvent disturbed)
        {
            context.ReactHost();
            lastDisturbanceClip = SpiritEncounterAudio.ChooseClip(settings.DisturbanceClips, lastDisturbanceClip, null, cosmeticRandom);
            context.PlayCue(lastDisturbanceClip);
            context.JoltAmbient();
            // The rule's own closed set of effects decides how the die moves.
            switch (disturbed.Kind)
            {
                case DieInterferenceKind.Flip:
                    yield return context.FlipDie(disturbed.Bay, disturbed.After);
                    break;
                case DieInterferenceKind.ForcedReroll:
                    yield return context.RerollDie(disturbed.Bay, disturbed.After);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(disturbed), disturbed.Kind, "Unknown interference effect.");
            }
            if (settings.Hold > 0f) yield return new WaitForSecondsRealtime(settings.Hold);
        }

        private IEnumerator Repelled()
        {
            context.ReactHost();
            lastBlockedClip = SpiritEncounterAudio.ChooseClip(settings.BlockedClips, lastBlockedClip, null, cosmeticRandom);
            context.PlayCue(lastBlockedClip);
            if (settings.Hold > 0f) yield return new WaitForSecondsRealtime(settings.Hold);
        }
    }
}
