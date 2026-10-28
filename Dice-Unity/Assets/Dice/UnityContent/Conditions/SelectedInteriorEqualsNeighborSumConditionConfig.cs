using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SelectedInteriorEqualsNeighborSumConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new SelectedInteriorEqualsNeighborSumCondition();
    }
}
