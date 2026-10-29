using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Modifier")]
    public sealed class ModifierAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private ModifierFamilyAsset family;
        [SerializeField] private ModifierPhase phase;
        [SerializeReference, SelectImplementation] private ModifierConditionConfig condition;
        [SerializeReference, SelectImplementation] private ModifierEffectConfig[] effects = Array.Empty<ModifierEffectConfig>();
        [SerializeField] private ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand;
        [SerializeField] private bool rewardEligible = true;
        [SerializeField, Min(1)] private int powerTier = 1;
        [SerializeField] private ModifierAccess access;

        public string Id => id;
        public ModifierFamilyAsset Family => family;
        /** The authored condition; its text values fill the charm's condition wording. */
        public ModifierConditionConfig Condition => condition;

        /** The charm's definition in the given family, the one built from Family that the content's charms of the family share. */
        public ModifierDefinition Build(ModifierFamilyDefinition familyDefinition)
        {
            if (condition == null) throw new InvalidOperationException("The charm " + id + " has no condition.");
            if (effects == null) throw new InvalidOperationException("The charm " + id + " has no effect list.");
            var effectDefinitions = new ModifierEffect[effects.Length];
            for (int i = 0; i < effects.Length; ++i)
            {
                var source = effects[i] ?? throw new InvalidOperationException("The charm " + id + " has a missing effect.");
                effectDefinitions[i] = source.Build();
            }
            return new ModifierDefinition(id, familyDefinition, phase, condition.Build(),
                effectDefinitions, activationLimit, rewardEligible,
                new ModifierAvailabilityDefinition(powerTier, access));
        }
    }
}
