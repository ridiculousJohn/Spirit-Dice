using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dice.Engine;

namespace Dice.UnityContent
{
    /**
     * A charm's authored condition. Each condition kind is its own config class with its own parameters and defaults,
     * and builds the engine condition it describes.
     */
    [Serializable]
    public abstract class ModifierConditionConfig
    {
        private const string ParameterName = "parameter";
        private const string SecondaryParameterName = "secondaryParameter";
        private static readonly IReadOnlyDictionary<string, int> NoTextValues =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

        /** Named values the charm's condition wording may show as {name} placeholders. */
        public virtual IReadOnlyDictionary<string, int> TextValues => NoTextValues;

        /** The engine condition with this config's parameters; parameters outside its range fail the engine's validation. */
        public abstract IModifierCondition Build();

        /** The wording names a condition's first number {parameter}. */
        protected static IReadOnlyDictionary<string, int> Text(int parameter)
            => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal) { [ParameterName] = parameter });

        /** The wording names a condition's first number {parameter} and its second {secondaryParameter}. */
        protected static IReadOnlyDictionary<string, int> Text(int parameter, int secondaryParameter)
            => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ParameterName] = parameter,
                [SecondaryParameterName] = secondaryParameter
            });
    }
}
