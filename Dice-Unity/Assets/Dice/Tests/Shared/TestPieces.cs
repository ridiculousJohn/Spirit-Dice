using System;
using System.Security.Cryptography;
using System.Text;
using Dice.Engine;

namespace Dice.Tests
{
    /** Test table piece ids named by text: each name has one id, derived from the name. */
    public static class TestPieces
    {
        public static TablePieceDefinition.Id IdOf(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            using (var hash = MD5.Create())
                return new TablePieceDefinition.Id(new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("piece:" + name))));
        }
    }
}
