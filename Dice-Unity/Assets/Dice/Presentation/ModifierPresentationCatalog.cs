using System;
using System.Collections.Generic;
using System.Globalization;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The presentation of every charm of one loaded content, built at load from a skin. Each presentation is matched
     * to its charm's definition through the presentation's own charm reference, and each charm shows the category the
     * skin maps the set of its effect kinds to. A content charm without exactly one presentation, a charm whose effect
     * kinds the skin maps to no category, a content charm family without exactly one name, an effect kind, phase or
     * activation limit the content uses without wording, or incomplete contribution wording or text format fails
     * construction.
     */
    public sealed class ModifierPresentationCatalog
    {
        private readonly Dictionary<ModifierDefinition, ModifierPresentationAsset> entries =
            new Dictionary<ModifierDefinition, ModifierPresentationAsset>();
        private readonly Dictionary<ModifierDefinition, int> order = new Dictionary<ModifierDefinition, int>();
        private readonly Dictionary<ModifierDefinition, ModifierCategoryPresentationAsset> categories =
            new Dictionary<ModifierDefinition, ModifierCategoryPresentationAsset>();
        private readonly Dictionary<ModifierFamilyDefinition.Id, string> familyNames = new Dictionary<ModifierFamilyDefinition.Id, string>();
        private readonly Dictionary<ModifierEffectKind, ModifierEffectWording> wording =
            new Dictionary<ModifierEffectKind, ModifierEffectWording>();
        private readonly Dictionary<ModifierPhase, ModifierPhaseWording> phaseWording =
            new Dictionary<ModifierPhase, ModifierPhaseWording>();
        private readonly Dictionary<ModifierActivationLimit, ModifierActivationLimitWording> limitWording =
            new Dictionary<ModifierActivationLimit, ModifierActivationLimitWording>();
        private readonly ModifierContributionWording contributionWording;
        private readonly ModifierTextFormat format;
        private readonly string noEffectsText;

        public MechanicalCatalog Catalog { get; }

        public ModifierPresentationCatalog(ModifierSkinAsset skin, MechanicalContent content)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (skin.Modifiers == null || skin.Categories == null || skin.FamilyWording == null || skin.EffectWording == null ||
                skin.PhaseWording == null || skin.LimitWording == null)
                throw new InvalidOperationException("The modifier skin " + skin.name + " has missing lists.");
            Catalog = content.Catalog;
            var effectCategories = new List<KeyValuePair<HashSet<ModifierEffectKind>, ModifierCategoryPresentationAsset>>();
            foreach (ModifierEffectsCategory entry in skin.Categories)
            {
                if (entry == null || entry.Category == null || entry.Effects == null)
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has a missing category.");
                var kinds = new HashSet<ModifierEffectKind>();
                foreach (ModifierEffectKind kind in entry.Effects)
                    if (!kinds.Add(kind))
                        throw new InvalidOperationException("The modifier skin " + skin.name + " lists the effect " + kind +
                            " twice for the category " + entry.Category.name + ".");
                foreach (var listed in effectCategories)
                    if (listed.Key.SetEquals(kinds))
                        throw new InvalidOperationException("The modifier skin " + skin.name + " maps the effects " + Describe(kinds) +
                            " to more than one category.");
                effectCategories.Add(new KeyValuePair<HashSet<ModifierEffectKind>, ModifierCategoryPresentationAsset>(kinds, entry.Category));
            }
            var presented = new HashSet<ModifierAsset>();
            foreach (ModifierPresentationAsset presentation in skin.Modifiers)
            {
                if (presentation == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has a missing charm presentation.");
                ModifierAsset modifier = presentation.Modifier;
                if (modifier == null)
                    throw new InvalidOperationException("The charm presentation " + presentation.name + " has no charm reference.");
                if (!presented.Add(modifier))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has more than one presentation for the charm " + modifier.name + ".");
                ModifierDefinition definition = content.GetDefinition(modifier);
                Validate(presentation);
                categories.Add(definition, CategoryOf(skin, effectCategories, definition, modifier));
                order.Add(definition, entries.Count);
                entries.Add(definition, presentation);
            }
            foreach (ModifierAsset modifier in content.Modifiers)
                if (!presented.Contains(modifier))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no presentation for the charm " + modifier.name + ".");
            foreach (ModifierFamilyWording family in skin.FamilyWording)
            {
                if (family == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has a missing family name.");
                if (family.Family == null)
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has a family name without a family: '" + family.Name + "'.");
                if (string.IsNullOrWhiteSpace(family.Name))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has an incomplete family name for the family " +
                        family.Family.name + ".");
                if (familyNames.ContainsKey(family.Family.Identity))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " names the family " + family.Family.name + " twice.");
                familyNames.Add(family.Family.Identity, family.Name);
            }
            foreach (ModifierAsset modifier in content.Modifiers)
                if (!familyNames.ContainsKey(content.GetDefinition(modifier).Family.Identity))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no name for the family " +
                        modifier.Family.name + " of the charm " + modifier.name + ".");
            foreach (ModifierEffectWording effect in skin.EffectWording)
            {
                if (effect == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has missing effect wording.");
                if (wording.ContainsKey(effect.Kind))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " words the effect " + effect.Kind + " twice.");
                if (string.IsNullOrWhiteSpace(effect.SingularLabel) || string.IsNullOrWhiteSpace(effect.PluralLabel) ||
                    string.IsNullOrWhiteSpace(effect.CompactLabel))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has incomplete wording for the effect " + effect.Kind + ".");
                wording.Add(effect.Kind, effect);
            }
            foreach (ModifierPhaseWording phase in skin.PhaseWording)
            {
                if (phase == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has missing phase wording.");
                if (phaseWording.ContainsKey(phase.Phase))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " words the phase " + phase.Phase + " twice.");
                if (string.IsNullOrWhiteSpace(phase.TimingLabel) || string.IsNullOrWhiteSpace(phase.DescriptionLead) ||
                    phase.ImpliedLimits == null)
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has incomplete wording for the phase " + phase.Phase + ".");
                phaseWording.Add(phase.Phase, phase);
            }
            foreach (ModifierActivationLimitWording limit in skin.LimitWording)
            {
                if (limit == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has missing activation limit wording.");
                if (limitWording.ContainsKey(limit.Limit))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " words the activation limit " + limit.Limit + " twice.");
                if (string.IsNullOrWhiteSpace(limit.TimingLabel) || limit.DescriptionNote == null)
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has incomplete wording for the activation limit " +
                        limit.Limit + ".");
                limitWording.Add(limit.Limit, limit);
            }
            // Every scored hand shows base score and multiplier, and any charm contribution can carry rerolls.
            contributionWording = skin.ContributionWording;
            if (contributionWording == null || string.IsNullOrWhiteSpace(contributionWording.BaseScoreLabel) ||
                string.IsNullOrWhiteSpace(contributionWording.MultiplierLabel) ||
                string.IsNullOrWhiteSpace(contributionWording.RerollLabel) || string.IsNullOrWhiteSpace(contributionWording.RerollsLabel))
                throw new InvalidOperationException("The modifier skin " + skin.name + " has incomplete contribution wording.");
            format = skin.TextFormat ?? throw new InvalidOperationException("The modifier skin " + skin.name + " has no text format.");
            RequireFormat(skin, format.Effect, "effect", false);
            RequireFormat(skin, format.EffectSeparator, "effectSeparator", true);
            RequireFormat(skin, format.CompactEffect, "compactEffect", false);
            RequireFormat(skin, format.LabeledCompactEffect, "labeledCompactEffect", false);
            RequireFormat(skin, format.CompactEffectSeparator, "compactEffectSeparator", true);
            RequireFormat(skin, format.Timing, "timing", false);
            RequireFormat(skin, format.Contribution, "contribution", false);
            RequireFormat(skin, format.ContributionSeparator, "contributionSeparator", true);
            // The content pairs every catalog charm with its asset, whose name names the charm.
            foreach (ModifierAsset modifier in content.Modifiers)
            {
                ModifierDefinition definition = content.GetDefinition(modifier);
                if (definition.Effects.Count == 0 && string.IsNullOrWhiteSpace(skin.NoEffectsText))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no text for the charm without effects " + modifier.name + ".");
                foreach (ModifierEffect effect in definition.Effects)
                    if (!wording.ContainsKey(effect.Kind))
                        throw new InvalidOperationException("The modifier skin " + skin.name + " has no wording for the effect " + effect.Kind + " of the charm " + modifier.name + ".");
                if (!phaseWording.ContainsKey(definition.Phase))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no wording for the phase " + definition.Phase +
                        " of the charm " + modifier.name + ".");
                if (!limitWording.ContainsKey(definition.ActivationLimit))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no wording for the activation limit " +
                        definition.ActivationLimit + " of the charm " + modifier.name + ".");
            }
            noEffectsText = skin.NoEffectsText;
        }

        /** A separator may be only spaces or line breaks, but not empty; any other format field needs visible text. */
        private static void RequireFormat(ModifierSkinAsset skin, string value, string field, bool separator)
        {
            if (separator ? string.IsNullOrEmpty(value) : string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("The modifier skin " + skin.name + " has no text format " + field + ".");
        }

        /**
         * The category the skin maps the set of the charm's effect kinds to. Several effects of one kind count as that
         * kind once; every effect counts, since the engine rejects an effect whose amount is not positive.
         */
        private static ModifierCategoryPresentationAsset CategoryOf(ModifierSkinAsset skin,
            List<KeyValuePair<HashSet<ModifierEffectKind>, ModifierCategoryPresentationAsset>> effectCategories,
            ModifierDefinition definition, ModifierAsset modifier)
        {
            var kinds = new HashSet<ModifierEffectKind>();
            foreach (ModifierEffect effect in definition.Effects) kinds.Add(effect.Kind);
            foreach (var listed in effectCategories)
                if (listed.Key.SetEquals(kinds)) return listed.Value;
            throw new InvalidOperationException("The modifier skin " + skin.name + " has no category for the effects " + Describe(kinds) +
                " of the charm " + modifier.name + ".");
        }

        /** The effect kinds in their declared order, as "(A, B)", or "(none)". */
        private static string Describe(HashSet<ModifierEffectKind> kinds)
        {
            var sorted = new List<ModifierEffectKind>(kinds);
            sorted.Sort();
            return sorted.Count == 0 ? "(none)" : "(" + string.Join(", ", sorted) + ")";
        }

        private static void Validate(ModifierPresentationAsset presentation)
        {
            string id = presentation.Modifier.name;
            if (string.IsNullOrWhiteSpace(presentation.DisplayName))
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires a display name: " + id);
            if (presentation.Icon == null)
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires an icon: " + id);
            if (string.IsNullOrWhiteSpace(presentation.ConditionTemplate))
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires condition text: " + id);
            // Any brace left once the condition's own placeholders are removed is an unknown or malformed placeholder.
            string unplaced = presentation.ConditionTemplate;
            foreach (var value in presentation.Modifier.Condition.TextValues) unplaced = unplaced.Replace(Placeholder(value.Key), "");
            if (unplaced.IndexOf('{') >= 0 || unplaced.IndexOf('}') >= 0)
                throw new InvalidOperationException("The condition template of " + presentation.name + " contains an unknown placeholder: " + id);
        }

        public string GetName(ModifierDefinition definition) => Entry(definition).DisplayName;

        /** The name of the charm's family, shared by every variant of the family. */
        public string GetFamilyName(ModifierDefinition definition)
        {
            Entry(definition);
            return familyNames[definition.Family.Identity];
        }

        /** The position of the charm's presentation in the skin; screens that list a family's variants list them in this order. */
        public int GetOrder(ModifierDefinition definition)
        {
            Entry(definition);
            return order[definition];
        }

        public string GetConditionText(ModifierDefinition definition)
        {
            ModifierPresentationAsset entry = Entry(definition);
            // Read the referenced mechanical asset, so changed parameters appear immediately.
            string text = entry.ConditionTemplate;
            foreach (var value in entry.Modifier.Condition.TextValues)
                text = text.Replace(Placeholder(value.Key), value.Value.ToString(CultureInfo.InvariantCulture));
            return text;
        }

        private static string Placeholder(string name) => "{" + name + "}";

        public string GetEffectText(ModifierDefinition definition)
        {
            Entry(definition);
            var parts = new List<string>();
            foreach (ModifierEffect effect in definition.Effects)
            {
                ModifierEffectWording effectWording = wording[effect.Kind];
                parts.Add(Amount(format.Effect, DiagnosticText.Number(effect.Amount),
                    effect.Amount == 1 ? effectWording.SingularLabel : effectWording.PluralLabel));
            }
            return parts.Count == 0 ? noEffectsText : string.Join(format.EffectSeparator, parts);
        }

        /** A format template with its {amount} and {label} filled. */
        private static string Amount(string template, string amount, string label) =>
            template.Replace(Placeholder("amount"), amount).Replace(Placeholder("label"), label);

        /** One line per effect; a charm with more than one effect marks each line with its effect's compact label. */
        public string GetCompactEffectText(ModifierDefinition definition)
        {
            Entry(definition);
            var lines = new List<string>();
            foreach (ModifierEffect effect in definition.Effects)
                lines.Add(Amount(definition.Effects.Count > 1 ? format.LabeledCompactEffect : format.CompactEffect,
                    DiagnosticText.Number(effect.Amount), wording[effect.Kind].CompactLabel));
            return string.Join(format.CompactEffectSeparator, lines);
        }

        /** The skin's timing label of the charm's phase, then of its activation limit. */
        public string GetTimingText(ModifierDefinition definition)
        {
            Entry(definition);
            return format.Timing.Replace(Placeholder("phase"), phaseWording[definition.Phase].TimingLabel)
                .Replace(Placeholder("limit"), limitWording[definition.ActivationLimit].TimingLabel);
        }

        /** The skin's description lead of the charm's phase. */
        public string GetDescriptionLead(ModifierDefinition definition)
        {
            Entry(definition);
            return phaseWording[definition.Phase].DescriptionLead;
        }

        /** The skin's description note of the charm's activation limit; empty when its phase implies the limit. */
        public string GetDescriptionNote(ModifierDefinition definition)
        {
            Entry(definition);
            ModifierPhaseWording phase = phaseWording[definition.Phase];
            foreach (ModifierActivationLimit implied in phase.ImpliedLimits)
                if (implied == definition.ActivationLimit) return "";
            return limitWording[definition.ActivationLimit].DescriptionNote;
        }

        /**
         * The nonzero parts of a score contribution in the skin's contribution wording and format, joined by the format's
         * contribution separator; empty when all are zero. Reroll counts are written without digit grouping.
         */
        public string GetContributionText(long baseScore, long multiplier, long rerolls)
        {
            var parts = new List<string>();
            if (baseScore != 0)
                parts.Add(Amount(format.Contribution, DiagnosticText.Number(baseScore), contributionWording.BaseScoreLabel));
            if (multiplier != 0)
                parts.Add(Amount(format.Contribution, DiagnosticText.Number(multiplier), contributionWording.MultiplierLabel));
            if (rerolls != 0)
                parts.Add(Amount(format.Contribution, rerolls.ToString(CultureInfo.InvariantCulture),
                    rerolls == 1 ? contributionWording.RerollLabel : contributionWording.RerollsLabel));
            return string.Join(format.ContributionSeparator, parts);
        }

        public Sprite GetIcon(ModifierDefinition definition) => Entry(definition).Icon;

        /** The category the skin shows for the set of the charm's effect kinds. */
        public ModifierCategoryPresentationAsset GetCategory(ModifierDefinition definition)
        {
            Entry(definition);
            return categories[definition];
        }

        public Color GetEffectColor(ModifierDefinition definition) => GetCategory(definition).AccentColor;
        public Color GetInkColor(ModifierDefinition definition) => GetCategory(definition).InkColor;
        /** The mark of the charm's category; null for a category without one. */
        public Sprite GetCategorySprite(ModifierDefinition definition) => GetCategory(definition).Symbol;

        private ModifierPresentationAsset Entry(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!entries.TryGetValue(definition, out ModifierPresentationAsset entry))
                throw new InvalidOperationException("The charm " + definition.Identity + " is not a charm of the loaded content.");
            return entry;
        }
    }
}
