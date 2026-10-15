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

        public RectTransform Root { get; private set; }

        private RectTransform panel;
        private RectTransform content;
        private ScrollRect scroll;
        private Text title;
        private Text detail;
        private Text clueTitle;
        private Text clueText;
        private Text unlockTitle;
        private Text unlocks;
        private RectTransform artworkFrame;
        private RawImage artwork;
        private Button returnButton;
        private Button retryButton;
        private Button dialogueContinueButton;
        private Button skipButton;
        private Action returnToTrail;
        private Action retry;
        private Action<float> activeFade;
        private EncounterSnapshot snapshot;
        private EncounterJourneyAsset journey;
        private EncounterOutcomePresentation presentation;
        private string unlockSummary;
        private EndingPhase phase;
        private int contextVersion;
        private Vector2 portraitSize;

        public void Build(RectTransform parent, Action returnToTrail, Action retry)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (returnToTrail == null) throw new ArgumentNullException(nameof(returnToTrail));
            if (Root != null) throw new InvalidOperationException("The encounter ending has already been built.");
            this.returnToTrail = returnToTrail;
            this.retry = retry;
            var theme = SpiritUi.GetTheme(parent);
            Root = SpiritUi.Full("EndingView", parent);
            var group = Root.gameObject.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            var blocker = Root.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            panel = SpiritUi.Panel("EndingPanel", Root, new Vector2(.045f, .055f), new Vector2(.955f, .62f),
                theme.panelColor, 0, true).rectTransform;
            title = SpiritUi.Heading("EndingTitle", panel, "", 35, new Vector2(.025f, .845f), new Vector2(.975f, .98f),
                TextAnchor.MiddleCenter, theme.amber);
            title.resizeTextMinSize = 25;
            scroll = SpiritUi.Scroll("EndingUnlockScroll", panel, new Vector2(.035f, .245f), new Vector2(.965f, .84f), out content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 6, 12);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            artworkFrame = SpiritUi.Full("EndingArtworkFrame", content);
            artworkFrame.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
            artwork = SpiritUi.Full("EndingArtwork", artworkFrame).gameObject.AddComponent<RawImage>();
            artwork.raycastTarget = false;
            var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            detail = ContentLabel("EndingDetail", 21, theme.cream);
            clueTitle = ContentLabel("EndingClueTitle", 24, theme.amber, true);
            clueText = ContentLabel("EndingClueText", 21, theme.cream);
            unlockTitle = ContentLabel("EndingUnlockTitle", 17, theme.muted, true);
            unlocks = ContentLabel("EndingUnlocks", 19, theme.amber);
            returnButton = SpiritUi.Button("EndingNewRunButton", panel, "Return to the trail",
                new Vector2(.065f, .045f), new Vector2(.935f, .215f),
                () => { if (IsSummary) this.returnToTrail(); }, true);
            var returnLabel = returnButton.GetComponentInChildren<Text>();
            returnLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            returnLabel.rectTransform.anchorMin = new Vector2(.18f, .08f);
            returnLabel.rectTransform.anchorMax = new Vector2(.82f, .92f);
            retryButton = SpiritUi.Button("EndingRetryButton", panel, "Retry",
                new Vector2(.065f, .045f), new Vector2(.495f, .215f),
                () => { if (IsSummary) this.retry?.Invoke(); });
            dialogueContinueButton = SpiritUi.Button("EndingDialogueContinueButton", panel, "Continue",
                new Vector2(.065f, .045f), new Vector2(.935f, .215f),
                ContinueDialogue, true);
            skipButton = SpiritUi.Button("EndingSkipButton", Root, "Skip farewell",
                new Vector2(.55f, .025f), new Vector2(.955f, .1f), Skip);
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
            ++contextVersion;
            CancelFade();
            snapshot = null;
            journey = null;
            presentation = null;
            unlockSummary = "";
            phase = EndingPhase.Hidden;
            if (Root != null) Root.gameObject.SetActive(false);
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

        private void OnDisable() => Reset();

        private void LateUpdate()
        {
            if (Root != null && Root.gameObject.activeInHierarchy && panel.gameObject.activeSelf &&
                Root.rect.size != portraitSize) FitPanel();
        }

        private void FitPanel()
        {
            portraitSize = Root.rect.size;
            if (portraitSize.y <= 0) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float height = Mathf.Min(portraitSize.y * .565f, Mathf.Max(360, (content.rect.height + 20) / .595f));
            panel.anchorMin = new Vector2(.045f, .62f - height / portraitSize.y);
        }

        private Text ContentLabel(string name, int size, Color color, bool heading = false)
        {
            var label = heading ? SpiritUi.Heading(name, content, "", size, Vector2.zero, Vector2.one,
                TextAnchor.UpperLeft, color) : SpiritUi.Label(name, content, "", size, Vector2.zero, Vector2.one,
                TextAnchor.UpperLeft, color);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            return label;
        }

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
            returnButton.GetComponent<RectTransform>().anchorMin = new Vector2(canRetry ? .505f : .065f, .045f);
            SpiritUi.SetButtonLabel(returnButton, journey != null && !string.IsNullOrWhiteSpace(journey.ResultContinueLabel)
                ? journey.ResultContinueLabel : "Return to the trail");
            if (!dialogue && !summary) return;

            title.text = dialogue ? journey.SpiritName : abandoned ? "Until next time" : journey == null
                ? lost ? "Encounter ended" : "Encounter complete"
                : lost ? journey.DefeatTitle : journey.VictoryTitle;
            if (dialogue)
                detail.text = FarewellText;
            else if (abandoned)
                detail.text = !string.IsNullOrWhiteSpace(FarewellText) ? FarewellText : "The trail is waiting.";
            else
                detail.text = (presentation == null && !string.IsNullOrWhiteSpace(FarewellText) ? FarewellText + "\n\n" : "") +
                    "Total score  " + DiagnosticText.Number(snapshot.TotalScore);
            detail.fontSize = dialogue ? 23 : 21;
            detail.gameObject.SetActive(!string.IsNullOrWhiteSpace(detail.text));
            bool clue = summary && snapshot.Outcome == EncounterOutcome.Won && journey != null;
            clueTitle.text = clue ? journey.CompletionClueTitle : "";
            clueText.text = clue ? journey.CompletionClueText : "";
            clueTitle.gameObject.SetActive(!string.IsNullOrWhiteSpace(clueTitle.text));
            clueText.gameObject.SetActive(!string.IsNullOrWhiteSpace(clueText.text));
            unlocks.text = summary ? unlockSummary : "";
            bool hasUnlocks = !string.IsNullOrWhiteSpace(unlocks.text);
            unlockTitle.text = "UNLOCKS";
            unlockTitle.gameObject.SetActive(hasUnlocks);
            unlocks.gameObject.SetActive(hasUnlocks);
            artwork.texture = presentation?.Artwork;
            artworkFrame.gameObject.SetActive(artwork.texture != null);
            if (artwork.texture != null)
                artwork.GetComponent<AspectRatioFitter>().aspectRatio = (float)artwork.texture.width / artwork.texture.height;
            FitPanel();
        }
    }
}
