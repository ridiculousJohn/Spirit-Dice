using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class PositiveRerollsDivisibleByConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int divisor = 3;

        public PositiveRerollsDivisibleByConditionConfig() { }

        public PositiveRerollsDivisibleByConditionConfig(int divisor) => this.divisor = divisor;

        public override IReadOnlyDictionary<string, int> TextValues => Text(divisor);
        public override IModifierCondition Build() => new PositiveRerollsDivisibleByCondition(divisor);
    }
}
