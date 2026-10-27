using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /** One theme's authored presentation for a mechanical modifier asset. */
    [CreateAssetMenu(menuName = "Dice/Modifier Presentation")]
    public sealed class ModifierPresentationAsset : ScriptableObject
    {
        [SerializeField] private ModifierAsset modifier;
        [SerializeField] private string displayName;
        /** The charm's artwork wherever the charm is shown; loading fails without one. */
        [SerializeField] private Sprite icon;
        /** The condition's wording; {parameter} and {secondaryParameter} show the referenced modifier's parameters. */
        [SerializeField] private string conditionTemplate;
        [SerializeField] private ModifierCategoryPresentationAsset category;

        public ModifierAsset Modifier => modifier;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string ConditionTemplate => conditionTemplate;
        public ModifierCategoryPresentationAsset Category => category;
    }
}
