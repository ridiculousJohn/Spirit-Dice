using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The rot rule's presenter: each die's mould shows its rot count, growing as the die rots on each rot step while the
     * rerolled dice are in the air and crumbling away as the die is thrown; under the Wither endpoint a kept 1 shows the
     * wither warning while the player chooses; mould closes over a withering die with the host's reaction. The per-die
     * visual is the binding's, a stand-in until rot's own effect is made: its mark level shows the mould of the die's rot
     * count and its growth level the wither warning and the withered shell.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Rule Presenter/Rot")]
    public sealed class RotPresenterAsset : RulePresenterAsset<RotRuleDefinition>
    {
        [Tooltip("Per-die visual created once under each of the tray's five dice; its mark level shows the die's mould and its growth level the wither warning and the withered shell.")]
        [SerializeField] private DieBindingVisual diePrefab;
        [Header("Mould")]
        [Tooltip("Mark level of a die by its rot count: entry n for a die that has rotted in n rot steps since it was last rolled, the last entry for every higher count.")]
        [SerializeField, Range(0, 1)] private float[] mouldLevels = { 0f, .3f, .55f, .8f, 1f };
        [Tooltip("Seconds a die's mould takes to grow on a rot step.")]
        [SerializeField, Min(0)] private float mouldGrowDuration = .5f;
        [Header("Choosing")]
        [Tooltip("Mould level of a kept 1 that will wither, while the player chooses dice.")]
        [SerializeField, Range(0, 1)] private float warningLevel = .35f;
        [Tooltip("Seconds the wither warning takes to show or clear.")]
        [SerializeField, Min(0)] private float markFadeDuration = .3f;
        [Header("Rot step")]
        [Tooltip("Played once per rot step; empty plays nothing.")]
        [SerializeField] private AudioClip rotClip;
        [Header("Wither")]
        [Tooltip("Seconds the mould takes to close over a withering die; playback waits this long.")]
        [SerializeField, Min(0)] private float witherDuration = .6f;
        [Tooltip("Played when a rot step withers a die; empty plays nothing.")]
        [SerializeField] private AudioClip witherClip;
        [Tooltip("The host's voice, played with the host's reaction when a rot step withers a die; empty plays nothing.")]
        [SerializeField] private AudioClip witherVoiceClip;
        [Header("Restore")]
        [Tooltip("Seconds a thrown die's mould takes to crumble away; playback waits this long.")]
        [SerializeField, Min(0)] private float restoreDuration = .4f;
        [Tooltip("Played when the mould of thrown dice crumbles away; empty plays nothing.")]
        [SerializeField] private AudioClip restoreClip;

        public DieBindingVisual DiePrefab => diePrefab;
        public IReadOnlyList<float> MouldLevels => mouldLevels;
        public float MouldGrowDuration => mouldGrowDuration;
        public float WarningLevel => warningLevel;
        public float MarkFadeDuration => markFadeDuration;
        public AudioClip RotClip => rotClip;
        public float WitherDuration => witherDuration;
        public AudioClip WitherClip => witherClip;
        public AudioClip WitherVoiceClip => witherVoiceClip;
        public float RestoreDuration => restoreDuration;
        public AudioClip RestoreClip => restoreClip;

        /** The mould level of a die with the rot count: its entry, or the last entry for a count beyond the list. */
        public float MouldLevel(int count) => mouldLevels[Math.Min(count, mouldLevels.Length - 1)];

        protected override RulePresenter Create(RotRuleDefinition rule, RulePresenterContext context)
        {
            if (diePrefab == null) throw new InvalidOperationException("The rot presenter " + name + " requires a per-die prefab.");
            if (mouldLevels == null || mouldLevels.Length == 0 || !Array.TrueForAll(mouldLevels, Level) || !Level(warningLevel))
                throw new InvalidOperationException("The rot presenter " + name + " requires mould levels and a warning level from 0 to 1.");
            if (!Duration(mouldGrowDuration) || !Duration(markFadeDuration) || !Duration(witherDuration) || !Duration(restoreDuration))
                throw new InvalidOperationException("The rot presenter " + name + " requires nonnegative durations.");
            return new RotPresenter(this, rule, context);
        }

        private static bool Level(float value) => FiniteValue.Is(value) && value >= 0f && value <= 1f;

        private static bool Duration(float value) => FiniteValue.Is(value) && value >= 0f;
    }

    /**
     * Shows a rot rule's mould, wither warnings and withered dice on per-die visuals created under the tray's dice. Each
     * die's mould follows its rot count from the synced snapshot and the rule's events, throughout play. The player is
     * choosing from a Sync of a snapshot in play, with or without rerolls left, until playback presents its next event;
     * only then does a kept 1 show the wither warning. The rule's rot steps and restorations play while the dice of the
     * roll before them are in the air.
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
        // Each die's rot count, as the last synced snapshot or the rule's latest presented event reported it.
        private RotCounts counts;
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
            counts = rot?.Counts ?? default;
            // The warning shows the hut's rot, so it stays while no reroll is left.
            choosing = rot != null;
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
                    return Restore(restored);
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
         * The kept dice that changed face turn to their new faces and those the rule rolled again reroll, together, while
         * the mould grows on the dice that rotted and crumbles from those the rule rolled again; mould closes over the dice
         * that withered, with the host's reaction, only when one did.
         */
        private IEnumerator Rot(DiceRottedEvent rotted)
        {
            int rerolled = rotted.RerolledMask & DiceValues.AllMask;
            int turned = rotted.RottedMask & ~rerolled & rotted.Before.DifferingMask(rotted.After);
            context.PlayCue(settings.RotClip);
            var motions = new List<IEnumerator>();
            if (turned != 0) motions.Add(context.FlipDice(turned, rotted.After));
            if (rerolled != 0) motions.Add(context.RerollDice(rerolled, rotted.After));
            bool grows = false;
            for (int bay = 0; bay < dice.Length; ++bay)
                if (settings.MouldLevel(rotted.Counts[bay]) > dice[bay].MarkTarget) grows = true;
            counts = rotted.Counts;
            witheredMask |= rotted.WitheredMask;
            Show(true);
            float hold = grows ? settings.MouldGrowDuration : 0f;
            if (rotted.WitheredMask != 0)
            {
                context.PlayCue(settings.WitherClip);
                context.PlayCue(settings.WitherVoiceClip);
                context.ReactHost();
                context.JoltAmbient();
                hold = Mathf.Max(hold, settings.WitherDuration);
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

        /** The thrown dice's mould crumbles away and the withered among them are restored; nothing plays when none of them shows mould. */
        private IEnumerator Restore(DiceRestoredEvent restored)
        {
            int thrown = (restored.RestoredMask | restored.ClearedMask) & DiceValues.AllMask;
            bool shown = false;
            for (int bay = 0; bay < dice.Length; ++bay)
                if ((thrown & (1 << bay)) != 0 && Shows(dice[bay])) shown = true;
            witheredMask &= ~restored.RestoredMask;
            counts = restored.Counts;
            Show(true);
            if (!shown) return null;
            context.PlayCue(settings.RestoreClip);
            return Wait(settings.RestoreDuration);
        }

        private static bool Shows(DieBindingVisual die)
            => die.MarkTarget > 0f || die.Mark > 0f || die.GrowthTarget > 0f || die.Growth > 0f;

        /**
         * Shows each die's levels: the mark is the mould of its rot count; a withered die is closed in mould; while the
         * player is choosing, under the Wither endpoint, a kept 1 shows the warning.
         */
        private void Show(bool animate)
        {
            for (int bay = 0; bay < dice.Length; ++bay)
            {
                int bit = 1 << bay;
                bool withered = (witheredMask & bit) != 0;
                bool warned = choosing && (selectedMask & bit) == 0 && !withered && endpoint == RotEndpoint.Wither &&
                    faces[bay] == DiceValues.MinimumFace;
                float mark = settings.MouldLevel(counts[bay]);
                float mould = withered ? 1f : warned ? settings.WarningLevel : 0f;
                var die = dice[bay];
                if (!animate)
                {
                    die.Show(mark, mould);
                    continue;
                }
                if (die.MarkTarget != mark)
                    die.AnimateMark(mark, mark > die.MarkTarget ? settings.MouldGrowDuration : settings.RestoreDuration);
                if (die.GrowthTarget != mould) die.AnimateGrowth(mould, MouldDuration(die.GrowthTarget, mould));
            }
        }

        /**
         * Mould closes over the wither duration and crumbles from a withered die over the restore duration; a warning shows
         * and clears over the mark fade duration.
         */
        private float MouldDuration(float from, float to)
            => to >= 1f ? settings.WitherDuration : from >= 1f ? settings.RestoreDuration : settings.MarkFadeDuration;

        private static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        }
    }
}
