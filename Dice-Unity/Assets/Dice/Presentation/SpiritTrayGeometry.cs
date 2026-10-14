using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Surface anchors keep animated content attached to the encounter's authored tray geometry. */
    public sealed class SpiritTrayGeometry : MonoBehaviour
    {
        public Transform[] DiceSlots = new Transform[DiceValues.Count];
        public Transform[] DiceLabelAnchors = new Transform[DiceValues.Count];
        public float DiceScale = 1;
        public Transform ModifierRoot;
        public float ModifierWidth = 5.4f;
        public float ModifierDepth = .62f;
        public float ModifierDieSize = .49f;
        public float ModifierSlotSpacing = .59f;
        public int ModifierVisibleCapacity = 9;

        public void Validate()
        {
            if (DiceSlots == null || DiceSlots.Length != DiceValues.Count ||
                DiceLabelAnchors == null || DiceLabelAnchors.Length != DiceValues.Count)
                throw new InvalidOperationException("Spirit tray geometry requires five dice and label anchors.");
            for (int i = 0; i < DiceValues.Count; ++i)
                if (DiceSlots[i] == null || DiceLabelAnchors[i] == null ||
                    !DiceSlots[i].IsChildOf(transform) || !DiceLabelAnchors[i].IsChildOf(transform))
                    throw new InvalidOperationException("Spirit tray anchors must belong to their geometry prefab.");
            if (ModifierRoot == null || !ModifierRoot.IsChildOf(transform) || DiceScale <= 0 ||
                ModifierWidth <= 0 || ModifierDepth <= 0 || ModifierDieSize <= 0 ||
                ModifierSlotSpacing <= 0 || ModifierVisibleCapacity <= 0 ||
                (ModifierVisibleCapacity - 1) * ModifierSlotSpacing + ModifierDieSize > ModifierWidth)
                throw new InvalidOperationException("Spirit tray bay dimensions must fit the authored content.");
        }
    }
}
