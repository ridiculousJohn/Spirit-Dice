using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The presentation of every charm of one loaded content, built at load from a skin. Each presentation is matched
     * to its charm's definition through the presentation's own charm reference; a content charm without exactly one
     * presentation, an effect kind, phase or activation limit the content uses without wording, or incomplete
     * contribution wording fails construction.
     */
    public sealed class ModifierPresentationCatalog
    {
        private const string ParameterPlaceholder = "{parameter}";
        private const string SecondaryParameterPlaceholder = "{secondaryParameter}";

        private readonly Dictionary<ModifierDefinition, ModifierPresentationAsset> entries =
            new Dictionary<ModifierDefinition, ModifierPresentationAsset>();
        private readonly Dictionary<ModifierEffectKind, ModifierEffectWording> wording =
            new Dictionary<ModifierEffectKind, ModifierEffectWording>();
        private readonly Dictionary<ModifierPhase, ModifierPhaseWording> phaseWording =
            new Dictionary<ModifierPhase, ModifierPhaseWording>();
        private readonly Dictionary<ModifierActivationLimit, ModifierActivationLimitWording> limitWording =
            new Dictionary<ModifierActivationLimit, ModifierActivationLimitWording>();
        private readonly ModifierContributionWording contributionWording;
        private readonly string noEffectsText;

        public MechanicalCatalog Catalog { get; }

        public ModifierPresentationCatalog(ModifierSkinAsset skin, MechanicalContent content)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (skin.Modifiers == null || skin.Categories == null || skin.EffectWording == null || skin.PhaseWording == null ||
                skin.LimitWording == null)
                throw new InvalidOperationException("The modifier skin " + skin.name + " has missing lists.");
            Catalog = content.Catalog;
            var categories = new HashSet<ModifierCategoryPresentationAsset>();
            foreach (ModifierCategoryPresentationAsset category in skin.Categories)
            {
                if (category == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has a missing category.");
                if (!categories.Add(category))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " lists the category " + category.name + " twice.");
            }
            var presented = new HashSet<ModifierAsset>();
            foreach (ModifierPresentationAsset presentation in skin.Modifiers)
            {
                if (presentation == null) throw new InvalidOperationException("The modifier skin " + skin.name + " has a missing charm presentation.");
                ModifierAsset modifier = presentation.Modifier;
                if (modifier == null)
                    throw new InvalidOperationException("The charm presentation " + presentation.name + " has no charm reference.");
                if (!presented.Add(modifier))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has more than one presentation for the charm " + modifier.Id + ".");
                ModifierDefinition definition = content.GetDefinition(modifier);
                Validate(presentation, categories);
                entries.Add(definition, presentation);
            }
            foreach (ModifierAsset modifier in content.Modifiers)
                if (!presented.Contains(modifier))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no presentation for the charm " + modifier.Id + ".");
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
            foreach (ModifierDefinition definition in Catalog.Modifiers)
            {
                if (definition.Effects.Count == 0 && string.IsNullOrWhiteSpace(skin.NoEffectsText))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no text for the charm without effects " + definition.Id + ".");
                foreach (ModifierEffect effect in definition.Effects)
                    if (!wording.ContainsKey(effect.Kind))
                        throw new InvalidOperationException("The modifier skin " + skin.name + " has no wording for the effect " + effect.Kind + " of the charm " + definition.Id + ".");
                if (!phaseWording.ContainsKey(definition.Phase))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no wording for the phase " + definition.Phase +
                        " of the charm " + definition.Id + ".");
                if (!limitWording.ContainsKey(definition.ActivationLimit))
                    throw new InvalidOperationException("The modifier skin " + skin.name + " has no wording for the activation limit " +
                        definition.ActivationLimit + " of the charm " + definition.Id + ".");
            }
            noEffectsText = skin.NoEffectsText;
        }

        private static void Validate(ModifierPresentationAsset presentation, HashSet<ModifierCategoryPresentationAsset> categories)
        {
            string id = presentation.Modifier.Id;
            if (string.IsNullOrWhiteSpace(presentation.DisplayName))
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires a display name: " + id);
            if (presentation.Icon == null)
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires an icon: " + id);
            if (string.IsNullOrWhiteSpace(presentation.ConditionTemplate))
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires condition text: " + id);
            // Any brace left once the known placeholders are removed is an unknown or malformed placeholder.
            string unplaced = presentation.ConditionTemplate.Replace(ParameterPlaceholder, "").Replace(SecondaryParameterPlaceholder, "");
            if (unplaced.IndexOf('{') >= 0 || unplaced.IndexOf('}') >= 0)
                throw new InvalidOperationException("The condition template of " + presentation.name + " contains an unknown placeholder: " + id);
            if (presentation.Category == null)
                throw new InvalidOperationException("The charm presentation " + presentation.name + " requires a category: " + id);
            if (!categories.Contains(presentation.Category))
                throw new InvalidOperationException("The charm presentation " + presentation.name + " uses the category " +
                    presentation.Category.name + ", which its skin does not list: " + id);
        }

        public string GetName(ModifierDefinition definition) => Entry(definition).DisplayName;

        public string GetConditionText(ModifierDefinition definition)
        {
            ModifierPresentationAsset entry = Entry(definition);
            // Read the referenced mechanical asset, so changed parameters appear immediately.
            ModifierAsset modifier = entry.Modifier;
            return entry.ConditionTemplate
                .Replace(ParameterPlaceholder, modifier.Parameter.ToString(CultureInfo.InvariantCulture))
                .Replace(SecondaryParameterPlaceholder, modifier.SecondaryParameter.ToString(CultureInfo.InvariantCulture));
        }

        public string GetEffectText(ModifierDefinition definition)
        {
            Entry(definition);
            var text = new StringBuilder();
            foreach (ModifierEffect effect in definition.Effects)
            {
                if (text.Length > 0) text.Append(", ");
                ModifierEffectWording effectWording = wording[effect.Kind];
                text.Append('+').Append(DiagnosticText.Number(effect.Amount)).Append(' ')
                    .Append(effect.Amount == 1 ? effectWording.SingularLabel : effectWording.PluralLabel);
            }
            return text.Length == 0 ? noEffectsText : text.ToString();
        }

        /** One line per effect; a charm with more than one effect marks each line with its effect's compact label. */
        public string GetCompactEffectText(ModifierDefinition definition)
        {
            Entry(definition);
            var lines = new List<string>();
            foreach (ModifierEffect effect in definition.Effects)
                lines.Add("+" + DiagnosticText.Number(effect.Amount) +
                    (definition.Effects.Count > 1 ? " " + wording[effect.Kind].CompactLabel : ""));
            return string.Join("\n", lines);
        }

        /** The skin's timing label of the charm's phase, then of its activation limit. */
        public string GetTimingText(ModifierDefinition definition)
        {
            Entry(definition);
            return phaseWording[definition.Phase].TimingLabel + " / " + limitWording[definition.ActivationLimit].TimingLabel;
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
         * The nonzero parts of a score contribution in the skin's contribution wording, joined by "  /  "; empty when all
         * are zero. Reroll counts are written without digit grouping.
         */
        public string GetContributionText(long baseScore, long multiplier, long rerolls)
        {
            var parts = new List<string>();
            if (baseScore != 0) parts.Add("+" + DiagnosticText.Number(baseScore) + " " + contributionWording.BaseScoreLabel);
            if (multiplier != 0) parts.Add("+" + DiagnosticText.Number(multiplier) + " " + contributionWording.MultiplierLabel);
            if (rerolls != 0)
                parts.Add("+" + rerolls.ToString(CultureInfo.InvariantCulture) + " " +
                    (rerolls == 1 ? contributionWording.RerollLabel : contributionWording.RerollsLabel));
            return string.Join("  /  ", parts);
        }

        public Sprite GetIcon(ModifierDefinition definition) => Entry(definition).Icon;

        public ModifierCategoryPresentationAsset GetCategory(ModifierDefinition definition) => Entry(definition).Category;
        public Color GetEffectColor(ModifierDefinition definition) => GetCategory(definition).AccentColor;
        public Color GetInkColor(ModifierDefinition definition) => GetCategory(definition).InkColor;
        /** The mark of the charm's category; null for a category without one. */
        public Sprite GetCategorySprite(ModifierDefinition definition) => GetCategory(definition).Symbol;

        private ModifierPresentationAsset Entry(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!entries.TryGetValue(definition, out ModifierPresentationAsset entry))
                throw new InvalidOperationException("The charm " + definition.Id + " is not a charm of the loaded content.");
            return entry;
        }
    }
}
