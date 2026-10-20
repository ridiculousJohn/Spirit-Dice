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

        public void Build(SpiritUiTheme theme)
        {
            if (Root == null || Status == null)
                throw new InvalidOperationException("The startup view requires authored UI references.");
        }
        public void ShowStatus(string status)
        {
            if (Root == null) throw new InvalidOperationException("The startup view has not been built.");
            Status.text = status;
            Root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }
    }
}
