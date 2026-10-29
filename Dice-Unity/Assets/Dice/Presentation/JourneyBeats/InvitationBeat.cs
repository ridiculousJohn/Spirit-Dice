using System;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * The offer to play the Encounter beat that directly follows it: shows the rules of that encounter's table and waits for
     * Accept. Accepting, skipping the intro and retrying all land here.
     */
    [Serializable]
    public sealed class InvitationBeat : EncounterJourneyBeat
    {
        internal override void Show(IJourneyStage stage, EncounterJourneyAsset journey, int index)
            => stage.ShowInvitation(journey.TableSceneAt(index + 1));

        internal override void Validate(EncounterJourneyAsset journey, int index, MechanicalCatalog catalog)
        {
            base.Validate(journey, index, catalog);
            if (journey.EncounterBeatAt(index + 1) == null)
                throw Invalid(journey, index, "must be directly followed by an Encounter beat");
            if (journey.SceneAt(index).Table == null) throw Invalid(journey, index, "must be in a table scene");
        }
    }
}
