using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Dice.Engine;

namespace Dice.Tests
{
    /**
     * Test charm families named by text. Each name has one family definition, shared by every charm built with that
     * name, whose id is derived from the name.
     */
    public static class TestFamilies
    {
        private static readonly Dictionary<string, ModifierFamilyDefinition> families =
            new Dictionary<string, ModifierFamilyDefinition>(StringComparer.Ordinal);
        private static readonly Dictionary<ModifierFamilyDefinition, string> names = new Dictionary<ModifierFamilyDefinition, string>();

        public static ModifierFamilyDefinition Of(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (!families.TryGetValue(name, out ModifierFamilyDefinition family))
            {
                using (var hash = MD5.Create())
                    family = new ModifierFamilyDefinition(new ModifierFamilyDefinition.Id(new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(name)))));
                families.Add(name, family);
                names.Add(family, name);
            }
            return family;
        }

        /** The name of a family made here; any other family is named by its id. */
        public static string NameOf(ModifierFamilyDefinition family)
            => names.TryGetValue(family, out string name) ? name : family.Identity.ToString();

        /** The id of the family named by the text. */
        public static ModifierFamilyDefinition.Id IdOf(string name) => Of(name).Identity;
    }
}
