using System.Runtime.CompilerServices;

// Interaction kinds, rule events and actions are defined only inside the engine; unit tests fabricate further ones to check how
// presentation and the session treat them.
[assembly: InternalsVisibleTo("Dice.Tests.EditMode")]
