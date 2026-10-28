using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Progression Reward")]
    public sealed class ProgressionRewardAsset : ScriptableObject
    {
        [SerializeField] private ModifierFamilyAsset[] discoveredFamilies = Array.Empty<ModifierFamilyAsset>();
        [SerializeField] private ModifierAsset[] grantedModifiers = Array.Empty<ModifierAsset>();

        public ProgressionRewardDefinition Build(MechanicalCatalog catalog)
        {
            var families = discoveredFamilies ?? throw new InvalidOperationException("Discovered families are missing.");
            var familyIds = new ModifierFamilyDefinition.Id[families.Length];
            for (int i = 0; i < familyIds.Length; ++i)
                familyIds[i] = families[i] != null ? families[i].Identity
                    : throw new InvalidOperationException("A discovered family reference is missing: " + name);
            var ids = new string[GrantedModifiers.Length];
            for (int i = 0; i < ids.Length; ++i) ids[i] = GrantedModifier(i).Id;
            var definition = new ProgressionRewardDefinition(familyIds, ids);
            definition.Validate(catalog);
            return definition;
        }

        /** Whether the reward grants the charm, read from its charm references. */
        public bool GrantsModifier(string modifierId)
        {
            for (int i = 0; i < GrantedModifiers.Length; ++i)
                if (GrantedModifier(i).Id == modifierId) return true;
            return false;
        }

        private ModifierAsset[] GrantedModifiers
            => grantedModifiers ?? throw new InvalidOperationException("Granted charms are missing.");

        private ModifierAsset GrantedModifier(int index)
            => grantedModifiers[index] != null ? grantedModifiers[index]
                : throw new InvalidOperationException("A granted charm reference is missing: " + name);
    }
}
