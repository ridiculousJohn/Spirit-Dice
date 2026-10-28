using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class RerollsExceedHandsRemainingConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new RerollsExceedHandsRemainingCondition();
    }
}
