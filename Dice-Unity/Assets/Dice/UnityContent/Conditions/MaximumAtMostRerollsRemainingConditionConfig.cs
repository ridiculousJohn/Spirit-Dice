using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class MaximumAtMostRerollsRemainingConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new MaximumAtMostRerollsRemainingCondition();
    }
}
