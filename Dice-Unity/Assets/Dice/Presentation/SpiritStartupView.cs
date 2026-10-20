using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents application loading and failures independently of gameplay. */
    public sealed class SpiritStartupView : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        public Text Status { get; private set; }

        public void Build(SpiritUiTheme theme)
        {
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            if (Root != null) throw new InvalidOperationException("The startup view has already been built.");

            var context = GetComponent<SpiritUiContext>() ?? gameObject.AddComponent<SpiritUiContext>();
            context.Configure(theme);
            Root = SpiritUi.Full("StartupCanvas", transform);
            var canvas = Root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = Root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;

            var backdropColor = theme.panelColor;
            backdropColor.a = 1;
            var backdrop = SpiritUi.Panel("StartupBackdrop", Root, Vector2.zero, Vector2.one, backdropColor);
            backdrop.raycastTarget = false;
            SpiritUi.Heading("StartupTitle", Root, "Spirit Dice", 42,
                new Vector2(.12f, .60f), new Vector2(.88f, .71f), TextAnchor.MiddleCenter, theme.amber);
            Status = SpiritUi.Label("StartupStatus", Root, "Loading...", 26,
                new Vector2(.12f, .28f), new Vector2(.88f, .58f), TextAnchor.MiddleCenter);
            Status.resizeTextForBestFit = true;
            Status.resizeTextMinSize = 18;
            Status.resizeTextMaxSize = 26;
        }

        public void ShowStatus(string status)
        {
            if (Root == null) throw new InvalidOperationException("The startup view has not been built.");
            Status.text = status;
            Root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }
    }
}
