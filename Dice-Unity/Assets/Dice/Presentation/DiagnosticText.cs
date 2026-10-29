using System.Globalization;
using System.Text;
using Dice.Engine;

namespace Dice.Presentation
{
    public static class DiagnosticText
    {
        public static string Humanize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var text = new StringBuilder(value.Length + 12);
            for (int i = 0; i < value.Length; ++i)
            {
                char current = value[i];
                if (current == '_' || current == '-' || current == '.')
                {
                    if (text.Length > 0 && text[text.Length - 1] != ' ') text.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(current) &&
                    (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]) ||
                     (i + 1 < value.Length && char.IsLower(value[i + 1]) && char.IsUpper(value[i - 1]))))
                    text.Append(' ');
                text.Append(current);
            }
            return text.ToString().Trim();
        }

        public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public static string DescribeInspectionStatus(ModifierInspectionStatus status)
        {
            switch (status)
            {
                case ModifierInspectionStatus.WouldActivate: return "would activate";
                case ModifierInspectionStatus.ConditionNotMet: return "condition not met";
                case ModifierInspectionStatus.ActivationLimitReached: return "activation limit reached";
                case ModifierInspectionStatus.DifferentPhase: return "evaluated on roll";
                case ModifierInspectionStatus.EncounterNotPlaying: return "encounter is not playing";
                default: return Humanize(status.ToString());
            }
        }

        /** Names charms by their presentation; hand upgrades and other values by their engine names. */
        public static string DescribeEvent(RuleEvent ruleEvent, MechanicalCatalog catalog, ModifierPresentationCatalog charms)
            => ruleEvent == null ? "" : ruleEvent.Describe(new EventFormat(catalog, charms));

        private sealed class EventFormat : IDiagnosticFormat
        {
            private readonly MechanicalCatalog catalog;
            private readonly ModifierPresentationCatalog charms;

            public EventFormat(MechanicalCatalog catalog, ModifierPresentationCatalog charms)
            {
                this.catalog = catalog;
                this.charms = charms;
            }

            public string Number(long value) => DiagnosticText.Number(value);
            public string Humanize(string value) => DiagnosticText.Humanize(value);
            public string ModifierName(ModifierDefinition.Id definitionId) => charms.GetName(catalog.GetModifier(definitionId));
        }
    }
}
