using System.Runtime.CompilerServices;

// Score playback is internal to the encounter screen; its unit tests drive it directly with a fabricated host and HUD.
[assembly: InternalsVisibleTo("Dice.Tests.EditMode")]
// PlayMode tests refresh the encounter screen and read the audio schedule through internal members.
[assembly: InternalsVisibleTo("Dice.Tests.PlayMode")]
// The editor's content validation runs the screen's, scenes' and journeys' own load-time checks through internal members.
[assembly: InternalsVisibleTo("Dice.Editor")]
