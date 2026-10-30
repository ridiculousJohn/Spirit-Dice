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
        /** The condition's wording; each {name} shows the text value of that name of the referenced modifier's condition. */
        [SerializeField] private string conditionTemplate;

        public ModifierAsset Modifier => modifier;
        public string DisplayName => displayName;
        public string ConditionTemplate => conditionTemplate;
    }
}
