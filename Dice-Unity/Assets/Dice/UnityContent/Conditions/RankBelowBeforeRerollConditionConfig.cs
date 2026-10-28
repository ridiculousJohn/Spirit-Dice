using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class RankBelowBeforeRerollConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new RankBelowBeforeRerollCondition();
    }
}
