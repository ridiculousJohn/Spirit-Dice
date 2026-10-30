using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Keeps the inspection width relative to its portrait while the backdrop covers the canvas. */
    [ExecuteAlways]
    public sealed class SpiritCharmPopupLayout : MonoBehaviour, ILayoutSelfController
    {
        [SerializeField] private RectTransform widthReference;
        [SerializeField, Range(0, 1)] private float widthFraction = .84f;

        private void OnEnable() => FitWidth();
        private void OnRectTransformDimensionsChange() => FitWidth();
        public void SetLayoutHorizontal() => FitWidth();
        public void SetLayoutVertical() { }

        private void FitWidth()
        {
            if (widthReference == null || widthReference.rect.width <= 0) return;
            var panel = (RectTransform)transform;
            float width = widthReference.rect.width * widthFraction;
            if (Mathf.Approximately(panel.rect.width, width)) return;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            LayoutRebuilder.MarkLayoutForRebuild(panel);
        }
    }
}
