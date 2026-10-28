using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class UniqueCountLessThanRerollsSpentConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new UniqueCountLessThanRerollsSpentCondition();
    }
}
