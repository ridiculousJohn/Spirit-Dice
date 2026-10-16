using System.Collections.Generic;

namespace Dice.Presentation
{
    public interface ISpiritProgressionControls
    {
        IReadOnlyList<string> ProfileUnlockedModifierIds { get; }
        IReadOnlyList<string> ProfileDiscoveredFamilyIds { get; }
        IReadOnlyList<string> CompletedEncounterIds { get; }
        string ProfileStatusMessage { get; }
    }
}
