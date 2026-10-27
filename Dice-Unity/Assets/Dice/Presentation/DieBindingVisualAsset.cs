using System;
using System.Collections;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The binding rule's presenter: a faint mark on the die the spirit has marked, and roots that grow around a die when
     * it binds and withdraw when the hand's bindings release. The per-die prefab renders the two levels; this asset times
     * them and the spirit's reaction and flare.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Die Binding")]
    public sealed class DieBindingVisualAsset : RulePresenterAsset<DieBindingRuleDefinition>
    {
        [Tooltip("Per-die visual created once under each of the tray's five dice.")]
        [SerializeField] private DieBindingVisual diePrefab;
        [Tooltip("Scene light raised for the spirit's reactions, or -1 for none.")]
        [SerializeField, Min(-1)] private int flareLight = -1;
        [Header("Mark")]
        [Tooltip("Seconds the mark takes to fade in; playback waits this long after a die is marked.")]
        [SerializeField, Min(0)] private float markFadeDuration = .45f;
        [Tooltip("Small pulse of the flare light when a die is marked, as a multiplier of its authored intensity.")]
        [SerializeField, Min(0)] private float markFlarePeak = .35f;
        [SerializeField, Min(0)] private float markFlareRise = .15f;
        [SerializeField, Min(0)] private float markFlareHold;
        [SerializeField, Min(0)] private float markFlareFall = .7f;
        [Header("Binding")]
        [Tooltip("Seconds the roots take to grow closed around a binding die; playback waits this long.")]
        [SerializeField, Min(0)] private float growDuration = .5f;
        [Tooltip("Seconds the mark takes to give way to the growing roots.")]
        [SerializeField, Min(0)] private float markYieldDuration = .3f;
        [Tooltip("Full flare of the flare light when a die binds, as a multiplier of its authored intensity.")]
        [SerializeField, Min(0)] private float bindFlarePeak = 1f;
        [SerializeField, Min(0)] private float bindFlareRise = .12f;
        [SerializeField, Min(0)] private float bindFlareHold = .3f;
        [SerializeField, Min(0)] private float bindFlareFall = 1.2f;
        [Header("Release")]
        [Tooltip("Seconds the roots and any mark take to withdraw once the hand's scoring finishes; playback waits this long.")]
        [SerializeField, Min(0)] private float releaseDuration = .5f;

        public DieBindingVisual DiePrefab => diePrefab;
        public int FlareLight => flareLight;
        public float MarkFadeDuration => markFadeDuration;
        public float GrowDuration => growDuration;
        public float MarkYieldDuration => markYieldDuration;
        public float ReleaseDuration => releaseDuration;

        protected override RulePresenter Create(DieBindingRuleDefinition rule, RulePresenterContext context)
        {
            if (diePrefab == null) throw new InvalidOperationException("Binding visuals require a per-die prefab.");
            if (flareLight >= 0 && context.Lights != null && flareLight >= context.Lights.LightCount)
                throw new InvalidOperationException("The binding flare requires a scene light.");
            return new DieBindingVisualPresenter(this, rule.Id, context);
        }

        internal void FlareMark(IRuleSceneLights lights) => Flare(lights, markFlarePeak, markFlareRise, markFlareHold, markFlareFall);

        internal void FlareBind(IRuleSceneLights lights) => Flare(lights, bindFlarePeak, bindFlareRise, bindFlareHold, bindFlareFall);

        /** Raises the flare light; nothing when the asset names no light or the encounter has no scene lighting. */
        private void Flare(IRuleSceneLights lights, float peak, float rise, float hold, float fall)
        {
            if (flareLight >= 0 && lights != null) lights.Flare(flareLight, peak, rise, hold, fall);
        }
    }

    /**
     * Shows a binding rule's marked and bound dice on per-die visuals created under the tray's dice. A mark fades in with
     * the spirit's reaction and a small flare; a binding grows the roots while the mark gives way, with the reaction and a
     * full flare; once a played hand's scoring finishes every root and mark withdraws, before the next opening roll.
     */
    public sealed class DieBindingVisualPresenter : RulePresenter
    {
        private readonly DieBindingVisualAsset settings;
        private readonly string ruleId;
        private readonly RulePresenterContext context;
        private readonly DieBindingVisual[] dice = new DieBindingVisual[DiceValues.Count];

        internal DieBindingVisualPresenter(DieBindingVisualAsset settings, string ruleId, RulePresenterContext context)
        {
            this.settings = settings;
            this.ruleId = ruleId;
            this.context = context;
            for (int i = 0; i < dice.Length; ++i)
                dice[i] = UnityEngine.Object.Instantiate(settings.DiePrefab, context.Dice[i], false);
        }

        /** The per-die visual on the die in the given bay. */
        public DieBindingVisual this[int bay] => dice[bay];

        public override void Sync(EncounterSnapshot snapshot, bool animate)
        {
            DieBindingRuleSnapshot binding = snapshot != null && snapshot.Phase == EncounterPhase.Playing ? Binding(snapshot) : null;
            for (int bay = 0; bay < dice.Length; ++bay)
            {
                if (dice[bay] == null) continue;
                float mark = binding != null && (binding.MarkedMask & (1 << bay)) != 0 ? 1f : 0f;
                float growth = binding != null && (binding.BoundMask & (1 << bay)) != 0 ? 1f : 0f;
                if (!animate) dice[bay].Show(mark, growth);
                else
                {
                    dice[bay].AnimateMark(mark, mark > dice[bay].Mark ? settings.MarkFadeDuration : settings.ReleaseDuration);
                    dice[bay].AnimateGrowth(growth, growth > dice[bay].Growth ? settings.GrowDuration : settings.ReleaseDuration);
                }
            }
        }

        public override IEnumerator Present(RuleEvent ruleEvent)
        {
            switch (ruleEvent)
            {
                case DieMarkedEvent marked when marked.RuleId == ruleId && Valid(marked.Bay):
                    return Mark(marked.Bay);
                case DieBoundEvent bound when bound.RuleId == ruleId && Valid(bound.Bay):
                    return Bind(bound.Bay);
                case HandScoredEvent _:
                    return Release();
                default:
                    return null;
            }
        }

        public override void Dispose()
        {
            for (int i = 0; i < dice.Length; ++i)
            {
                if (dice[i] != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(dice[i].gameObject);
                    else UnityEngine.Object.DestroyImmediate(dice[i].gameObject);
                }
                dice[i] = null;
            }
        }

        private bool Valid(int bay) => bay >= 0 && bay < dice.Length && dice[bay] != null;

        /** This presenter's rule in the snapshot, or null when the encounter has no such binding rule. */
        private DieBindingRuleSnapshot Binding(EncounterSnapshot snapshot)
        {
            foreach (var rule in snapshot.EncounterRules)
                if (rule.Id == ruleId) return rule as DieBindingRuleSnapshot;
            return null;
        }

        private IEnumerator Mark(int bay)
        {
            dice[bay].AnimateMark(1f, settings.MarkFadeDuration);
            context.ReactHost();
            settings.FlareMark(context.Lights);
            return Wait(settings.MarkFadeDuration);
        }

        private IEnumerator Bind(int bay)
        {
            dice[bay].AnimateMark(0f, settings.MarkYieldDuration);
            dice[bay].AnimateGrowth(1f, settings.GrowDuration);
            context.ReactHost();
            settings.FlareBind(context.Lights);
            return Wait(settings.GrowDuration);
        }

        /** Withdraws every root and mark after a played hand's scoring; nothing to wait for when none is shown. */
        private IEnumerator Release()
        {
            bool shown = false;
            foreach (var die in dice)
            {
                if (die == null || die.MarkTarget == 0f && die.GrowthTarget == 0f && !die.IsAnimating) continue;
                die.AnimateMark(0f, settings.ReleaseDuration);
                die.AnimateGrowth(0f, settings.ReleaseDuration);
                shown = true;
            }
            return shown ? Wait(settings.ReleaseDuration) : null;
        }

        private static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        }
    }
}
