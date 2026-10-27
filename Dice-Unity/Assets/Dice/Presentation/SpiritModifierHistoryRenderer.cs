using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dice.Presentation
{
    /** Small bone dice move within a fixed tray using the encounter dice's rendering mechanism. */
    public sealed class SpiritModifierHistoryRenderer : MonoBehaviour
    {
        public const float DieSize = .49f;
        public const float SlotSpacing = .59f;
        public const float FeltHeight = .035f;
        public const float InnerHalfWidth = 2.70f;
        public const int VisibleCapacity = 9;
        public const float ArrivalDuration = .30f;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        private readonly Dictionary<uint, Texture2D> sigils = new Dictionary<uint, Texture2D>();
        [SerializeField] private GameObject renderRoot;
        [SerializeField] private SpiritModifierToken tokenPrefab;
        private bool initialized;
        private Transform trayRoot;
        private Camera renderCamera;
        private RenderTexture texture;
        private Mesh bodyMesh;
        private Vector3[] supportPoints;
        private float normalizedScroll;
        private int resolvingIndex = -1;
        private SceneLightingState sceneLighting;
        private SpiritTrayScene sharedScene;
        [SerializeField] private Transform contentRoot;
        private Transform LightingRoot => sharedScene != null ? sharedScene.RenderRoot : RenderRoot;
        private Transform BayRoot => sharedScene != null ? sharedScene.Geometry.ModifierRoot : renderRoot.transform;
        private float BayHalfWidth => sharedScene != null ? sharedScene.Geometry.ModifierWidth * .5f : InnerHalfWidth;
        private float BayDepth => sharedScene != null ? sharedScene.Geometry.ModifierDepth : .62f;
        private float SurfaceHeight => sharedScene != null ? 0 : FeltHeight;
        private float ContentDieSize => sharedScene != null ? sharedScene.Geometry.ModifierDieSize : DieSize;
        private float ContentSlotSpacing => sharedScene != null ? sharedScene.Geometry.ModifierSlotSpacing : SlotSpacing;
        public int Capacity => sharedScene != null ? sharedScene.Geometry.ModifierVisibleCapacity : VisibleCapacity;

        public RenderTexture Texture => texture;
        public Camera RenderCamera => renderCamera;
        public Transform RenderRoot => sharedScene != null ? sharedScene.RenderRoot : renderRoot != null ? renderRoot.transform : null;
        public Transform TrayRoot => trayRoot;
        public int Count => entries.Count;
        public int ResolvingIndex => resolvingIndex;

        public void SetSceneLighting(SceneLightingState state)
        {
            sceneLighting = state;
            if (renderRoot == null) return;
            foreach (UnityEngine.Object resource in ownedResources)
                if (resource is Material material)
                    SceneLightingReceiverMaterial.Apply(material, state, LightingRoot);
        }

        private sealed class Entry
        {
            internal GameObject Token;
            internal Transform Die;
            internal MeshRenderer Renderer;
            internal MeshRenderer Shadow;
            internal Material Material;
            internal Material ShadowMaterial;
            internal float Elapsed;
            internal float SupportHeight;
            internal Color BoneColor;
            internal Vector3 ShadowScale;
        }

        public void Configure(SpiritTrayPresentationAsset presentation)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            presentation.Validate();
            if (trayPresentation != presentation || sharedScene != null) ReleasePresentation();
            trayPresentation = presentation;
        }

        public void Configure(SpiritTrayPresentationAsset presentation, SpiritTrayScene scene)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            presentation.ValidateScene();
            if (scene.Presentation != presentation)
                throw new InvalidOperationException("The modifier tray must use the shared scene's presentation.");
            if (trayPresentation != presentation || sharedScene != scene) ReleasePresentation();
            trayPresentation = presentation;
            sharedScene = scene;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (trayPresentation == null || sharedScene == null || renderRoot == null || contentRoot == null || tokenPrefab == null)
                throw new InvalidOperationException("The modifier renderer requires an authored shared scene, content root, and token prefab.");
            sharedScene.Initialize();
            texture = sharedScene.Texture;
            renderCamera = sharedScene.RenderCamera;
            trayRoot = sharedScene.Geometry.ModifierRoot;
            bodyMesh = tokenPrefab.Body.GetComponent<MeshFilter>().sharedMesh;
            supportPoints = bodyMesh.vertices;
            initialized = true;
            renderRoot.SetActive(isActiveAndEnabled);
        }

        public void Clear()
        {
            foreach (Entry entry in entries)
            {
                if (entry.Token != null)
                {
                    entry.Token.SetActive(false);
                    Release(entry.Token);
                }
                ownedResources.Remove(entry.Material);
                Release(entry.Material);
                ownedResources.Remove(entry.ShadowMaterial);
                Release(entry.ShadowMaterial);
            }
            entries.Clear();
            normalizedScroll = 0;
            resolvingIndex = -1;
        }

        public void Add(ModifierContribution contribution, ModifierPresentationCatalog charms, ModifierDefinition definition,
            bool animate)
        {
            if (charms == null) throw new ArgumentNullException(nameof(charms));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (contribution.DefinitionId != definition.Id)
                throw new ArgumentException("The modifier definition must match the activation.", nameof(definition));
            Initialize();
            SpiritModifierToken token = Instantiate(tokenPrefab, contentRoot, false);
            Material material = new Material(token.Body.sharedMaterial) { name = "Spirit Modifier Bone " + entries.Count };
            ownedResources.Add(material);
            token.Body.sharedMaterial = material;
            Material shadow = new Material(token.Shadow.sharedMaterial);
            ownedResources.Add(shadow);
            token.Shadow.sharedMaterial = shadow;
            SetClip(shadow);
            SceneLightingReceiverMaterial.Apply(material, sceneLighting, LightingRoot);
            material.SetFloat("_ShowPips", 0);
            material.SetFloat("_ShowFaceIcon", 1);
            material.SetFloat("_Seed", entries.Count * 17.31f);
            material.SetColor("_FaceIconColor", charms.GetInkColor(definition));
            SetIcon(material, charms, definition);
            SetCategory(material, charms, definition);
            SetClip(material);
            Transform die = token.Body.transform;
            die.localScale *= ContentDieSize / DieSize;
            var entry = new Entry
            {
                Token = token.gameObject,
                Die = die,
                Renderer = token.Body,
                Shadow = token.Shadow,
                Material = material,
                ShadowMaterial = shadow,
                Elapsed = animate ? 0 : ArrivalDuration,
                BoneColor = material.GetColor("_BaseColor"),
                ShadowScale = token.Shadow.transform.localScale
            };
            entries.Add(entry);
            Position(entry, entries.Count - 1, true);
            SetScroll(normalizedScroll);
        }

        public void SetScroll(float normalized)
        {
            if (float.IsNaN(normalized) || float.IsInfinity(normalized)) throw new ArgumentOutOfRangeException(nameof(normalized));
            normalizedScroll = Mathf.Clamp01(normalized);
            for (int i = 0; i < entries.Count; ++i) Position(entries[i], i, false);
        }

        public void SetResolving(int index)
        {
            if (index < -1 || index >= entries.Count) throw new ArgumentOutOfRangeException(nameof(index));
            resolvingIndex = index;
            if (renderRoot == null || contentRoot == null) return;
            for (int i = 0; i < entries.Count; ++i)
            {
                Entry entry = entries[i];
                if (entry.Die == null || entry.Shadow == null || entry.Material == null) continue;
                entry.Material.SetColor("_BaseColor", i == index ? new Color(1, .74f, .32f) : entry.BoneColor);
                Position(entry, i, false);
            }
        }

        public void Advance(float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            for (int i = 0; i < entries.Count; ++i)
            {
                Entry entry = entries[i];
                if (entry.Elapsed >= ArrivalDuration) continue;
                entry.Elapsed = Mathf.Min(ArrivalDuration, entry.Elapsed + seconds);
                Position(entry, i, true);
            }
        }

        public Transform GetDieTransform(int index) => entries[index].Die;
        public MeshRenderer GetDieRenderer(int index) => entries[index].Renderer;
        public MeshRenderer GetShadowRenderer(int index) => entries[index].Shadow;
        public float GetArrivalProgress(int index) => entries[index].Elapsed / ArrivalDuration;

        public Rect GetDieViewportRect(int index)
        {
            Transform die = entries[index].Die;
            Vector2 min = Vector2.one * float.MaxValue;
            Vector2 max = Vector2.one * float.MinValue;
            float localMin = float.MaxValue;
            float localMax = float.MinValue;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 world = die.TransformPoint(new Vector3(x, y, z) * .5f);
                        Vector3 local = BayRoot.InverseTransformPoint(world);
                        localMin = Mathf.Min(localMin, local.x);
                        localMax = Mathf.Max(localMax, local.x);
                        local.x = Mathf.Clamp(local.x, -BayHalfWidth, BayHalfWidth);
                        world = BayRoot.TransformPoint(local);
                        Vector3 point = renderCamera.WorldToViewportPoint(world);
                        min = Vector2.Min(min, new Vector2(point.x, point.y));
                        max = Vector2.Max(max, new Vector2(point.x, point.y));
                    }
            if (localMax <= -BayHalfWidth || localMin >= BayHalfWidth) return Rect.zero;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public Rect GetBayViewportRect()
        {
            Initialize();
            Vector2 min = Vector2.one * float.MaxValue;
            Vector2 max = Vector2.one * float.MinValue;
            for (int x = -1; x <= 1; x += 2)
                for (int y = 0; y <= 1; ++y)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = renderCamera.WorldToViewportPoint(BayRoot.TransformPoint(
                            new Vector3(x * BayHalfWidth, SurfaceHeight + y * ContentDieSize * 1.5f, z * BayDepth * .5f)));
                        min = Vector2.Min(min, new Vector2(point.x, point.y));
                        max = Vector2.Max(max, new Vector2(point.x, point.y));
                    }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void Position(Entry entry, int index, bool rotationChanged)
        {
            float remaining = Mathf.Pow(1 - Mathf.Clamp01(entry.Elapsed / ArrivalDuration), 3);
            if (rotationChanged)
            {
                entry.Die.localRotation = Quaternion.Euler(-14 * remaining, (index % 3 - 1) * 4, 8 * remaining);
                float minimum = float.MaxValue;
                foreach (Vector3 point in supportPoints)
                    minimum = Mathf.Min(minimum, (entry.Die.localRotation * Vector3.Scale(point, entry.Die.localScale)).y);
                entry.SupportHeight = -minimum;
            }
            float overflow = Mathf.Max(0, (entries.Count - Capacity) * ContentSlotSpacing);
            float x = -(Capacity - 1) * ContentSlotSpacing * .5f + index * ContentSlotSpacing - overflow * normalizedScroll;
            x -= .20f * remaining;
            float lift = .08f * remaining + (index == resolvingIndex ? ContentDieSize * .12f : 0);
            entry.Die.localPosition = new Vector3(x, SurfaceHeight + entry.SupportHeight + lift, 0);
            entry.Shadow.transform.localPosition = new Vector3(x, SurfaceHeight + .003f, 0);
            entry.Shadow.transform.localScale = Vector3.Scale(entry.ShadowScale,
                new Vector3(ContentDieSize * (1.08f + lift), 1, ContentDieSize * (1.08f + lift)));
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_ShadowLift", lift / .08f);
            entry.Shadow.SetPropertyBlock(properties);
        }

        private void SetIcon(Material material, ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            Sprite sprite = charms.GetIcon(definition);
            if (sprite != null)
            {
                SetSprite(material, sprite, "_FaceIcon", "_FaceIconRect", "_FaceIconUVTransform");
            }
            else
            {
                uint seed = charms.GetSigilSeed(definition);
                if (!sigils.TryGetValue(seed, out Texture2D icon))
                {
                    icon = SpiritModifierSigilTexture.Create(seed);
                    sigils.Add(seed, icon);
                    ownedResources.Add(icon);
                }
                material.SetTexture("_FaceIcon", icon);
                material.SetVector("_FaceIconRect", new Vector4(0, 0, 1, 1));
            }
        }

        private static void SetCategory(Material material, ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            Sprite sprite = charms.GetCategorySprite(definition);
            bool visible = charms.GetCategorySymbol(definition) != ModifierCategorySymbol.None && sprite != null;
            material.SetFloat("_ShowCategory", visible ? 1 : 0);
            if (!visible) return;
            material.SetColor("_CategoryColor", charms.GetInkColor(definition));
            SetSprite(material, sprite, "_CategoryIcon", "_CategoryIconRect", "_CategoryIconUVTransform");
        }

        private static void SetSprite(Material material, Sprite sprite, string texture, string rect, string uvTransform)
        {
            Vector2 min = Vector2.one;
            Vector2 max = Vector2.zero;
            foreach (Vector2 uv in sprite.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
            material.SetTexture(texture, sprite.texture);
            material.SetVector(rect, new Vector4(min.x, min.y, max.x - min.x, max.y - min.y));
            Vector4 transform = new Vector4(1, 1, 0, 0);
            if (sprite.packed)
            {
                if (sprite.packingRotation == SpritePackingRotation.FlipHorizontal) transform.x = -1;
                if (sprite.packingRotation == SpritePackingRotation.FlipVertical) transform.y = -1;
                if (sprite.packingRotation == SpritePackingRotation.Rotate180) { transform.x = -1; transform.y = -1; }
            }
            material.SetVector(uvTransform, transform);
        }



        private void SetClip(Material material)
        {
            if (sharedScene == null)
                material.SetVector("_ClipWorldX", new Vector4(renderRoot.transform.position.x - InnerHalfWidth,
                    renderRoot.transform.position.x + InnerHalfWidth, 1, 0));
            else
            {
                material.SetVector("_ClipLocalX", new Vector4(-BayHalfWidth, BayHalfWidth, 1, 0));
                material.SetMatrix("_ClipWorldToLocal", BayRoot.worldToLocalMatrix);
            }
        }



        private void OnEnable()
        {
            if (renderRoot != null) renderRoot.SetActive(true);
        }

        private void OnDisable()
        {
            if (renderRoot != null) renderRoot.SetActive(false);
        }

        public void ReleasePresentation()
        {
            Clear();
            foreach (UnityEngine.Object resource in ownedResources) Release(resource);
            ownedResources.Clear();
            sigils.Clear();
            texture = null;
            renderCamera = null;
            trayRoot = null;
            sharedScene = null;
            initialized = false;
            normalizedScroll = 0;
        }

        private void OnDestroy() => ReleasePresentation();

        private static void Release(UnityEngine.Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
