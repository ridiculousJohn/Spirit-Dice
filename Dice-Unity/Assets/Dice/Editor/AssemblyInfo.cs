using System.Runtime.CompilerServices;

// The preview windows keep their stage internal; their EditMode tests drive and inspect it directly.
[assembly: InternalsVisibleTo("Dice.Tests.EditMode")]
