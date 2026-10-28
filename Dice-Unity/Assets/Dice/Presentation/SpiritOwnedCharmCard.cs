using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Authored inventory card references; the panel binds the current charm instance. */
    public sealed class SpiritOwnedCharmCard : MonoBehaviour
    {
        [field: SerializeField] public Button Button { get; private set; }
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text Body { get; private set; }
        [field: SerializeField] public Text Effect { get; private set; }
        [field: SerializeField] public Image Icon { get; private set; }
        [field: SerializeField] public SpiritModifierCategoryBadge Category { get; private set; }
        [field: SerializeField] public SpiritCharmStatusBadge Badge { get; private set; }
        [field: SerializeField] public Image SelectionOutline { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; } = Color.white;
        [field: SerializeField] public Color SelectedColor { get; private set; } = new Color32(255, 232, 179, 255);
        public long InstanceId { get; set; }
    }
}
