using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class EveryDieRerolledConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new EveryDieRerolledCondition();
    }
}
