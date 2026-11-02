using UnityEngine;

namespace Dice.Presentation
{
    /** Fits an authored portrait to the device safe area without changing its internal layout. */
    public sealed class SpiritPortraitSafeArea : MonoBehaviour
    {
        /** The portrait layout's authored size. */
        public static readonly Vector2 DefaultReferenceSize = new Vector2(540, 960);
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform portrait;
        [Tooltip("The portrait layout's authored width; the portrait never grows wider.")]
        [SerializeField] private float referenceWidth = DefaultReferenceSize.x;

        private void OnEnable() => Fit();

        // The canvas resizes its root object, which carries this fit, whenever the screen changes size or orientation.
        private void OnRectTransformDimensionsChange() => Fit();

        private void Fit() => FitToScreen(safeArea, portrait, referenceWidth);

        /**
         * Anchors the safe area, a stretched child of the canvas root, to the device's safe area and narrows the portrait
         * to the safe area's width, never wider than the reference width. The canvas has sized the root before this runs,
         * so the safe area's rect is current even in a frame where the Screen size still reports the previous size; the
         * safe area's anchors are fractions of the Screen size, read together with the Screen safe area.
         */
        public static void FitToScreen(RectTransform safeArea, RectTransform portrait, float referenceWidth)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            portrait.sizeDelta = new Vector2(Mathf.Min(referenceWidth, safeArea.rect.width), portrait.sizeDelta.y);
        }
    }
}
