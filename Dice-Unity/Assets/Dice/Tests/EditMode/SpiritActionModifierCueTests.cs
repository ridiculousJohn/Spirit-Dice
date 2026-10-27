using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritActionModifierCueTests
    {
        /** One action button's cue group and the label the cue crossfades with. */
        private sealed class ActionParts
        {
            public CanvasGroup Group;
            public CanvasGroup Label;
        }

        /**
         * Checks a cue that is cleared, or replaced by a cue on the other action, returns its button label to the opacity it
         * had when the cue appeared, and a new cue starts transparent on its phase's button / fails if a faded label stays
         * faded, the replaced cue stays visible, or the new cue starts opaque or on the wrong button.
         */
        [TestCase(false)]
        [TestCase(true)]
        public void ClearingOrRetriggeringRestoresTheLabelOpacityCapturedWhenTheCueAppeared(bool retrigger)
        {
            using var ui = new SyntheticUi();
            var cue = NewCue(ui, out var reroll, out var play);
            play.Label.alpha = .8f;
            play.Group.alpha = reroll.Group.alpha = 1;
            cue.Show(null, ModifierPhase.PlayHand, 1);
            Assert.That(cue.IsPlayVisible, Is.True);
            Assert.That(cue.IsRerollVisible, Is.False);
            Assert.That(play.Group.alpha, Is.Zero, "A cue fades in from transparent.");
            // The label as the crossfade leaves it while the cue shows.
            play.Label.alpha = .1f;

            if (retrigger) cue.Show(null, ModifierPhase.PlayerReroll, 1);
            else cue.Clear();

            Assert.That(play.Label.alpha, Is.EqualTo(.8f), "The faded label returns to its opacity before the cue.");
            Assert.That(cue.IsPlayVisible, Is.False);
            Assert.That(cue.IsRerollVisible, Is.EqualTo(retrigger));
            Assert.That(cue.ActivePhase, Is.EqualTo(retrigger ? ModifierPhase.PlayerReroll : (ModifierPhase?)null));
            Assert.That(reroll.Label.alpha, Is.EqualTo(1f));
            if (retrigger) Assert.That(reroll.Group.alpha, Is.Zero, "The replacing cue fades in from transparent.");
        }

        private static SpiritActionModifierCue NewCue(SyntheticUi ui, out ActionParts reroll, out ActionParts play)
        {
            var cue = ui.Add<SpiritActionModifierCue>(ui.Root, "Action Cue");
            reroll = Author(ui, cue, "reroll");
            play = Author(ui, cue, "play");
            cue.Initialize(null, null);
            return cue;
        }

        private static ActionParts Author(SyntheticUi ui, SpiritActionModifierCue cue, string action)
        {
            var button = ui.Rect(cue.transform, action + " button");
            var group = ui.Add<CanvasGroup>(button, action + " cue");
            var artwork = ui.Add<Image>(group.transform, "Artwork");
            var label = ui.Add<CanvasGroup>(button, action + " label");
            SyntheticUi.Set(cue, action + ".Group", group);
            SyntheticUi.Set(cue, action + ".Artwork", artwork);
            SyntheticUi.Set(cue, action + ".Sigil", ui.Add<SpiritModifierSigilGraphic>(group.transform, "Sigil"));
            SyntheticUi.Set(cue, action + ".Category", ui.CategoryBadge(group.transform));
            SyntheticUi.Set(cue, action + ".Border", artwork.gameObject.AddComponent<Outline>());
            SyntheticUi.Set(cue, action + ".Label", label);
            return new ActionParts { Group = group, Label = label };
        }
    }
}
