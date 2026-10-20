using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Authored pause controls bind to their containing encounter screen. */
    public sealed class SpiritPauseMenu : MonoBehaviour
    {
        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text ProfileSummary { get; private set; }
        [field: SerializeField] public Button SoundButton { get; private set; }
        [field: SerializeField] public Button CollectionButton { get; private set; }
        [field: SerializeField] public Button SaveButton { get; private set; }
        [field: SerializeField] public Button LoadButton { get; private set; }
        [field: SerializeField] public Button NewRunButton { get; private set; }
        [field: SerializeField] public Button AbandonButton { get; private set; }
        [field: SerializeField] public Button MainMenuButton { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public RectTransform CreditsBacking { get; private set; }
        [field: SerializeField] public Text Credits { get; private set; }
    }
}
