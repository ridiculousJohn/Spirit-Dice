using System;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Compact centered inspection of one charm instance; any tap on the backdrop dismisses it. */
    public sealed class SpiritCharmPopup : MonoBehaviour
    {
        [SerializeField] private Button dismissButton;
        [SerializeField] private RectTransform statusRow;

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public RectTransform Panel { get; private set; }
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Image Icon { get; private set; }
        [field: SerializeField] public SpiritModifierSigilGraphic Sigil { get; private set; }
        [field: SerializeField] public SpiritModifierCategoryBadge Category { get; private set; }
        [field: SerializeField] public Text Effect { get; private set; }
        [field: SerializeField] public Text Description { get; private set; }
        [field: SerializeField] public Text Status { get; private set; }
        [field: SerializeField] public SpiritCharmStatusBadge Badge { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        public long InstanceId { get; private set; } = -1;
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        /** The overlay covers the parent canvas; the panel width follows the portrait. */
        public void Build(Transform parent, RectTransform portraitRect)
        {
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(Close);
            dismissButton.onClick.RemoveAllListeners();
            dismissButton.onClick.AddListener(Close);
        }

        public void Show(long instanceId, ModifierPresentationCatalog charms, ModifierDefinition definition,
            SpiritCharmStatusPresentation status, EncounterPhase phase)
        {
            InstanceId = instanceId;
            SpiritCharmVisuals.SetIcon(Icon, Sigil, Category, charms, definition);
            Title.text = charms.GetName(definition);
            Effect.text = charms.GetEffectText(definition);
            Effect.color = charms.GetEffectColor(definition);
            var theme = SpiritUi.GetTheme(transform);
            Description.text = SpiritRewardCard.GetModifierDescription(charms, definition, theme.amber,
                theme.GetModifierTriggerColor(definition.Phase));
            Badge.Render(status);
            Status.text = status.GetDetailText(definition.Phase, phase);
            Status.color = Badge.Icon.color;
            statusRow.gameObject.SetActive(!string.IsNullOrEmpty(Status.text));
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Layout();
        }

        public void Close()
        {
            if (Root != null) Root.gameObject.SetActive(false);
            InstanceId = -1;
        }

        public void Layout() => LayoutRebuilder.MarkLayoutForRebuild(Panel);
    }
}
