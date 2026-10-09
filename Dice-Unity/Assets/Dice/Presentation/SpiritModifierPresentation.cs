using System;
using System.Collections.Generic;
using System.Text;
using Dice.Engine;

namespace Dice.Presentation
{
    /** Shared charm identity and wording, independent of modifier mechanics. */
    public static class SpiritModifierPresentation
    {
        private readonly struct Identity
        {
            internal readonly string Name;
            internal readonly SpiritModifierArtwork.Kind Artwork;
            internal readonly Func<IModifierCondition, string> ConditionText;

            internal Identity(string name, SpiritModifierArtwork.Kind artwork,
                Func<IModifierCondition, string> conditionText)
            {
                Name = name;
                Artwork = artwork;
                ConditionText = conditionText;
            }
        }

        private static readonly Dictionary<string, Identity> identities = new Dictionary<string, Identity>(StringComparer.Ordinal)
        {
            { "modifier.initial_roll.initial_pair.base_score", new Identity("Welcoming Pair", SpiritModifierArtwork.Kind.WelcomingPair,
                condition => condition is InitialPairCondition ? "Your initial roll is a Pair." : null) },
            { "modifier.player_reroll.selected_face_unchanged.rerolls", new Identity("Second Chance", SpiritModifierArtwork.Kind.SecondChance,
                condition => condition is SelectedFaceUnchangedCondition ? "At least one rerolled die keeps its face." : null) },
            { "modifier.player_reroll.last_reroll_on_last_hand.combined", new Identity("Last Lantern", SpiritModifierArtwork.Kind.LastLantern,
                condition => condition is LastRerollOnLastHandCondition ? "Spend your last reroll on your last hand." : null) },
            { "modifier.play_hand.no_extreme_faces.base_score", new Identity("Quiet Grove", SpiritModifierArtwork.Kind.QuietGrove,
                condition => condition is NoExtremeFacesCondition value
                    ? "No die shows " + value.Parameter + " or " + value.SecondaryParameter + "." : null) },
            { "modifier.play_hand.contains_face.multiplier", new Identity("One Spark", SpiritModifierArtwork.Kind.OneSpark,
                condition => condition is ContainsFaceCondition value ? "At least one die shows " + value.Parameter + "." : null) },
            { "modifier.play_hand.middle_unique.combined", new Identity("Lone Watcher", SpiritModifierArtwork.Kind.LoneWatcher,
                condition => condition is MiddleUniqueCondition ? "The middle die has a face no other die shows." : null) }
        };

        public static string GetName(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (identities.TryGetValue(definition.Id, out Identity identity)) return identity.Name;
            string typeName = definition.Condition.GetType().Name;
            if (typeName.EndsWith("Condition", StringComparison.Ordinal)) typeName = typeName.Substring(0, typeName.Length - 9);
            return DiagnosticText.Humanize(typeName);
        }

        public static string GetConditionText(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (identities.TryGetValue(definition.Id, out Identity identity))
            {
                string text = identity.ConditionText(definition.Condition);
                if (text != null) return text;
            }
            return DiagnosticText.DescribeCondition(definition.Condition);
        }

        public static string GetEffectText(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!identities.ContainsKey(definition.Id)) return DiagnosticText.DescribeEffects(definition);
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
                case ModifierPhase.InitialRoll: phase = "On initial roll"; break;
                case ModifierPhase.PlayerReroll: phase = "After rerolling"; break;
                case ModifierPhase.PlayHand: phase = "When played"; break;
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

        internal static bool TryGetArtwork(string id, out SpiritModifierArtwork.Kind artwork)
        {
            if (identities.TryGetValue(id, out Identity identity))
            {
                artwork = identity.Artwork;
                return true;
            }
            artwork = default;
            return false;
        }
    }
}
