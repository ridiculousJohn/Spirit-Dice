using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Binds an invitation rule while retaining authored icon and text layout. */
    public sealed class SpiritInvitationRuleView : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text description;
        [SerializeField] private RectTransform iconRoot;
        [SerializeField] private Image icon;
        private readonly Dictionary<SpiritHostPresentationAsset, Graphic> sigils = new Dictionary<SpiritHostPresentationAsset, Graphic>();

        public Text Title => title;
        public Text Description => description;
        public RectTransform IconRoot => iconRoot;

        /** Shows the rule's name, effect and icon; a rule without an icon shows the host's sigil, or no icon without a host. */
        public void Render(string name, string effect, Sprite sprite, SpiritHostPresentationAsset host)
        {
            title.text = name;
            description.text = effect;
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
            foreach (var instance in sigils.Values) instance.gameObject.SetActive(false);
            if (sprite == null && host != null)
            {
                if (!sigils.TryGetValue(host, out var sigil))
                {
                    sigil = host.CreateSigil("HostSigil", iconRoot);
                    sigils.Add(host, sigil);
                }
                sigil.gameObject.SetActive(true);
            }
            iconRoot.gameObject.SetActive(sprite != null || host != null);
        }
    }
}
