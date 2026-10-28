using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class MiddleEqualsOddCountConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new MiddleEqualsOddCountCondition();
    }
}
