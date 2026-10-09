using UnityEngine;

namespace Dice.Presentation
{
    /** The host's wording and artwork remain separate from the encounter's mechanical rule. */
    [CreateAssetMenu(menuName = "Dice/Encounter Rule Presentation")]
    public sealed class EncounterRulePresentationAsset : ScriptableObject
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string displayName;
        [SerializeField] private string availableText;
        [SerializeField] private string usedText;
        [SerializeField] private Sprite icon;

        public string RuleId => ruleId;
        public string DisplayName => displayName;
        public string AvailableText => availableText;
        public string UsedText => usedText;
        public Sprite Icon => icon;
    }
}
