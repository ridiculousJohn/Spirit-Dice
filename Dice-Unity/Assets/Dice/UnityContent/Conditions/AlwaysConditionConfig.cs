using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class AlwaysConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new AlwaysCondition();
    }
}
