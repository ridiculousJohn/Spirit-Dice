using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class RankEqualsBeforeRerollConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new RankEqualsBeforeRerollCondition();
    }
}
