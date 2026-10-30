using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * The charms a reward slot draws from, and the charm it offers when none of them is eligible. Encounters' reward
     * layouts reference it directly; it has no id, and its name names it only in diagnostics.
     */
    [CreateAssetMenu(menuName = "Dice/Reward Pool")]
    public sealed class RewardPoolAsset : ScriptableObject
    {
        [SerializeField] private ModifierAsset[] modifiers = Array.Empty<ModifierAsset>();
        [SerializeField] private ModifierAsset fallbackModifier;

        /** The pool's definition; each charm it references is passed to use, so the catalog collects its charms. */
        internal ModifierRewardPoolDefinition Build(Action<ModifierAsset> use)
        {
            if (fallbackModifier != null) use(fallbackModifier);
            return new ModifierRewardPoolDefinition(name, MechanicalCatalogAsset.Ids(modifiers, "the reward pool " + name, use),
                fallbackModifier == null ? (ModifierDefinition.Id?)null : fallbackModifier.Identity);
        }
    }
}
