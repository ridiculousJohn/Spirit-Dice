using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * One kind of rule presentation and its authored tuning, referenced by a rule's presentation card. Each kind creates
     * its own runtime presenter, so a new kind needs no change to the cards, the encounter screen or score playback.
     */
    public abstract class RulePresenterAsset : ScriptableObject
    {
        /**
         * Creates an independent presenter for the card's rule; the card supplies the rule's wording and artwork. Fails
         * when the rule is not the kind this asset presents.
         */
        public abstract RulePresenter Create(EncounterRulePresentationAsset card, EncounterRuleDefinition rule,
            RulePresenterContext context);
    }

    /** A presenter asset for one kind of rule definition; any other kind of rule fails at creation. */
    public abstract class RulePresenterAsset<TRule> : RulePresenterAsset where TRule : EncounterRuleDefinition
    {
        public sealed override RulePresenter Create(EncounterRulePresentationAsset card, EncounterRuleDefinition rule,
            RulePresenterContext context)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!(rule is TRule typed))
                throw new InvalidOperationException("The rule presenter " + name + " presents " + typeof(TRule).Name +
                    " rules, but the rule of the card " + card.name + " is a " + rule.GetType().Name + ".");
            return Create(typed, context);
        }

        /** Creates the presenter of one rule of this kind; presenters of a rule kind need only its definition. */
        protected abstract RulePresenter Create(TRule rule, RulePresenterContext context);
    }

    /**
     * What rule presentation may use from the encounter table, nothing rule-specific: the tray's five dice, the host's
     * reaction and action cue, the scene lights, turning or rerolling any number of dice together, the encounter's cue
     * voices, the ambient activity's jolt, the table pieces' flares, and the HUD's rule badge, selection line and spirit
     * sweep. Different dice may animate at the same time, by one presenter or several, or alongside the tray's own roll;
     * a die cannot be moved by two animations at once, and the tray throws when a flip or reroll claims a die another
     * animation is moving.
     */
    public abstract class RulePresenterContext
    {
        protected RulePresenterContext(IReadOnlyList<Transform> dice)
        {
            if (dice == null || dice.Count != DiceValues.Count)
                throw new ArgumentException("Rule presenters require the tray's five dice.", nameof(dice));
            for (int i = 0; i < dice.Count; ++i)
                if (dice[i] == null) throw new ArgumentException("Rule presenters require the tray's five dice.", nameof(dice));
            Dice = dice;
        }

        /** The die in each tray slot; per-die objects created as their children follow every roll, flip and rest pose. */
        public IReadOnlyList<Transform> Dice { get; }

        /** The encounter's scene lights; null when the encounter has no scene lighting. */
        public abstract IRuleSceneLights Lights { get; }

        /** Plays the host's reaction; does nothing when the encounter has no host. */
        public abstract void ReactHost();

        /**
         * Turns the masked dice over together, each to its face in faces; the routine ends when they land. Only the masked
         * entries of faces are used.
         */
        public abstract IEnumerator FlipDice(int mask, DiceValues faces);

        /**
         * Rerolls the masked dice together, each to its face in faces, with the tray's roll sound for that many dice; the
         * routine ends when they land. Only the masked entries of faces are used.
         */
        public abstract IEnumerator RerollDice(int mask, DiceValues faces);

        /** Plays the clip on the encounter's cue voices, under the encounter audio's mute, focus and pause. */
        public abstract void PlayCue(AudioClip clip);

        /** Jolts the table's ambient activity; does nothing when the table authors none. */
        public abstract void JoltAmbient();

        /** Whether the table shows a prop for the table piece. */
        public abstract bool ShowsPiece(TablePieceDefinition.Id pieceId);

        /**
         * Flares the prop of the table piece and returns how long the flare lasts; zero while the prop is
         * hidden. The table must show the prop.
         */
        public abstract float FlarePiece(TablePieceDefinition.Id pieceId);

        /** Shows the HUD's rule badge as the resolution being presented leaves its rule. */
        public abstract void ShowRuleBadge();

        /**
         * Marks the button of the phase's action with the host's cue for one host gesture, showing the icon, or the host's
         * sigil without one.
         */
        public abstract void CueAction(ModifierPhase phase, Sprite icon);

        /** Shows the text on the HUD's selection line in the theme's highlight colour. */
        public abstract void ShowSelection(string text);

        /**
         * Sweeps a rule spirit, showing the icon, or the host's sigil without one, across the tray over the duration; the
         * routine ends when it has crossed.
         */
        public abstract IEnumerator SweepSpirit(Sprite icon, float duration);
    }

    /**
     * A rule's presentation for the lifetime of the encounter table's presentation. Sync shows a snapshot's state,
     * without animation on configure and resume, animated when settling after a resolution. For every event score playback
     * first waits the longest lead-in any presenter asks for, presents the event's core changes, then runs every
     * presenter's routine for the event at once and waits for all of them; events a presenter claims during the roll before
     * them start with that roll instead (PresentsDuringRoll).
     */
    public abstract class RulePresenter : IDisposable
    {
        /** Shows the state of the snapshot, or nothing for a null snapshot or one that is not in play. */
        public virtual void Sync(EncounterSnapshot snapshot, bool animate) { }

        /**
         * Shows the dice selection the player sees, as a mask of bays; told once when the presenter is created and again
         * whenever the selection changes, including when playback clears it.
         */
        public virtual void SelectionChanged(int selectedMask) { }

        /** Seconds playback pauses before presenting the event; zero when the presenter needs no pause. */
        public virtual float LeadIn(RuleEvent ruleEvent) => 0f;

        /**
         * Whether the presenter presents the event while the dice of the roll before it are in the air. Playback starts the
         * routines of the events right after a roll that a presenter claims together with that roll and waits for all of
         * them; a claimed event gets no lead-in and no core presentation of its own, so an event the core presents cannot be
         * claimed.
         */
        public virtual bool PresentsDuringRoll(RuleEvent ruleEvent) => false;

        /** Presents one resolution event; returns the routine the playback waits for, or null when there is nothing to present. */
        public abstract IEnumerator Present(RuleEvent ruleEvent);

        /** Destroys anything the presenter created; the presenter is not used afterwards. */
        public virtual void Dispose() { }
    }
}
