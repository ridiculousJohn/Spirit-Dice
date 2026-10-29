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
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var authoredHost = controller.JourneyCatalog.Entries.Select(entry => entry.TableSceneAt(0)?.HostPresentation)
                .First(asset => asset != null && asset.SmokeShader != null && asset.Layers.Any(layer => layer.Smoke != null && layer.Smoke.Enabled));
            var host = new GameObject("Smoke Test", typeof(RectTransform));
            var presentation = Object.Instantiate(authoredHost);
            try
            {
                presentation.Sequences = new[]
                {
                    new SpiritPresentationSequence { Id = "Arrival", State = SpiritPresentationState.Arrival },
                    new SpiritPresentationSequence { Id = "Reaction", State = SpiritPresentationState.Reaction }
                };
                var parent = host.GetComponent<RectTransform>();
                parent.sizeDelta = presentation.ReferenceSize;
                var portrait = UnityEngine.Object.Instantiate(presentation.PortraitPrefab, parent, false);
                portrait.Build(presentation);
                var smokeLayer = presentation.Layers.First(layer => layer.Smoke != null && layer.Smoke.Enabled);
                var solidLayer = presentation.Layers.First(layer => layer.Smoke == null || !layer.Smoke.Enabled);
                var smoke = portrait.GetLayer(smokeLayer.Id);
                var solid = portrait.GetLayer(solidLayer.Id);
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
                    Assert.That(smoke.color.a, Is.EqualTo(smokeLayer.Tint.a));
                    Assert.That(solid.texture, Is.SameAs(solidLayer.Texture));
                }

                var owned = smoke.material;
                var noise = owned.GetTexture("_NoiseTex");
                var mask = owned.GetTexture("_MotionMask");
                Object.Destroy(host);
                yield return null;
                yield return null;
                Assert.That(owned == null, Is.True);
                Assert.That(noise != null && mask != null, Is.True, "Authored smoke textures remain available after a material instance is released.");
                Assert.That(smokeLayer.Texture != null, Is.True);
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(presentation);
            }
        }

    }
}
