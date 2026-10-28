using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SameParityConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new SameParityCondition();
    }
}
