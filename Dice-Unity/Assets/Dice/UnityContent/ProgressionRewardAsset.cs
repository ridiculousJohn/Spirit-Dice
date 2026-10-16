using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Progression Reward")]
    public sealed class ProgressionRewardAsset : ScriptableObject
    {
        [SerializeField] private string[] discoveredFamilyIds = Array.Empty<string>();
        [SerializeField] private string[] grantedModifierIds = Array.Empty<string>();

        public ProgressionRewardDefinition Build(MechanicalCatalog catalog)
        {
            var definition = new ProgressionRewardDefinition(discoveredFamilyIds, grantedModifierIds);
            definition.Validate(catalog);
            return definition;
        }
    }
}
