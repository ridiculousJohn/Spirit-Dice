using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class OrderedConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new OrderedCondition();
    }
}
