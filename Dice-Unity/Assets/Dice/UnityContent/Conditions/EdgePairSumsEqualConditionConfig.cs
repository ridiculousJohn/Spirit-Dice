using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class EdgePairSumsEqualConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new EdgePairSumsEqualCondition();
    }
}
