using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * A rule presenter context over five stand-in dice that records, in order, what presenters ask of the table: host
     * reactions, die flips and rerolls (each landing after one frame), cue clips, ambient jolts, piece flares, rule badge
     * refreshes, action cues, selection text and spirit sweeps (each a realtime wait of its duration). Piece flares last
     * the configured duration.
     */
    internal sealed class TestRulePresenterContext : RulePresenterContext, IDisposable
    {
        public readonly List<string> Log = new List<string>();
        public readonly RecordingLights RecordedLights = new RecordingLights();
        private readonly bool hasLights;

        private TestRulePresenterContext(Transform[] dice, bool hasLights) : base(dice) => this.hasLights = hasLights;

        /** Seconds each piece flare reports. */
        public float FlareDuration { get; set; }

        /** Whether the table shows a prop for every piece; true unless a test removes them. */
        public bool ShowsPieces { get; set; } = true;

        public override IRuleSceneLights Lights => hasLights ? RecordedLights : null;

        public static TestRulePresenterContext Create(bool hasLights = true)
        {
            var dice = new Transform[DiceValues.Count];
            for (int i = 0; i < dice.Length; ++i) dice[i] = new GameObject("Die " + i).transform;
            return new TestRulePresenterContext(dice, hasLights);
        }

        public int Count(string entry) => Log.FindAll(value => value == entry).Count;

        public override void ReactHost() => Log.Add("react");

        public override IEnumerator FlipDie(int bay, int face) => Move("flip " + bay + " to " + face);

        public override IEnumerator RerollDie(int bay, int face) => Move("reroll " + bay + " to " + face);

        public override void PlayCue(AudioClip clip) => Log.Add("cue " + (clip != null ? clip.name : "none"));

        public override void JoltAmbient() => Log.Add("jolt");

        public override bool ShowsPiece(TablePieceDefinition.Id pieceId) => ShowsPieces;

        public override float FlarePiece(TablePieceDefinition.Id pieceId)
        {
            Log.Add("flare " + pieceId);
            return FlareDuration;
        }

        public override void ShowRuleBadge() => Log.Add("badge");

        public override void CueAction(ModifierPhase phase, Sprite icon) => Log.Add("action " + phase + " " + Name(icon));

        public override void ShowSelection(string text) => Log.Add("selection " + text);

        public override IEnumerator SweepSpirit(Sprite icon, float duration)
        {
            Log.Add("sweep " + Name(icon));
            yield return new WaitForSecondsRealtime(duration);
        }

        private static string Name(Object value) => value != null ? value.name : "none";

        public void Dispose()
        {
            foreach (var die in Dice)
                if (die != null) Object.DestroyImmediate(die.gameObject);
        }

        private IEnumerator Move(string entry)
        {
            Log.Add(entry);
            yield return null;
            Log.Add("landed");
        }

        /** Runs a presentation routine to its end, entering the routines it yields and skipping realtime waits. */
        public static void Run(IEnumerator routine)
        {
            if (routine == null) return;
            int steps = 0;
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested && !(nested is CustomYieldInstruction)) Run(nested);
                if (++steps >= 100) throw new InvalidOperationException("The routine did not end.");
            }
        }

        /** The realtime waits a routine yields, in seconds, entering the routines it yields. */
        public static List<float> Waits(IEnumerator routine)
        {
            var waits = new List<float>();
            void Walk(IEnumerator current)
            {
                while (current.MoveNext())
                {
                    if (current.Current is WaitForSecondsRealtime wait) waits.Add(wait.waitTime);
                    else if (current.Current is IEnumerator nested) Walk(nested);
                }
            }
            if (routine != null) Walk(routine);
            return waits;
        }

        internal readonly struct RecordedFlare : IEquatable<RecordedFlare>
        {
            public readonly int Index;
            public readonly float Peak, Rise, Hold, Fall;

            public RecordedFlare(int index, float peak, float rise, float hold, float fall)
            {
                Index = index;
                Peak = peak;
                Rise = rise;
                Hold = hold;
                Fall = fall;
            }

            public bool Equals(RecordedFlare other)
                => Index == other.Index && Peak == other.Peak && Rise == other.Rise && Hold == other.Hold && Fall == other.Fall;

            public override bool Equals(object obj) => obj is RecordedFlare other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Index, Peak, Rise, Hold, Fall);
            public override string ToString() => "light " + Index + " peak " + Peak + " rise " + Rise + " hold " + Hold + " fall " + Fall;
        }

        internal sealed class RecordingLights : IRuleSceneLights
        {
            public readonly List<RecordedFlare> Flares = new List<RecordedFlare>();
            public int LightCount => SceneLightingAsset.MaximumLights;
            public void Flare(int index, float peak, float rise, float hold, float fall)
                => Flares.Add(new RecordedFlare(index, peak, rise, hold, fall));
        }
    }
}
