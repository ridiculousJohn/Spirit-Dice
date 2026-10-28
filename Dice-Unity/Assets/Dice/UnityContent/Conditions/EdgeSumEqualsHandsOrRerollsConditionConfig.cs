using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class EdgeSumEqualsHandsOrRerollsConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new EdgeSumEqualsHandsOrRerollsCondition();
    }
}
