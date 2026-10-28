using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class RerollsSpentAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int rerolls = 3;

        public RerollsSpentAtLeastConditionConfig() { }

        public RerollsSpentAtLeastConditionConfig(int rerolls) => this.rerolls = rerolls;

        public override IReadOnlyDictionary<string, int> TextValues => Text(rerolls);
        public override IModifierCondition Build() => new RerollsSpentAtLeastCondition(rerolls);
    }
}
