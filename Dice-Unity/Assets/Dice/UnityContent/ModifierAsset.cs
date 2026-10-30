using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * A charm: its family, trigger and effects. Its identity is its asset GUID, under which saves and the player profile
     * store it; its asset name names it in load errors.
     */
    [CreateAssetMenu(menuName = "Dice/Modifier")]
    public sealed class ModifierAsset : ContentAsset
    {
        [SerializeField] private ModifierFamilyAsset family;
        [SerializeField] private ModifierPhase phase;
        [SerializeReference, SelectImplementation] private ModifierConditionConfig condition;
        [SerializeReference, SelectImplementation] private ModifierEffectConfig[] effects = Array.Empty<ModifierEffectConfig>();
        [SerializeField] private ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand;
        [SerializeField] private bool rewardEligible = true;
        [SerializeField, Min(1)] private int powerTier = 1;
        [SerializeField] private ModifierAccess access;

        public ModifierDefinition.Id Identity => new ModifierDefinition.Id(AssetGuid);
        public ModifierFamilyAsset Family => family;
        /** The authored condition; its text values fill the charm's condition wording. */
        public ModifierConditionConfig Condition => condition;

        /** The charm's definition in the given family, the one built from Family that the content's charms of the family share. */
        public ModifierDefinition Build(ModifierFamilyDefinition familyDefinition)
        {
            if (condition == null) throw new InvalidOperationException("The charm " + name + " has no condition.");
            if (effects == null) throw new InvalidOperationException("The charm " + name + " has no effect list.");
            var effectDefinitions = new ModifierEffect[effects.Length];
            try
            {
                for (int i = 0; i < effects.Length; ++i)
                {
                    var source = effects[i] ?? throw new InvalidOperationException("The charm " + name + " has a missing effect.");
                    effectDefinitions[i] = source.Build();
                }
                return new ModifierDefinition(Identity, familyDefinition, phase, condition.Build(),
                    effectDefinitions, activationLimit, rewardEligible,
                    new ModifierAvailabilityDefinition(powerTier, access));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("The charm " + name + " is invalid: " + exception.Message, exception);
            }
        }
    }
}
