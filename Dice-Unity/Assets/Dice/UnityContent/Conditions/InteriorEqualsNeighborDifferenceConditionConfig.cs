using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class InteriorEqualsNeighborDifferenceConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new InteriorEqualsNeighborDifferenceCondition();
    }
}
