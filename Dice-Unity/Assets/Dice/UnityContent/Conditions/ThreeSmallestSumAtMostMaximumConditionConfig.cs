using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class ThreeSmallestSumAtMostMaximumConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new ThreeSmallestSumAtMostMaximumCondition();
    }
}
