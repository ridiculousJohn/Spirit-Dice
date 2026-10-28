using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class AdjacentDifferenceAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int difference = 2;

        public AdjacentDifferenceAtLeastConditionConfig() { }

        public AdjacentDifferenceAtLeastConditionConfig(int difference) => this.difference = difference;

        public override IReadOnlyDictionary<string, int> TextValues => Text(difference);
        public override IModifierCondition Build() => new AdjacentDifferenceAtLeastCondition(difference);
    }
}
