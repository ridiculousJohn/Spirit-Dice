using UnityEngine;

namespace Dice.Presentation
{
    /** Releases objects that presentation code created for itself. */
    public static class OwnedObject
    {
        /** Destroys at the end of the frame in play mode; edit mode, which has no frame loop, destroys at once. */
        public static void Release(Object owned)
        {
            if (Application.isPlaying) Object.Destroy(owned);
            else Object.DestroyImmediate(owned);
        }
    }
}
