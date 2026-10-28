using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class EdgesMatchInteriorConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new EdgesMatchInteriorCondition();
    }
}
