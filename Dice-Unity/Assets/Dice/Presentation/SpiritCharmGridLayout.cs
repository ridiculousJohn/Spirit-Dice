using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** The inventory retains two columns while allowing every card's complete wording to fit. */
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class SpiritCharmGridLayout : MonoBehaviour
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

        private void Arrange()
        {
            if (arranging) return;
            var rect = transform as RectTransform;
            var grid = GetComponent<GridLayoutGroup>();
            if (rect == null || grid == null || rect.rect.width <= 0 || Mathf.Approximately(rect.rect.width, measuredWidth)) return;
            arranging = true;
            measuredWidth = rect.rect.width;
            float width = (measuredWidth - grid.padding.horizontal - grid.spacing.x) * .5f;
            float height = 156;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var card = child.GetComponent<SpiritCharmCardLayout>();
                if (card != null) height = Mathf.Max(height, card.Arrange(width));
            }
            grid.cellSize = new Vector2(width, height);
            arranging = false;
        }
    }
}
