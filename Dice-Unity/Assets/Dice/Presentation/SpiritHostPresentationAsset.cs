using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Selects a host renderer through authored presentation data. */
    public abstract class SpiritHostPresentationAsset : ScriptableObject
    {
        public abstract SpiritHostPortrait Create(RectTransform parent);
        public virtual Graphic CreateSigil(string name, RectTransform parent)
        {
            var sigil = SpiritUi.Full(name, parent).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            sigil.color = SpiritUi.Amber;
            sigil.raycastTarget = false;
            return sigil;
        }
    }
}
