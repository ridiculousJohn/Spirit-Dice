using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class AdjacentDifferenceAtMostConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int difference = 1;

        public AdjacentDifferenceAtMostConditionConfig() { }

        public AdjacentDifferenceAtMostConditionConfig(int difference) => this.difference = difference;

        public override IReadOnlyDictionary<string, int> TextValues => Text(difference);
        public override IModifierCondition Build() => new AdjacentDifferenceAtMostCondition(difference);
    }
}
