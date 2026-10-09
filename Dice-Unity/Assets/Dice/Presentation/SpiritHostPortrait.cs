using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** A table host presents cosmetic reactions independently of encounter mechanics. */
    public abstract class SpiritHostPortrait : MonoBehaviour
    {
        public abstract float GestureDuration { get; }
        public abstract event Action SpiritGesture;
        public abstract void React();
        public abstract void ResetPose();
        public abstract void SetVisible(bool visible);
    }
}
