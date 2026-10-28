using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class ContainsUpperFaceConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int face = 6;

        public ContainsUpperFaceConditionConfig() { }

        public ContainsUpperFaceConditionConfig(int face) => this.face = face;

        public override IReadOnlyDictionary<string, int> TextValues => Text(face);
        public override IModifierCondition Build() => new ContainsUpperFaceCondition(face);
    }
}
