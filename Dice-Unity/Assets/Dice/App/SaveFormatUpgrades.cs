using System;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    /**
     * The ordered upgrade steps of one versioned save document, run on the parsed document before it is read.
     * Step i upgrades a document of format OldestVersion + i to the next format, so the steps reach CurrentVersion.
     */
    internal sealed class SaveFormatUpgrades
    {
        private readonly string versionField;
        private readonly Action<JObject>[] steps;

        internal int CurrentVersion { get; }
        internal int OldestVersion => CurrentVersion - steps.Length;

        internal SaveFormatUpgrades(string versionField, int currentVersion, params Action<JObject>[] steps)
        {
            if (string.IsNullOrWhiteSpace(versionField)) throw new ArgumentException("A version field is required.", nameof(versionField));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (currentVersion - steps.Length < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion));
            foreach (var step in steps)
                if (step == null) throw new ArgumentException("Upgrade steps cannot be null.", nameof(steps));
            this.versionField = versionField;
            this.steps = (Action<JObject>[])steps.Clone();
            CurrentVersion = currentVersion;
        }

        /**
         * Validates the document's version token and, when its version is supported, runs the steps from that version in order,
         * writing each step's resulting version. A rejected document is left unchanged; each caller maps the result to its own error.
         * A version beyond the long range throws OverflowException, which callers already treat as an out-of-range integer.
         */
        internal SaveFormatCheck Upgrade(JObject document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var version = document[versionField];
            if (version == null || version.Type != JTokenType.Integer) return SaveFormatCheck.VersionNotInteger;
            long value = version.Value<long>();
            if (value < OldestVersion || value > CurrentVersion) return SaveFormatCheck.VersionUnsupported;
            for (int next = (int)value; next < CurrentVersion; ++next)
            {
                steps[next - OldestVersion](document);
                document[versionField] = next + 1;
            }
            return SaveFormatCheck.Upgraded;
        }
    }

    internal enum SaveFormatCheck { Upgraded, VersionNotInteger, VersionUnsupported }
}
