using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SelectedMatchesHandsRemainingConditionConfig : ModifierConditionConfig
    {
        public override IModifierCondition Build() => new SelectedMatchesHandsRemainingCondition();
    }
}
