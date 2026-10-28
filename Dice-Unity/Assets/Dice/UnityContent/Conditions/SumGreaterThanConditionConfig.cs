using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SumGreaterThanConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int sum = 22;

        public SumGreaterThanConditionConfig() { }

        public SumGreaterThanConditionConfig(int sum) => this.sum = sum;

        public override IReadOnlyDictionary<string, int> TextValues => Text(sum);
        public override IModifierCondition Build() => new SumGreaterThanCondition(sum);
    }
}
