using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class InteriorEqualsNeighborSumConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new InteriorEqualsNeighborSumCondition();
    }
}
