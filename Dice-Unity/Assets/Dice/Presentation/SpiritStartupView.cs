using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents application loading and failures independently of gameplay. */
    public sealed class SpiritStartupView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Text Status { get; private set; }

        public void Build()
        {
            if (Root == null || Status == null)
                throw new InvalidOperationException("The startup view requires authored UI references.");
            SpiritUi.RequireWording(transform);
        }

        public void ShowStatus(SpiritStatus status)
        {
            if (Root == null) throw new InvalidOperationException("The startup view has not been built.");
            Status.text = SpiritStatusText.Describe(status, SpiritUi.GetWording(transform).status);
            Root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }
    }
}
