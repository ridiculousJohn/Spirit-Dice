using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Dice.Engine;

namespace Dice.Tests
{
    /** Test charm ids named by text: each name has one id, derived from the name, and the id names its text back. */
    public static class TestModifiers
    {
        private static readonly Dictionary<ModifierDefinition.Id, string> names = new Dictionary<ModifierDefinition.Id, string>();

        public static ModifierDefinition.Id IdOf(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            ModifierDefinition.Id id;
            using (var hash = MD5.Create())
                id = new ModifierDefinition.Id(new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("modifier:" + name))));
            names[id] = name;
            return id;
        }

        /** The ids of the names, in order. */
        public static ModifierDefinition.Id[] IdsOf(params string[] names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            var ids = new ModifierDefinition.Id[names.Length];
            for (int i = 0; i < names.Length; ++i) ids[i] = IdOf(names[i]);
            return ids;
        }

        /** The name of an id made here; any other id is named by its GUID text. */
        public static string NameOf(ModifierDefinition.Id id) => names.TryGetValue(id, out string name) ? name : id.ToString();
    }
}
