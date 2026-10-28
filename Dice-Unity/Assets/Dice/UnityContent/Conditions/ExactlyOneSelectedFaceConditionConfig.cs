using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class ExactlyOneSelectedFaceConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int face = 1;

        public ExactlyOneSelectedFaceConditionConfig() { }

        public ExactlyOneSelectedFaceConditionConfig(int face) => this.face = face;

        public override IReadOnlyDictionary<string, int> TextValues => Text(face);
        public override IModifierCondition Build() => new ExactlyOneSelectedFaceCondition(face);
    }
}
