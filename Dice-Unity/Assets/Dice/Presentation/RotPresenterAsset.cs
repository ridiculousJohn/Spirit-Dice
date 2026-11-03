using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The rot rule's presenter: while the player chooses dice, every kept die shows a faint mark and, under the Wither
     * endpoint, a kept 1 shows mould partway up its sides instead; a rot step turns the kept dice down while the rerolled
     * dice are in the air; mould closes over a withering die with the host's reaction; a restored die's mould crumbles away
     * as it is thrown. The per-die visual is the binding's, a stand-in until rot's own effect is made: its mark level shows
     * the faint mark and its growth level the mould.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Rot")]
    public sealed class RotPresenterAsset : RulePresenterAsset<RotRuleDefinition>
    {
        [Tooltip("Per-die visual created once under each of the tray's five dice; its mark level shows the kept-die mark and its growth level the mould.")]
        [SerializeField] private DieBindingVisual diePrefab;
        [Header("Choosing")]
        [Tooltip("Mark level of a kept die while the player chooses dice.")]
        [SerializeField, Range(0, 1)] private float keptMarkLevel = .35f;
        [Tooltip("Mould level of a kept 1 that will wither, while the player chooses dice.")]
        [SerializeField, Range(0, 1)] private float warningLevel = .35f;
        [Tooltip("Seconds the kept-die marks and warnings take to show or clear.")]
        [SerializeField, Min(0)] private float markFadeDuration = .3f;
        [Header("Rot step")]
        [Tooltip("Played once per rot step; empty plays nothing.")]
        [SerializeField] private AudioClip rotClip;
        [Header("Wither")]
        [Tooltip("Seconds the mould takes to close over a withering die; playback waits this long.")]
        [SerializeField, Min(0)] private float witherDuration = .6f;
        [Tooltip("Played when a rot step withers a die; empty plays nothing.")]
        [SerializeField] private AudioClip witherClip;
        [Header("Restore")]
        [Tooltip("Seconds a restored die's mould takes to crumble away; playback waits this long.")]
        [SerializeField, Min(0)] private float restoreDuration = .4f;
        [Tooltip("Played when withered dice are restored; empty plays nothing.")]
        [SerializeField] private AudioClip restoreClip;

        public DieBindingVisual DiePrefab => diePrefab;
        public float KeptMarkLevel => keptMarkLevel;
        public float WarningLevel => warningLevel;
        public float MarkFadeDuration => markFadeDuration;
        public AudioClip RotClip => rotClip;
        public float WitherDuration => witherDuration;
        public AudioClip WitherClip => witherClip;
        public float RestoreDuration => restoreDuration;
        public AudioClip RestoreClip => restoreClip;

        protected override RulePresenter Create(RotRuleDefinition rule, RulePresenterContext context)
        {
            if (diePrefab == null) throw new InvalidOperationException("The rot presenter " + name + " requires a per-die prefab.");
            if (!Level(keptMarkLevel) || !Level(warningLevel))
                throw new InvalidOperationException("The rot presenter " + name + " requires mark and warning levels from 0 to 1.");
            if (!Duration(markFadeDuration) || !Duration(witherDuration) || !Duration(restoreDuration))
                throw new InvalidOperationException("The rot presenter " + name + " requires nonnegative durations.");
            return new RotPresenter(this, rule, context);
        }

        private static bool Level(float value) => FiniteValue.Is(value) && value >= 0f && value <= 1f;

        private static bool Duration(float value) => FiniteValue.Is(value) && value >= 0f;
    }

    /**
     * Shows a rot rule's kept-die marks, warnings and withered dice on per-die visuals created under the tray's dice. The
     * player is choosing from a Sync of a snapshot in play in which a player reroll is possible until playback presents its
     * next event; only then do kept dice show marks and warnings. The rule's rot steps and restorations play while the dice
     * of the roll before them are in the air.
     */
    public sealed class RotPresenter : RulePresenter
    {
        private readonly RotPresenterAsset settings;
        private readonly EncounterRuleDefinition.Id ruleId;
        private readonly RotEndpoint endpoint;
        private readonly RulePresenterContext context;
        private readonly DieBindingVisual[] dice = new DieBindingVisual[DiceValues.Count];
        private bool choosing;
        private int selectedMask;
        private int witheredMask;
        // The faces of the last synced snapshot, read only while the player is choosing.
        private DiceValues faces;

        internal RotPresenter(RotPresenterAsset settings, RotRuleDefinition rule, RulePresenterContext context)
        {
            this.settings = settings;
            ruleId = rule.Identity;
            endpoint = rule.Endpoint;
            this.context = context;
            for (int i = 0; i < dice.Length; ++i)
                dice[i] = UnityEngine.Object.Instantiate(settings.DiePrefab, context.Dice[i], false);
        }

        /** The per-die visual on the die in the given bay. */
        public DieBindingVisual this[int bay] => dice[bay];

        public override void Sync(EncounterSnapshot snapshot, bool animate)
        {
            RotRuleSnapshot rot = snapshot != null && snapshot.Phase == EncounterPhase.Playing ? Rot(snapshot) : null;
            witheredMask = rot?.WitheredMask ?? 0;
            // The same availability the HUD's reroll button reads.
            choosing = rot != null && snapshot.CanReroll;
            if (choosing) faces = snapshot.CurrentDice;
            Show(animate);
        }

        public override void SelectionChanged(int selectedMask)
        {
            this.selectedMask = selectedMask & DiceValues.AllMask;
            if (choosing) Show(true);
        }

        public override bool PresentsDuringRoll(RuleEvent ruleEvent)
            => ruleEvent is DiceRottedEvent rotted && rotted.RuleId == ruleId ||
                ruleEvent is DiceRestoredEvent restored && restored.RuleId == ruleId;

        public override IEnumerator Present(RuleEvent ruleEvent)
        {
            // Any event playback presents ends the player's choice.
            if (choosing)
            {
                choosing = false;
                Show(true);
            }
            switch (ruleEvent)
            {
                case DiceRottedEvent rotted when rotted.RuleId == ruleId:
                    return Rot(rotted);
                case DiceRestoredEvent restored when restored.RuleId == ruleId:
                    return Restore(restored.RestoredMask);
                default:
                    return null;
            }
        }

        public override void Dispose()
        {
            for (int i = 0; i < dice.Length; ++i)
            {
                OwnedObject.Release(dice[i].gameObject);
                dice[i] = null;
            }
        }

        /** This presenter's rule in the snapshot, or null when the encounter has no such rot rule. */
        private RotRuleSnapshot Rot(EncounterSnapshot snapshot)
        {
            foreach (var rule in snapshot.EncounterRules)
                if (rule.Id == ruleId) return rule as RotRuleSnapshot;
            return null;
        }

        /**
         * The kept dice that changed face turn to their new faces and those the rule rolled again reroll, together; mould
         * closes over the dice that withered, with the host's reaction, only when one did.
         */
        private IEnumerator Rot(DiceRottedEvent rotted)
        {
            int rerolled = rotted.RerolledMask & DiceValues.AllMask;
            int turned = rotted.RottedMask & ~rerolled & rotted.Before.DifferingMask(rotted.After);
            context.PlayCue(settings.RotClip);
            var motions = new List<IEnumerator>();
            if (turned != 0) motions.Add(context.FlipDice(turned, rotted.After));
            if (rerolled != 0) motions.Add(context.RerollDice(rerolled, rotted.After));
            float hold = 0f;
            if (rotted.WitheredMask != 0)
            {
                witheredMask |= rotted.WitheredMask;
                Show(true);
                context.PlayCue(settings.WitherClip);
                context.ReactHost();
                context.JoltAmbient();
                hold = settings.WitherDuration;
            }
            return motions.Count > 0 || hold > 0f ? Together(motions, hold) : null;
        }

        /** Runs the dice motions side by side and ends when they have landed and at least the hold has passed. */
        private static IEnumerator Together(List<IEnumerator> motions, float hold)
        {
            double start = Time.unscaledTimeAsDouble;
            if (motions.Count > 0) yield return new ConcurrentRoutines(motions);
            float remaining = hold - (float)(Time.unscaledTimeAsDouble - start);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        }

        /** The restored dice's mould crumbles away; nothing plays when none of them shows mould. */
        private IEnumerator Restore(int restoredMask)
        {
            bool shown = false;
            for (int bay = 0; bay < dice.Length; ++bay)
                if ((restoredMask & (1 << bay)) != 0 && (dice[bay].GrowthTarget > 0f || dice[bay].Growth > 0f)) shown = true;
            witheredMask &= ~restoredMask;
            Show(true);
            if (!shown) return null;
            context.PlayCue(settings.RestoreClip);
            return Wait(settings.RestoreDuration);
        }

        /**
         * Shows each die's levels: a withered die is closed in mould; while the player is choosing, a kept die shows the
         * faint mark, or under the Wither endpoint a kept 1 shows the warning instead.
         */
        private void Show(bool animate)
        {
            for (int bay = 0; bay < dice.Length; ++bay)
            {
                int bit = 1 << bay;
                bool withered = (witheredMask & bit) != 0;
                bool kept = choosing && (selectedMask & bit) == 0 && !withered;
                bool warned = kept && endpoint == RotEndpoint.Wither && faces[bay] == DiceValues.MinimumFace;
                float mark = kept && !warned ? settings.KeptMarkLevel : 0f;
                float mould = withered ? 1f : warned ? settings.WarningLevel : 0f;
                var die = dice[bay];
                if (!animate)
                {
                    die.Show(mark, mould);
                    continue;
                }
                if (die.MarkTarget != mark) die.AnimateMark(mark, settings.MarkFadeDuration);
                if (die.GrowthTarget != mould) die.AnimateGrowth(mould, MouldDuration(die.GrowthTarget, mould));
            }
        }

        /** Mould closes over the wither duration and crumbles from a withered die over the restore duration; a warning fades like a mark. */
        private float MouldDuration(float from, float to)
            => to >= 1f ? settings.WitherDuration : from >= 1f ? settings.RestoreDuration : settings.MarkFadeDuration;

        private static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        }
    }
}
