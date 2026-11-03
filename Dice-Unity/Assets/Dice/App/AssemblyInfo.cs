using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dice.Tests.EditMode")]
// PlayMode tests read the controller's run and isolate its save through internal members.
[assembly: InternalsVisibleTo("Dice.Tests.PlayMode")]
// The Save Games window writes saves through SaveFileCommit and recognises save files by the save code's own names.
[assembly: InternalsVisibleTo("Dice.Editor")]
