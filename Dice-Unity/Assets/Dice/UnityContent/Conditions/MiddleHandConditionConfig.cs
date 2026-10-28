using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class MiddleHandConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new MiddleHandCondition();
    }
}
