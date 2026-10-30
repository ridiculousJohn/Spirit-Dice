using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * A lead on the investigation board and the beats that follow it, in order. Its identity is its asset GUID; what the
     * journey plays, where it takes place and how it ends belong to its beats.
     */
    [CreateAssetMenu(menuName = "Dice/Encounter Journey")]
    public sealed class EncounterJourneyAsset : ContentAsset
    {
        /** Identifies a journey where an identity is stored, as in the player profile. */
        public readonly struct Id : IEquatable<Id>, IContentId
        {
            public Guid Value { get; }

            public Id(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentException("A journey id requires a nonempty GUID.", nameof(value));
                Value = value;
            }

            public bool Equals(Id other) => Value == other.Value;
            public override bool Equals(object obj) => obj is Id other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            /** The 32 hexadecimal digits, as the journey's asset and its .meta file write its GUID. */
            public override string ToString() => Value.ToString("N");
            public static bool operator ==(Id left, Id right) => left.Equals(right);
            public static bool operator !=(Id left, Id right) => !left.Equals(right);
        }

        [SerializeField] private string locationTitle;
        [SerializeField] private string spiritName;
        [SerializeField, TextArea] private string leadText;
        [SerializeField] private Texture2D cardArtwork;
        [SerializeReference, SelectImplementation] private EncounterJourneyBeat[] beats = Array.Empty<EncounterJourneyBeat>();
        [Tooltip("The beat whose title and text are the clue the board shows once the journey is completed; one of the " +
            "journey's beats. Empty when the journey has no clue.")]
        [SerializeReference] private EncounterJourneyBeat clueBeat;
        [SerializeField] private ProgressionRewardAsset completionReward;
        [SerializeField] private EncounterJourneyAsset[] followUpJourneys = Array.Empty<EncounterJourneyAsset>();
        [SerializeField] private EncounterJourneyAsset[] requiredCompletedJourneys = Array.Empty<EncounterJourneyAsset>();

        // A nested type and a member cannot share a name, so the id property is not called Id.
        public Id Identity => new Id(AssetGuid);
        public string LocationTitle => locationTitle;
        public string SpiritName => spiritName;
        public string LeadText => leadText;
        public Texture2D CardArtwork => cardArtwork;
        public IReadOnlyList<EncounterJourneyBeat> Beats => beats;
        /** The beat whose title and text are the journey's clue, one of its beats; null when the journey has none. */
        public EncounterJourneyBeat ClueBeat => clueBeat;
        /** Applied when the journey completes, with the clue. */
        public ProgressionRewardAsset CompletionReward => completionReward;
        /** The leads the board highlights after an encounter of the journey is first won. */
        public IReadOnlyList<EncounterJourneyAsset> FollowUpJourneys => followUpJourneys;
        /** The journeys the player must complete before this journey is available. */
        public IReadOnlyList<EncounterJourneyAsset> RequiredCompletedJourneys => requiredCompletedJourneys;

        /** The encounters the journey's Encounter beats play, in beat order. */
        public IEnumerable<EncounterAsset> Encounters
        {
            get
            {
                foreach (var beat in beats)
                    if (beat is EncounterBeat encounterBeat) yield return encounterBeat.Encounter;
            }
        }

        /** The scene of the beat at the index: the scene the latest beat up to it sets. */
        public JourneySceneAsset SceneAt(int index)
        {
            for (int beat = index; beat >= 0; --beat)
                if (beats[beat].Scene != null) return beats[beat].Scene;
            // Validation requires the first beat to set a scene; an invalid journey has none.
            return null;
        }

        /**
         * The table scene the journey is at on the beat at the index, or, while a picture shows, the table scene it reaches
         * next, which stays loaded behind the picture. Null when the journey reaches none.
         */
        public EncounterTablePresentationAsset TableSceneAt(int index)
        {
            var scene = SceneAt(index);
            var table = scene != null ? scene.Table : null;
            for (int beat = index + 1; table == null && beat < beats.Length; ++beat)
                if (beats[beat].Scene != null) table = beats[beat].Scene.Table;
            return table;
        }

        /** The Encounter beat at the index, or null when there is no beat there or it is of another kind. */
        public EncounterBeat EncounterBeatAt(int index)
            => index >= 0 && index < beats.Length ? beats[index] as EncounterBeat : null;

        /** The index of the Encounter beat that plays the encounter, or -1 when the journey does not play it. */
        public int IndexOfEncounter(EncounterDefinition.Id encounterId)
        {
            for (int index = 0; index < beats.Length; ++index)
            {
                var beat = EncounterBeatAt(index);
                if (beat != null && beat.Encounter != null && beat.Encounter.Identity == encounterId) return index;
            }
            return -1;
        }

        /** The Encounter beat that plays the encounter, or null when the journey does not play it. */
        public EncounterBeat EncounterBeatOf(EncounterDefinition.Id encounterId) => EncounterBeatAt(IndexOfEncounter(encounterId));

        public void Validate(MechanicalCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            completionReward?.Build(catalog);
            ValidateJourneys(followUpJourneys, "follow-up journeys");
            ValidateJourneys(requiredCompletedJourneys, "required completed journeys");
            if (string.IsNullOrWhiteSpace(locationTitle) || string.IsNullOrWhiteSpace(spiritName) ||
                string.IsNullOrWhiteSpace(leadText))
                throw new FormatException("The journey " + name + " requires its location, host, and investigation lead.");
            if (beats == null || beats.Length == 0) throw new FormatException("The journey " + name + " requires beats.");
            for (int index = 0; index < beats.Length; ++index)
                if (beats[index] == null) throw new FormatException("Beat " + index + " of the journey " + name + " is empty.");
            if (beats[0].Scene == null) throw new FormatException("The first beat of the journey " + name + " must set a scene.");
            if (clueBeat != null && Array.IndexOf(beats, clueBeat) < 0)
                throw new FormatException("The clue beat of the journey " + name + " must be one of its beats.");
            for (int index = 0; index < beats.Length; ++index) beats[index].Validate(this, index, catalog);
        }

        private void ValidateJourneys(EncounterJourneyAsset[] journeys, string description)
        {
            if (journeys == null) throw new FormatException("The journey " + name + " requires an array of " + description + ".");
            var unique = new HashSet<EncounterJourneyAsset>();
            foreach (var journey in journeys)
                if (journey == null || journey == this || !unique.Add(journey))
                    throw new FormatException("The " + description + " of the journey " + name + " must be present, unique " +
                        "and different from the journey" + (journey != null ? ": " + journey.name : "") + ".");
        }
    }
}
