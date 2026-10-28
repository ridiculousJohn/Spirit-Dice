using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class RangeGreaterThanConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int range = 3;

        public RangeGreaterThanConditionConfig() { }

        public RangeGreaterThanConditionConfig(int range) => this.range = range;

        public override IReadOnlyDictionary<string, int> TextValues => Text(range);
        public override IModifierCondition Build() => new RangeGreaterThanCondition(range);
    }
}
