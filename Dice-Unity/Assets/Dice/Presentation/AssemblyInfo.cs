using System.Runtime.CompilerServices;

// Score playback is internal to the encounter screen; its unit tests drive it directly with a fabricated host and HUD.
[assembly: InternalsVisibleTo("Dice.Tests.EditMode")]
