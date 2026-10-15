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
        private static int rendererOrdinal;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        private readonly Dictionary<uint, Texture2D> sigils = new Dictionary<uint, Texture2D>();
        private GameObject renderRoot;
        private Transform trayRoot;
        private Camera renderCamera;
        private RenderTexture texture;
        private Mesh bodyMesh;
        private Mesh shadowMesh;
        private Vector3[] supportPoints;
        private Material shadowMaterial;
        private Shader shader;
        private float normalizedScroll;
        private int resolvingIndex = -1;
        private SceneLightingState sceneLighting;
        private SpiritTrayScene sharedScene;
        private Transform contentRoot;
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
            internal Transform Die;
            internal MeshRenderer Renderer;
            internal MeshRenderer Shadow;
            internal Material Material;
            internal float Elapsed;
            internal float SupportHeight;
            internal Color BoneColor;
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
            if (renderRoot != null) return;
            if (trayPresentation == null) throw new InvalidOperationException("Configure the modifier history presentation before initialization.");
            trayPresentation.Validate();
            shader = trayPresentation.DiceShader;
            renderRoot = new GameObject("Spirit Modifier History Render Root");
            renderRoot.layer = SpiritDiceTray.RenderLayer;
            if (sharedScene != null)
            {
                sharedScene.Initialize();
                renderRoot.transform.SetParent(sharedScene.Geometry.ModifierRoot, false);
                texture = sharedScene.Texture;
                renderCamera = sharedScene.RenderCamera;
                trayRoot = sharedScene.Geometry.ModifierRoot;
            }
            else
            {
                renderRoot.transform.position = new Vector3(12000 + rendererOrdinal++ * 100, 0, 0);
                texture = new RenderTexture(1536, 192, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Spirit Modifier History Texture",
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();
                renderCamera = Child("Spirit Modifier History Camera", renderRoot.transform).AddComponent<Camera>();
                renderCamera.transform.localPosition = new Vector3(0, 6.8f, -4.8f);
                renderCamera.transform.LookAt(renderRoot.transform.position + Vector3.up * .20f);
                renderCamera.orthographic = false;
                renderCamera.fieldOfView = 6.9f;
                renderCamera.aspect = (float)texture.width / texture.height;
                renderCamera.nearClipPlane = .1f;
                renderCamera.farClipPlane = 30;
                renderCamera.clearFlags = CameraClearFlags.SolidColor;
                renderCamera.backgroundColor = Color.clear;
                renderCamera.cullingMask = 1 << SpiritDiceTray.RenderLayer;
                renderCamera.allowHDR = false;
                renderCamera.allowMSAA = true;
                renderCamera.targetTexture = texture;
            }
            contentRoot = renderRoot.transform;

            bodyMesh = SpiritDiceTray.CreateRoundedCube(false, .18f, 32);
            bodyMesh.name = "Spirit Modifier Rounded Bone";
            shadowMesh = SpiritDiceTray.CreateShadowPlane();
            ownedResources.Add(bodyMesh);
            ownedResources.Add(shadowMesh);
            supportPoints = bodyMesh.vertices;
            shadowMaterial = Surface("Spirit Modifier Contact Shadow", new Color(.025f, .018f, .011f, .66f), 5, true);
            SetClip(shadowMaterial);
            if (sharedScene == null)
            {
                Mesh boxMesh = SpiritDiceTray.CreateRoundedCube(false, .09f, 8);
                ownedResources.Add(boxMesh);
                BuildTray(boxMesh);
            }
            renderRoot.SetActive(isActiveAndEnabled);
        }

        public void Clear()
        {
            foreach (Entry entry in entries)
            {
                entry.Die.gameObject.SetActive(false);
                entry.Shadow.gameObject.SetActive(false);
                Release(entry.Die.gameObject);
                Release(entry.Shadow.gameObject);
                ownedResources.Remove(entry.Material);
                Release(entry.Material);
            }
            entries.Clear();
            normalizedScroll = 0;
            resolvingIndex = -1;
        }

        public void Add(ModifierContribution contribution, ModifierDefinition definition, bool animate)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (contribution.DefinitionId != definition.Id)
                throw new ArgumentException("The modifier definition must match the activation.", nameof(definition));
            Initialize();
            Material material = Surface("Spirit Modifier Bone " + entries.Count, new Color(1, .97f, .9f), 0);
            material.SetFloat("_ShowPips", 0);
            material.SetFloat("_ShowFaceIcon", 1);
            material.SetFloat("_Seed", entries.Count * 17.31f);
            Texture2D bone = trayPresentation.BoneTexture;
            if (bone != null) material.SetTexture("_SurfaceTexture", bone);
            Color ink = SpiritModifierIcons.GetEffectColor(contribution);
            material.SetColor("_FaceIconColor", new Color(ink.r * .45f, ink.g * .45f, ink.b * .45f, 1));
            SetIcon(material, definition);
            SetClip(material);
            Transform die = Child("Spirit Modifier Die " + entries.Count, contentRoot).transform;
            die.localScale = Vector3.one * ContentDieSize;
            var entry = new Entry
            {
                Die = die,
                Renderer = AddMesh(die.gameObject, bodyMesh, material),
                Shadow = AddMesh(Child("Modifier Contact Shadow " + entries.Count, contentRoot), shadowMesh, shadowMaterial),
                Material = material,
                Elapsed = animate ? 0 : ArrivalDuration,
                BoneColor = material.GetColor("_BaseColor")
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
                    minimum = Mathf.Min(minimum, (entry.Die.localRotation * point).y * ContentDieSize);
                entry.SupportHeight = -minimum;
            }
            float overflow = Mathf.Max(0, (entries.Count - Capacity) * ContentSlotSpacing);
            float x = -(Capacity - 1) * ContentSlotSpacing * .5f + index * ContentSlotSpacing - overflow * normalizedScroll;
            x -= .20f * remaining;
            float lift = .08f * remaining + (index == resolvingIndex ? ContentDieSize * .12f : 0);
            entry.Die.localPosition = new Vector3(x, SurfaceHeight + entry.SupportHeight + lift, 0);
            entry.Shadow.transform.localPosition = new Vector3(x, SurfaceHeight + .003f, 0);
            entry.Shadow.transform.localScale = new Vector3(ContentDieSize * (1.08f + lift), 1, ContentDieSize * (1.08f + lift));
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_ShadowLift", lift / .08f);
            entry.Shadow.SetPropertyBlock(properties);
        }

        private void SetIcon(Material material, ModifierDefinition definition)
        {
            Sprite sprite = SpiritModifierIcons.GetSprite(definition);
            if (sprite != null)
            {
                Vector2 min = Vector2.one;
                Vector2 max = Vector2.zero;
                foreach (Vector2 uv in sprite.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
                material.SetTexture("_FaceIcon", sprite.texture);
                material.SetVector("_FaceIconRect", new Vector4(min.x, min.y, max.x - min.x, max.y - min.y));
                Vector4 transform = new Vector4(1, 1, 0, 0);
                if (sprite.packed)
                {
                    if (sprite.packingRotation == SpritePackingRotation.FlipHorizontal) transform.x = -1;
                    if (sprite.packingRotation == SpritePackingRotation.FlipVertical) transform.y = -1;
                    if (sprite.packingRotation == SpritePackingRotation.Rotate180) { transform.x = -1; transform.y = -1; }
                }
                material.SetVector("_FaceIconUVTransform", transform);
            }
            else
            {
                uint seed = SpiritModifierIcons.GetSigilSeed(definition);
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

        private void BuildTray(Mesh boxMesh)
        {
            trayRoot = Child("Spirit Modifier Wooden Tray", renderRoot.transform).transform;
            Texture2D woodTexture = trayPresentation.WoodTexture;
            Material wood = Surface("Spirit Modifier Worn Walnut", woodTexture != null ? Color.white : new Color(.33f, .145f, .052f), 1);
            if (woodTexture != null) wood.SetTexture("_SurfaceTexture", woodTexture);
            Material felt = Surface("Spirit Modifier Deep Green Felt", new Color(.085f, .12f, .067f), 2);
            Material brass = Surface("Spirit Modifier Aged Brass", new Color(.66f, .43f, .16f), 3);
            Box("History Tray Foundation", boxMesh, wood, new Vector3(0, -.04f, 0), new Vector3(5.98f, .10f, .76f));
            Box("History Tray Felt", boxMesh, felt, new Vector3(0, .0175f, 0), new Vector3(5.40f, .035f, .62f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box("History Side Rail " + side, boxMesh, wood, new Vector3(side * 2.84f, .08f, 0), new Vector3(.28f, .19f, .86f));
                Box("History Side Brass " + side, boxMesh, brass, new Vector3(side * 2.84f, .178f, 0), new Vector3(.014f, .006f, .74f));
                Box("History End Rail " + side, boxMesh, wood, new Vector3(0, .075f, side * .365f), new Vector3(5.94f, .18f, .13f));
                Box("History End Brass " + side, boxMesh, brass, new Vector3(0, .168f, side * .365f), new Vector3(5.65f, .006f, .012f));
            }
        }

        private void Box(string name, Mesh mesh, Material material, Vector3 position, Vector3 size)
        {
            GameObject box = Child(name, trayRoot);
            box.transform.localPosition = position;
            bool wood = material.GetFloat("_SurfaceKind") == 1;
            if (wood && size.z > size.x)
            {
                box.transform.localRotation = Quaternion.Euler(0, 90, 0);
                size = new Vector3(size.z, size.y, size.x);
            }
            box.transform.localScale = size;
            MeshRenderer renderer = AddMesh(box, mesh, material);
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_GrainScale", new Vector4(size.x * 2, size.y * 20, size.z * 16, 0));
            properties.SetFloat("_Seed", position.x * 11.17f + position.z * 31.73f);
            properties.SetVector("_SurfaceTexture_ST", new Vector4(size.x * .2f, Mathf.Max(size.y, size.z) * 2,
                position.x * .19f + position.z * .27f, 0));
            renderer.SetPropertyBlock(properties);
        }

        private Material Surface(string name, Color color, int kind, bool transparent = false)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_SurfaceKind", kind);
            if (transparent)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.renderQueue = 3000;
            }
            ownedResources.Add(material);
            SceneLightingReceiverMaterial.Apply(material, sceneLighting, LightingRoot);
            return material;
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

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.layer = SpiritDiceTray.RenderLayer;
            child.transform.SetParent(parent, false);
            return child;
        }

        private static MeshRenderer AddMesh(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
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
            if (sharedScene == null && renderCamera != null) { renderCamera.enabled = false; renderCamera.targetTexture = null; }
            if (sharedScene == null && texture != null) { texture.Release(); Release(texture); }
            if (renderRoot != null) renderRoot.SetActive(false);
            Release(renderRoot);
            foreach (UnityEngine.Object resource in ownedResources) Release(resource);
            ownedResources.Clear();
            sigils.Clear();
            entries.Clear();
            resolvingIndex = -1;
            texture = null;
            renderRoot = null;
            renderCamera = null;
            trayRoot = null;
            contentRoot = null;
            sharedScene = null;
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
