using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Carries the explicitly assigned style to controls in one UI hierarchy. */
    [DisallowMultipleComponent]
    public sealed class SpiritUiContext : MonoBehaviour
    {
        [SerializeField] private SpiritUiTheme theme;
        public SpiritUiTheme Theme => theme;

        public void Configure(SpiritUiTheme selectedTheme)
        {
            if (selectedTheme == null) throw new ArgumentNullException(nameof(selectedTheme));
            theme = selectedTheme;
        }
    }
}
