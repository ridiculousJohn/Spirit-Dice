using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class HandsRemainingEvenConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new HandsRemainingEvenCondition();
    }
}
