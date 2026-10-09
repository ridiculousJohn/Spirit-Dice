using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    public abstract class EncounterRuleAsset : ScriptableObject
    {
        public abstract EncounterRuleDefinition Build();
    }
}
