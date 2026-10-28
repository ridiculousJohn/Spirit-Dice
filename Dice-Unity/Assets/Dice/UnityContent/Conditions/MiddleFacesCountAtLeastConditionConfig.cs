using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class MiddleFacesCountAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 4;

        public MiddleFacesCountAtLeastConditionConfig() { }

        public MiddleFacesCountAtLeastConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new MiddleFacesCountAtLeastCondition(count);
    }
}
