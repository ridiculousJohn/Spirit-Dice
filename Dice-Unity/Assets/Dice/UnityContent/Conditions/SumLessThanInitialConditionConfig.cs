using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SumLessThanInitialConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new SumLessThanInitialCondition();
    }
}
