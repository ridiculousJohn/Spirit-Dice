using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class EdgeSumEqualsEitherConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int firstSum = 6;
        [SerializeField] private int secondSum = 8;

        public EdgeSumEqualsEitherConditionConfig() { }

        public EdgeSumEqualsEitherConditionConfig(int firstSum, int secondSum)
        {
            this.firstSum = firstSum;
            this.secondSum = secondSum;
        }

        public override IReadOnlyDictionary<string, int> TextValues => Text(firstSum, secondSum);
        public override IModifierCondition Build() => new EdgeSumEqualsEitherCondition(firstSum, secondSum);
    }
}
