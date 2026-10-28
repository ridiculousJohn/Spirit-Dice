using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class InteriorSumDivisibleByConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int divisor = 3;

        public InteriorSumDivisibleByConditionConfig() { }

        public InteriorSumDivisibleByConditionConfig(int divisor) => this.divisor = divisor;

        public override IReadOnlyDictionary<string, int> TextValues => Text(divisor);
        public override IModifierCondition Build() => new InteriorSumDivisibleByCondition(divisor);
    }
}
