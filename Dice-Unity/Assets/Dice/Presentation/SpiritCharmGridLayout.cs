using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Fits the authored column count while keeping every card's complete wording visible. */
    [ExecuteAlways, RequireComponent(typeof(GridLayoutGroup))]
    public sealed class SpiritCharmGridLayout : MonoBehaviour, ILayoutSelfController
    {
        private float measuredWidth = -1;
        private bool arranging;

        internal void RefreshLayout()
        {
            measuredWidth = -1;
            Arrange();
        }

        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => Arrange();
        private void OnTransformChildrenChanged() => RefreshLayout();
        private void OnValidate() => RefreshLayout();
        public void SetLayoutHorizontal() => RefreshLayout();
        public void SetLayoutVertical() { }

        private void Arrange()
        {
            if (arranging) return;
            var rect = transform as RectTransform;
            var grid = GetComponent<GridLayoutGroup>();
            if (rect == null || grid == null || rect.rect.width <= 0 || Mathf.Approximately(rect.rect.width, measuredWidth)) return;
            arranging = true;
            measuredWidth = rect.rect.width;
            int columns = Mathf.Max(1, grid.constraintCount);
            float width = (measuredWidth - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
            float height = 0;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var childRect = child as RectTransform;
                if (childRect == null) continue;
                childRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
                height = Mathf.Max(height, LayoutUtility.GetPreferredHeight(childRect));
            }
            grid.cellSize = new Vector2(width, height);
            arranging = false;
        }
    }
}
