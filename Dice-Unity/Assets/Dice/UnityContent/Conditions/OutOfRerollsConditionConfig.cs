using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class OutOfRerollsConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new OutOfRerollsCondition();
    }
}
