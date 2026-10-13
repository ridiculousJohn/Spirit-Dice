using System.Collections;
using System.Linq;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class SpiritSmokePlayTests
    {
        [UnityTest]
        public IEnumerator SmokePhaseSurvivesPoseResetsVisibilityAndPresentationStates()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var authoredHost = controller.JourneyCatalog.Entries.Select(entry => entry.TablePresentation?.HostPresentation)
                .First(asset => asset != null && asset.SmokeShader != null);
            var host = new GameObject("Smoke Test", typeof(RectTransform));
            var presentation = ScriptableObject.CreateInstance<SpiritHostPresentationAsset>();
            try
            {
                presentation.SmokeShader = authoredHost.SmokeShader;
                var parent = host.GetComponent<RectTransform>();
                parent.sizeDelta = presentation.ReferenceSize;
                presentation.Layers = new[]
                {
                    new SpiritPortraitLayer { Id = "Smoke", Texture = Texture2D.whiteTexture, Size = new Vector2(40f, 60f),
                        Tint = new Color(1f, 1f, 1f, .4f), Smoke = new SpiritPortraitSmoke { Enabled = true } },
                    new SpiritPortraitLayer { Id = "Solid", Texture = Texture2D.whiteTexture, Size = new Vector2(40f, 60f) }
                };
                presentation.Sequences = new[]
                {
                    new SpiritPresentationSequence { Id = "Arrival", State = SpiritPresentationState.Arrival },
                    new SpiritPresentationSequence { Id = "Reaction", State = SpiritPresentationState.Reaction }
                };
                var portrait = host.AddComponent<SpiritHostPortrait>();
                portrait.Build(parent, presentation);
                var smoke = portrait.GetLayer("Smoke");
                var solid = portrait.GetLayer("Solid");
                Assert.That(smoke.material.shader.name, Is.EqualTo("Dice/Spirit Smoke"));
                Assert.That(solid.material.shader.name, Is.Not.EqualTo(smoke.material.shader.name));
                portrait.Advance(.25f);
                float elapsed = .25f;

                foreach (var state in new[] { SpiritPresentationState.Waiting, SpiritPresentationState.Arrival,
                    SpiritPresentationState.Idle, SpiritPresentationState.Reaction })
                {
                    portrait.SetState(state, .5f);
                    Assert.That(smoke.material.GetFloat("_EffectTime"), Is.EqualTo(elapsed).Within(.0001f));
                    portrait.Advance(.25f);
                    elapsed += .25f;
                    Assert.That(smoke.material.GetFloat("_EffectTime"), Is.EqualTo(elapsed).Within(.0001f));
                    if (state == SpiritPresentationState.Arrival)
                    {
                        portrait.SetState(state, .9f);
                        Assert.That(smoke.material.GetFloat("_EffectTime"), Is.EqualTo(elapsed).Within(.0001f));
                    }
                    portrait.ResetPose();
                    Assert.That(smoke.material.GetFloat("_EffectTime"), Is.EqualTo(elapsed).Within(.0001f));
                    portrait.SetVisible(false);
                    portrait.Advance(.5f);
                    portrait.SetVisible(true);
                    Assert.That(smoke.material.GetFloat("_EffectTime"), Is.EqualTo(elapsed).Within(.0001f));
                    Assert.That(smoke.color.a, Is.EqualTo(presentation.Layers[0].Tint.a));
                    Assert.That(solid.texture, Is.SameAs(presentation.Layers[1].Texture));
                }

                var owned = smoke.material;
                var noise = owned.GetTexture("_NoiseTex");
                var mask = owned.GetTexture("_MotionMask");
                Object.Destroy(host);
                yield return null;
                yield return null;
                Assert.That(owned == null, Is.True);
                Assert.That(noise == null && mask == null, Is.True);
                Assert.That(presentation.Layers[0].Texture != null, Is.True);
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(presentation);
            }
        }

    }
}
