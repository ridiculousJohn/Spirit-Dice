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
        [SerializeReference, SelectImplementation] private ModifierConditionConfig condition;
        [SerializeReference, SelectImplementation] private ModifierEffectConfig[] effects = Array.Empty<ModifierEffectConfig>();
        [SerializeField] private ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand;
        [SerializeField] private bool rewardEligible = true;
        [SerializeField, Min(1)] private int powerTier = 1;
        [SerializeField] private ModifierAccess access;
        [SerializeField] private ModifierAvailabilityScope availabilityScope;
        [SerializeField] private string availabilityScopeId;

        public string Id => id;
        public string FamilyId => familyId;
        /** The authored condition; its text values fill the charm's condition wording. */
        public ModifierConditionConfig Condition => condition;

        public ModifierDefinition Build()
        {
            if (condition == null) throw new InvalidOperationException("The charm " + id + " has no condition.");
            if (effects == null) throw new InvalidOperationException("The charm " + id + " has no effect list.");
            var effectDefinitions = new ModifierEffect[effects.Length];
            for (int i = 0; i < effects.Length; ++i)
            {
                var source = effects[i] ?? throw new InvalidOperationException("The charm " + id + " has a missing effect.");
                effectDefinitions[i] = source.Build();
            }
            return new ModifierDefinition(id, familyId, phase, condition.Build(),
                effectDefinitions, activationLimit, rewardEligible,
                new ModifierAvailabilityDefinition(powerTier, availabilityScope,
                    string.IsNullOrEmpty(availabilityScopeId) ? null : availabilityScopeId, access));
        }
    }
}
