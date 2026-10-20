using UnityEngine;

namespace Dice.Presentation
{
    /** Fits an authored portrait to the device safe area without changing its internal layout. */
    public sealed class SpiritPortraitSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform portrait;
        [SerializeField] private Vector2 referenceSize = new Vector2(540, 960);
        private Rect previousSafeArea;
        private Vector2 previousScreenSize;

        private void OnEnable() => Fit();

        private void Update()
        {
            if (previousSafeArea != Screen.safeArea || previousScreenSize != new Vector2(Screen.width, Screen.height)) Fit();
        }

        private void Fit()
        {
            if (safeArea == null || portrait == null || Screen.width <= 0 || Screen.height <= 0) return;
            previousSafeArea = Screen.safeArea;
            previousScreenSize = new Vector2(Screen.width, Screen.height);
            safeArea.anchorMin = new Vector2(previousSafeArea.xMin / Screen.width, previousSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(previousSafeArea.xMax / Screen.width, previousSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            float scale = Screen.height / referenceSize.y;
            float width = Mathf.Min(referenceSize.x, previousSafeArea.width / scale);
            portrait.sizeDelta = new Vector2(width, portrait.sizeDelta.y);
        }
    }
}
