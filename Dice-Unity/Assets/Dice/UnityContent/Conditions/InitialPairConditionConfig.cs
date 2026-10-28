using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class InitialPairConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new InitialPairCondition();
    }
}
