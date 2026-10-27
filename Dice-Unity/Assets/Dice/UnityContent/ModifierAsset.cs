using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Modifier")]
    public sealed class ModifierAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string familyId;
        [SerializeField] private ModifierPhase phase;
        [SerializeField] private ModifierConditionKind conditionKind;
        [SerializeField] private int parameter;
        [SerializeField] private int secondaryParameter;
        [SerializeField] private ModifierEffectData[] effects = Array.Empty<ModifierEffectData>();
        [SerializeField] private ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand;
        [SerializeField] private bool rewardEligible = true;
        [SerializeField, Min(1)] private int powerTier = 1;
        [SerializeField] private ModifierAccess access;
        [SerializeField] private ModifierAvailabilityScope availabilityScope;
        [SerializeField] private string availabilityScopeId;

        public string Id => id;
        public string FamilyId => familyId;
        public int Parameter => parameter;
        public int SecondaryParameter => secondaryParameter;

        public ModifierDefinition Build()
        {
            if (effects == null) throw new InvalidOperationException("Modifier effects are missing.");
            var effectDefinitions = new ModifierEffect[effects.Length];
            for (int i = 0; i < effects.Length; ++i)
            {
                var source = effects[i] ?? throw new InvalidOperationException("A modifier effect is missing.");
                effectDefinitions[i] = new ModifierEffect(source.kind, source.amount);
            }
            return new ModifierDefinition(id, familyId, phase,
                ModifierConditions.Create(conditionKind, parameter, secondaryParameter),
                effectDefinitions, activationLimit, rewardEligible,
                new ModifierAvailabilityDefinition(powerTier, availabilityScope,
                    string.IsNullOrEmpty(availabilityScopeId) ? null : availabilityScopeId, access));
        }

        [Serializable]
        private sealed class ModifierEffectData
        {
            public ModifierEffectKind kind;
            public long amount;
        }
    }
}
