using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class AdjacentDifferentConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new AdjacentDifferentCondition();
    }
}
