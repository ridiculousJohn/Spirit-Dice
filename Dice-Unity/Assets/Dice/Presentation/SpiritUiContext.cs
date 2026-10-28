using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Carries the explicitly assigned style and wording to controls in one UI hierarchy. */
    [DisallowMultipleComponent]
    public sealed class SpiritUiContext : MonoBehaviour
    {
        [SerializeField] private SpiritUiTheme theme;
        [SerializeField] private SpiritUiWording wording;
        public SpiritUiTheme Theme => theme;
        public SpiritUiWording Wording => wording;

        public void Configure(SpiritUiTheme selectedTheme, SpiritUiWording selectedWording)
        {
            if (selectedTheme == null) throw new ArgumentNullException(nameof(selectedTheme));
            if (selectedWording == null) throw new ArgumentNullException(nameof(selectedWording));
            theme = selectedTheme;
            wording = selectedWording;
        }
    }
}
