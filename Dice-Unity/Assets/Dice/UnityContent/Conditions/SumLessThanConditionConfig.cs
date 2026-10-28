using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SumLessThanConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int sum = 15;

        public SumLessThanConditionConfig() { }

        public SumLessThanConditionConfig(int sum) => this.sum = sum;

        public override IReadOnlyDictionary<string, int> TextValues => Text(sum);
        public override IModifierCondition Build() => new SumLessThanCondition(sum);
    }
}
