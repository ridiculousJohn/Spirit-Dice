using System;
using System.Security.Cryptography;
using System.Text;
using Dice.Engine;

namespace Dice.Tests
{
    /** Test encounter ids named by text: each name has one id, derived from the name. */
    public static class TestEncounters
    {
        public static EncounterDefinition.Id IdOf(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            using (var hash = MD5.Create())
                return new EncounterDefinition.Id(new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("encounter:" + name))));
        }
    }
}
