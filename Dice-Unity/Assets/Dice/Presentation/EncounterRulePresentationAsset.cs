using UnityEngine;
using Dice.Engine;

namespace Dice.Presentation
{
    /** The host's wording and artwork remain separate from the encounter's mechanical rule. */
    [CreateAssetMenu(menuName = "Dice/Encounter Rule Presentation")]
    public sealed class EncounterRulePresentationAsset : ScriptableObject
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string availableText;
        [SerializeField] private string usedText;
        [SerializeField] private Sprite icon;
        [SerializeField] private ModifierPhase activationPhase = ModifierPhase.PlayerReroll;

        public string RuleId => ruleId;
        public string DisplayName => displayName;
        public string Description => string.IsNullOrWhiteSpace(description) ? availableText : description;
        public string AvailableText => availableText;
        public string UsedText => usedText;
        public Sprite Icon => icon;
        public ModifierPhase ActivationPhase => activationPhase;
    }
}
