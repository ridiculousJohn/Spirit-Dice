using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [Serializable]
    public sealed class NoExtremeFacesConditionConfig : ModifierConditionConfig
    {
        [SerializeField] private int firstFace = 1;
        [SerializeField] private int secondFace = 6;

        public NoExtremeFacesConditionConfig() { }

        public NoExtremeFacesConditionConfig(int firstFace, int secondFace)
        {
            this.firstFace = firstFace;
            this.secondFace = secondFace;
        }

        public override IReadOnlyDictionary<string, int> TextValues => Text(firstFace, secondFace);
        public override IModifierCondition Build() => new NoExtremeFacesCondition(firstFace, secondFace);
    }
}
