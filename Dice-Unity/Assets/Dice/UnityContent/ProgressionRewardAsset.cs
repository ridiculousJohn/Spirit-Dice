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
            if (grantedModifiers == null) throw new InvalidOperationException("Granted charms are missing.");
            var ids = new string[grantedModifiers.Length];
            for (int i = 0; i < ids.Length; ++i)
                ids[i] = grantedModifiers[i] != null ? grantedModifiers[i].Id
                    : throw new InvalidOperationException("A granted charm reference is missing: " + name);
            var definition = new ProgressionRewardDefinition(discoveredFamilyIds, ids);
            definition.Validate(catalog);
            return definition;
        }
    }
}
