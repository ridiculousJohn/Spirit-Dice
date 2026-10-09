using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    [CreateAssetMenu(menuName = "Dice/Presentation/Fox Host")]
    public sealed class SpiritFoxHostPresentationAsset : SpiritHostPresentationAsset
    {
        [SerializeField] private Texture2D bodyTexture;
        [SerializeField] private Texture2D headTexture;
        [SerializeField] private Texture2D turnedHeadTexture;

        public override SpiritHostPortrait Create(RectTransform parent)
        {
            var host = SpiritUi.Full("HostPortrait", parent);
            var portrait = host.gameObject.AddComponent<SpiritFoxPortrait>();
            portrait.Build(host, bodyTexture, headTexture, turnedHeadTexture);
            return portrait;
        }

        public override Graphic CreateSigil(string name, RectTransform parent)
        {
            var sigil = SpiritUi.Full(name == "HostSigil" ? "FoxSigil" : name, parent)
                .gameObject.AddComponent<SpiritFoxSigilGraphic>();
            sigil.color = SpiritUi.Amber;
            sigil.raycastTarget = false;
            return sigil;
        }
    }
}
