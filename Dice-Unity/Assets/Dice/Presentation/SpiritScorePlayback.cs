using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Presentation
{
    /** View state that score playback reads live, and the view actions it calls back into. */
    internal interface ISpiritScorePlaybackHost
    {
        ISpiritEncounterControls Controls { get; }
        IEncounterJourneyControls Journey { get; }
        SpiritHostPortrait Host { get; }
        /** The configured table's rule cards. */
        IReadOnlyList<EncounterRulePresentationAsset> RuleCards { get; }
        /** The configured table's rule card shown on the HUD, or null when the table shows none. */
        EncounterRulePresentationAsset HudRuleCard { get; }
        /** The table's ambient activity, or null when the encounter authors none. */
        EncounterAmbientActivity Ambient { get; }
        /** The presenters of the table's rules, or null when no rule card references one. */
        EncounterRulePresenters RulePresenters { get; }
        void SetDiceSelection(int mask);
        void HideRewardsAndEnding();
        void SetOpeningOpacity(float opacity);
        void ShowEnding();
        void RecordScore(ScoreBreakdown score);
        void FinishPresentation(Resolution resolution);
    }

    /**
     * Plays an accepted resolution's ordered events on the HUD, tray, host, and audio, then hands completion back to the
     * view. For each event it waits the longest lead-in the rule presenters ask for, presents the core changes, then runs
     * every rule presenter's presentation of the event side by side and waits for all of them.
     */
    internal sealed class SpiritScorePlayback
    {
        private const float ScoreStepDuration = .52f;
        // Seconds the host's gesture is assumed to last when the encounter has no host portrait.
        private const float FallbackGestureDuration = 1.8f;
        // Seconds the remaining-threshold counter takes to count down, and the pause after it.
        private const float ThresholdCountDuration = .35f;
        private const float ThresholdHold = .16f;
        // Seconds a later stage's intro panel holds before play resumes.
        private const float StageIntroHold = .9f;
        // Seconds a later hand's cleared counters hold before its roll.
        private const float HandStartHold = .16f;
        private readonly ISpiritScorePlaybackHost host;
        private readonly SpiritGameplayHud hud;
        private readonly SpiritDiceTray diceTray;
        private readonly SpiritModifierHistoryTray modifierHistory;
        private readonly SpiritEncounterAudio encounterAudio;
        private readonly SpiritActionModifierCue actionModifierCue;
        private readonly SpiritEncounterEndingView endingView;
        private IEnumerator activeRoll;
        private ConcurrentRoutines activeRules;

        internal SpiritScorePlayback(ISpiritScorePlaybackHost host, SpiritGameplayHud hud, SpiritDiceTray diceTray,
            SpiritModifierHistoryTray modifierHistory, SpiritEncounterAudio encounterAudio,
            SpiritActionModifierCue actionModifierCue, SpiritEncounterEndingView endingView)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.hud = hud;
            this.diceTray = diceTray;
            this.modifierHistory = modifierHistory;
            this.encounterAudio = encounterAudio;
            this.actionModifierCue = actionModifierCue;
            this.endingView = endingView;
        }

        /** Releases an interrupted roll and the rule presentations still running; the view stops the playback coroutine itself. */
        internal void CancelRoll()
        {
            (activeRoll as IDisposable)?.Dispose();
            activeRoll = null;
            activeRules?.Dispose();
            activeRules = null;
        }

        internal IEnumerator Play(Resolution resolution, float openingFade)
        {
            // The committed snapshot may include a new hand; only event dice are shown until presentation completes.
            foreach (var ruleEvent in resolution.Events)
            {
                var presenters = host.RulePresenters;
                float leadIn = presenters != null ? presenters.LeadIn(ruleEvent) : 0f;
                if (leadIn > 0f) yield return new WaitForSecondsRealtime(leadIn);
                IEnumerator core = PresentCore(ruleEvent, resolution, openingFade);
                if (core != null) yield return core;
                activeRules = presenters?.Present(ruleEvent);
                if (activeRules != null) yield return activeRules;
                activeRules = null;
            }
            if (resolution.Snapshot.Phase == EncounterPhase.Completed)
            {
                host.Ambient?.End(resolution.Snapshot.Outcome);
                endingView.Render(resolution.Snapshot, host.Journey?.SelectedJourney,
                    SpiritCharmVisuals.UnlockSummary(host.Controls, resolution.Snapshot.NewlyUnlockedModifierIds, Wording));
                yield return endingView.Present(host.Host, host.SetOpeningOpacity, host.ShowEnding,
                    clip => encounterAudio.PlayCue(clip));
            }
            host.FinishPresentation(resolution);
        }

        /** The core presentation of an event the core presents, or null for an event only rule presenters present. */
        private IEnumerator PresentCore(RuleEvent ruleEvent, Resolution resolution, float openingFade)
        {
            switch (ruleEvent)
            {
                case DiceRolledEvent rolled: return PresentRoll(rolled, openingFade);
                case ModifierActivatedEvent activated: return PresentBankedBonus(activated);
                case HandScoredEvent scored: return PresentHand(scored);
                case ThresholdChangedEvent threshold: return PresentThreshold(threshold);
                case StageClearedEvent _: return PresentStageCleared(resolution);
                case StageStartedEvent stage: return PresentStage(stage, resolution);
                case HandStartedEvent hand: return PresentHandStart(hand, resolution);
                case ResourceChangedEvent resource: return PresentResource(resource);
                default: return null;
            }
        }

        private IEnumerator PresentRoll(DiceRolledEvent rolled, float openingFade)
        {
            modifierHistory.Clear();
            diceTray.SetScoringHighlight(0);
            hud.PlayingPanel.gameObject.SetActive(true);
            host.HideRewardsAndEnding();
            if (rolled.Before.HasValue) diceTray.SetValues(rolled.Before.Value);
            host.SetDiceSelection(rolled.SelectedMask);
            if (openingFade > 0 && rolled.Kind == RollKind.Initial)
                yield return FadeOpening(openingFade);
            int rollMask = rolled.Kind == RollKind.Initial ? DiceValues.AllMask : rolled.SelectedMask;
            SpiritRollTiming rollTiming = encounterAudio.BeginRoll(DiceValues.CountBays(rollMask));
            activeRoll = diceTray.Roll(rolled.After, rollMask, rollTiming);
            yield return activeRoll;
            activeRoll = null;
        }

        private IEnumerator PresentBankedBonus(ModifierActivatedEvent activated)
        {
            if (activated.Phase == ModifierPhase.PlayHand) yield break;
            // A banked roll bonus leaves the tray highlight, resolving token and total as the roll left them.
            yield return PlayStep(Steps().BuildBankedContribution(activated, hud.DisplayedBaseScore, hud.DisplayedMultiplier));
        }

        private IEnumerator PresentHand(HandScoredEvent scored)
        {
            host.RecordScore(scored.Score);
            host.SetDiceSelection(0);
            diceTray.SetValues(scored.Dice);
            var controls = host.Controls;
            modifierHistory.SynchronizeReady(scored.Score.ModifierContributions, controls.Catalog, controls.ModifierPresentation);
            var steps = Steps().BuildHand(scored, hud.DisplayedBaseScore, hud.DisplayedMultiplier, host.RuleCards);
            foreach (var step in steps)
            {
                diceTray.SetScoringHighlight(step.HighlightMask);
                if (step.ResolvingIndex >= 0) modifierHistory.SetResolving(step.Contribution.Value);
                else modifierHistory.ClearResolving();
                if (!step.RevealsTotal) hud.HandScoreLabel.text = DiagnosticText.Number(step.Total);
                yield return PlayStep(step);
            }
        }

        private IEnumerator PresentThreshold(ThresholdChangedEvent threshold)
        {
            hud.TargetCaption.text = Wording.hud.toClear;
            yield return hud.AnimateThreshold(threshold.Before, threshold.After, ThresholdCountDuration);
            yield return new WaitForSecondsRealtime(ThresholdHold);
        }

        private IEnumerator PresentStageCleared(Resolution resolution)
        {
            hud.TargetLabel.text = "0";
            hud.TargetCaption.text = Wording.hud.stageCleared;
            encounterAudio.Play(SpiritAudioCue.StageClear);
            if (resolution.Snapshot.Outcome != EncounterOutcome.Won ||
                host.Journey?.SelectedJourney?.GetOutcomePresentation(EncounterOutcome.Won) == null)
            {
                host.Host?.React();
                yield return new WaitForSecondsRealtime(GestureDuration);
            }
        }

        private IEnumerator PresentStage(StageStartedEvent stage, Resolution resolution)
        {
            var wording = Wording.hud;
            hud.StageLabel.text = wording.Stage(stage.StageIndex + 1, resolution.Snapshot.StageCount);
            hud.TargetLabel.text = DiagnosticText.Number(stage.Threshold);
            hud.TargetCaption.text = wording.toClear;
            hud.HandsPanel.gameObject.SetActive(true);
            hud.RerollsPanel.gameObject.SetActive(true);
            hud.ResourcesLabel.gameObject.SetActive(false);
            hud.SetResources(stage.Hands, stage.Rerolls);
            hud.PlayingPanel.gameObject.SetActive(false);
            host.HideRewardsAndEnding();
            hud.StageIntroTitle.text = hud.StageLabel.text;
            hud.StageIntroDetail.fontSize = hud.StageIntroDetailFontSize;
            hud.StageIntroDetail.text = wording.StageIntro(stage.Threshold, stage.Hands, stage.Rerolls);
            if (resolution.PreviousRevision >= 0)
            {
                hud.StageIntroPanel.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(StageIntroHold);
            }
            hud.StageIntroPanel.gameObject.SetActive(false);
        }

        private IEnumerator PresentHandStart(HandStartedEvent hand, Resolution resolution)
        {
            host.Ambient?.Begin();
            modifierHistory.Clear();
            hud.RenderEncounterRule(resolution.Snapshot, host.HudRuleCard);
            hud.SetResources(hand.HandsRemaining, hand.RerollsRemaining);
            hud.SetScoreValues(0, 0);
            hud.HandScoreLabel.text = "0";
            hud.CategoryLabel.text = "";
            hud.ModifierEmblem.gameObject.SetActive(false);
            hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).muted;
            hud.SelectionLabel.text = Wording.hud.newHand;
            hud.ScoreDetail.text = "";
            if (resolution.PreviousRevision >= 0) yield return new WaitForSecondsRealtime(HandStartHold);
        }

        private IEnumerator PresentResource(ResourceChangedEvent resource)
        {
            // A charm's grant is presented with the charm's own step.
            if (resource.GrantedBy != null) yield break;
            hud.ShowResource(resource);
            if (resource.Resource == ResourceKind.Rerolls) hud.SelectionLabel.text = Wording.hud.RerollsRemaining(resource.After);
        }

        private IEnumerator FadeOpening(float duration)
        {
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                host.SetOpeningOpacity(Mathf.SmoothStep(0, 1, elapsed / duration));
                yield return null;
            }
            host.SetOpeningOpacity(1);
        }

        /** Shows a scoring step's labels, emblem, cues and resource changes, animates its counters, and holds it. */
        private IEnumerator PlayStep(SpiritScoringStep step)
        {
            if (step.HostReacts) host.Host?.React();
            if (step.Emblem != null) hud.ShowActivationIcon(Charms, step.Emblem);
            else hud.ModifierEmblem.gameObject.SetActive(false);
            hud.CategoryLabel.text = step.CategoryText;
            hud.SelectionLabel.color = step.SelectionColor;
            hud.SelectionLabel.text = step.SelectionText;
            hud.ScoreDetail.text = step.StepLabel;
            if (step.Charm != null)
                actionModifierCue.Show(Charms, step.Charm, ScoreStepDuration + step.Hold, step.RerollCue, step.PlayCue);
            if (step.RuleCue)
                actionModifierCue.ShowHost(step.Rule.Icon, ModifierPhase.PlayHand, GestureDuration);
            var highlights = new List<SpiritCounterHighlight>();
            if (step.HighlightsBaseScore) SpiritGameplayHud.AddCounterHighlight(highlights, hud.BaseScoreHighlight, Charms, step.Charm);
            if (step.HighlightsMultiplier) SpiritGameplayHud.AddCounterHighlight(highlights, hud.MultiplierHighlight, Charms, step.Charm);
            if (step.HighlightsRerolls) SpiritGameplayHud.AddCounterHighlight(highlights, hud.RerollsHighlight, Charms, step.Charm);
            if (step.HighlightsHands) SpiritGameplayHud.AddCounterHighlight(highlights, hud.HandsHighlight, Charms, step.Charm);
            foreach (var grant in step.Grants) hud.ShowResource(grant);
            encounterAudio.Play(SpiritAudioCue.Score);
            if (step.RevealsTotal) yield return hud.AnimateTotal(step.Total, ScoreStepDuration);
            else yield return hud.AnimateScore(hud.DisplayedBaseScore, hud.DisplayedMultiplier, step.BaseScore, step.Multiplier,
                ScoreStepDuration, highlights);
            yield return new WaitForSecondsRealtime(step.Hold);
        }

        /** Seconds the host's gesture lasts, or the assumed length when the encounter has no host portrait. */
        internal static float GestureDurationOf(SpiritHostPortrait hostPortrait)
            => hostPortrait != null ? hostPortrait.GestureDuration : FallbackGestureDuration;

        private float GestureDuration => GestureDurationOf(host.Host);

        private SpiritScoringStepBuilder Steps()
            => new SpiritScoringStepBuilder(host.Controls.Catalog, Charms, SpiritUi.GetTheme(hud.transform), Wording);

        private SpiritUiWording Wording => SpiritUi.GetWording(hud.transform);

        private ModifierPresentationCatalog Charms => host.Controls.ModifierPresentation;
    }
}
