using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class ContainsRerollsRemainingConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new ContainsRerollsRemainingCondition();
    }
}
