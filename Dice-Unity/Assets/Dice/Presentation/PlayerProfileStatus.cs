namespace Dice.Presentation
{
    /** The state of the player profile and its writes; every kind except None names a failure. */
    public enum PlayerProfileStatusKind
    {
        None,
        LoadFailed,
        /** Progress waits for the run save that commits a consumed unlock offer. */
        EncounterSavePending,
        /** Progress waits for the profile save. */
        ProfileSavePending,
        RewardFailed,
        StoryFailed,
        VisitFailed
    }

    /** The profile's state with the failure's developer detail, such as an exception message, kept apart from its wording. */
    public sealed class PlayerProfileStatus
    {
        public static readonly PlayerProfileStatus None = new PlayerProfileStatus(PlayerProfileStatusKind.None, null);

        public PlayerProfileStatusKind Kind { get; }
        /** Developer text describing the failure; null for None. */
        public string Detail { get; }
        /** Progress was recorded but could not yet be written. */
        public bool IsPending => Kind == PlayerProfileStatusKind.EncounterSavePending || Kind == PlayerProfileStatusKind.ProfileSavePending;

        public PlayerProfileStatus(PlayerProfileStatusKind kind, string detail)
        {
            Kind = kind;
            Detail = detail;
        }

        public override string ToString() => Detail == null ? Kind.ToString() : Kind + ": " + Detail;
    }
}
