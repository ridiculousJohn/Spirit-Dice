using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class CarriedFaceMatchCountAtMostConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int count = 0;

        public CarriedFaceMatchCountAtMostConditionConfig() { }

        public CarriedFaceMatchCountAtMostConditionConfig(int count) => this.count = count;

        public override IReadOnlyDictionary<string, int> TextValues => Text(count);
        public override IModifierCondition Build() => new CarriedFaceMatchCountAtMostCondition(count);
    }
}
