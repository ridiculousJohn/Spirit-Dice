using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dice.Presentation
{
    /**
     * On the parent of the dice's touch targets: a press on a die's target keeps its tap for the die, while a drag that
     * starts there goes to what the target covers, such as an item's handle over its emblem on the die's bay. When the
     * press begins, the event system offers this component the drag (it is the targets' nearest drag handler); it hands the
     * drag on to the drag handler of the topmost hit beneath the targets, or to none, so a press with nothing beneath taps
     * the die as before. Once the drag starts the event system cancels the die's tap, so dragging never changes the dice
     * selection.
     */
    [DisallowMultipleComponent]
    public sealed class SpiritDragPassThrough : MonoBehaviour, IInitializePotentialDragHandler, IDragHandler
    {
        private readonly List<RaycastResult> hits = new List<RaycastResult>();

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            GameObject beneath = DragHandlerBeneath(eventData);
            eventData.pointerDrag = beneath;
            if (beneath != null) ExecuteEvents.Execute(beneath, eventData, ExecuteEvents.initializePotentialDrag);
        }

        // The press hands every drag on when it begins, so a drag never reaches this component.
        public void OnDrag(PointerEventData eventData) { }

        /** The drag handler of the topmost hit that is not one of the touch targets, or null when it has none. */
        private GameObject DragHandlerBeneath(PointerEventData eventData)
        {
            if (EventSystem.current == null) return null;
            EventSystem.current.RaycastAll(eventData, hits);
            foreach (RaycastResult hit in hits)
            {
                if (hit.gameObject == null || hit.gameObject.transform.IsChildOf(transform)) continue;
                GameObject handler = ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject);
                return handler != gameObject ? handler : null;
            }
            return null;
        }
    }
}
