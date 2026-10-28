using System;
using System.Collections;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents the host's farewell before the encounter's clue and unlock summary. */
    public sealed class SpiritEncounterEndingView : MonoBehaviour
    {
        private enum EndingPhase { Hidden, PendingSummary, Holding, Reaction, Fading, Dialogue, Summary }

        [field: SerializeField] public RectTransform Root { get; private set; }

        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Text title;
        [SerializeField] private Text detail;
        [SerializeField] private Text clueTitle;
        [SerializeField] private Text clueText;
        [SerializeField] private Text unlockTitle;
        [SerializeField] private Text unlocks;
        [SerializeField] private RectTransform artworkFrame;
        [SerializeField] private RawImage artwork;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button dialogueContinueButton;
        [SerializeField] private Button skipButton;
        private Action returnToTrail;
        private Action retry;
        private Action<float> activeFade;
        private EncounterSnapshot snapshot;
        private EncounterJourneyAsset journey;
        private EncounterOutcomePresentation presentation;
        private string unlockSummary;
        private EndingPhase phase;
        private int contextVersion;
        [SerializeField] private AspectRatioFitter artworkAspect;
        [SerializeField] private int detailDialogueSize = 23;
        [SerializeField] private int detailSummarySize = 21;

        public void Build(Action returnToTrail, Action retry)
        {
            SpiritUi.RequireWording(transform);
            if (returnToTrail == null) throw new ArgumentNullException(nameof(returnToTrail));
            if (Root == null || panel == null || content == null || scroll == null || title == null || detail == null ||
                clueTitle == null || clueText == null || unlockTitle == null || unlocks == null || artworkFrame == null ||
                artwork == null || artworkAspect == null || returnButton == null || retryButton == null ||
                dialogueContinueButton == null || skipButton == null)
                throw new InvalidOperationException("The encounter ending requires authored UI references.");
            this.returnToTrail = returnToTrail;
            this.retry = retry;
            returnButton.onClick.RemoveListener(ReturnToTrail);
            returnButton.onClick.AddListener(ReturnToTrail);
            retryButton.onClick.RemoveListener(Retry);
            retryButton.onClick.AddListener(Retry);
            dialogueContinueButton.onClick.RemoveListener(ContinueDialogue);
            dialogueContinueButton.onClick.AddListener(ContinueDialogue);
            skipButton.onClick.RemoveListener(Skip);
            skipButton.onClick.AddListener(Skip);
            Reset();
        }

        public void Render(EncounterSnapshot snapshot, EncounterJourneyAsset journey, string unlockSummary)
        {
            if (Root == null) throw new InvalidOperationException("Build the encounter ending before rendering it.");
            if (snapshot == null || snapshot.Phase != EncounterPhase.Completed)
            {
                Reset();
                return;
            }
            bool changed = this.snapshot == null || this.snapshot.EncounterId != snapshot.EncounterId ||
                this.snapshot.Revision != snapshot.Revision || this.snapshot.Outcome != snapshot.Outcome || this.journey != journey;
            if (changed)
            {
                CancelFade();
                ++contextVersion;
                phase = EndingPhase.PendingSummary;
            }
            this.snapshot = snapshot;
            this.journey = journey;
            this.unlockSummary = unlockSummary ?? "";
            presentation = journey != null ? journey.GetOutcomePresentation(snapshot.Outcome) : null;
            Root.gameObject.SetActive(true);
            Refresh();
            if (changed) scroll.verticalNormalizedPosition = 1;
        }

        public IEnumerator Present(SpiritHostPortrait host, Action<float> fadeGameplay, Action hideGameplay, Action<AudioClip> playCue)
        {
            if (snapshot == null || phase != EndingPhase.PendingSummary) yield break;
            if (presentation == null)
            {
                phase = EndingPhase.Summary;
                Refresh();
                yield break;
            }
            int version = contextVersion;
            var outcome = presentation;
            phase = EndingPhase.Holding;
            activeFade = fadeGameplay;
            Refresh();
            if (outcome.AudioCue != null) playCue?.Invoke(outcome.AudioCue);
            try
            {
                float age = 0;
                while (Current(version) && phase == EndingPhase.Holding && age < outcome.HoldDuration)
                {
                    yield return null;
                    age += Time.unscaledDeltaTime;
                }
                if (!Current(version)) yield break;
                if (phase == EndingPhase.Holding) phase = EndingPhase.Reaction;
                float duration = host != null ? host.GetDuration(outcome.HostState) : 0;
                age = 0;
                while (Current(version) && phase == EndingPhase.Reaction && age < duration)
                {
                    host.SetState(outcome.HostState, Mathf.Clamp01(age / duration));
                    yield return null;
                    age += Time.unscaledDeltaTime;
                }
                if (!Current(version)) yield break;
                if (host != null) host.SetState(outcome.HostState, 1);
                phase = EndingPhase.Fading;
                Refresh();
                age = 0;
                while (Current(version) && age < outcome.FadeDuration)
                {
                    fadeGameplay?.Invoke(1 - Mathf.Clamp01(age / outcome.FadeDuration));
                    yield return null;
                    age += Time.unscaledDeltaTime;
                }
                if (!Current(version)) yield break;
                fadeGameplay?.Invoke(0);
                hideGameplay?.Invoke();
                if (!Current(version)) yield break;
                Root.gameObject.SetActive(true);
                phase = outcome.ShowDialogue ? EndingPhase.Dialogue : EndingPhase.Summary;
                Refresh();
                scroll.verticalNormalizedPosition = 1;
            }
            finally
            {
                if (version == contextVersion) CancelFade();
            }
        }

        public void Reset()
        {
            ClearContext();
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private void ClearContext()
        {
            ++contextVersion;
            CancelFade();
            snapshot = null;
            journey = null;
            presentation = null;
            unlockSummary = "";
            phase = EndingPhase.Hidden;
        }

        private void ReturnToTrail()
        {
            if (IsSummary) returnToTrail?.Invoke();
        }

        private void Retry()
        {
            if (IsSummary) retry?.Invoke();
        }

        private bool IsSummary => phase == EndingPhase.PendingSummary || phase == EndingPhase.Summary;

        private bool Current(int version) => version == contextVersion && snapshot != null && isActiveAndEnabled;

        private void Skip()
        {
            if (phase != EndingPhase.Holding && phase != EndingPhase.Reaction) return;
            phase = EndingPhase.Fading;
            Refresh();
        }

        private void ContinueDialogue()
        {
            if (phase != EndingPhase.Dialogue) return;
            phase = EndingPhase.Summary;
            Refresh();
            scroll.verticalNormalizedPosition = 1;
        }

        private void CancelFade()
        {
            var fade = activeFade;
            activeFade = null;
            fade?.Invoke(1);
        }

        private void OnDisable() => ClearContext();

        private string FarewellText => journey == null ? "" : snapshot.Outcome == EncounterOutcome.Won ? journey.VictoryText :
            snapshot.Outcome == EncounterOutcome.Abandoned ? journey.AbandonText : journey.DefeatText;

        private void Refresh()
        {
            if (snapshot == null) return;
            bool dialogue = phase == EndingPhase.Dialogue;
            bool summary = IsSummary;
            panel.gameObject.SetActive(dialogue || summary);
            skipButton.gameObject.SetActive(phase == EndingPhase.Holding || phase == EndingPhase.Reaction);
            dialogueContinueButton.gameObject.SetActive(dialogue);
            returnButton.gameObject.SetActive(summary);
            bool lost = snapshot.Outcome == EncounterOutcome.Lost;
            bool abandoned = snapshot.Outcome == EncounterOutcome.Abandoned;
            bool canRetry = summary && lost && retry != null;
            retryButton.gameObject.SetActive(canRetry);
            returnButton.interactable = summary;
            retryButton.interactable = canRetry;
            var wording = SpiritUi.GetWording(transform).ending;
            SpiritUi.SetButtonLabel(returnButton, journey != null && !string.IsNullOrWhiteSpace(journey.ResultContinueLabel)
                ? journey.ResultContinueLabel : wording.returnButton);
            if (!dialogue && !summary) return;

            title.text = dialogue ? journey.SpiritName : abandoned ? wording.abandonedTitle : journey == null
                ? lost ? wording.lostTitle : wording.wonTitle
                : lost ? journey.DefeatTitle : journey.VictoryTitle;
            if (dialogue)
                detail.text = FarewellText;
            else if (abandoned)
                detail.text = !string.IsNullOrWhiteSpace(FarewellText) ? FarewellText : wording.abandonedDetail;
            else
                detail.text = wording.Summary(presentation == null ? FarewellText : null, snapshot.TotalScore);
            detail.fontSize = dialogue ? detailDialogueSize : detailSummarySize;
            detail.gameObject.SetActive(!string.IsNullOrWhiteSpace(detail.text));
            bool clue = summary && snapshot.Outcome == EncounterOutcome.Won && journey != null;
            clueTitle.text = clue ? journey.CompletionClueTitle : "";
            clueText.text = clue ? journey.CompletionClueText : "";
            clueTitle.gameObject.SetActive(!string.IsNullOrWhiteSpace(clueTitle.text));
            clueText.gameObject.SetActive(!string.IsNullOrWhiteSpace(clueText.text));
            unlocks.text = summary ? unlockSummary : "";
            bool hasUnlocks = !string.IsNullOrWhiteSpace(unlocks.text);
            unlockTitle.gameObject.SetActive(hasUnlocks);
            unlocks.gameObject.SetActive(hasUnlocks);
            artwork.texture = presentation?.Artwork;
            artworkFrame.gameObject.SetActive(artwork.texture != null);
            if (artwork.texture != null)
                artworkAspect.aspectRatio = (float)artwork.texture.width / artwork.texture.height;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
