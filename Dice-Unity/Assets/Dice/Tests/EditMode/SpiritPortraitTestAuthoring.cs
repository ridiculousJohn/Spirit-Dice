using System.Linq;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    /** Synthetic timeline tests author their layer references before binding the runtime evaluator. */
    internal static class SpiritPortraitTestAuthoring
    {
        public static SpiritHostPortrait Create(SpiritHostPresentationAsset asset, RectTransform parent)
        {
            asset.Validate();
            var root = Rect("Test Portrait", parent);
            var portrait = root.gameObject.AddComponent<SpiritHostPortrait>();
            var layersRoot = Rect("Portrait Layers", root);
            layersRoot.anchorMin = layersRoot.anchorMax = Vector2.zero;
            layersRoot.pivot = Vector2.zero;
            layersRoot.sizeDelta = asset.ReferenceSize;
            layersRoot.anchoredPosition = asset.Offset;
            layersRoot.localScale = new Vector3(asset.Scale, asset.Scale, 1);
            var images = new SpiritPortraitImage[asset.Layers.Length];
            for (int i = 0; i < images.Length; ++i)
            {
                var source = asset.Layers[i];
                var rect = Rect(source.Id, layersRoot);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = source.Pivot; rect.anchoredPosition = source.Position; rect.sizeDelta = source.Size;
                rect.localRotation = Quaternion.Euler(0, 0, source.Rotation);
                var image = rect.gameObject.AddComponent<SpiritPortraitImage>();
                image.texture = source.Texture; image.color = source.Tint; image.raycastTarget = false;
                image.Configure(source.Mesh);
                if (source.Weights != null && source.Weights.Length == image.Weights.Count) image.SetWeights(source.Weights);
                if (source.Smoke != null && source.Smoke.Enabled)
                {
                    var template = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                        "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsHost.asset");
                    image.material = template.PortraitPrefab.GetComponentsInChildren<SpiritPortraitImage>(true)
                        .First(value => value.material != null && value.material.shader == asset.SmokeShader).material;
                }
                images[i] = image;
            }
            var serialized = new SerializedObject(portrait);
            serialized.FindProperty("portraitRoot").objectReferenceValue = layersRoot;
            var layers = serialized.FindProperty("authoredLayers"); layers.arraySize = images.Length;
            for (int i = 0; i < images.Length; ++i) layers.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return portrait;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; return rect;
        }
    }
}
