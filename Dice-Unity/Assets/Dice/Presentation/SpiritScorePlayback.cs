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
        private const float ScoreStepHold = .35f;
        private const float ModifierStepHold = .55f;
        private const float DisturbanceHold = .3f;
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
                    if (activatedRule.MultiplierBonus != 0) continue;
                    if (host.RulePresentation == null || activatedRule.RuleId != host.RulePresentation.RuleId) continue;
                    hud.RenderEncounterRule(resolution.Snapshot, host.RulePresentation);
                    actionModifierCue.ShowHost(host.RulePresentation.Icon, ModifierPhase.PlayerReroll,
                        host.Host != null ? host.Host.GestureDuration : 1.8f);
                    hud.SelectionLabel.text = host.RulePresentation.DisplayName;
                    hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).amber;
                    host.Host?.React();
                    yield return hud.AnimateRuleSpirit(host.RulePresentation.Icon, .65f);
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
                    host.Host?.React();
                    encounterAudio.Play(SpiritAudioCue.Disturbance);
                    host.Ambient?.Disturb();
                    activeRoll = disturbed.Kind == DieInterferenceKind.Flip
                        ? diceTray.Flip(disturbed.Bay, disturbed.After)
                        : diceTray.RollDie(disturbed.Bay, disturbed.After, encounterAudio.BeginRoll(1));
                    yield return activeRoll;
                    activeRoll = null;
                    if (rollDisturbed) hud.SetScore(controls.Preview?.Score, false);
                    rollDisturbed = false;
                    yield return new WaitForSecondsRealtime(DisturbanceHold);
                }
                else if (ruleEvent is DisturbanceBlockedEvent blocked)
                {
                    host.Host?.React();
                    encounterAudio.Play(SpiritAudioCue.DisturbanceBlocked);
                    yield return new WaitForSecondsRealtime(Mathf.Max(host.ShowProtection(blocked), DisturbanceHold));
                }
                else if (ruleEvent is ModifierActivatedEvent activated && activated.Phase != ModifierPhase.PlayHand)
                {
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return AnimateContribution(activated.Contribution, activated.StageIndex, activated.HandIndex, resolution, true);
                    yield return new WaitForSecondsRealtime(ModifierStepHold);
                }
                else if (ruleEvent is HandScoredEvent scored)
                {
                    host.RecordScore(scored.Score);
                    host.SetDiceSelection(0);
                    diceTray.SetValues(scored.Dice);
                    hud.SetScore(scored.Score, true);
                    modifierHistory.SynchronizeReady(scored.Score.ModifierContributions, controls.Catalog);
                    diceTray.SetScoringHighlight(DiceValues.AllMask);
                    hud.HandScoreLabel.text = "0";
                    host.Host?.React();
                    hud.ModifierEmblem.gameObject.SetActive(false);
                    hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).muted;
                    hud.SelectionLabel.text = "DICE\n+" + DiagnosticText.Number(scored.Score.DiceSum) + " base";
                    hud.CategoryLabel.text = "COUNTING DICE";
                    hud.ScoreDetail.text = "STEP 1 OF 4 - DICE";
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return hud.AnimateScore(hud.DisplayedBaseScore, hud.DisplayedMultiplier,
                        checked(hud.DisplayedBaseScore + scored.Score.DiceSum), hud.DisplayedMultiplier, ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(ScoreStepHold);

                    long handBase = checked(scored.Score.CategoryBaseScore + scored.Score.UpgradeBaseScore);
                    long handMultiplier = checked(scored.Score.CategoryMultiplier + scored.Score.UpgradeMultiplier);
                    hud.CategoryLabel.text = DiagnosticText.Humanize(scored.Score.Category.ToString()).ToUpperInvariant();
                    diceTray.SetScoringHighlight(SpiritHandScoringHighlights.GetMask(scored.Dice, scored.Score.Category));
                    hud.SelectionLabel.text = hud.CategoryLabel.text + " / LEVEL " + scored.Score.UpgradeCount + "\n" +
                        ContributionDelta(handBase, handMultiplier, 0);
                    hud.ScoreDetail.text = "STEP 2 OF 4 - HAND RANK";
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return hud.AnimateScore(hud.DisplayedBaseScore, hud.DisplayedMultiplier,
                        checked(hud.DisplayedBaseScore + handBase),
                        checked(hud.DisplayedMultiplier + handMultiplier), ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(ScoreStepHold);

                    // Recorded contributions retain the eligibility and activation order of the engine phase.
                    diceTray.SetScoringHighlight(0);
                    foreach (var contribution in scored.Score.ModifierContributions)
                    {
                        if (contribution.Phase != ModifierPhase.PlayHand) continue;
                        encounterAudio.Play(SpiritAudioCue.Score);
                        yield return AnimateContribution(contribution, scored.StageIndex, scored.HandIndex, resolution, false);
                        yield return new WaitForSecondsRealtime(ModifierStepHold);
                    }
                    modifierHistory.ClearResolving();
                    if (scored.Score.EncounterRuleMultiplier != 0)
                    {
                        hud.SelectionLabel.text = (host.RulePresentation != null ? host.RulePresentation.DisplayName : "ENCOUNTER RULE") +
                            "\n+" + DiagnosticText.Number(scored.Score.EncounterRuleMultiplier) + " multiplier";
                        hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).amber;
                        hud.CategoryLabel.text = "ENCOUNTER RULE";
                        hud.ScoreDetail.text = "STEP 3 OF 4 - ENCOUNTER RULE";
                        actionModifierCue.ShowHost(host.RulePresentation != null ? host.RulePresentation.Icon : null,
                            ModifierPhase.PlayHand, host.Host != null ? host.Host.GestureDuration : 1.8f);
                        host.Host?.React();
                        encounterAudio.Play(SpiritAudioCue.Score);
                        yield return hud.AnimateScore(hud.DisplayedBaseScore, hud.DisplayedMultiplier, hud.DisplayedBaseScore,
                            checked(hud.DisplayedMultiplier + scored.Score.EncounterRuleMultiplier), ScoreStepDuration);
                        yield return new WaitForSecondsRealtime(ScoreStepHold);
                    }
                    hud.ModifierEmblem.gameObject.SetActive(false);
                    hud.CategoryLabel.text = "FINAL SCORE";
                    hud.SelectionLabel.text = "TOTAL";
                    hud.SelectionLabel.color = SpiritUi.GetTheme(hud.transform).muted;
                    hud.ScoreDetail.text = "STEP 4 OF 4 - " + DiagnosticText.Number(scored.Score.BaseScore) + " x " +
                        DiagnosticText.Number(scored.Score.Multiplier);
                    encounterAudio.Play(SpiritAudioCue.Score);
                    yield return hud.AnimateTotal(scored.Score.HandScore, ScoreStepDuration);
                    yield return new WaitForSecondsRealtime(.55f);
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
                else if (ruleEvent is ResourceChangedEvent resource)
                {
                    if (IsModifierGrant(resource, resolution)) continue;
                    hud.ShowResource(resource);
                    if (resource.Resource == ResourceKind.Rerolls && resource.Cause != ResourceChangeCause.ModifierEffect)
                        hud.SelectionLabel.text = resource.After + " rerolls remaining";
                }
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

        private void ShowModifierGrants(long instanceId, int stageIndex, int handIndex, Resolution resolution,
            ModifierDefinition definition, List<SpiritCounterHighlight> highlights)
        {
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ResourceChangedEvent resource && resource.Cause == ResourceChangeCause.ModifierEffect &&
                    resource.StageIndex == stageIndex && resource.HandIndex == handIndex && resource.SourceInstanceId == instanceId)
                {
                    hud.ShowResource(resource);
                    if (resource.After == resource.Before) continue;
                    if (resource.Resource == ResourceKind.Rerolls)
                        SpiritGameplayHud.AddCounterHighlight(highlights, hud.RerollsHighlight, definition);
                    else if (resource.Resource == ResourceKind.Hands)
                        SpiritGameplayHud.AddCounterHighlight(highlights, hud.HandsHighlight, definition);
                }
        }

        private void ShowContribution(ModifierContribution contribution, bool banked)
        {
            var definition = host.Controls.Catalog.GetModifier(contribution.DefinitionId);
            if (!banked) modifierHistory.SetResolving(contribution);
            hud.ShowActivationIcon(definition);
            hud.CategoryLabel.text = CharmName(definition);
            hud.SelectionLabel.color = SpiritModifierIcons.GetEffectColor(definition);
            hud.SelectionLabel.text = CharmName(definition) + "\n" +
                ContributionDelta(contribution.BaseScore, contribution.Multiplier, contribution.Rerolls) +
                (banked ? " (banked)" : " (on play)");
            hud.ScoreDetail.text = banked ? "ROLL BONUS" : "STEP 3 OF 4 - CHARMS";
        }

        private IEnumerator AnimateContribution(ModifierContribution contribution, int stageIndex, int handIndex,
            Resolution resolution, bool banked)
        {
            ShowContribution(contribution, banked);
            var definition = host.Controls.Catalog.GetModifier(contribution.DefinitionId);
            bool showReroll = contribution.Rerolls != 0;
            bool showPlay = false;
            foreach (var ruleEvent in resolution.Events)
                if (ruleEvent is ResourceChangedEvent resource && resource.Cause == ResourceChangeCause.ModifierEffect &&
                    resource.StageIndex == stageIndex && resource.HandIndex == handIndex &&
                    resource.SourceInstanceId == contribution.InstanceId && resource.After != resource.Before)
                {
                    showReroll |= resource.Resource == ResourceKind.Rerolls;
                    showPlay |= resource.Resource == ResourceKind.Hands;
                }
            if (contribution.BaseScore != 0 || contribution.Multiplier != 0 || (!showReroll && !showPlay))
            {
                showPlay |= definition.Phase == ModifierPhase.PlayHand;
                showReroll |= definition.Phase != ModifierPhase.PlayHand;
            }
            actionModifierCue.Show(definition, ScoreStepDuration + ModifierStepHold, showReroll, showPlay);
            var highlights = new List<SpiritCounterHighlight>();
            if (contribution.BaseScore != 0) SpiritGameplayHud.AddCounterHighlight(highlights, hud.BaseScoreHighlight, definition);
            if (contribution.Multiplier != 0) SpiritGameplayHud.AddCounterHighlight(highlights, hud.MultiplierHighlight, definition);
            ShowModifierGrants(contribution.InstanceId, stageIndex, handIndex, resolution, definition, highlights);
            yield return hud.AnimateScore(hud.DisplayedBaseScore, hud.DisplayedMultiplier,
                checked(hud.DisplayedBaseScore + contribution.BaseScore), checked(hud.DisplayedMultiplier + contribution.Multiplier),
                ScoreStepDuration, highlights);
        }

        private static string ContributionDelta(long baseScore, long multiplier, long rerolls)
        {
            var effects = new List<string>();
            if (baseScore != 0) effects.Add("+" + DiagnosticText.Number(baseScore) + " base");
            if (multiplier != 0) effects.Add("+" + DiagnosticText.Number(multiplier) + " mult");
            if (rerolls != 0) effects.Add("+" + rerolls + (rerolls == 1 ? " reroll" : " rerolls"));
            return string.Join("  /  ", effects);
        }

        private static string CharmName(ModifierDefinition definition)
            => SpiritModifierPresentation.GetName(definition);
    }
}
