using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * One kind of rule-supplied dice visual and its authored tuning, referenced by a rule's presentation card. Each kind
     * creates its own runtime presenter, so a new kind needs no change to the cards, the encounter screen or score playback.
     */
    public abstract class RuleDiceVisualAsset : ScriptableObject
    {
        /** Creates an independent presenter for the rule with this id; it builds its per-die objects under the context's dice. */
        public abstract RuleDiceVisualPresenter Create(string ruleId, RuleDiceVisualContext context);
    }

    /** What a rule's dice visuals may use: the tray's five dice, the host's reaction and timed raises of the scene lights. */
    public sealed class RuleDiceVisualContext
    {
        public RuleDiceVisualContext(IReadOnlyList<Transform> dice, Action hostReaction, IRuleSceneLights lights)
        {
            if (dice == null || dice.Count != DiceValues.Count)
                throw new ArgumentException("Rule dice visuals require the tray's five dice.", nameof(dice));
            for (int i = 0; i < dice.Count; ++i)
                if (dice[i] == null) throw new ArgumentException("Rule dice visuals require the tray's five dice.", nameof(dice));
            Dice = dice;
            HostReaction = hostReaction;
            Lights = lights;
        }

        /** The die in each tray slot; per-die objects created as their children follow every roll, flip and rest pose. */
        public IReadOnlyList<Transform> Dice { get; }
        /** Plays the host's reaction; null when the encounter has no host. */
        public Action HostReaction { get; }
        /** The encounter's scene lights; null when the encounter has no scene lighting. */
        public IRuleSceneLights Lights { get; }
    }

    /**
     * A rule's dice visuals for the lifetime of the encounter table's presentation. Sync shows a snapshot's state, without
     * animation on configure and resume, animated when settling after a resolution. Score playback calls Present for every
     * event after its own presentation of that event and waits for the returned routine, if any.
     */
    public abstract class RuleDiceVisualPresenter : IDisposable
    {
        /** Shows the state of the snapshot, or nothing for a null snapshot or one that is not in play. */
        public abstract void Sync(EncounterSnapshot snapshot, bool animate);

        /** Presents one resolution event; returns the routine the playback waits for, or null when there is nothing to wait for. */
        public abstract IEnumerator Present(RuleEvent ruleEvent);

        /** Destroys the per-die objects; the presenter is not used afterwards. */
        public abstract void Dispose();
    }
}
