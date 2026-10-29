using System;

namespace Dice.Engine
{
    /**
     * A typed id of content identified by its asset GUID (ModifierFamilyDefinition.Id, EncounterDefinition.Id). Saves
     * write every such id the same way, through this interface.
     */
    public interface IContentId
    {
        Guid Value { get; }
    }
}
