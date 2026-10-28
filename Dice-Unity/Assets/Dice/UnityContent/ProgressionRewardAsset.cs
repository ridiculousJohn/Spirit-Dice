using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Progression Reward")]
    public sealed class ProgressionRewardAsset : ScriptableObject
    {
        [SerializeField] private string[] discoveredFamilyIds = Array.Empty<string>();
        [SerializeField] private ModifierAsset[] grantedModifiers = Array.Empty<ModifierAsset>();

        public ProgressionRewardDefinition Build(MechanicalCatalog catalog)
        {
            var ids = new string[GrantedModifiers.Length];
            for (int i = 0; i < ids.Length; ++i) ids[i] = GrantedModifier(i).Id;
            var definition = new ProgressionRewardDefinition(discoveredFamilyIds, ids);
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
