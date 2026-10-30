using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    /** A scene figure's outcome animation, on a synthetic figure whose idle and outcome clips move two different parts. */
    public sealed class EncounterOutcomeAnimationTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        /**
         * Checks that an outcome without a clip plays nothing, that the outcome's clip plays while the idle keeps moving
         * its own part, and that resetting stops the outcome clip at its first frame with the idle still playing.
         */
        [Test]
        public void TheOutcomeClipPlaysOverTheIdleOnlyForItsOutcomeAndResetReturnsToTheIdle()
        {
            var idleCurve = AnimationCurve.Linear(0f, 0f, 4f, 1f);
            var outcomeCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.5f, 1f), new Keyframe(1f, 0f));
            var root = Own(new GameObject("Figure"));
            Transform idlePart = Part(root, "Idle Part"), outcomePart = Part(root, "Outcome Part");
            AnimationClip idle = Clip("Synthetic Idle", WrapMode.Loop, "Idle Part", idleCurve);
            AnimationClip outcome = Clip("Synthetic Outcome", WrapMode.Once, "Outcome Part", outcomeCurve);
            var animation = root.AddComponent<Animation>();
            animation.AddClip(idle, idle.name);
            animation.clip = idle;
            var figure = root.AddComponent<EncounterOutcomeAnimation>();
            var serialized = new SerializedObject(figure);
            serialized.FindProperty("figure").objectReferenceValue = animation;
            var clips = serialized.FindProperty("outcomeClips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).FindPropertyRelative("Outcome").intValue = (int)EncounterOutcome.Lost;
            clips.GetArrayElementAtIndex(0).FindPropertyRelative("Clip").objectReferenceValue = outcome;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            figure.Validate();
            // The figure idles as its Animation plays its default clip automatically in play.
            Assert.That(animation.Play(), Is.True);
            AnimationState idleState = animation[idle.name];
            Advance(animation, 1f);

            figure.Play(EncounterOutcome.Won);
            Assert.That(figure.IsPlayingOutcome, Is.False, "An outcome without a clip plays nothing.");
            Advance(animation, .25f);
            Assert.That(outcomePart.localPosition.x, Is.Zero, "Nothing moves the outcome part.");
            Assert.That(idlePart.localPosition.x, Is.EqualTo(idleCurve.Evaluate(1.25f)).Within(.0001f));

            figure.Play(EncounterOutcome.Lost);
            Assert.That(figure.IsPlayingOutcome, Is.True, "The outcome's clip plays.");
            Advance(animation, .25f);
            Assert.That(outcomePart.localPosition.x, Is.EqualTo(outcomeCurve.Evaluate(.25f)).Within(.0001f),
                "The outcome part follows the outcome clip from its start.");
            Assert.That(animation.IsPlaying(idle.name), Is.True, "The idle keeps playing under the outcome clip.");
            Assert.That(idlePart.localPosition.x, Is.EqualTo(idleCurve.Evaluate(idleState.time)).Within(.0001f),
                "The idle keeps moving its own part at full weight.");

            figure.ResetToIdle();
            Assert.That(figure.IsPlayingOutcome, Is.False, "Resetting stops the outcome clip.");
            Assert.That(outcomePart.localPosition.x, Is.EqualTo(outcomeCurve.Evaluate(0f)).Within(.0001f),
                "The outcome part returns to the outcome clip's first frame.");
            Assert.That(animation.IsPlaying(idle.name), Is.True, "The figure is left in its idle.");
            Advance(animation, .25f);
            Assert.That(outcomePart.localPosition.x, Is.EqualTo(outcomeCurve.Evaluate(0f)).Within(.0001f));
            Assert.That(idlePart.localPosition.x, Is.EqualTo(idleCurve.Evaluate(idleState.time)).Within(.0001f));
        }

        /** Advances every playing state by the step and applies them, as the player loop does in play. */
        private static void Advance(Animation animation, float step)
        {
            foreach (AnimationState state in animation)
                if (state.enabled) state.time += step;
            animation.Sample();
        }

        private Transform Part(GameObject root, string name)
        {
            var part = new GameObject(name).transform;
            part.SetParent(root.transform, false);
            return part;
        }

        /** A legacy clip that moves the named part along x by the curve and holds it at 0 on y and z. */
        private AnimationClip Clip(string name, WrapMode wrapMode, string part, AnimationCurve x)
        {
            var clip = Own(new AnimationClip { legacy = true, name = name, wrapMode = wrapMode });
            clip.SetCurve(part, typeof(Transform), "localPosition.x", x);
            clip.SetCurve(part, typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, x.keys[x.length - 1].time, 0f));
            clip.SetCurve(part, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, x.keys[x.length - 1].time, 0f));
            return clip;
        }

        private T Own<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
