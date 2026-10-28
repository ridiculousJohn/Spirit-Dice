using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class ExtremeEqualsHandsRemainingConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new ExtremeEqualsHandsRemainingCondition();
    }
}
