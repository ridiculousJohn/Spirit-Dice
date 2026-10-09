using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Shared charm identity and wording supplied by the active theme's assets. */
    public static class SpiritModifierPresentation
    {
        private sealed class Entry
        {
            internal readonly ModifierPresentationAsset Asset;
            internal readonly Type ConditionType;

            internal Entry(ModifierPresentationAsset asset)
            {
                Asset = asset;
                Type type = asset.Modifier.Build().Condition.GetType();
                ConditionType = type.Name == asset.ConditionTypeName ? type : null;
            }
        }

        private sealed class Theme : IDisposable
        {
            internal readonly Dictionary<string, Entry> Entries;
            internal Theme(Dictionary<string, Entry> entries) { Entries = entries; }
            public void Dispose()
            {
                // An older scene's teardown must not remove a newer scene's installed theme.
                if (ReferenceEquals(activeTheme, this)) activeTheme = null;
            }
        }

        private static Theme activeTheme;

        public static IDisposable InstallTheme(IEnumerable<ModifierPresentationAsset> assets, MechanicalCatalog catalog)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var required = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModifierDefinition definition in catalog.Modifiers) required.Add(definition.Id);
            var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (ModifierPresentationAsset asset in assets)
            {
                if (asset == null) throw new InvalidOperationException("A modifier presentation asset is missing.");
                if (asset.Modifier == null) throw new InvalidOperationException("A modifier presentation requires a mechanical asset reference: " + asset.name);
                string id = asset.Modifier.Id;
                if (string.IsNullOrWhiteSpace(id) || !required.Contains(id))
                    throw new InvalidOperationException("A modifier presentation references an unknown modifier: " + id);
                if (string.IsNullOrWhiteSpace(asset.DisplayName))
                    throw new InvalidOperationException("A modifier presentation requires a display name: " + id);
                if (entries.ContainsKey(id))
                    throw new InvalidOperationException("The theme contains duplicate modifier presentations: " + id);
                if (!Enum.IsDefined(typeof(ModifierArtworkKind), asset.Artwork))
                    throw new InvalidOperationException("A modifier presentation has an invalid artwork kind: " + id);
                if (!string.IsNullOrEmpty(asset.ConditionTemplate) && string.IsNullOrWhiteSpace(asset.ConditionTypeName))
                    throw new InvalidOperationException("A condition template requires its condition type guard: " + id);
                entries.Add(id, new Entry(asset));
            }
            foreach (string id in required)
                if (!entries.ContainsKey(id)) throw new InvalidOperationException("The theme is missing a modifier presentation: " + id);
            var theme = new Theme(entries);
            activeTheme = theme;
            return theme;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { activeTheme = null; }

        public static string GetName(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (TryGetEntry(definition.Id, out Entry entry)) return entry.Asset.DisplayName;
            string typeName = definition.Condition.GetType().Name;
            if (typeName.EndsWith("Condition", StringComparison.Ordinal)) typeName = typeName.Substring(0, typeName.Length - 9);
            return DiagnosticText.Humanize(typeName);
        }

        public static string GetConditionText(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (TryGetEntry(definition.Id, out Entry entry) && entry.ConditionType == definition.Condition.GetType() &&
                !string.IsNullOrEmpty(entry.Asset.ConditionTemplate))
            {
                string template = entry.Asset.ConditionTemplate;
                if (TryFormatTemplate(template, definition.Condition, out string text)) return text;
            }
            return DiagnosticText.DescribeCondition(definition.Condition);
        }

        public static string GetEffectText(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var text = new StringBuilder();
            foreach (ModifierEffect effect in definition.Effects)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append('+').Append(DiagnosticText.Number(effect.Amount)).Append(' ');
                switch (effect.Kind)
                {
                    case ModifierEffectKind.AddBaseScore: text.Append("base score"); break;
                    case ModifierEffectKind.AddMultiplier: text.Append("multiplier"); break;
                    case ModifierEffectKind.AddRerolls: text.Append(effect.Amount == 1 ? "reroll" : "rerolls"); break;
                    default: text.Append(DiagnosticText.Humanize(effect.Kind.ToString())); break;
                }
            }
            return text.Length == 0 ? "No effects" : text.ToString();
        }

        public static string GetTimingText(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string phase;
            switch (definition.Phase)
            {
                case ModifierPhase.InitialRoll: phase = "On the initial roll"; break;
                case ModifierPhase.PlayerReroll: phase = "After a reroll"; break;
                case ModifierPhase.PlayHand: phase = "When your hand is scored"; break;
                default: phase = DiagnosticText.Humanize(definition.Phase.ToString()); break;
            }
            string limit;
            switch (definition.ActivationLimit)
            {
                case ModifierActivationLimit.OncePerHand: limit = "Once per hand"; break;
                case ModifierActivationLimit.OncePerStage: limit = "Once per stage"; break;
                case ModifierActivationLimit.Unlimited: limit = "Every time"; break;
                default: limit = DiagnosticText.Humanize(definition.ActivationLimit.ToString()); break;
            }
            return phase + " / " + limit;
        }

        internal static Sprite GetIcon(string id)
            => TryGetEntry(id, out Entry entry) ? entry.Asset.Icon : null;

        internal static bool TryGetArtwork(string id, out ModifierArtworkKind artwork)
        {
            artwork = TryGetEntry(id, out Entry entry) ? entry.Asset.Artwork : ModifierArtworkKind.None;
            return artwork != ModifierArtworkKind.None;
        }

        private static bool TryGetEntry(string id, out Entry entry)
        {
            entry = null;
            return activeTheme != null && activeTheme.Entries.TryGetValue(id, out entry);
        }

        private static bool TryFormatTemplate(string template, IModifierCondition condition, out string text)
        {
            text = template;
            if (!template.Contains("{parameter}") && !template.Contains("{secondaryParameter}")) return true;
            int parameter;
            int secondaryParameter = 0;
            bool hasSecondary = false;
            // Read the live immutable predicate, so changed mechanical parameters appear immediately.
            switch (condition)
            {
                case ContainsFaceCondition value: parameter = value.Parameter; break;
                case NoExtremeFacesCondition value:
                    parameter = value.Parameter; secondaryParameter = value.SecondaryParameter; hasSecondary = true; break;
                default: return false;
            }
            if (template.Contains("{secondaryParameter}") && !hasSecondary) return false;
            text = template.Replace("{parameter}", parameter.ToString(CultureInfo.InvariantCulture))
                .Replace("{secondaryParameter}", secondaryParameter.ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }
}
