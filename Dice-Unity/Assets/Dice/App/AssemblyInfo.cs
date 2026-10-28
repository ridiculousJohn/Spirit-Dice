using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dice.Tests.EditMode")]
// PlayMode tests read the controller's run and isolate its save through internal members.
[assembly: InternalsVisibleTo("Dice.Tests.PlayMode")]
