using System;

namespace Dice.Engine
{
    /**
     * A typed id of content identified by its asset GUID (ModifierDefinition.Id, ModifierFamilyDefinition.Id,
     * EncounterDefinition.Id, TablePieceDefinition.Id, EncounterRuleDefinition.Id, EncounterJourneyAsset.Id). Saves write every
     * such id the same way, through this interface.
     */
    public interface IContentId
    {
        Guid Value { get; }
    }
}
