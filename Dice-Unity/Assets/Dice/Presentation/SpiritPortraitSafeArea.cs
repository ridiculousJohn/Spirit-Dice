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
        [SerializeField] private Vector2 referenceSize = DefaultReferenceSize;

        private void OnEnable() => Fit();

        // The canvas resizes its root object, which carries this fit, whenever the screen changes size or orientation.
        private void OnRectTransformDimensionsChange() => Fit();

        private void Fit()
        {
            if (safeArea == null || portrait == null || Screen.width <= 0 || Screen.height <= 0) return;
            FitToScreen(safeArea, portrait, referenceSize);
        }

        /**
         * Anchors the safe area to the device's safe area and narrows the portrait to fit it when scaled to the screen height,
         * never wider than the reference width.
         */
        public static void FitToScreen(RectTransform safeArea, RectTransform portrait, Vector2 referenceSize)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            float scale = Screen.height / referenceSize.y;
            float width = Mathf.Min(referenceSize.x, area.width / scale);
            portrait.sizeDelta = new Vector2(width, portrait.sizeDelta.y);
        }
    }
}
