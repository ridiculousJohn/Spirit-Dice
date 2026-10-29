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
        /** Size of the die in the token prefab; tokens scale from it to the tray geometry's modifier die size. */
        public const float DieSize = .49f;
        public const float ArrivalDuration = .30f;
        /** Height an arriving die drops from, in tray units. */
        public const float ArrivalLift = .08f;
        /** Distance an arriving die slides in from the right, in tray units. */
        private const float ArrivalSlide = .20f;
        /** Degrees an arriving die is pitched and rolled as it drops. */
        private const float ArrivalPitch = 14f;
        private const float ArrivalRoll = 8f;
        /** Degrees each slot's die turns from its neighbour, cycling over three slots. */
        private const float SlotYawStep = 4f;
        /** Extra lift of the resolving die, in die sizes. */
        private const float ResolvingLift = .12f;
        private static readonly Color ResolvingColor = new Color(1, .74f, .32f);
        /** A die's shadow size at rest, in die sizes; it grows by the die's lift. */
        private const float ShadowSize = 1.08f;
        /** Height of a die's shadow above the bay floor, in tray units. */
        private const float ShadowHeight = .003f;
        /** Height of the bay the tray frames, in die sizes. */
        private const float BayHeight = 1.5f;
        /** Spacing between the cosmetic seeds of successive tokens. */
        private const float TokenSeedStep = 17.31f;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        [SerializeField] private GameObject renderRoot;
        [SerializeField] private SpiritModifierToken tokenPrefab;
        private bool initialized;
        private Camera renderCamera;
        private RenderTexture texture;
        private Mesh bodyMesh;
        private Vector3[] supportPoints;
        private float normalizedScroll;
        private int resolvingIndex = -1;
        private SceneLightingState sceneLighting;
        private SpiritTrayScene sharedScene;
        [SerializeField] private Transform contentRoot;
        private Transform BayRoot => sharedScene.Geometry.ModifierRoot;
        private float BayHalfWidth => sharedScene.Geometry.ModifierWidth * .5f;
        private float BayDepth => sharedScene.Geometry.ModifierDepth;
        private float ContentDieSize => sharedScene.Geometry.ModifierDieSize;
        private float ContentSlotSpacing => sharedScene.Geometry.ModifierSlotSpacing;
        public int Capacity => sharedScene.Geometry.ModifierVisibleCapacity;

        public RenderTexture Texture => texture;
        public Camera RenderCamera => renderCamera;
        public Transform RenderRoot => sharedScene != null ? sharedScene.RenderRoot : renderRoot != null ? renderRoot.transform : null;
        public int Count => entries.Count;
        public int ResolvingIndex => resolvingIndex;

        public void SetSceneLighting(SceneLightingState state)
        {
            sceneLighting = state;
            if (renderRoot == null) return;
            foreach (UnityEngine.Object resource in ownedResources)
                if (resource is Material material)
                    SceneLightingReceiverMaterial.Apply(material, state, sharedScene.RenderRoot);
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
                    OwnedObject.Release(entry.Token);
                }
                ownedResources.Remove(entry.Material);
                OwnedObject.Release(entry.Material);
                ownedResources.Remove(entry.ShadowMaterial);
                OwnedObject.Release(entry.ShadowMaterial);
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
            if (contribution.DefinitionId != definition.Identity)
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
            SceneLightingReceiverMaterial.Apply(material, sceneLighting, sharedScene.RenderRoot);
            material.SetFloat("_ShowPips", 0);
            material.SetFloat("_ShowFaceIcon", 1);
            material.SetFloat("_Seed", entries.Count * TokenSeedStep);
            material.SetColor("_FaceIconColor", charms.GetInkColor(definition));
            SetSprite(material, charms.GetIcon(definition), "_FaceIcon", "_FaceIconRect", "_FaceIconUVTransform");
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
            if (!FiniteValue.Is(normalized)) throw new ArgumentOutOfRangeException(nameof(normalized));
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
                entry.Material.SetColor("_BaseColor", i == index ? ResolvingColor : entry.BoneColor);
                Position(entry, i, false);
            }
        }

        public void Advance(float seconds)
        {
            if (seconds < 0 || !FiniteValue.Is(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
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
                            new Vector3(x * BayHalfWidth, y * ContentDieSize * BayHeight, z * BayDepth * .5f)));
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
                entry.Die.localRotation = Quaternion.Euler(-ArrivalPitch * remaining, (index % 3 - 1) * SlotYawStep, ArrivalRoll * remaining);
                float minimum = float.MaxValue;
                foreach (Vector3 point in supportPoints)
                    minimum = Mathf.Min(minimum, (entry.Die.localRotation * Vector3.Scale(point, entry.Die.localScale)).y);
                entry.SupportHeight = -minimum;
            }
            float overflow = Mathf.Max(0, (entries.Count - Capacity) * ContentSlotSpacing);
            float x = -(Capacity - 1) * ContentSlotSpacing * .5f + index * ContentSlotSpacing - overflow * normalizedScroll;
            x -= ArrivalSlide * remaining;
            float lift = ArrivalLift * remaining + (index == resolvingIndex ? ContentDieSize * ResolvingLift : 0);
            entry.Die.localPosition = new Vector3(x, entry.SupportHeight + lift, 0);
            entry.Shadow.transform.localPosition = new Vector3(x, ShadowHeight, 0);
            entry.Shadow.transform.localScale = Vector3.Scale(entry.ShadowScale,
                new Vector3(ContentDieSize * (ShadowSize + lift), 1, ContentDieSize * (ShadowSize + lift)));
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_ShadowLift", lift / ArrivalLift);
            entry.Shadow.SetPropertyBlock(properties);
        }

        private static void SetCategory(Material material, ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            Sprite sprite = charms.GetCategorySprite(definition);
            bool visible = sprite != null;
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
            material.SetVector("_ClipLocalX", new Vector4(-BayHalfWidth, BayHalfWidth, 1, 0));
            material.SetMatrix("_ClipWorldToLocal", BayRoot.worldToLocalMatrix);
        }

        // Internal so EditMode tests, where Unity sends no lifecycle messages, drive them directly.
        internal void OnEnable()
        {
            if (renderRoot != null) renderRoot.SetActive(true);
        }

        internal void OnDisable()
        {
            if (renderRoot != null) renderRoot.SetActive(false);
        }

        public void ReleasePresentation()
        {
            Clear();
            foreach (UnityEngine.Object resource in ownedResources) OwnedObject.Release(resource);
            ownedResources.Clear();
            texture = null;
            renderCamera = null;
            sharedScene = null;
            initialized = false;
            normalizedScroll = 0;
        }

        private void OnDestroy() => ReleasePresentation();
    }
}
