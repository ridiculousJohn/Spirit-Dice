using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SumDivisibleByConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int divisor = 5;

        public SumDivisibleByConditionConfig() { }

        public SumDivisibleByConditionConfig(int divisor) => this.divisor = divisor;

        public override IReadOnlyDictionary<string, int> TextValues => Text(divisor);
        public override IModifierCondition Build() => new SumDivisibleByCondition(divisor);
    }
}
