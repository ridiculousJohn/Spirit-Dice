using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    public enum ModifierArtworkKind { None, WelcomingPair, SecondChance, LastLantern, QuietGrove, OneSpark, LoneWatcher, TwinKnots }

    /** One theme's authored presentation for a mechanical modifier asset. */
    [CreateAssetMenu(menuName = "Dice/Modifier Presentation")]
    public sealed class ModifierPresentationAsset : ScriptableObject
    {
        [SerializeField] private ModifierAsset modifier;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        /** The condition's wording; {parameter} and {secondaryParameter} show the referenced modifier's parameters. */
        [SerializeField] private string conditionTemplate;
        [SerializeField] private ModifierArtworkKind artwork;
        [SerializeField] private ModifierCategoryPresentationAsset category;

        public ModifierAsset Modifier => modifier;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string ConditionTemplate => conditionTemplate;
        public ModifierArtworkKind Artwork => artwork;
        public ModifierCategoryPresentationAsset Category => category;
    }
}
