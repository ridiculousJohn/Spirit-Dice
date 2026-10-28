using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class RankBelowPreviousConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new RankBelowPreviousCondition();
    }
}
