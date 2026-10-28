using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class HighDieConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new HighDieCondition();
    }
}
