using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class BoundFaceCountAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 3;

        public BoundFaceCountAtLeastConditionConfig() { }

        public BoundFaceCountAtLeastConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new BoundFaceCountAtLeastCondition(count);
    }
}
