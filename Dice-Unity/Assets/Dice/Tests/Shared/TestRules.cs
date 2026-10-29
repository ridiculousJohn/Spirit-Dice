using System;
using System.Security.Cryptography;
using System.Text;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Dice.Tests
{
    /** Test encounter rule ids named by text, each name having one id derived from it, and rule assets carrying them. */
    public static class TestRules
    {
        public static EncounterRuleDefinition.Id IdOf(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            using (var hash = MD5.Create())
                return new EncounterRuleDefinition.Id(new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("rule:" + name))));
        }

        /**
         * A rule asset whose id is the given id, for a rule card to reference; the caller destroys it. A card reads only
         * its rule's id, so the asset's kind does not matter.
         */
        public static EncounterRuleAsset Asset(EncounterRuleDefinition.Id id)
        {
#if UNITY_EDITOR
            var asset = ScriptableObject.CreateInstance<FirstRerollFreeRuleAsset>();
            asset.name = "Rule " + id;
            using (var serialized = new SerializedObject(asset))
            {
                serialized.FindProperty(ContentAsset.AssetGuidField).stringValue = id.Value.ToString("N");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return asset;
#else
            throw new NotSupportedException("Test rule assets are authored through the Unity editor's serialized objects.");
#endif
        }
    }
}
