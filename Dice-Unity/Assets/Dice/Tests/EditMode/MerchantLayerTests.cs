using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dice.Tests
{
    [Explicit("Art-generation check; run it by name while the merchant art is being changed.")]
    [Category("ArtAuthoring")]
    public sealed class MerchantLayerTests
    {
        private const string MaterialPath = "Assets/Dice/Content/Presentation/Ambient/";

        /**
         * The installed footprint and response map are the preserved sources scaled down about the candle point
         * (Tools/Presentation/author-candle-light-footprint.py): a tighter pool, unchanged at the flame, whose brightest
         * texels and distance minimum stay on the candle, and every level of which shrinks by the same factor.
         */
        [Test]
        public void CandleLightIsATighterPoolOfItsPreservedSourceAboutTheCandlePoint()
        {
            string sources = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/Reference/Art/"));
            Texture2D source = DecodeFile(sources + "HauntedMerchantCandleFootprintSource.png");
            Texture2D sourceResponse = DecodeFile(sources + "HauntedMerchantCandleResponseSource.png");
            Texture2D footprint = Decode("CandleLightFootprintCentered.png");
            Texture2D response = Decode("CandleLightResponseCentered.png");
            try
            {
                foreach (Texture2D texture in new[] { sourceResponse, footprint, response })
                    Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(source.width, source.height)),
                        "The light textures share the source frame.");
                Color32[] before = source.GetPixels32(), after = footprint.GetPixels32();
                Color32[] beforeDistance = sourceResponse.GetPixels32(), afterDistance = response.GetPixels32();
                Vector2Int candle = Minimum(beforeDistance, source.width);
                Assert.That(Minimum(afterDistance, footprint.width), Is.EqualTo(candle), "The distance minimum stays on the candle.");
                int row = candle.y * source.width;
                Assert.That(after[row + candle.x].r, Is.EqualTo(before[row + candle.x].r), "The light at the flame is unchanged.");
                Assert.That(after.Max(pixel => pixel.r), Is.EqualTo(before.Max(pixel => pixel.r)), "The pool is not brighter at its center.");
                // Every level of the pool, sideways, toward the viewer and toward the cart.
                var probes = new List<(string Name, float Before, float After)>();
                foreach (int level in new[] { 192, 128, 64, 16 })
                    foreach (var (name, dx, dy) in new[] { ("right", 1, 0), ("left", -1, 0), ("near", 0, -1), ("far", 0, 1) })
                        probes.Add((name + " " + level, Reach(before, source.width, source.height, candle, dx, dy, level),
                            Reach(after, footprint.width, footprint.height, candle, dx, dy, level)));
                // The installed pool's reach relative to its source, fitted over every probe.
                float reach = probes.Sum(probe => probe.After) / probes.Sum(probe => probe.Before);
                Assert.That(reach, Is.LessThan(1f), "The installed pool is tighter than its source.");
                foreach (var probe in probes)
                    Assert.That(probe.After, Is.EqualTo(reach * probe.Before).Within(2f), probe.Name + " shrinks by the pool's common factor.");
                // The authored distance reaches one at the scaled reach, so dimming falls off over the tighter pool.
                Assert.That(Reach(afterDistance, response.width, response.height, candle, 1, 0, 255, true),
                    Is.EqualTo(reach * Reach(beforeDistance, response.width, response.height, candle, 1, 0, 255, true)).Within(2f));
                for (int index = 0; index < after.Length; ++index)
                    Assert.That(after[index].r == after[index].g && after[index].g == after[index].b, Is.True, "The footprint stays gray.");
            }
            finally
            {
                foreach (Texture2D texture in new[] { source, sourceResponse, footprint, response }) Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MerchantLayersUseTheExistingHostPresentationContract()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(MaterialPath + "MerchantHost.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.DoesNotThrow(asset.Validate);
            Assert.That(asset.PortraitPrefab, Is.Not.Null);
            // One shared field keeps every overlapping layer registered while the figure breathes.
            Assert.That(asset.Layers.Select(layer => layer.Mesh).Distinct().Count(), Is.EqualTo(1));
            Assert.That(asset.Layers[0].Mesh, Is.Not.Null);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset.PortraitPrefab.gameObject, scene);
                SpiritHostPortrait host = root.GetComponent<SpiritHostPortrait>();
                Assert.That(host, Is.Not.Null);
                Assert.DoesNotThrow(() => host.Build(asset));
                // Every part spans the one source frame.
                Vector2 frame = host.GetLayer(asset.Layers[0].Id).rectTransform.rect.size;
                foreach (SpiritPortraitLayer layer in asset.Layers)
                {
                    SpiritPortraitImage image = host.GetLayer(layer.Id);
                    Assert.That(image, Is.Not.Null, layer.Id);
                    Assert.That(image.name, Is.EqualTo(layer.Id));
                    Assert.That(image.texture, Is.SameAs(layer.Texture));
                    Assert.That(image.rectTransform.rect.size, Is.EqualTo(frame), layer.Id);
                    Assert.That(image.Weights.Count, Is.EqualTo(layer.Mesh != null ? layer.Mesh.Shapes.Length : 0));
                    Assert.That(image.Weights, Is.Not.Empty, layer.Id + " requires the shared breathing channel.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("MerchantTorso", 385, 850)]
        [TestCase("MerchantHead", 350, 600)]
        [TestCase("MerchantSupportingArm", 230, 830)]
        [TestCase("MerchantSupportingArm", 265, 642)]
        [TestCase("MerchantSupportingArm", 299, 664)]
        [TestCase("MerchantSupportingArm", 305, 685)]
        [TestCase("MerchantSupportingArm", 304, 690)]
        [TestCase("MerchantRestingArm", 540, 800)]
        [TestCase("CandleBody", 600, 700)]
        [TestCase("CandleFlame", 590, 200)]
        [TestCase("CandleBody", 578, 450)]
        public void AnatomicalSamplesBelongToTheirIndependentlySelectablePart(string part, int x, int y)
        {
            Texture2D mask = Decode(part + "Mask.png");
            try
            {
                Assert.That(mask.GetPixels32()[(mask.height - 1 - y) * mask.width + x].a, Is.EqualTo(255),
                    $"{part} must own source point ({x}, {y}), measured from the top-left.");
            }
            finally
            {
                Object.DestroyImmediate(mask);
            }
        }

        [TestCase("MerchantHost.png", new[] { "MerchantTorso", "MerchantHead", "MerchantSupportingArm", "MerchantRestingArm" })]
        [TestCase("ProtectiveCandle.png", new[] { "CandleBody", "CandleFlame" })]
        public void OwnershipMasksPartitionTheCompleteUnmodifiedSourceFrame(string sourceFilename, string[] parts)
        {
            Texture2D source = Decode(sourceFilename);
            var masks = new Texture2D[parts.Length];
            try
            {
                Color32[][] pixels = new Color32[parts.Length][];
                for (int part = 0; part < parts.Length; ++part)
                {
                    string filename = parts[part] + "Mask.png";
                    masks[part] = Decode(filename);
                    Assert.That(masks[part].width, Is.EqualTo(source.width), filename);
                    Assert.That(masks[part].height, Is.EqualTo(source.height), filename);
                    pixels[part] = masks[part].GetPixels32();
                    AssertDataImport(filename, source.width, source.height);
                }
                int invalidPixels = 0, ownershipErrors = 0;
                var ownedPixels = new int[parts.Length];
                for (int index = 0; index < source.width * source.height; ++index)
                {
                    int owners = 0;
                    for (int part = 0; part < parts.Length; ++part)
                    {
                        byte alpha = pixels[part][index].a;
                        if (alpha != 0 && alpha != 255) ++invalidPixels;
                        if (alpha != 255) continue;
                        ++owners;
                        ++ownedPixels[part];
                    }
                    if (owners != 1) ++ownershipErrors;
                }
                Assert.That(invalidPixels, Is.Zero, "Ownership data must be binary without antialiasing.");
                Assert.That(ownershipErrors, Is.Zero,
                    "Every source pixel, including transparent margins, must belong to exactly one part.");
                Assert.That(ownedPixels.All(count => count > 0), Is.True, "Every independently selectable part needs ownership.");
            }
            finally
            {
                Object.DestroyImmediate(source);
                foreach (Texture2D mask in masks)
                    if (mask != null) Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void CompleteTorsoPreservesVisibleSourceAndProvidesConcealedCoverage()
        {
            Texture2D source = Decode("MerchantHost.png");
            Texture2D complete = Decode("MerchantTorsoAnimation.png");
            Texture2D originalMask = Decode("MerchantTorsoMask.png");
            Texture2D completeMask = Decode("MerchantTorsoAnimationMask.png");
            Texture2D supportingMask = Decode("MerchantSupportingArmMask.png");
            Texture2D forearmMask = Decode("MerchantSupportingForearmAnimationMask.png");
            var textures = new[] { source, complete, originalMask, completeMask, supportingMask, forearmMask };
            try
            {
                foreach (Texture2D texture in textures)
                    Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(source.width, source.height)));
                Color32[] original = source.GetPixels32(), backing = complete.GetPixels32();
                Color32[] originalOwnership = originalMask.GetPixels32(), ownership = completeMask.GetPixels32();
                Color32[] supporting = supportingMask.GetPixels32(), forearm = forearmMask.GetPixels32();
                int visibleChanges = 0, missingSleeve = 0, concealedCoverage = 0;
                for (int index = 0; index < original.Length; ++index)
                {
                    bool sleeve = supporting[index].a == 255 && forearm[index].a == 0 && original[index].a > 0;
                    bool visible = original[index].a > 0 && (originalOwnership[index].a == 255 || sleeve);
                    if (visible && !original[index].Equals(backing[index])) ++visibleChanges;
                    if (sleeve && ownership[index].a != 255) ++missingSleeve;
                    if (originalOwnership[index].a == 0 && ownership[index].a == 255 && backing[index].a > 240)
                        ++concealedCoverage;
                }
                Assert.That(visibleChanges, Is.Zero, "The torso and fixed sleeve must retain exact visible source RGBA.");
                Assert.That(missingSleeve, Is.Zero, "The supporting upper sleeve must remain in the fixed torso.");
                Assert.That(concealedCoverage, Is.GreaterThan(100), "The fixed torso must provide concealed opaque backing beneath moving parts.");
                AssertDataImport("MerchantTorsoAnimationMask.png", source.width, source.height);
                AssertDataImport("MerchantSupportingForearmAnimationMask.png", source.width, source.height);
            }
            finally
            {
                foreach (Texture2D texture in textures) Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ConcealedCheekRepairChangesOnlyItsDeclaredRegionAndCompletesTheHead()
        {
            Texture2D source = Decode("MerchantHost.png");
            Texture2D repaired = Decode("MerchantHeadRepaired.png");
            Texture2D repair = Decode("MerchantHeadRepairMask.png");
            Texture2D head = Decode("MerchantHeadMask.png");
            Texture2D complete = Decode("MerchantHeadCompleteMask.png");
            Texture2D arm = Decode("MerchantSupportingArmMask.png");
            var textures = new[] { source, repaired, repair, head, complete, arm };
            try
            {
                foreach (Texture2D texture in textures)
                    Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(source.width, source.height)));
                AssertDataImport("MerchantHeadRepairMask.png", source.width, source.height);
                AssertDataImport("MerchantHeadCompleteMask.png", source.width, source.height);
                Color32[] original = source.GetPixels32(), fixedPixels = repaired.GetPixels32();
                Color32[] repairPixels = repair.GetPixels32(), headPixels = head.GetPixels32();
                Color32[] completePixels = complete.GetPixels32(), armPixels = arm.GetPixels32();
                int outsideChanges = 0, changedPixels = 0, maskErrors = 0, overlapPixels = 0;
                for (int index = 0; index < original.Length; ++index)
                {
                    byte selected = repairPixels[index].a, completed = completePixels[index].a;
                    if (selected != 0 && selected != 255 || completed != 0 && completed != 255) ++maskErrors;
                    byte expected = headPixels[index].a == 255 || selected == 255 ? (byte)255 : (byte)0;
                    if (completed != expected) ++maskErrors;
                    if (selected == 255 && armPixels[index].a != 255)
                    {
                        bool nearArm = false;
                        int column = index % source.width, row = index / source.width;
                        if (headPixels[index].a == 255)
                            for (int dy = -5; dy <= 5 && !nearArm; ++dy)
                                for (int dx = -5; dx <= 5; ++dx)
                                {
                                    if (dx * dx + dy * dy > 25 || column + dx < 0 || column + dx >= source.width
                                        || row + dy < 0 || row + dy >= source.height) continue;
                                    if (armPixels[(row + dy) * source.width + column + dx].a != 255) continue;
                                    nearArm = true;
                                    break;
                                }
                        if (!nearArm) ++maskErrors;
                    }
                    if (!original[index].Equals(fixedPixels[index]))
                    {
                        if (selected == 0) ++outsideChanges;
                        else ++changedPixels;
                    }
                    if (selected == 255 && armPixels[index].a == 255) ++overlapPixels;
                }
                Assert.That(outsideChanges, Is.Zero, "The repaired source must retain exact original RGBA outside the declared cheek repair.");
                Assert.That(changedPixels, Is.GreaterThan(0), "The concealed cheek must contain reconstructed source pixels.");
                Assert.That(maskErrors, Is.Zero,
                    "Complete head ownership must be the exact binary union of base head and repair; repair must lie beneath supporting-arm ownership or within five source pixels into adjacent base head.");
                Assert.That(overlapPixels, Is.GreaterThan(0), "The concealed cheek must extend beneath the independently owned supporting hand.");
            }
            finally
            {
                foreach (Texture2D texture in textures) Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void BlinkOverlayTransfersOnlyTheEyelidsThroughASoftMaskOnTheFace()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaterialPath + "MerchantHost.prefab");
            SpiritPortraitImage blink = prefab.GetComponentsInChildren<SpiritPortraitImage>(true).Single(image => image.name == "Blink");
            SpiritPortraitImage head = prefab.GetComponentsInChildren<SpiritPortraitImage>(true).Single(image => image.name == "Head");
            Material material = blink.material;
            Assert.That(material, Is.Not.Null);
            Texture eyesTexture = material.GetTexture("_SourceTex"), maskTexture = material.GetTexture("_LayerMask");
            Assert.That(eyesTexture, Is.Not.Null, "The overlay draws its closed eyes through the masked source.");
            Assert.That(maskTexture, Is.Not.Null, "The overlay transfers only its masked eyelids.");
            Assert.That(material.GetFloat("_InvertMask"), Is.Zero, "The mask selects the eyelids rather than excluding them.");
            Assert.That(blink.color.a, Is.Zero, "The closed eyes must stay hidden until a blink.");
            Assert.That(blink.transform.GetSiblingIndex(), Is.EqualTo(head.transform.GetSiblingIndex() + 1), "The blink draws directly above the head.");
            Assert.That(blink.rectTransform.rect, Is.EqualTo(head.rectTransform.rect));
            Assert.That(blink.rectTransform.anchoredPosition, Is.EqualTo(head.rectTransform.anchoredPosition));
            Texture2D eyes = DecodeFile(AssetDatabase.GetAssetPath(eyesTexture)), mask = DecodeFile(AssetDatabase.GetAssetPath(maskTexture));
            Texture2D face = Decode("MerchantHeadCompleteMask.png");
            try
            {
                Assert.That(new Vector2Int(eyes.width, eyes.height), Is.EqualTo(new Vector2Int(mask.width, mask.height)));
                AssertDataImport(Path.GetFileName(AssetDatabase.GetAssetPath(maskTexture)), mask.width, mask.height);
                Assert.That(eyesTexture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), "Clamped edges keep the overlay transparent outside its crop.");
                // The small overlay spans the full source frame through its UV rectangle.
                Rect uv = blink.uvRect;
                Assert.That(uv.width * eyes.width, Is.EqualTo(face.width).Within(.01f));
                Assert.That(uv.height * eyes.height, Is.EqualTo(face.height).Within(.01f));
                int left = Mathf.RoundToInt(-uv.x * eyes.width), bottom = Mathf.RoundToInt(-uv.y * eyes.height);
                Assert.That(-uv.x * eyes.width, Is.EqualTo(left).Within(.01f), "The crop must align with source pixels.");
                Assert.That(-uv.y * eyes.height, Is.EqualTo(bottom).Within(.01f), "The crop must align with source pixels.");
                Color32[] coverage = mask.GetPixels32(), facePixels = face.GetPixels32();
                int partial = 0, full = 0, offFace = 0, border = 0;
                for (int y = 0; y < mask.height; ++y)
                    for (int x = 0; x < mask.width; ++x)
                    {
                        byte alpha = coverage[y * mask.width + x].a;
                        if (alpha == 0) continue;
                        if (x == 0 || y == 0 || x == mask.width - 1 || y == mask.height - 1) ++border;
                        if (alpha == 255) ++full;
                        else ++partial;
                        // Texture rows count from the bottom in both the crop and the source frame.
                        if (facePixels[(bottom + y) * face.width + left + x].a != 255) ++offFace;
                    }
                Assert.That(border, Is.Zero, "The mask must vanish at the crop border.");
                Assert.That(full, Is.GreaterThan(0), "The eyelids must be fully replaced at the blink's peak.");
                Assert.That(partial, Is.GreaterThan(full / 2), "The transfer must blend through a soft edge.");
                Assert.That(offFace, Is.Zero, "The eyelid transfer must stay on the completed head.");
                Assert.That(mask.width * mask.height, Is.LessThan(face.width * face.height / 100), "The eyelid overlay is a small crop.");
            }
            finally
            {
                Object.DestroyImmediate(eyes);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(face);
            }
        }

        private static void AssertDataImport(string filename, int width, int height)
        {
            string path = MerchantSceneTests.ArtPath + filename;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.sRGBTexture, Is.False, path);
            Assert.That(importer.alphaIsTransparency, Is.False, path);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, path);
            FilterMode filter = filename == "MerchantHeadCompleteMask.png" || filename == "MerchantSupportingForearmAnimationMask.png"
                || filename == "MerchantBlinkMask.png" ? FilterMode.Bilinear : FilterMode.Point;
            Assert.That(texture.filterMode, Is.EqualTo(filter), path);
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
            Assert.That(texture.width, Is.EqualTo(width), path);
            Assert.That(texture.height, Is.EqualTo(height), path);
        }

        private static Texture2D Decode(string filename) => DecodeFile(MerchantSceneTests.ArtPath + filename);

        private static Texture2D DecodeFile(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        /** Centroid texel of the lowest red values, rounded down, in bottom-up texture rows. */
        private static Vector2Int Minimum(Color32[] pixels, int width)
        {
            byte lowest = pixels.Min(pixel => pixel.r);
            long x = 0, y = 0, count = 0;
            for (int index = 0; index < pixels.Length; ++index)
            {
                if (pixels[index].r != lowest) continue;
                x += index % width;
                y += index / width;
                ++count;
            }
            return new Vector2Int((int)(x / count), (int)(y / count));
        }

        /** Texels from the candle along one axis until red falls below the level (or rises to it, for distance). */
        private static float Reach(Color32[] pixels, int width, int height, Vector2Int from, int dx, int dy, int level, bool rising = false)
        {
            for (int step = 0; ; ++step)
            {
                int x = from.x + dx * step, y = from.y + dy * step;
                if (x < 0 || y < 0 || x >= width || y >= height) return step;
                byte value = pixels[y * width + x].r;
                if (rising ? value >= level : value < level) return step;
            }
        }
    }
}
