using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class NeverConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new NeverCondition();
    }
}
