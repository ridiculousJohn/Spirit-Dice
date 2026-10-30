using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dice.Editor
{
    /**
     * One problem or measurement a content check found. It names the asset and, for an object inside a scene or prefab, the
     * object's hierarchy path there.
     */
    public sealed class ContentValidationEntry
    {
        public ContentValidationEntry(string assetPath, string objectPath, string message, double magnitude = double.NaN)
        {
            AssetPath = assetPath ?? "";
            ObjectPath = string.IsNullOrEmpty(objectPath) ? null : objectPath;
            Message = message ?? "";
            Magnitude = magnitude;
        }

        public string AssetPath { get; }
        /** The object's hierarchy path inside the scene or prefab at AssetPath, or null for the asset itself. */
        public string ObjectPath { get; }
        public string Message { get; }
        /** The measured magnitude that orders a measurement among its check's entries; NaN for a problem. */
        public double Magnitude { get; }

        public string Location => ObjectPath != null ? AssetPath + " > " + ObjectPath : AssetPath;
    }

    /** The entries one check found, in the order the check lists them. */
    public sealed class ContentValidationCheck
    {
        private readonly List<ContentValidationEntry> entries = new List<ContentValidationEntry>();

        public ContentValidationCheck(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public string Title { get; }
        public string Description { get; }
        /** What the check examined, such as the tables or clips it read, so an empty check shows what it covered. */
        public string Coverage { get; set; } = "";
        public IReadOnlyList<ContentValidationEntry> Entries => entries;

        public void Add(string assetPath, string message) => entries.Add(new ContentValidationEntry(assetPath, null, message));

        public void Add(ContentValidationEntry entry) => entries.Add(entry ?? throw new ArgumentNullException(nameof(entry)));

        /** Orders measurement entries largest first, keeping the order of equal ones. */
        internal void SortByMagnitude()
        {
            // OrderByDescending is a stable sort.
            var ordered = entries.OrderByDescending(entry => entry.Magnitude).ToList();
            entries.Clear();
            entries.AddRange(ordered);
        }
    }

    /** The checks of one content validation run and their entries, as text for the batch report. */
    public sealed class ContentValidationReport
    {
        public ContentValidationReport(IReadOnlyList<ContentValidationCheck> checks, DateTime finished)
        {
            Checks = checks ?? throw new ArgumentNullException(nameof(checks));
            Finished = finished;
        }

        public IReadOnlyList<ContentValidationCheck> Checks { get; }
        public DateTime Finished { get; }

        public string ToText()
        {
            var text = new StringBuilder();
            text.Append("Spirit Dice content validation, ").Append(Finished.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            foreach (var check in Checks)
            {
                text.Append('\n').Append("== ").Append(check.Title).Append(" (").Append(check.Entries.Count)
                    .Append(check.Entries.Count == 1 ? " entry" : " entries").Append(")\n");
                text.Append(check.Description).Append('\n');
                if (check.Coverage.Length > 0) text.Append("Examined: ").Append(check.Coverage).Append('\n');
                if (check.Entries.Count == 0) text.Append("  None.\n");
                foreach (var entry in check.Entries)
                    text.Append("- ").Append(entry.Location).Append('\n').Append("    ").Append(entry.Message.Replace("\n", "\n    "))
                        .Append('\n');
            }
            return text.ToString();
        }
    }
}
