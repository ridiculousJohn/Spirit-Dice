using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SecondFourthProductAtLeastSumConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new SecondFourthProductAtLeastSumCondition();
    }
}
