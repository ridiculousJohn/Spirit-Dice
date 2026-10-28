using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class HighDieOrStraightConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new HighDieOrStraightCondition();
    }
}
