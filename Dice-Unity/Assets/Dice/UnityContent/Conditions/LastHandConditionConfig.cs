using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class LastHandConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new LastHandCondition();
    }
}
