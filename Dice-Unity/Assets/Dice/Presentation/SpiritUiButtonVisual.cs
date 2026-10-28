using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Keep the button's live label and ornaments legible through Selectable transitions. */
    public sealed class SpiritUiButtonVisual : Button
    {
        private Graphic[] content;
        [SerializeField] private Transform untintedContent;

        protected override void Awake()
        {
            base.Awake();
            CaptureContent();
        }

        public void CaptureContent()
        {
            content = System.Array.FindAll(GetComponentsInChildren<Graphic>(true),
                graphic => untintedContent == null || !graphic.transform.IsChildOf(untintedContent));
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (content == null) return;
            Color tint = state == SelectionState.Disabled ? colors.disabledColor :
                state == SelectionState.Pressed ? colors.pressedColor :
                state == SelectionState.Highlighted || state == SelectionState.Selected ? colors.highlightedColor : Color.white;
            for (int index = 0; index < content.Length; ++index)
            {
                if (content[index] == null || content[index] == targetGraphic) continue;
                content[index].CrossFadeColor(tint, instant ? 0 : colors.fadeDuration, true, true);
            }
        }
    }
}
