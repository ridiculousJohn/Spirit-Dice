using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SelectedSumEqualsEitherConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int firstSum = 8;
        [SerializeField] private int secondSum = 10;

        public SelectedSumEqualsEitherConditionConfig() { }

        public SelectedSumEqualsEitherConditionConfig(int firstSum, int secondSum)
        {
            this.firstSum = firstSum;
            this.secondSum = secondSum;
        }

        public override IReadOnlyDictionary<string, int> TextValues => Text(firstSum, secondSum);
        public override IModifierCondition Build() => new SelectedSumEqualsEitherCondition(firstSum, secondSum);
    }
}
