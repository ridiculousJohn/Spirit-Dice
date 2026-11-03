using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class UncountedDiceCountAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 1;

        public UncountedDiceCountAtLeastConditionConfig() { }

        public UncountedDiceCountAtLeastConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new UncountedDiceCountAtLeastCondition(count);
    }
}
