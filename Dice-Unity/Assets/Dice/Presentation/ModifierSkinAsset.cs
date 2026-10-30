using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * How one effect kind is worded: "+N " and the singular label when N is 1, otherwise the plural label. The compact
     * label marks each effect of a charm with more than one effect in compact effect lists.
     */
    [Serializable]
    public sealed class ModifierEffectWording
    {
        [SerializeField] private ModifierEffectKind kind;
        [SerializeField] private string singularLabel;
        [SerializeField] private string pluralLabel;
        [SerializeField] private string compactLabel;

        public ModifierEffectKind Kind => kind;
        public string SingularLabel => singularLabel;
        public string PluralLabel => pluralLabel;
        public string CompactLabel => compactLabel;

        public ModifierEffectWording(ModifierEffectKind kind, string singularLabel, string pluralLabel, string compactLabel)
        {
            this.kind = kind;
            this.singularLabel = singularLabel;
            this.pluralLabel = pluralLabel;
            this.compactLabel = compactLabel;
        }
    }

    /**
     * How one phase is worded: its timing label names it in timing text ("label / limit label"), and its description
     * lead opens a charm's description. A limit the phase implies, such as once per hand for a phase that runs once per
     * hand, adds no note to the description.
     */
    [Serializable]
    public sealed class ModifierPhaseWording
    {
        [SerializeField] private ModifierPhase phase;
        [SerializeField] private string timingLabel;
        [SerializeField] private string descriptionLead;
        [SerializeField] private ModifierActivationLimit[] impliedLimits = Array.Empty<ModifierActivationLimit>();

        public ModifierPhase Phase => phase;
        public string TimingLabel => timingLabel;
        public string DescriptionLead => descriptionLead;
        public IReadOnlyList<ModifierActivationLimit> ImpliedLimits => impliedLimits;

        public ModifierPhaseWording(ModifierPhase phase, string timingLabel, string descriptionLead,
            params ModifierActivationLimit[] impliedLimits)
        {
            this.phase = phase;
            this.timingLabel = timingLabel;
            this.descriptionLead = descriptionLead;
            this.impliedLimits = impliedLimits ?? Array.Empty<ModifierActivationLimit>();
        }
    }

    /**
     * How one activation limit is worded: its timing label follows the phase label in timing text, and its description
     * note closes a charm's description unless the charm's phase implies the limit. An empty note adds nothing.
     */
    [Serializable]
    public sealed class ModifierActivationLimitWording
    {
        [SerializeField] private ModifierActivationLimit limit;
        [SerializeField] private string timingLabel;
        [SerializeField] private string descriptionNote;

        public ModifierActivationLimit Limit => limit;
        public string TimingLabel => timingLabel;
        public string DescriptionNote => descriptionNote;

        public ModifierActivationLimitWording(ModifierActivationLimit limit, string timingLabel, string descriptionNote)
        {
            this.limit = limit;
            this.timingLabel = timingLabel;
            this.descriptionNote = descriptionNote;
        }
    }

    /**
     * How score playback words a contribution: "+N " and the base score label, "+N " and the multiplier label, and
     * "+N " and the reroll label when N is 1, otherwise the rerolls label.
     */
    [Serializable]
    public sealed class ModifierContributionWording
    {
        [SerializeField] private string baseScoreLabel;
        [SerializeField] private string multiplierLabel;
        [SerializeField] private string rerollLabel;
        [SerializeField] private string rerollsLabel;

        public string BaseScoreLabel => baseScoreLabel;
        public string MultiplierLabel => multiplierLabel;
        public string RerollLabel => rerollLabel;
        public string RerollsLabel => rerollsLabel;

        public ModifierContributionWording(string baseScoreLabel, string multiplierLabel, string rerollLabel, string rerollsLabel)
        {
            this.baseScoreLabel = baseScoreLabel;
            this.multiplierLabel = multiplierLabel;
            this.rerollLabel = rerollLabel;
            this.rerollsLabel = rerollsLabel;
        }
    }

    /**
     * How charm text joins the skin's labels and amounts. An effect, a labeled compact effect and a contribution part
     * fill {amount} and {label}; an unlabeled compact effect fills {amount}; timing text fills {phase} and {limit} with
     * the phase's and the activation limit's timing labels. The separators join a charm's effects, its compact effect
     * lines and a contribution's parts.
     */
    [Serializable]
    public sealed class ModifierTextFormat
    {
        [SerializeField] private string effect;
        [SerializeField] private string effectSeparator;
        [SerializeField] private string compactEffect;
        [SerializeField] private string labeledCompactEffect;
        [SerializeField] private string compactEffectSeparator;
        [SerializeField] private string timing;
        [SerializeField] private string contribution;
        [SerializeField] private string contributionSeparator;

        public string Effect => effect;
        public string EffectSeparator => effectSeparator;
        public string CompactEffect => compactEffect;
        public string LabeledCompactEffect => labeledCompactEffect;
        public string CompactEffectSeparator => compactEffectSeparator;
        public string Timing => timing;
        public string Contribution => contribution;
        public string ContributionSeparator => contributionSeparator;

        public ModifierTextFormat(string effect, string effectSeparator, string compactEffect, string labeledCompactEffect,
            string compactEffectSeparator, string timing, string contribution, string contributionSeparator)
        {
            this.effect = effect;
            this.effectSeparator = effectSeparator;
            this.compactEffect = compactEffect;
            this.labeledCompactEffect = labeledCompactEffect;
            this.compactEffectSeparator = compactEffectSeparator;
            this.timing = timing;
            this.contribution = contribution;
            this.contributionSeparator = contributionSeparator;
        }
    }

    /**
     * The category shown for every charm whose effects are of exactly these kinds, however many effects of each kind it
     * has. The effect kinds are a set: each kind is listed once, and no kinds is the category of a charm without effects.
     */
    [Serializable]
    public sealed class ModifierEffectsCategory
    {
        [SerializeField] private ModifierEffectKind[] effects = Array.Empty<ModifierEffectKind>();
        [SerializeField] private ModifierCategoryPresentationAsset category;

        public IReadOnlyList<ModifierEffectKind> Effects => effects;
        public ModifierCategoryPresentationAsset Category => category;

        public ModifierEffectsCategory(ModifierCategoryPresentationAsset category, params ModifierEffectKind[] effects)
        {
            this.category = category;
            this.effects = effects ?? Array.Empty<ModifierEffectKind>();
        }
    }

    /** How a charm family is shown: the name it is shown under and its artwork, shared by every variant of the family. */
    [Serializable]
    public sealed class ModifierFamilyPresentation
    {
        [SerializeField] private ModifierFamilyAsset family;
        [SerializeField] private string name;
        [Tooltip("The family's artwork, shown for every variant of the family wherever a charm is shown; loading fails without one.")]
        [SerializeField] private Sprite icon;

        public ModifierFamilyAsset Family => family;
        public string Name => name;
        public Sprite Icon => icon;

        public ModifierFamilyPresentation(ModifierFamilyAsset family, string name, Sprite icon)
        {
            this.family = family;
            this.name = name;
            this.icon = icon;
        }
    }

    /**
     * One theme's charm presentation: a presentation for every charm the content uses, the category style shown for each
     * set of effect kinds, the name and artwork of every charm family, the wording of every effect kind, phase and
     * activation limit, the wording of score contributions and the format that joins them. Loading validates it against
     * the loaded content.
     */
    [CreateAssetMenu(menuName = "Dice/Modifier Skin")]
    public sealed class ModifierSkinAsset : ScriptableObject
    {
        [SerializeField] private ModifierPresentationAsset[] modifiers = Array.Empty<ModifierPresentationAsset>();
        [SerializeField] private ModifierEffectsCategory[] categories = Array.Empty<ModifierEffectsCategory>();
        [SerializeField] private ModifierFamilyPresentation[] families = Array.Empty<ModifierFamilyPresentation>();
        [SerializeField] private ModifierEffectWording[] effectWording = Array.Empty<ModifierEffectWording>();
        /** Shown as the effect text of a charm without effects. */
        [SerializeField] private string noEffectsText;
        [SerializeField] private ModifierPhaseWording[] phaseWording = Array.Empty<ModifierPhaseWording>();
        [SerializeField] private ModifierActivationLimitWording[] limitWording = Array.Empty<ModifierActivationLimitWording>();
        [SerializeField] private ModifierContributionWording contributionWording;
        [SerializeField] private ModifierTextFormat textFormat;

        public IReadOnlyList<ModifierPresentationAsset> Modifiers => modifiers;
        public IReadOnlyList<ModifierEffectsCategory> Categories => categories;
        public IReadOnlyList<ModifierFamilyPresentation> Families => families;
        public IReadOnlyList<ModifierEffectWording> EffectWording => effectWording;
        public string NoEffectsText => noEffectsText;
        public IReadOnlyList<ModifierPhaseWording> PhaseWording => phaseWording;
        public IReadOnlyList<ModifierActivationLimitWording> LimitWording => limitWording;
        public ModifierContributionWording ContributionWording => contributionWording;
        public ModifierTextFormat TextFormat => textFormat;
    }
}
