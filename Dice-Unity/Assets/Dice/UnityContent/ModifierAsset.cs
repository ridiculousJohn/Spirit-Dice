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
        [SerializeField] private ModifierAvailabilityScope availabilityScope;
        [SerializeField, Tooltip("Spirit scope only: the spirit whose encounters offer the charm.")]
        private string availabilityScopeId;
        [SerializeField, Tooltip("Encounter scope only: the encounter that offers the charm.")]
        private EncounterAsset availabilityEncounter;

        public string Id => id;
        public ModifierFamilyAsset Family => family;
        /** The authored condition; its text values fill the charm's condition wording. */
        public ModifierConditionConfig Condition => condition;

        /**
         * What is wrong with the authored availability, or null. A Global scope names neither a spirit nor an encounter,
         * a Spirit scope names only its spirit id and an Encounter scope only its encounter.
         */
        public string AvailabilityProblem
        {
            get
            {
                bool spirit = !string.IsNullOrEmpty(availabilityScopeId);
                bool encounter = availabilityEncounter != null;
                switch (availabilityScope)
                {
                    case ModifierAvailabilityScope.Global:
                        return spirit || encounter ? "A Global scope names neither a spirit id nor an encounter." : null;
                    case ModifierAvailabilityScope.Spirit:
                        return !spirit || encounter ? "A Spirit scope names a spirit id and no encounter." : null;
                    case ModifierAvailabilityScope.Encounter:
                        return !encounter || spirit ? "An Encounter scope names an encounter and no spirit id." : null;
                    default:
                        return "The availability scope " + availabilityScope + " is unknown.";
                }
            }
        }

        /** The charm's definition in the given family, the one built from Family that the content's charms of the family share. */
        public ModifierDefinition Build(ModifierFamilyDefinition familyDefinition)
        {
            if (condition == null) throw new InvalidOperationException("The charm " + id + " has no condition.");
            if (effects == null) throw new InvalidOperationException("The charm " + id + " has no effect list.");
            string availabilityProblem = AvailabilityProblem;
            if (availabilityProblem != null) throw new InvalidOperationException("The charm " + id + " has an invalid availability: " + availabilityProblem);
            var effectDefinitions = new ModifierEffect[effects.Length];
            for (int i = 0; i < effects.Length; ++i)
            {
                var source = effects[i] ?? throw new InvalidOperationException("The charm " + id + " has a missing effect.");
                effectDefinitions[i] = source.Build();
            }
            return new ModifierDefinition(id, familyDefinition, phase, condition.Build(),
                effectDefinitions, activationLimit, rewardEligible,
                new ModifierAvailabilityDefinition(powerTier, availabilityScope,
                    string.IsNullOrEmpty(availabilityScopeId) ? null : availabilityScopeId,
                    availabilityEncounter != null ? availabilityEncounter.Identity : (EncounterDefinition.Id?)null, access));
        }
    }
}
