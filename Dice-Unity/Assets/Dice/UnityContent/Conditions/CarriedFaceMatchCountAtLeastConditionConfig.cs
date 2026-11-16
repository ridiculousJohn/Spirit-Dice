using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class CarriedFaceMatchCountAtLeastConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 2;

        public CarriedFaceMatchCountAtLeastConditionConfig() { }

        public CarriedFaceMatchCountAtLeastConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new CarriedFaceMatchCountAtLeastCondition(count);
    }
}
