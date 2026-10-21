using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    public sealed class EncounterAmbientActivityTests
    {
        private const float Step = 1f / 60f;
        private const string TablePath = "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantUnquietCartTable.asset";
        private const string CupPrefabPath = "Assets/Dice/Content/Presentation/Ambient/HangingCup.prefab";
        private const string PreviewScenePath = "Assets/Scenes/AmbientMotionPreview.unity";
        private const string PropsFolder = "Assets/Dice/Art/HauntedMerchant/Props/";
        private const string RelightFolder = "Assets/Dice/Art/HauntedMerchant/Relight/";
        private const string ResponsePath = "Assets/Dice/Art/HauntedMerchant/MerchantLanternResponse.png";
        private const string LitShaderPath = "Assets/Dice/Presentation/Shared/SceneLitImage.shader";
        private const float PlateWidth = 941f, PlateHeight = 1672f;
        private static readonly (string Id, string Name)[] PropActors =
        {
            ("hanging-pot", "HangingPot"), ("hanging-pan", "HangingPan"), ("shelf-bottle", "ShelfBottle"), ("trunk-lid", "TrunkLid"),
        };
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void IdleOutagesFollowTheSeededScheduleAndRestoreExactlyFullIntensity()
        {
            var actor = new AmbientLightActor { LightIndex = 1 };
            var asset = Activity(new[] { actor }, Array.Empty<AmbientPropActor>());
            var lighting = new RecordingLighting(2);
            var activity = new EncounterAmbientActivity(asset, lighting, Array.Empty<EncounterAmbientPropBinding>());
            activity.Begin(12345);
            Assert.That(lighting.Values[1], Is.EqualTo(1f), "The light starts on.");
            Run(activity, lighting, 900f);

            Assert.That(lighting.Writes.Where(write => write.Index == 0), Is.Empty, "Only the authored light is driven.");
            var writes = lighting.Writes.Where(write => write.Index == 1).ToList();
            Assert.That(writes.All(write => write.Value >= 0f && write.Value <= 1f), Is.True);
            var outages = new List<(double Start, double End, double Dark)>();
            double start = -1, dark = 0;
            for (int i = 0; i < writes.Count; ++i)
            {
                if (start < 0 && writes[i].Value < 1f) start = writes[i].Time;
                if (writes[i].Value == 0f && i + 1 < writes.Count) dark = writes[i + 1].Time - writes[i].Time;
                if (start >= 0 && writes[i].Value == 1f)
                {
                    outages.Add((start, writes[i].Time, dark));
                    start = -1;
                }
            }
            Assert.That(outages.Count, Is.GreaterThanOrEqualTo(15));
            double previousEnd = 0;
            foreach (var outage in outages)
            {
                Assert.That(outage.Start - previousEnd, Is.InRange(20 - 2 * Step, 40 + 2 * Step), "Idle interval");
                Assert.That(outage.Dark, Is.InRange(1.5f - 2 * Step, 4f + 2 * Step), "Dark duration");
                Assert.That(outage.End - outage.Start, Is.LessThanOrEqualTo(4f + 12 * .14f + 2 * Step), "Outage length");
                previousEnd = outage.End;
            }
            Assert.That(lighting.Values[1] == 1f || start >= 0, Is.True, "Between outages the light rests at exactly one.");

            var repeat = new RecordingLighting(2);
            var again = new EncounterAmbientActivity(asset, repeat, Array.Empty<EncounterAmbientPropBinding>());
            again.Begin(12345);
            Run(again, repeat, 900f);
            Assert.That(repeat.Writes, Is.EqualTo(lighting.Writes), "The presentation random is seeded at activation.");
            var other = new RecordingLighting(2);
            var different = new EncounterAmbientActivity(asset, other, Array.Empty<EncounterAmbientPropBinding>());
            different.Begin(54321);
            Run(different, other, 900f);
            Assert.That(other.Writes, Is.Not.EqualTo(lighting.Writes));
        }

        [Test]
        public void StoppingDuringAnOutageRestoresFullIntensityAndRestPoses()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1f, 1f) };
            var prop = Prop("Cup");
            var asset = Activity(new[] { light }, new[] { PropActor("cup", Swing(10f)) });
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) });
            activity.Begin(9);
            RunUntil(activity, lighting, () => lighting.Values[0] == 0f, 10f);
            activity.Disturb();
            Run(activity, lighting, .2f);
            Assert.That(prop.localRotation, Is.Not.EqualTo(Quaternion.identity));
            activity.Stop();
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prop.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(activity.IsRunning, Is.False);
            Assert.That(activity.IsAtRest, Is.True);
            int writes = lighting.Writes.Count;
            Run(activity, lighting, 60f);
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes), "A stopped activity stays still.");
        }

        [Test]
        public void DisturbanceStuttersTheLightAndJoltsExactlyOneProp()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1000f, 1000f) };
            var swing = Swing(10f);
            string[] ids = { "a", "b", "c" };
            var asset = Activity(new[] { light }, ids.Select(id => PropActor(id, swing)).ToArray());
            var struck = new HashSet<string>();
            for (int seed = 1; seed <= 8; ++seed)
            {
                var props = ids.Select(Prop).ToArray();
                var lighting = new RecordingLighting(1);
                var activity = new EncounterAmbientActivity(asset, lighting, ids.Select((id, i) => Binding(id, props[i])).ToArray());
                activity.Disturb();
                Assert.That(lighting.Writes, Is.Empty, "Nothing happens before the first hand.");
                activity.Begin(seed);
                Run(activity, lighting, 1f);
                int before = lighting.Writes.Count;
                activity.Disturb();
                Run(activity, lighting, .1f);
                var moved = props.Where(prop => prop.localRotation != Quaternion.identity).ToArray();
                Assert.That(moved, Has.Length.EqualTo(1), "Seed " + seed);
                struck.Add(moved[0].name);
                Run(activity, lighting, 1f);
                var stutter = lighting.Writes.Skip(before).ToList();
                Assert.That(stutter.Min(write => write.Value), Is.LessThan(1f), "The light stutters.");
                Assert.That(stutter.Last().Value, Is.EqualTo(1f));
                for (int i = 0; i + 1 < stutter.Count; ++i)
                    Assert.That(stutter[i + 1].Time - stutter[i].Time, Is.LessThanOrEqualTo(.14f + 2 * Step), "A stutter never holds dark.");
                Assert.That(stutter.Last().Time - stutter.First().Time, Is.LessThanOrEqualTo(4 * .14f + 2 * Step));
                Run(activity, lighting, 30f);
                Assert.That(props.All(prop => prop.localRotation == Quaternion.identity && prop.localPosition == Vector3.zero), Is.True);
                Assert.That(activity.IsAtRest, Is.True);
            }
            Assert.That(struck.Count, Is.GreaterThan(1), "The jolted prop is chosen at random.");
        }

        [Test]
        public void VictorySettlesLightsAndPropsAndStopsIdleEvents()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(2f, 3f) };
            var prop = Prop("Cup");
            var asset = Activity(new[] { light },
                new[] { PropActor("cup", Swing(10f), new AmbientRange(1f, 2f)) });
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) });
            activity.Begin(7);
            RunUntil(activity, lighting, () => lighting.Values[0] == 0f, 10f);
            activity.Disturb();
            Run(activity, lighting, .2f);
            Assert.That(prop.localRotation, Is.Not.EqualTo(Quaternion.identity));

            int settled = lighting.Writes.Count;
            activity.Settle();
            Assert.That(activity.IsRunning, Is.False);
            Run(activity, lighting, 1f + 2 * Step);
            var recovery = lighting.Writes.Skip(settled).Select(write => write.Value).ToArray();
            Assert.That(recovery, Is.Ordered, "Lights return steadily, without flicker.");
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Run(activity, lighting, 5f);
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prop.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(activity.IsAtRest, Is.True);

            int writes = lighting.Writes.Count;
            activity.Disturb();
            Run(activity, lighting, 120f);
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes), "Idle events and disturbances stop after victory.");
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void DefeatKnocksOnePropHardOnceAndStops()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1000f, 1000f) };
            var swing = Swing(10f);
            var asset = Activity(new[] { light }, new[] { PropActor("a", swing), PropActor("b", swing) });
            float disturbed = PeakAfter(asset, activity => activity.Disturb());
            float knocked = PeakAfter(asset, activity => activity.Knock());
            Assert.That(knocked / disturbed, Is.EqualTo(asset.DefeatStrength / asset.DisturbanceStrength).Within(.001f),
                "The final knock strikes harder than a disturbance.");

            var props = new[] { Prop("a"), Prop("b") };
            var lighting = new RecordingLighting(1);
            var knock = new EncounterAmbientActivity(asset, lighting, new[] { Binding("a", props[0]), Binding("b", props[1]) });
            knock.Begin(4);
            Run(knock, lighting, .5f);
            knock.Knock();
            Assert.That(knock.IsRunning, Is.False);
            var peaks = new float[2];
            for (float elapsed = 0; elapsed < 60f; elapsed += Step)
            {
                lighting.Clock += Step;
                knock.Tick(Step);
                for (int i = 0; i < props.Length; ++i)
                    peaks[i] = Mathf.Max(peaks[i], Mathf.Abs(Mathf.DeltaAngle(0f, props[i].localEulerAngles.z)));
            }
            Assert.That(peaks.Count(peak => peak > 0f), Is.EqualTo(1), "Exactly one prop knocks.");
            Assert.That(props.All(prop => prop.localRotation == Quaternion.identity), Is.True);
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Assert.That(knock.IsAtRest, Is.True);
            knock.Knock();
            knock.Disturb();
            int writes = lighting.Writes.Count;
            Run(knock, lighting, 60f);
            Assert.That(props.All(prop => prop.localRotation == Quaternion.identity), Is.True, "The knock happens once.");
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes));
        }

        private static IEnumerable<TestCaseData> MotionKinds()
        {
            yield return new TestCaseData("Swing", Vector3.zero, Vector3.zero)
                .SetName("SwingIsAdditiveOverAnAnimatedPoseAndReturnsToRest");
            // The default wobble contacts sit a quarter of the width in from each side of the base.
            yield return new TestCaseData("Wobble", new Vector3(-10f, -30f, 0f), new Vector3(10f, -30f, 0f))
                .SetName("WobbleIsAdditiveOverAnAnimatedPoseAndReturnsToRest");
            yield return new TestCaseData("Thump", Vector3.zero, Vector3.zero)
                .SetName("ThumpIsAdditiveOverAnAnimatedPoseAndReturnsToRest");
        }

        [TestCaseSource(nameof(MotionKinds))]
        public void MotionKindIsAdditiveOverAnAnimatedPoseAndReturnsToRest(string kind, Vector3 leftPivot, Vector3 rightPivot)
        {
            AmbientMotionAsset asset = kind == "Swing" ? Swing(10f)
                : kind == "Wobble" ? (AmbientMotionAsset)Track(ScriptableObject.CreateInstance<AmbientWobbleMotionAsset>())
                : Track(ScriptableObject.CreateInstance<AmbientThumpMotionAsset>());
            var animated = Prop("Animated");
            var reference = Prop("Reference");
            var onAnimated = asset.Create(animated);
            var onReference = asset.Create(reference);
            onAnimated.Strike(.1, 1.5f);
            onReference.Strike(.1, 1.5f);
            float peakAngle = 0f, peakOffset = 0f, leftmost = 0f, rightmost = 0f;
            Quaternion animatedRotation = Quaternion.identity;
            Vector3 animatedPosition = Vector3.zero;
            for (double time = 0; time < 15; time += Step)
            {
                // An idle animation rewrites the pose every frame before the motion applies.
                animatedRotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(2f * Mathf.PI * (float)time / 4f));
                animatedPosition = new Vector3(5f + 2f * Mathf.Sin((float)time), 7f, 0f);
                animated.localRotation = animatedRotation;
                animated.localPosition = animatedPosition;
                onAnimated.Apply(time);
                onReference.Apply(time);

                // A counterclockwise turn stands on the left contact, a clockwise one on the right.
                float turn = Mathf.DeltaAngle(0f, reference.localEulerAngles.z);
                Vector3 pivot = turn >= 0f ? leftPivot : rightPivot;
                leftmost = Mathf.Max(leftmost, turn);
                rightmost = Mathf.Min(rightmost, turn);
                Quaternion offsetRotation = Quaternion.Inverse(animatedRotation) * animated.localRotation;
                Assert.That(Quaternion.Angle(offsetRotation, reference.localRotation), Is.LessThan(.01f), "Rotation at " + time);
                Vector3 offset = reference.localPosition + reference.localRotation * pivot - pivot;
                Vector3 expected = animatedPosition + animatedRotation * pivot + offset;
                Assert.That(Vector3.Distance(animated.localPosition + animated.localRotation * pivot, expected), Is.LessThan(.001f),
                    "The motion's pivot follows the animated pose at " + time);
                peakAngle = Mathf.Max(peakAngle, Quaternion.Angle(Quaternion.identity, reference.localRotation));
                peakOffset = Mathf.Max(peakOffset, offset.magnitude);
                if (kind == "Thump") Assert.That(offset.y, Is.GreaterThanOrEqualTo(-.0001f), "A thump hops upward.");
            }
            Assert.That(peakAngle, Is.GreaterThan(1f), kind + " moves the prop.");
            if (kind != "Thump") Assert.That(leftmost > 1f && rightmost < -1f, Is.True, kind + " turns both ways.");
            if (kind == "Thump") Assert.That(peakOffset, Is.GreaterThan(5f));
            else Assert.That(peakOffset, Is.LessThan(.001f), kind + " keeps its pivot in place.");
            Assert.That(onAnimated.IsMoving || onAnimated.IsOffset || onReference.IsMoving || onReference.IsOffset, Is.False);
            Assert.That(Quaternion.Angle(animated.localRotation, animatedRotation), Is.LessThan(.0001f),
                "The animated pose is left as animated.");
            Assert.That(Vector3.Distance(animated.localPosition, animatedPosition), Is.LessThan(.00001f));
            Assert.That(reference.localRotation, Is.EqualTo(Quaternion.identity), "A still prop returns exactly to rest.");
            Assert.That(reference.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MerchantTableAuthorsLanternOutagesAndTheHangingCupSwing()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            Assert.That(table, Is.Not.Null);
            var activity = table.AmbientActivity;
            Assert.That(activity, Is.Not.Null);
            Assert.DoesNotThrow(activity.Validate);
            var lantern = activity.Lights.Single();
            Assert.That(lantern.LightIndex, Is.Zero, "Light 0 is the merchant's fixed lantern.");
            Assert.That(table.SceneLighting.Lights.Length, Is.GreaterThan(lantern.LightIndex));
            Assert.That(table.PieceLights.All(pieceLight => pieceLight.LightIndex != lantern.LightIndex), Is.True,
                "The lantern is not the candle's piece light.");
            Assert.That(lantern.IdleInterval.Min, Is.EqualTo(20f));
            Assert.That(lantern.IdleInterval.Max, Is.EqualTo(40f));
            Assert.That(lantern.DarkDuration.Min, Is.EqualTo(1.5f));
            Assert.That(lantern.DarkDuration.Max, Is.EqualTo(4f));
            Assert.That(lantern.StutterFlickers.Max, Is.LessThan(lantern.SputterFlickers.Max + 1));
            var cup = activity.Props.Single(actor => actor.Id == "hanging-cup");
            var swing = cup.Motion as AmbientSwingMotionAsset;
            Assert.That(swing, Is.Not.Null, "The hanging cup swings.");
            Assert.That(swing.Amplitude * cup.Strength * activity.DisturbanceStrength, Is.GreaterThan(3f),
                "A disturbance swings harder than the cup's three-degree idle sway.");
            Assert.That(cup.IdleInterval.Min, Is.GreaterThan(0f));
            Assert.That(activity.DefeatStrength, Is.GreaterThan(activity.DisturbanceStrength));
        }

        [Test]
        public void MerchantSceneShowsTheHangingCupAtItsPreviewPlacementBoundToTheActivity()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            Vector2 previewPosition, previewParentSize, previewAnchor;
            var preview = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            try
            {
                var previewCup = (RectTransform)FindCup(preview.GetRootGameObjects()).transform;
                previewPosition = previewCup.anchoredPosition;
                previewAnchor = previewCup.anchorMin;
                previewParentSize = ((RectTransform)previewCup.parent).sizeDelta;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var bindings = (List<EncounterAmbientPropBinding>)typeof(SpiritEncounterView)
                    .GetField("ambientProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                foreach (var actor in table.AmbientActivity.Props)
                    Assert.That(bindings.Any(binding => binding.ActorId == actor.Id && binding.Target != null), Is.True, actor.Id);
                var cup = (RectTransform)bindings.Single(binding => binding.ActorId == "hanging-cup").Target;
                Assert.That(cup.gameObject, Is.SameAs(FindCup(roots)));
                Assert.That(PrefabUtility.GetOutermostPrefabInstanceRoot(cup.gameObject), Is.SameAs(cup.gameObject),
                    "The swing turns the cup's root about its pin, over the prefab's own sway.");
                Assert.That(cup.gameObject.activeSelf, Is.True);
                Assert.That(cup.IsChildOf(view.Portrait), Is.True);
                Assert.That(cup.anchorMin, Is.EqualTo(previewAnchor));
                Assert.That(cup.anchorMax, Is.EqualTo(previewAnchor));
                Assert.That(cup.anchoredPosition, Is.EqualTo(previewPosition));
                var parent = (RectTransform)cup.parent;
                Assert.That(parent.sizeDelta, Is.EqualTo(previewParentSize), "The cup's frame matches the preview plate.");
                // The canopy draws first, the cup over it, and the merchant over both; his layers are clear at the cup.
                Assert.That(parent.Find("Canopy").GetSiblingIndex(), Is.LessThan(cup.GetSiblingIndex()));
                Assert.That(parent.Find("Merchant").GetSiblingIndex(), Is.GreaterThan(cup.GetSiblingIndex()));
                Assert.DoesNotThrow(() => new EncounterAmbientActivity(table.AmbientActivity,
                    new RecordingLighting(table.SceneLighting.Lights.Length), bindings));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void MerchantTableAuthorsTheMovablePropsWithTheirMotionKinds()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var activity = table.AmbientActivity;
            Assert.DoesNotThrow(activity.Validate);
            var cuts = ReadPropCuts();
            Assert.That(activity.Props.Select(actor => actor.Id),
                Is.EquivalentTo(PropActors.Select(prop => prop.Id).Append("hanging-cup")), "The cup stays an actor beside the props.");
            float eventsPerSecond = 0f;
            foreach (var actor in activity.Props)
            {
                // No single prop is busy, but together something moves every several seconds.
                Assert.That(actor.IdleInterval.Min, Is.GreaterThanOrEqualTo(15f), actor.Id);
                eventsPerSecond += 2f / (actor.IdleInterval.Min + actor.IdleInterval.Max);
            }
            Assert.That(1f / eventsPerSecond, Is.InRange(3f, 8f), "Mean seconds between idle prop events.");
            foreach (var (id, name) in PropActors)
            {
                var actor = activity.Props.Single(candidate => candidate.Id == id);
                var cut = cuts.Single(candidate => candidate.Name == name);
                Assert.That(actor.Strength, Is.GreaterThan(0f), id);
                if (name == "ShelfBottle")
                {
                    var wobble = actor.Motion as AmbientWobbleMotionAsset;
                    Assert.That(wobble, Is.Not.Null, "The bottle rocks on its base.");
                    Vector2[] contacts = cut.Contacts.Select(contact => cut.Normalized(contact)).ToArray();
                    Assert.That(Vector2.Distance(wobble.LeftContact, contacts[0]), Is.LessThan(.0001f));
                    Assert.That(Vector2.Distance(wobble.RightContact, contacts[1]), Is.LessThan(.0001f));
                }
                else if (name == "TrunkLid") Assert.That(actor.Motion, Is.InstanceOf<AmbientThumpMotionAsset>(), "The lid thumps.");
                else Assert.That(actor.Motion, Is.InstanceOf<AmbientSwingMotionAsset>(), name + " swings from its ring.");

                // Idle jolts stay near the cut's suggested motion; a disturbance reads clearly on a phone.
                bool lift = name == "TrunkLid";
                float suggested = lift ? cut.SuggestedLift * 960f / 1672f : cut.SuggestedDegrees;
                float idle = PeakResponse(actor.Motion, actor.Strength * actor.IdleStrength.Max, lift);
                float disturbance = PeakResponse(actor.Motion, actor.Strength * activity.DisturbanceStrength, lift);
                Assert.That(idle / suggested, Is.InRange(.5f, 1.1f), name + " idle peak " + idle);
                Assert.That(disturbance / suggested, Is.GreaterThanOrEqualTo(2.5f), name + " disturbance peak " + disturbance);
            }
        }

        [Test]
        public void MerchantScenePlacesTheMovablePropsOverTheirRemovedRegionsBoundToTheActivity()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var cuts = ReadPropCuts();
            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var bindings = (List<EncounterAmbientPropBinding>)typeof(SpiritEncounterView)
                    .GetField("ambientProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Transform frame = FindCup(roots).transform.parent;
                Assert.That(frame.name, Is.EqualTo("Merchant Scene"));
                foreach (var (id, name) in PropActors)
                    Assert.That(bindings.Single(binding => binding.ActorId == id).Target, Is.SameAs(frame.Find(name)), id);
                foreach (float width in new[] { 540f, 443f })
                {
                    view.Portrait.anchorMin = view.Portrait.anchorMax = new Vector2(.5f, .5f);
                    view.Portrait.sizeDelta = new Vector2(width, 960);
                    view.Portrait.anchoredPosition = Vector2.zero;
                    // A preview scene neither sizes nor scales its overlay canvas, so the test gives it unit scale and recalculates rects.
                    view.Canvas.transform.localScale = Vector3.one;
                    foreach (var rect in view.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    RectTransform front = view.TableImage.rectTransform, back = view.RoomImage.rectTransform;
                    // Props register to the encounter's background images, which show the room texture across both halves.
                    AssertPropsPlaced(frame, cuts, table, world =>
                    {
                        Vector2 uv = SpiritTablePieceLayout.TextureCoordinate(front.InverseTransformPoint(world), front.rect,
                            view.TableImage.uvRect, back.InverseTransformPoint(world), back.rect, view.RoomImage.uvRect);
                        return new Vector2(uv.x * PlateWidth, (1f - uv.y) * PlateHeight);
                    });
                }
                Assert.That(frame.Find("Canopy").GetSiblingIndex(), Is.LessThan(FindCup(roots).transform.GetSiblingIndex()));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PreviewScenePlacesTheMovablePropsOverTheirRemovedRegions()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var cuts = ReadPropCuts();
            var scene = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                Transform frame = FindCup(roots).transform.parent;
                var plate = frame.GetComponent<RawImage>();
                Assert.That(plate, Is.Not.Null, "The preview's props register to its background plate.");
                Assert.That(plate.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                AssertPropsPlaced(frame, cuts, table, world =>
                {
                    Rect bounds = plate.rectTransform.rect;
                    Vector3 local = plate.rectTransform.InverseTransformPoint(world);
                    return new Vector2((local.x - bounds.xMin) / bounds.width * PlateWidth,
                        (bounds.yMax - local.y) / bounds.height * PlateHeight);
                });
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void MerchantScenesLightTheHangingCupWithTheLanternAtItsRestRect()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            foreach (string path in new[] { AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID), PreviewScenePath })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var cup = (RectTransform)FindCup(scene.GetRootGameObjects()).transform;
                    var frame = (RectTransform)cup.parent;
                    var image = cup.GetComponentsInChildren<RawImage>(true).Single();
                    Assert.That(image.rectTransform.parent.name, Is.EqualTo("Swing"), path);
                    Assert.That(image.raycastTarget, Is.False, path);
                    Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)), path);
                    Assert.That(AssetDatabase.GetAssetPath(image.texture), Is.EqualTo(RelightFolder + "HangingCupUnlit.png"), path);
                    Assert.That(image.material.shader, Is.SameAs(AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath)), path);
                    Assert.That(image.material.GetTexture("_LayerMask"), Is.Null, "The cup has no ownership mask.");
                    Assert.That(image.material.GetFloat("_InvertMask"), Is.EqualTo(1), "Without a mask the inverted selection covers the cup.");
                    Assert.That(image.material.GetTexture("_SourceTex"), Is.SameAs(image.texture), path);

                    // The reusable prefab keeps only its animation and image; the scene instance adds the lighting.
                    var lit = image.GetComponent<SceneLitImage>();
                    Assert.That(lit, Is.Not.Null, path);
                    Assert.That(PrefabUtility.IsAddedComponentOverride(lit), Is.True, path);
                    Assert.That(lit.enabled, Is.True, path);
                    Assert.That(lit.Lighting, Is.SameAs(table.SceneLighting), path);
                    Assert.DoesNotThrow(lit.Layer.Validate);
                    Assert.That(AssetDatabase.GetAssetPath(lit.Layer.BaseTexture), Is.EqualTo(RelightFolder + "HangingCupUnlit.png"), path);
                    var lantern = lit.Layer.Lights.Single();
                    Assert.That(lantern.LightIndex, Is.Zero, "Only the lantern lights the cup; no computed candle light.");
                    Assert.That(lantern.Mode, Is.EqualTo(SceneLitLightMode.Reference), path);
                    Assert.That(AssetDatabase.GetAssetPath(lantern.Reference), Is.EqualTo(RelightFolder + "HangingCupLanternLit.png"), path);
                    Assert.That(AssetDatabase.GetAssetPath(lantern.ResponseMap), Is.EqualTo(ResponsePath), path);

                    // The response map is a smooth distance field, so the swinging cup samples it at its rest rect.
                    Assert.That(cup.Find("Swing").localRotation, Is.EqualTo(Quaternion.identity), "The scene holds the cup at rest.");
                    foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    Rect bounds = frame.rect;
                    Assert.That(bounds.size, Is.EqualTo(new Vector2(540, 960)), path + " frame is the plate's 540 x 960 frame.");
                    // Frame-local, so the result does not depend on the scale a preview scene gives its canvas.
                    Vector2 Plate(Vector2 point)
                    {
                        Vector3 local = point;
                        for (Transform node = image.rectTransform; node != frame; node = node.parent)
                            local = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale).MultiplyPoint3x4(local);
                        return new Vector2((local.x - bounds.xMin) / bounds.width * PlateWidth, (bounds.yMax - local.y) / bounds.height * PlateHeight);
                    }
                    Rect imageRect = image.rectTransform.rect;
                    Vector2 topLeft = Plate(new Vector2(imageRect.xMin, imageRect.yMax)), bottomRight = Plate(new Vector2(imageRect.xMax, imageRect.yMin));
                    var mapRect = new Rect(topLeft.x / PlateWidth, 1f - bottomRight.y / PlateHeight,
                        (bottomRight.x - topLeft.x) / PlateWidth, (bottomRight.y - topLeft.y) / PlateHeight);
                    Assert.That(Vector4.Distance(new Vector4(lit.Layer.MapRect.x, lit.Layer.MapRect.y, lit.Layer.MapRect.width,
                        lit.Layer.MapRect.height), new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)), Is.LessThan(.00001f),
                        path + " samples the plate's response map under the cup's rest rect " + mapRect.ToString("F6"));
                    // The material carries the same rectangle for an image no component lights.
                    Vector4 materialRect = image.material.GetVector("_SceneLitMapRect");
                    Assert.That(Vector4.Distance(materialRect, new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)),
                        Is.LessThan(.00001f), path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [Test]
        public void MovablePropsCompositeOverThePatchedPlatesIntoTheInstalledPlates()
        {
            foreach (var plates in new[] { (Cut: "Unlit", Patched: "MerchantAmbientUnlitPropsRemoved.png", Installed: "MerchantAmbientUnlit.png"),
                (Cut: "LanternLit", Patched: "MerchantLanternOnlyPropsRemoved.png", Installed: "MerchantLanternOnly.png") })
            {
                var patched = Decode(PropsFolder + plates.Patched);
                var installed = Decode(PropsFolder + "../" + plates.Installed);
                try
                {
                    Assert.That(new Vector2Int(installed.width, installed.height), Is.EqualTo(new Vector2Int((int)PlateWidth, (int)PlateHeight)));
                    Color32[] before = installed.GetPixels32(), after = patched.GetPixels32();
                    var cuts = ReadPropCuts().OrderBy(cut => cut.DrawOrder).ToList();
                    // Plate coverage of every piece drawn after each piece; the cut completes an earlier piece where a later one hides it.
                    var coveredLater = new bool[cuts.Count][];
                    var covered = new bool[before.Length];
                    for (int index = cuts.Count - 1; index >= 0; --index)
                    {
                        coveredLater[index] = (bool[])covered.Clone();
                        var cover = Decode(PropsFolder + cuts[index].Name + plates.Cut + ".png");
                        try
                        {
                            Color32[] coverPixels = cover.GetPixels32();
                            for (int row = 0; row < cover.height; ++row)
                                for (int column = 0; column < cover.width; ++column)
                                    if (coverPixels[row * cover.width + column].a > 0)
                                        covered[(installed.height - 1 - (cuts[index].Crop.y + cover.height - 1 - row)) * installed.width
                                            + cuts[index].Crop.x + column] = true;
                        }
                        finally { Object.DestroyImmediate(cover); }
                    }
                    for (int index = 0; index < cuts.Count; ++index)
                    {
                        var cut = cuts[index];
                        var piece = Decode(PropsFolder + cut.Name + plates.Cut + ".png");
                        try
                        {
                            Assert.That(new Vector2Int(piece.width, piece.height), Is.EqualTo(cut.Crop.size), cut.Name);
                            Color32[] pixels = piece.GetPixels32();
                            int opaque = 0, removed = 0;
                            for (int row = 0; row < piece.height; ++row)
                                for (int column = 0; column < piece.width; ++column)
                                {
                                    Color32 pixel = pixels[row * piece.width + column];
                                    if (pixel.a < 255) continue;
                                    // Texture rows start at the bottom; crop rows start at the top.
                                    int plateIndex = (installed.height - 1 - (cut.Crop.y + piece.height - 1 - row)) * installed.width
                                        + cut.Crop.x + column;
                                    // Completed pixels behind a later piece show only through its soft edge and need not match the plate.
                                    if (coveredLater[index][plateIndex]) continue;
                                    Color32 expected = before[plateIndex];
                                    Assert.That(Math.Max(Math.Abs(pixel.r - expected.r), Math.Max(Math.Abs(pixel.g - expected.g),
                                        Math.Abs(pixel.b - expected.b))), Is.LessThanOrEqualTo(2),
                                        $"{cut.Name} {plates.Cut} at crop ({column}, {piece.height - 1 - row})");
                                    ++opaque;
                                    Color32 behind = after[plateIndex];
                                    if (Math.Max(Math.Abs(behind.r - expected.r), Math.Max(Math.Abs(behind.g - expected.g),
                                        Math.Abs(behind.b - expected.b))) > 2) ++removed;
                                }
                            Assert.That(opaque, Is.GreaterThan(500), cut.Name);
                            Assert.That(removed, Is.GreaterThan(opaque / 2), cut.Name + " is removed from the patched plate.");
                        }
                        finally { Object.DestroyImmediate(piece); }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(patched);
                    Object.DestroyImmediate(installed);
                }
            }
        }

        private static void AssertPropsPlaced(Transform frame, IReadOnlyList<PropCut> cuts, EncounterTablePresentationAsset table,
            Func<Vector3, Vector2> plate)
        {
            Transform canopy = frame.Find("Canopy"), merchant = frame.Find("Merchant");
            Transform cup = frame.Cast<Transform>().Single(child =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == CupPrefabPath);
            Assert.That(canopy, Is.Not.Null);
            Assert.That(merchant, Is.Not.Null);
            int previous = -1;
            foreach (var cut in cuts.OrderBy(candidate => candidate.DrawOrder))
            {
                var prop = frame.Find(cut.Name) as RectTransform;
                Assert.That(prop, Is.Not.Null, cut.Name);
                Assert.That(prop.gameObject.activeSelf, Is.True, cut.Name);
                // Props draw over the background in the cut's order (the pot behind the pan), under the canopy, cup and merchant.
                Assert.That(prop.GetSiblingIndex(), Is.GreaterThan(previous), cut.Name);
                previous = prop.GetSiblingIndex();
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(canopy.GetSiblingIndex()), cut.Name);
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(cup.GetSiblingIndex()), cut.Name);
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(merchant.GetSiblingIndex()), cut.Name);

                var image = prop.GetComponent<RawImage>();
                Assert.That(image, Is.Not.Null, cut.Name);
                Assert.That(image.raycastTarget, Is.False, cut.Name + " must not intercept input.");
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(AssetDatabase.GetAssetPath(image.texture), Is.EqualTo(PropsFolder + cut.Name + "Unlit.png"));
                Assert.That(image.material.shader, Is.SameAs(AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath)), cut.Name);
                Assert.That(image.material.GetTexture("_LayerMask"), Is.Null, "Props have no ownership mask.");
                Assert.That(image.material.GetFloat("_InvertMask"), Is.EqualTo(1), "Without a mask the inverted selection covers the prop.");
                Assert.That(image.material.GetTexture("_SourceTex"), Is.SameAs(image.texture));

                var lit = prop.GetComponent<SceneLitImage>();
                Assert.That(lit, Is.Not.Null, cut.Name);
                Assert.That(lit.enabled, Is.True);
                Assert.That(lit.Lighting, Is.SameAs(table.SceneLighting));
                Assert.DoesNotThrow(lit.Layer.Validate);
                Assert.That(AssetDatabase.GetAssetPath(lit.Layer.BaseTexture), Is.EqualTo(PropsFolder + cut.Name + "Unlit.png"));
                var lantern = lit.Layer.Lights.Single();
                Assert.That(lantern.LightIndex, Is.Zero, "Only the lantern lights the props; no computed candle light.");
                Assert.That(lantern.Mode, Is.EqualTo(SceneLitLightMode.Reference));
                Assert.That(AssetDatabase.GetAssetPath(lantern.Reference), Is.EqualTo(PropsFolder + cut.Name + "LanternLit.png"));
                Assert.That(AssetDatabase.GetAssetPath(lantern.ResponseMap), Is.EqualTo(ResponsePath));
                var mapRect = new Rect(cut.Crop.x / PlateWidth, 1f - (cut.Crop.y + cut.Crop.height) / PlateHeight,
                    cut.Crop.width / PlateWidth, cut.Crop.height / PlateHeight);
                Assert.That(Vector4.Distance(new Vector4(lit.Layer.MapRect.x, lit.Layer.MapRect.y, lit.Layer.MapRect.width,
                    lit.Layer.MapRect.height), new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)), Is.LessThan(.00001f),
                    cut.Name + " samples the plate's response map under its crop.");

                // The pivot is the cut's hang point, base centre or hinge, so motion turns and lifts about it.
                Assert.That(Vector2.Distance(prop.pivot, cut.Normalized(cut.Pivot)), Is.LessThan(.00001f), cut.Name);
                Assert.That(Vector2.Distance(plate(prop.position), cut.Pivot), Is.LessThan(.1f), cut.Name + " pivot");
                var corners = new Vector3[4];
                prop.GetWorldCorners(corners);
                Vector2 topLeft = plate(corners[1]), bottomRight = plate(corners[3]);
                Assert.That(Vector2.Distance(topLeft, cut.Crop.position), Is.LessThan(.1f), cut.Name + " top-left " + topLeft);
                Assert.That(Vector2.Distance(bottomRight, cut.Crop.max), Is.LessThan(.1f), cut.Name + " bottom-right " + bottomRight);

                // Every solid pixel of the cut lands inside the placed rectangle, so the prop covers its removed region at rest.
                var piece = Decode(PropsFolder + cut.Name + "Unlit.png");
                try
                {
                    Color32[] pixels = piece.GetPixels32();
                    int solid = 0;
                    for (int row = 0; row < piece.height; ++row)
                        for (int column = 0; column < piece.width; ++column)
                        {
                            if (pixels[row * piece.width + column].a <= 127) continue;
                            ++solid;
                            var point = new Vector2(cut.Crop.x + column + .5f, cut.Crop.y + piece.height - 1 - row + .5f);
                            Assert.That(point.x > topLeft.x && point.x < bottomRight.x && point.y > topLeft.y && point.y < bottomRight.y,
                                Is.True, cut.Name + " pixel " + point);
                        }
                    Assert.That(solid, Is.GreaterThan(500), cut.Name);
                }
                finally { Object.DestroyImmediate(piece); }
            }
        }

        private static float PeakResponse(AmbientMotionAsset asset, float strength, bool lift)
        {
            var target = new GameObject("Peak", typeof(RectTransform));
            try
            {
                ((RectTransform)target.transform).sizeDelta = new Vector2(40f, 60f);
                var motion = asset.Create(target.transform);
                motion.Strike(0, strength);
                float peak = 0f;
                for (double time = 0; time < 10; time += Step / 4f)
                {
                    motion.Apply(time);
                    peak = Mathf.Max(peak, lift ? target.transform.localPosition.y
                        : Mathf.Abs(Mathf.DeltaAngle(0f, target.transform.localEulerAngles.z)));
                }
                return peak;
            }
            finally { Object.DestroyImmediate(target); }
        }

        private static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        private static List<PropCut> ReadPropCuts()
        {
            var json = JObject.Parse(File.ReadAllText(PropsFolder + "props.json"));
            Assert.That(json["source_plates"]["size"].Values<float>(), Is.EqualTo(new[] { PlateWidth, PlateHeight }));
            var cuts = new List<PropCut>();
            foreach (var piece in json["pieces"])
            {
                var crop = piece["crop_rect"];
                var pivot = piece["pivot"]["plate"].Values<float>().ToArray();
                var contacts = piece["pivot"]["base_contacts_plate"];
                var motion = piece["motion"];
                cuts.Add(new PropCut
                {
                    Name = (string)piece["name"],
                    DrawOrder = (int)piece["draw_order"],
                    Crop = new RectInt((int)crop["x"], (int)crop["y"], (int)crop["w"], (int)crop["h"]),
                    Pivot = new Vector2(pivot[0], pivot[1]),
                    Contacts = contacts == null ? Array.Empty<Vector2>() : contacts
                        .Select(point => new Vector2((float)point[0], (float)point[1])).ToArray(),
                    SuggestedDegrees = (float?)motion["rotate_degrees"] ?? 0f,
                    SuggestedLift = (float?)motion["lift_px"] ?? 0f,
                });
            }
            Assert.That(cuts.Select(cut => cut.Name), Is.EquivalentTo(PropActors.Select(prop => prop.Name)));
            return cuts;
        }

        private sealed class PropCut
        {
            public string Name;
            public int DrawOrder;
            /** Plate pixels, top-left origin. */
            public RectInt Crop;
            public Vector2 Pivot;
            public Vector2[] Contacts;
            public float SuggestedDegrees;
            public float SuggestedLift;

            /** Plate point as normalized bottom-left coordinates of the crop, like a RectTransform pivot. */
            public Vector2 Normalized(Vector2 point)
                => new Vector2((point.x - Crop.x) / Crop.width, 1f - (point.y - Crop.y) / Crop.height);
        }

        private static GameObject FindCup(GameObject[] roots)
            => roots.SelectMany(root => root.GetComponentsInChildren<Animation>(true))
                .Single(animation => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animation.gameObject) == CupPrefabPath)
                .gameObject;

        private float PeakAfter(EncounterAmbientActivityAsset asset, Action<EncounterAmbientActivity> strike)
        {
            var props = new[] { Prop("a"), Prop("b") };
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("a", props[0]), Binding("b", props[1]) });
            activity.Begin(4);
            Run(activity, lighting, .5f);
            strike(activity);
            Run(activity, lighting, .25f);
            return props.Max(prop => Mathf.Abs(Mathf.DeltaAngle(0f, prop.localEulerAngles.z)));
        }

        private static void Run(EncounterAmbientActivity activity, RecordingLighting lighting, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; ++i)
            {
                lighting.Clock += Step;
                activity.Tick(Step);
            }
        }

        private static void RunUntil(EncounterAmbientActivity activity, RecordingLighting lighting, Func<bool> condition, float limit)
        {
            for (float elapsed = 0; elapsed < limit; elapsed += Step)
            {
                lighting.Clock += Step;
                activity.Tick(Step);
                if (condition()) return;
            }
            Assert.Fail("The condition was not reached within " + limit + " seconds.");
        }

        private EncounterAmbientActivityAsset Activity(AmbientLightActor[] lights, AmbientPropActor[] props)
        {
            var asset = Track(ScriptableObject.CreateInstance<EncounterAmbientActivityAsset>());
            Set(asset, "lights", lights);
            Set(asset, "props", props);
            return asset;
        }

        private AmbientSwingMotionAsset Swing(float amplitude)
        {
            var asset = Track(ScriptableObject.CreateInstance<AmbientSwingMotionAsset>());
            Set(asset, "amplitude", amplitude);
            return asset;
        }

        private static AmbientPropActor PropActor(string id, AmbientMotionAsset motion, AmbientRange? interval = null)
            => new AmbientPropActor { Id = id, Motion = motion, IdleInterval = interval ?? new AmbientRange(1000f, 1000f) };

        private static EncounterAmbientPropBinding Binding(string id, Transform target)
            => new EncounterAmbientPropBinding { ActorId = id, Target = target };

        private RectTransform Prop(string name)
        {
            var prop = Track(new GameObject(name, typeof(RectTransform)));
            var rect = (RectTransform)prop.transform;
            rect.sizeDelta = new Vector2(40f, 60f);
            return rect;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class RecordingLighting : IAmbientLightingTarget
        {
            public readonly float[] Values;
            public readonly List<(double Time, int Index, float Value)> Writes = new List<(double Time, int Index, float Value)>();
            public double Clock;

            public RecordingLighting(int count) => Values = Enumerable.Repeat(1f, count).ToArray();

            public int LightCount => Values.Length;
            public float GetLightIntensity(int index) => Values[index];

            public void SetLightIntensity(int index, float multiplier)
            {
                Assert.That(float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f, Is.False);
                Values[index] = multiplier;
                Writes.Add((Clock, index, multiplier));
            }
        }
    }
}
