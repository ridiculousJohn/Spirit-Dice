using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierConditionConfigTests
    {
        /**
         * Checks a new condition config builds its condition with its own default parameters and words them under the
         * condition wording's names / fails if a config builds another condition, loses a default or swaps its text values.
         */
        [Test]
        public void AConditionConfigBuildsItsConditionWithItsDefaults()
        {
            var config = new NoExtremeFacesConditionConfig();
            var condition = (NoExtremeFacesCondition)config.Build();
            Assert.That(condition.Parameter, Is.EqualTo(1));
            Assert.That(condition.SecondaryParameter, Is.EqualTo(6));
            Assert.That(config.TextValues, Is.EquivalentTo(new Dictionary<string, int>
            {
                ["parameter"] = 1,
                ["secondaryParameter"] = 6
            }));
            Assert.That(new AlwaysConditionConfig().TextValues, Is.Empty);
        }

        /**
         * Checks every condition config builds its own sealed, immutable condition class, since conditions are shared across
         * sessions and evaluated during previews / fails if two configs build one class or a condition gains mutable state.
         */
        [Test]
        public void EveryConditionConfigBuildsItsOwnSealedImmutableCondition()
        {
            var built = new HashSet<Type>();
            foreach (Type configType in typeof(ModifierConditionConfig).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(ModifierConditionConfig).IsAssignableFrom(type)))
            {
                Type type = ((ModifierConditionConfig)Activator.CreateInstance(configType)).Build().GetType();
                Assert.That(type.IsSealed, Is.True, configType.Name);
                Assert.That(built.Add(type), Is.True, configType.Name + " builds the condition class of another config.");
                for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
                {
                    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly;
                    foreach (var property in level.GetProperties(Declared))
                        Assert.That(property.SetMethod, Is.Null, configType.Name + ": " + property.Name);
                    foreach (var field in level.GetFields(Declared))
                        Assert.That(field.IsInitOnly, Is.True, configType.Name + ": " + field.Name);
                }
            }
            Assert.That(built, Is.Not.Empty);
        }
    }
}
