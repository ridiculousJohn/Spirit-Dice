using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class FaceEqualsPositionConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new FaceEqualsPositionCondition();
    }
}
