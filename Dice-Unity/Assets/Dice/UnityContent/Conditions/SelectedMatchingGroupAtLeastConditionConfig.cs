using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class SelectedMatchingGroupAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 3;

        public SelectedMatchingGroupAtLeastConditionConfig() { }

        public SelectedMatchingGroupAtLeastConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new SelectedMatchingGroupAtLeastCondition(count);
    }
}
