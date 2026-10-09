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

        public string Id => id;
        public string FamilyId => familyId;

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
                effectDefinitions, activationLimit, rewardEligible);
        }

        [Serializable]
        private sealed class ModifierEffectData
        {
            public ModifierEffectKind kind;
            public long amount;
        }
    }
}
