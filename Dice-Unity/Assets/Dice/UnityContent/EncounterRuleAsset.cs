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
    }
}
