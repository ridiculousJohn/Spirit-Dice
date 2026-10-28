using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class InteriorPairMatchesEdgeConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new InteriorPairMatchesEdgeCondition();
    }
}
