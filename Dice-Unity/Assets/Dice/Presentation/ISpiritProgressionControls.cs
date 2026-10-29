using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    public interface ISpiritProgressionControls
    {
        IReadOnlyList<string> ProfileUnlockedModifierIds { get; }
        IReadOnlyList<ModifierFamilyDefinition.Id> ProfileDiscoveredFamilyIds { get; }
        IReadOnlyList<EncounterDefinition.Id> CompletedEncounterIds { get; }
        PlayerProfileStatus ProfileStatus { get; }
    }
}
