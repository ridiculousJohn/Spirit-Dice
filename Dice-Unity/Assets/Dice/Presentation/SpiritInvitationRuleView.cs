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
        private readonly Dictionary<Graphic, Graphic> sigils = new Dictionary<Graphic, Graphic>();

        public Text Title => title;
        public Text Description => description;
        public RectTransform IconRoot => iconRoot;

        public void Render(string name, string effect, Sprite sprite, Graphic sigilPrefab)
        {
            title.text = name;
            description.text = effect;
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
            foreach (var instance in sigils.Values) instance.gameObject.SetActive(false);
            if (sprite == null && sigilPrefab != null)
            {
                if (!sigils.TryGetValue(sigilPrefab, out var sigil))
                {
                    sigil = Instantiate(sigilPrefab, iconRoot, false);
                    sigils.Add(sigilPrefab, sigil);
                }
                sigil.gameObject.SetActive(true);
            }
            iconRoot.gameObject.SetActive(sprite != null || sigilPrefab != null);
        }
    }
}
