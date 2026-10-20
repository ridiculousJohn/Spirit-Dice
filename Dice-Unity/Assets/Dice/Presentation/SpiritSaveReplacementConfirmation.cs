using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Exposes the authored confirmation controls to the containing save slot picker. */
    public sealed class SpiritSaveReplacementConfirmation : MonoBehaviour
    {
        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text Description { get; private set; }
        [field: SerializeField] public Button ConfirmButton { get; private set; }
        [field: SerializeField] public Button CancelButton { get; private set; }
    }
}
