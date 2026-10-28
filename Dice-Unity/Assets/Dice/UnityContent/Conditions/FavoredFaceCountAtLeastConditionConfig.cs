using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class FavoredFaceCountAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 2;

        public FavoredFaceCountAtLeastConditionConfig() { }

        public FavoredFaceCountAtLeastConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new FavoredFaceCountAtLeastCondition(count);
    }
}
