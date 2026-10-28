using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class NoDiceRerolledConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new NoDiceRerolledCondition();
    }
}
