using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    /**
     * An encounter rule: one class per kind, each building its engine definition. Its identity is its asset GUID, under
     * which the run save keeps the rule's state; the rule cards that present it reference the asset.
     */
    public abstract class EncounterRuleAsset : ContentAsset
    {
        public EncounterRuleDefinition.Id Identity => new EncounterRuleDefinition.Id(AssetGuid);

        public abstract EncounterRuleDefinition Build();

        /** The definitions of the rules an encounter or item lists; owner names it in failures, such as "encounter X". */
        internal static EncounterRuleDefinition[] BuildAll(EncounterRuleAsset[] sources, string owner)
        {
            if (sources == null) throw new InvalidOperationException("The " + owner + " has no rule list.");
            var definitions = new EncounterRuleDefinition[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("The " + owner + " has a missing rule asset.");
                try { definitions[i] = source.Build(); }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException("The rule " + source.name + " of the " + owner + " is invalid: " +
                        exception.Message, exception);
                }
            }
            return definitions;
        }
    }
}
