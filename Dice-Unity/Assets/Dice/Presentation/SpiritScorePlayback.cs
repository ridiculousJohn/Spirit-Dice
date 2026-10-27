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
        EncounterRulePresentationAsset RulePresentation { get; }
        /** The table's ambient activity, or null when the encounter authors none. */
        EncounterAmbientActivity Ambient { get; }
        /** The table's rule dice visuals, or null when no rule card references one. */
        EncounterRuleDiceVisuals RuleDiceVisuals { get; }
        void SetDiceSelection(int mask);
        void HideRewardsAndEnding();
        void SetOpeningOpacity(float opacity);
        void ShowEnding();
        void RecordScore(ScoreBreakdown score);
        /** Shows the table piece that repelled an interference attempt and returns how long its reaction lasts. */
        float ShowProtection(DisturbanceBlockedEvent blocked);
        void FinishPresentation(Resolution resolution);
    }

    /** Plays an accepted resolution's ordered events on the HUD, tray, host, and audio, then hands completion back to the view. */
    internal sealed class SpiritScorePlayback
    {
        private const float ScoreStepDuration = .52f;
        private const float DisturbanceHold = .3f;
        // A committed hand pauses before interference that precedes its scoring, so the poltergeist reads as interrupting it.
        private const float PlayHandDisturbanceBeat = .25f;
        private readonly ISpiritScorePlaybackHost host;
        private readonly SpiritGameplayHud hud;
        private readonly SpiritDiceTray diceTray;
        private readonly SpiritModifierHistoryTray modifierHistory;
        private readonly SpiritEncounterAudio encounterAudio;
        private readonly SpiritActionModifierCue actionModifierCue;
        private readonly SpiritEncounterEndingView endingView;
        private IEnumerator activeRoll;

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

        /** Releases an interrupted roll; the view stops the playback coroutine itself. */
        internal void CancelRoll()
        {
            (activeRoll as IDisposable)?.Dispose();
            activeRoll = null;
        }

        internal IEnumerator Play(Resolution resolution, float openingFade)
        {
            var controls = host.Controls;
            bool rollDisturbed = false;
            // The committed snapshot may include a new hand; only event dice are shown until presentation completes.
            foreach (var ruleEvent in resolution.Events)
            {
                if (ruleEvent is EncounterRuleActivatedEvent activatedRule)
                {
                    // Scored-hand rule bonuses are presented after the hand's modifier contributions.
                    if (activatedRule.MultiplierBonus == 0 && host.RulePresentation != null &&
                        activatedRule.RuleId == host.RulePresentation.RuleId)
                    {
                        hud.RenderEncounterRule(resolution.Snapshot, host.RulePresentation);
                        actionModifierCue.ShowHost(host.RulePresentation.Icon, ModifierPhase.PlayerReroll,
                            host.Host != null ? host.Host.GestureDuration : 1.8f);
                        hud.SelectionLabel.text = host.RulePresentation.DisplayName;
                        hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).amber;
                        host.Host?.React();
                        yield return hud.AnimateRuleSpirit(host.RulePresentation.Icon, .65f);
                    }
                }
                else if (ruleEvent is DiceRolledEvent rolled)
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
                    int diceCount = 0;
                    for (int die = 0; die < DiceValues.Count; die++)
                        if ((rollMask & (1 << die)) != 0) diceCount++;
                    SpiritRollTiming rollTiming = encounterAudio.BeginRoll(diceCount);
                    activeRoll = diceTray.Roll(rolled.After, rollMask, rollTiming);
                    yield return activeRoll;
                    activeRoll = null;
                    // The preview already includes a disturbance that follows this roll; its category waits for that die.
                    rollDisturbed = DisturbedAfter(resolution, rolled);
                    hud.SetScore(rollDisturbed ? null : controls.Preview?.Score, false);
                }
                else if (ruleEvent is DieDisturbedEvent disturbed)
                {
                    // Interference lands after the player's roll or before scoring; the die visibly moves even if its face returns.
                    HandScoredEvent disturbedHand = ScoredAfter(resolution, disturbed);
                    if (disturbedHand != null) yield return new WaitForSecondsRealtime(PlayHandDisturbanceBeat);
                    host.Host?.React();
                    encounterAudio.Play(SpiritAudioCue.Disturbance);
                    host.Ambient?.Disturb();
                    activeRoll = disturbed.Kind == DieInterferenceKind.Flip
                        ? diceTray.Flip(disturbed.Bay, disturbed.After)
                        : diceTray.RollDie(disturbed.Bay, disturbed.After, encounterAudio.BeginRoll(1));
                    yield return activeRoll;
                    activeRoll = null;
                    // The rank follows the landed die: the hand about to be scored, or the preview after a reroll.
                    if (disturbedHand != null) hud.SetScore(disturbedHand.Score, false);
                    else if (rollDisturbed) hud.SetScore(controls.Preview?.Score, false);
                    rollDisturbed = false;
                    yield return new WaitForSecondsRealtime(DisturbanceHold);
                }
                else if (ruleEvent is DisturbanceBlockedEvent blocked)
                {
                    if (ScoredAfter(resolution, blocked) != null) yield return new WaitForSecondsRealtime(PlayHandDisturbanceBeat);
                    host.Host?.React();
                    encounterAudio.Play(SpiritAudioCue.DisturbanceBlocked);
                    yield return new WaitForSecondsRealtime(Mathf.Max(host.ShowProtection(blocked), DisturbanceHold));
                }
                else if (ruleEvent is ModifierActivatedEvent activated && activated.Phase != ModifierPhase.PlayHand)
                {
                    // A banked roll bonus leaves the tray highlight, resolving token and total as the roll left them.
                    yield return PlayStep(Steps().BuildBankedContribution(activated.Contribution, hud.DisplayedBaseScore,
                        hud.DisplayedMultiplier, resolution.Events, activated.StageIndex, activated.HandIndex));
                }
                else if (ruleEvent is HandScoredEvent scored)
                {
                    host.RecordScore(scored.Score);
                    host.SetDiceSelection(0);
                    diceTray.SetValues(scored.Dice);
                    hud.SetScore(scored.Score, true);
                    modifierHistory.SynchronizeReady(scored.Score.ModifierContributions, controls.Catalog, controls.ModifierPresentation);
                    var steps = Steps().BuildHand(scored.Score, scored.Dice, hud.DisplayedBaseScore, hud.DisplayedMultiplier,
                        host.RulePresentation, resolution.Events, scored.StageIndex, scored.HandIndex);
                    foreach (var step in steps)
                    {
                        diceTray.SetScoringHighlight(step.HighlightMask);
                        if (step.ResolvingIndex >= 0) modifierHistory.SetResolving(step.Contribution.Value);
                        else modifierHistory.ClearResolving();
                        if (!step.RevealsTotal) hud.HandScoreLabel.text = DiagnosticText.Number(step.Total);
                        yield return PlayStep(step);
                    }
                }
                else if (ruleEvent is ThresholdChangedEvent threshold)
                {
                    hud.TargetCaption.text = "TO CLEAR";
                    yield return hud.AnimateThreshold(threshold.Before, threshold.After, .35f);
                    yield return new WaitForSecondsRealtime(.16f);
                }
                else if (ruleEvent is StageClearedEvent)
                {
                    hud.TargetLabel.text = "0";
                    hud.TargetCaption.text = "STAGE CLEARED";
                    encounterAudio.Play(SpiritAudioCue.StageClear);
                    if (resolution.Snapshot.Outcome != EncounterOutcome.Won ||
                        host.Journey?.SelectedJourney?.GetOutcomePresentation(EncounterOutcome.Won) == null)
                    {
                        host.Host?.React();
                        yield return new WaitForSecondsRealtime(host.Host != null ? host.Host.GestureDuration : 1.8f);
                    }
                }
                else if (ruleEvent is StageStartedEvent stage)
                {
                    hud.StageLabel.text = "STAGE " + (stage.StageIndex + 1) + " / " + resolution.Snapshot.StageCount;
                    hud.TargetLabel.text = DiagnosticText.Number(stage.Threshold);
                    hud.TargetCaption.text = "TO CLEAR";
                    hud.HandsPanel.gameObject.SetActive(true);
                    hud.RerollsPanel.gameObject.SetActive(true);
                    hud.ResourcesLabel.gameObject.SetActive(false);
                    hud.SetResources(stage.Hands, stage.Rerolls);
                    hud.PlayingPanel.gameObject.SetActive(false);
                    host.HideRewardsAndEnding();
                    hud.StageIntroTitle.text = hud.StageLabel.text;
                    hud.StageIntroDetail.fontSize = hud.StageIntroDetailFontSize;
                    hud.StageIntroDetail.text = "CLEAR " + DiagnosticText.Number(stage.Threshold) + " POINTS\n" +
                        stage.Hands + " hands / " + stage.Rerolls + " rerolls";
                    if (resolution.PreviousRevision >= 0)
                    {
                        hud.StageIntroPanel.gameObject.SetActive(true);
                        yield return new WaitForSecondsRealtime(.9f);
                    }
                    hud.StageIntroPanel.gameObject.SetActive(false);
                }
                else if (ruleEvent is HandStartedEvent hand)
                {
                    host.Ambient?.Begin();
                    modifierHistory.Clear();
                    hud.RenderEncounterRule(resolution.Snapshot, host.RulePresentation);
                    hud.SetResources(hand.HandsRemaining, hand.RerollsRemaining);
                    hud.SetScoreValues(0, 0);
                    hud.HandScoreLabel.text = "0";
                    hud.CategoryLabel.text = "";
                    hud.ModifierEmblem.gameObject.SetActive(false);
                    hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).muted;
                    hud.SelectionLabel.text = "NEW HAND";
                    hud.ScoreDetail.text = "";
                    if (resolution.PreviousRevision >= 0) yield return new WaitForSecondsRealtime(.16f);
                }
                else if (ruleEvent is ResourceChangedEvent resource && !IsModifierGrant(resource, resolution))
                {
                    hud.ShowResource(resource);
                    if (resource.Resource == ResourceKind.Rerolls && resource.Cause != ResourceChangeCause.ModifierEffect)
                        hud.SelectionLabel.text = resource.After + " rerolls remaining";
                }
                // A rule's dice visuals present each event after the playback's own presentation of it.
                IEnumerator ruleDice = host.RuleDiceVisuals?.Present(ruleEvent);
                if (ruleDice != null) yield return ruleDice;
            }
            if (resolution.Snapshot.Phase == EncounterPhase.Completed)
            {
                // Defeat ends the haunting with a final knock; victory and abandonment let it settle.
                if (resolution.Snapshot.Outcome == EncounterOutcome.Lost) host.Ambient?.Knock();
                else host.Ambient?.Settle();
                endingView.Render(resolution.Snapshot, host.Journey?.SelectedJourney,
                    SpiritCharmVisuals.UnlockSummary(controls, resolution.Snapshot.NewlyUnlockedModifierIds));
                yield return endingView.Present(host.Host, host.SetOpeningOpacity, host.ShowEnding,
                    clip => encounterAudio.PlayJourneyCue(clip));
            }
            host.FinishPresentation(resolution);
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

        private static bool DisturbedAfter(Resolution resolution, DiceRolledEvent rolled)
        {
            int next = resolution.Events.IndexOf(rolled) + 1;
            return next > 0 && next < resolution.Events.Count && resolution.Events[next] is DieDisturbedEvent;
        }

        /** The hand scored after an interference event in the same action, or null when no hand is scored before the next roll. */
        private static HandScoredEvent ScoredAfter(Resolution resolution, RuleEvent interference)
        {
            for (int next = resolution.Events.IndexOf(interference) + 1; next > 0 && next < resolution.Events.Count; next++)
            {
                if (resolution.Events[next] is DiceRolledEvent) return null;
                if (resolution.Events[next] is HandScoredEvent scored) return scored;
            }
            return null;
        }

        private static bool IsModifierGrant(ResourceChangedEvent resource, Resolution resolution)
        {
            if (resource.Cause != ResourceChangeCause.ModifierEffect) return false;
            if (IsPlayHandGrant(resource, resolution)) return true;
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ModifierActivatedEvent activated && activated.Phase != ModifierPhase.PlayHand &&
                    activated.StageIndex == resource.StageIndex && activated.HandIndex == resource.HandIndex &&
                    activated.InstanceId == resource.SourceInstanceId) return true;
            return false;
        }

        private static bool IsPlayHandGrant(ResourceChangedEvent resource, Resolution resolution)
        {
            if (resource.Cause != ResourceChangeCause.ModifierEffect) return false;
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ModifierActivatedEvent activated && activated.Phase == ModifierPhase.PlayHand &&
                    activated.StageIndex == resource.StageIndex && activated.HandIndex == resource.HandIndex &&
                    activated.InstanceId == resource.SourceInstanceId) return true;
            return false;
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
                actionModifierCue.ShowHost(host.RulePresentation != null ? host.RulePresentation.Icon : null,
                    ModifierPhase.PlayHand, host.Host != null ? host.Host.GestureDuration : 1.8f);
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

        private SpiritScoringStepBuilder Steps()
            => new SpiritScoringStepBuilder(host.Controls.Catalog, Charms, SpiritUi.GetTheme(hud.transform));

        private ModifierPresentationCatalog Charms => host.Controls.ModifierPresentation;
    }
}
