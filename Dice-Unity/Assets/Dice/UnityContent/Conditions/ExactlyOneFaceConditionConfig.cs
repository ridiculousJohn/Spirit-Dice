using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class ExactlyOneFaceConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int face = 2;

        public ExactlyOneFaceConditionConfig() { }

        public ExactlyOneFaceConditionConfig(int face) => this.face = face;

        public override IReadOnlyDictionary<string, int> TextValues => Text(face);
        public override IModifierCondition Build() => new ExactlyOneFaceCondition(face);
    }
}
