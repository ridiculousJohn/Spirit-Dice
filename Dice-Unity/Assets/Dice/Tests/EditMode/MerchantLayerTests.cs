using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class MerchantLayerTests
    {
        private const string MaterialPath = "Assets/Dice/Content/Presentation/Ambient/";

        [Test]
        public void MovableCandleFootprintHasDarkBordersAndIndependentLightReferences()
        {
            var lighting = AssetDatabase.LoadAssetAtPath<SceneLightingAsset>(MaterialPath + "MerchantSceneLighting.asset");
            Assert.That(lighting, Is.Not.Null);
            Assert.DoesNotThrow(lighting.Validate);
            Assert.That(lighting.Lights.Length, Is.EqualTo(2));
            Assert.That(lighting.Lights.All(light => light.ResponseMap != null), Is.True);
            Assert.That(lighting.Lights[0].Contribution, Is.Not.SameAs(lighting.Lights[1].Contribution));
            Assert.That(lighting.Lights[1].ModulateByBaseColor, Is.True);
            Texture2D footprint = Decode("CandleLightFootprintCentered.png");
            try
            {
                Color32[] pixels = footprint.GetPixels32();
                for (int y = 0; y < footprint.height; ++y)
                    for (int x = 0; x < footprint.width; ++x)
                    {
                        if (x != 0 && y != 0 && x != footprint.width - 1 && y != footprint.height - 1) continue;
                        Color32 pixel = pixels[y * footprint.width + x];
                        Assert.That(pixel.r + pixel.g + pixel.b, Is.Zero,
                            "Moving the footprint must not reveal a lit texture border.");
                    }
            }
            finally { Object.DestroyImmediate(footprint); }
        }

        [Test]
        public void MerchantLayersUseTheExistingHostPresentationContract()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(MaterialPath + "MerchantHost.asset");
            Assert.That(asset, Is.Not.Null);
            Assert.DoesNotThrow(asset.Validate);
            Assert.That(asset.PortraitPrefab, Is.Not.Null);
            Assert.That(asset.Layers.Select(layer => layer.Id),
                Is.EquivalentTo(new[] { "Torso", "Head", "Resting Arm", "Supporting Arm" }));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset.PortraitPrefab.gameObject, scene);
                SpiritHostPortrait host = root.GetComponent<SpiritHostPortrait>();
                Assert.That(host, Is.Not.Null);
                Assert.DoesNotThrow(() => host.Build((RectTransform)root.transform, asset));
                foreach (SpiritPortraitLayer layer in asset.Layers)
                {
                    SpiritPortraitImage image = host.GetLayer(layer.Id);
                    Assert.That(image, Is.Not.Null, layer.Id);
                    Assert.That(image.name, Is.EqualTo(layer.Id));
                    Assert.That(image.texture, Is.SameAs(layer.Texture));
                    Assert.That(image.rectTransform.rect.size, Is.EqualTo(new Vector2(941, 1672)));
                    Assert.That(image.Weights.Count, Is.EqualTo(layer.Mesh != null ? layer.Mesh.Shapes.Length : 0));
                    if (layer.Id != "Torso")
                        Assert.That(image.Weights, Is.Not.Empty, layer.Id + " requires authored deformation channels.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("MerchantHost.png", "F733AB1D178BAEC18C4AD14E35DAE45E34DBB874ABCEC4DFDC1C8A905745C016")]
        [TestCase("ProtectiveCandle.png", "0C447D1B3E87DA64FDA6CC7D68B7D8AA04BF4C0B04A6C328C3F21669C23A1F46")]
        public void LayerSeparationPreservesTheInstalledSourceFile(string filename, string expectedHash)
        {
            using (SHA256 hash = SHA256.Create())
                Assert.That(System.BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(CanopyMotionTests.ArtPath + filename)))
                    .Replace("-", ""), Is.EqualTo(expectedHash), filename);
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
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + parts[part] + ".mat");
                    Assert.That(material, Is.Not.Null, parts[part]);
                    Assert.That(AssetDatabase.GetAssetPath(material.shader),
                        Is.EqualTo("Assets/Dice/Presentation/Shared/SourceMaskedImage.shader"));
                    Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_SourceTex")),
                        Is.EqualTo(CanopyMotionTests.ArtPath +
                            (parts[part] == "MerchantHead" ? "MerchantHeadRepaired.png"
                                : parts[part] == "MerchantTorso" ? "MerchantTorsoAnimation.png" : sourceFilename)));
                    Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_LayerMask")),
                        Is.EqualTo(CanopyMotionTests.ArtPath +
                            (parts[part] == "MerchantHead" ? "MerchantHeadCompleteMask.png"
                                : parts[part] == "MerchantTorso" ? "MerchantTorsoAnimationMask.png"
                                : parts[part] == "MerchantSupportingArm" ? "MerchantSupportingForearmAnimationMask.png" : filename)));
                    Assert.That(material.GetFloat("_InvertMask"), Is.Zero);
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
                    Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(941, 1672)));
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
                AssertDataImport("MerchantTorsoAnimationMask.png", 941, 1672);
                AssertDataImport("MerchantSupportingForearmAnimationMask.png", 941, 1672);
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
                    Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(941, 1672)));
                AssertDataImport("MerchantHeadRepairMask.png", 941, 1672);
                AssertDataImport("MerchantHeadCompleteMask.png", 941, 1672);
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

        private static void AssertDataImport(string filename, int width, int height)
        {
            string path = CanopyMotionTests.ArtPath + filename;
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
                ? FilterMode.Bilinear : FilterMode.Point;
            Assert.That(texture.filterMode, Is.EqualTo(filter), path);
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
            Assert.That(texture.width, Is.EqualTo(width), path);
            Assert.That(texture.height, Is.EqualTo(height), path);
        }

        private static Texture2D Decode(string filename)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(texture.LoadImage(File.ReadAllBytes(CanopyMotionTests.ArtPath + filename)), Is.True, filename);
            return texture;
        }
    }
}
