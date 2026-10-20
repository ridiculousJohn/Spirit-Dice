using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** First creation bakes editable scene objects and immutable surface assets. */
    public static class SpiritEncounterObjectAuthoring
    {
        public const string RigFolder = "Assets/Dice/Content/Presentation/Rigs";
        public const string UiFolder = "Assets/Dice/Content/Presentation/UI";
        private static Mesh body;
        private static Mesh modifierBody;
        private static Mesh shadow;
        private static Mesh ring;

        public static void CreateAssets()
        {
            Folder(RigFolder);
            Folder(UiFolder);
            Folder(RigFolder + "/Meshes");
            Folder(RigFolder + "/Materials");
            body = MeshAsset("DiceBone", () => SpiritEncounterMeshes.CreateRoundedCube());
            modifierBody = MeshAsset("ModifierBone", () => SpiritEncounterMeshes.CreateRoundedCube(false, .18f, 32));
            shadow = MeshAsset("ContactShadow", SpiritEncounterMeshes.CreateShadowPlane);
            ring = MeshAsset("SelectionFrame", SpiritEncounterMeshes.CreateSelectionRing);
            foreach (string guid in AssetDatabase.FindAssets("t:SpiritTrayPresentationAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SpiritTrayPresentationAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset.GeometryPrefab == null) continue;
                string path = RigFolder + "/" + asset.name + "Rig.prefab";
                var rig = AssetDatabase.LoadAssetAtPath<SpiritTrayScene>(path);
                if (rig == null) rig = CreateRig(asset, path);
                if (asset.RigPrefab == null) { asset.RigPrefab = rig; EditorUtility.SetDirty(asset); }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:SpiritHostPresentationAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(AssetDatabase.GUIDToAssetPath(guid));
                CreateHost(asset);
            }
            CreateHistoryUi();
            AssetDatabase.SaveAssets();
        }

        private static SpiritTrayScene CreateRig(SpiritTrayPresentationAsset asset, string path)
        {
            RequireVacant(path);
            var root = new GameObject(asset.name + "Rig");
            var scene = root.AddComponent<SpiritTrayScene>();
            var stage = Child("Tray Stage", root.transform);
            var geometry = ((GameObject)PrefabUtility.InstantiatePrefab(asset.GeometryPrefab.gameObject, stage.transform))
                .GetComponent<SpiritTrayGeometry>();
            var camera = Child("Tray Camera", stage.transform).AddComponent<Camera>();
            camera.transform.localRotation = Quaternion.Euler(asset.CameraEulerAngles);
            camera.orthographic = asset.Orthographic;
            camera.fieldOfView = asset.FieldOfView;
            camera.aspect = (float)asset.TextureSize.x / asset.TextureSize.y;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << SpiritDiceTray.RenderLayer;
            camera.allowHDR = false;
            var light = Child("Tray Key Light", stage.transform).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.localRotation = Quaternion.Euler(asset.FallbackLightEulerAngles);
            light.color = asset.FallbackLightColor;
            light.intensity = asset.FallbackLightIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .8f;
            light.shadowBias = .025f;
            light.shadowNormalBias = .04f;
            light.cullingMask = 1 << SpiritDiceTray.RenderLayer;
            Set(scene, "presentation", asset);
            Set(scene, "renderRoot", stage);
            Set(scene, "renderCamera", camera);
            Set(scene, "geometry", geometry);
            Set(scene, "light", light);

            var tray = Child("Dice Presentation", stage.transform).AddComponent<SpiritDiceTray>();
            var dice = new Object[DiceValues.Count];
            var slots = new Object[DiceValues.Count];
            var outlines = new Object[DiceValues.Count];
            var frames = new Object[DiceValues.Count];
            var shadows = new Object[DiceValues.Count];
            Material shadowMaterial = Surface(asset, "ContactShadow", new Color(.025f, .018f, .011f, .66f), 5, true);
            Material edge = Surface(asset, "SelectionEdge", new Color(1, .82f, .34f), 6, true, 3030, true);
            Material halo = Surface(asset, "SelectionHalo", new Color(1, .49f, .08f, .32f), 6, true, 3020, true);
            Material outer = Surface(asset, "SelectionOuterHalo", new Color(1, .35f, .035f, .13f), 6, true, 3010, true);
            Material frame = Surface(asset, "SelectionFrame", new Color(1, .72f, .2f, .9f), 6, true);
            Vector3[] normals = { Vector3.forward, Vector3.up, Vector3.right, Vector3.left, Vector3.down, Vector3.back };
            for (int i = 0; i < dice.Length; ++i)
            {
                var slot = Child("Dice Slot Content " + i, geometry.DiceSlots[i]).transform;
                var die = Child("Die " + i, slot);
                die.transform.localScale = Vector3.one * geometry.DiceScale;
                die.transform.localPosition = Vector3.up * .5f * geometry.DiceScale;
                die.transform.localRotation = Quaternion.AngleAxis(i % 4 * 90 + (i - 2) * 3, Vector3.up)
                    * Quaternion.FromToRotation(normals[i], Vector3.up);
                var ivory = Surface(asset, "Ivory" + i, new Color(1, .97f, .9f), 0, seed: i * 17.31f);
                MeshObject(die, body, ivory);
                var selection = Child("Selection Outline", die.transform);
                foreach (var pair in new[] { ("Outer Halo", 1.13f, outer), ("Halo", 1.07f, halo), ("Edge", 1.022f, edge) })
                {
                    var part = Child(pair.Item1, selection.transform);
                    part.transform.localScale = Vector3.one * pair.Item2;
                    MeshObject(part, body, pair.Item3);
                }
                selection.SetActive(false);
                var shadowObject = Child("Contact Shadow", slot);
                shadowObject.transform.localPosition = Vector3.up * .003f;
                shadowObject.transform.localScale = new Vector3(1.24f, 1, 1.16f) * geometry.DiceScale;
                shadows[i] = MeshObject(shadowObject, shadow, shadowMaterial);
                var frameObject = Child("Selection Frame", slot);
                frameObject.transform.localPosition = Vector3.up * .009f;
                frameObject.transform.localScale = Vector3.one * geometry.DiceScale;
                MeshObject(frameObject, ring, frame);
                frameObject.SetActive(false);
                slots[i] = slot; dice[i] = die.transform; outlines[i] = selection; frames[i] = frameObject;
            }
            Set(tray, "renderRoot", tray.gameObject); Set(tray, "trayPresentation", asset);
            SetArray(tray, "dice", dice); SetArray(tray, "slotRoots", slots);
            SetArray(tray, "selectionOutlines", outlines); SetArray(tray, "selectionFrames", frames);
            SetArray(tray, "contactShadows", shadows);
            var history = Child("Modifier Presentation", stage.transform).AddComponent<SpiritModifierHistoryRenderer>();
            var historyRoot = Child("Modifier Tokens", geometry.ModifierRoot);
            Set(history, "renderRoot", historyRoot); Set(history, "contentRoot", historyRoot.transform);
            Set(history, "trayPresentation", asset); Set(history, "tokenPrefab", CreateToken(asset));
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = SpiritDiceTray.RenderLayer;
            scene.FrameAuthoredGeometry();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<SpiritTrayScene>();
        }

        private static SpiritModifierToken CreateToken(SpiritTrayPresentationAsset asset)
        {
            string path = RigFolder + "/" + asset.name + "ModifierToken.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<SpiritModifierToken>(path);
            if (existing != null) return existing;
            RequireVacant(path);
            var root = new GameObject("Modifier Token");
            var token = root.AddComponent<SpiritModifierToken>();
            var bone = Surface(asset, "ModifierIvory", new Color(1, .97f, .9f), 0, modifier: true);
            token.Body = MeshObject(Child("Bone Die", root.transform), modifierBody, bone);
            token.Body.transform.localPosition = Vector3.up * .5f * .49f;
            token.Body.transform.localScale = Vector3.one * .49f;
            token.Shadow = MeshObject(Child("Contact Shadow", root.transform), shadow,
                Surface(asset, "ModifierShadow", new Color(.025f, .018f, .011f, .66f), 5, true));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<SpiritModifierToken>();
        }

        private static void CreateHost(SpiritHostPresentationAsset asset)
        {
            string path = UiFolder + "/" + asset.name + "Portrait.prefab";
            var portrait = AssetDatabase.LoadAssetAtPath<SpiritHostPortrait>(path);
            if (portrait == null)
            {
                RequireVacant(path);
                var root = Rect("Host Portrait", null);
                var component = root.gameObject.AddComponent<SpiritHostPortrait>();
                var layersRoot = Rect("Portrait Layers", root);
                layersRoot.anchorMin = layersRoot.anchorMax = Vector2.zero;
                layersRoot.pivot = Vector2.zero;
                layersRoot.sizeDelta = asset.ReferenceSize;
                layersRoot.anchoredPosition = asset.Offset;
                layersRoot.localScale = new Vector3(asset.Scale, asset.Scale, 1);
                var layers = new Object[asset.Layers.Length];
                for (int i = 0; i < layers.Length; ++i)
                {
                    SpiritPortraitLayer source = asset.Layers[i];
                    var rect = Rect(source.Id, layersRoot);
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.pivot = source.Pivot;
                    rect.anchoredPosition = source.Position;
                    rect.sizeDelta = source.Size;
                    rect.localRotation = Quaternion.Euler(0, 0, source.Rotation);
                    var image = rect.gameObject.AddComponent<SpiritPortraitImage>();
                    image.texture = source.Texture; image.color = source.Tint; image.raycastTarget = false;
                    image.Configure(source.Mesh);
                    if (source.Weights != null && source.Weights.Length == image.Weights.Count) image.SetWeights(source.Weights);
                    if (source.Silhouette && asset.SilhouetteShader != null)
                        image.material = MaterialAsset(asset.name + "Silhouette", () => new Material(asset.SilhouetteShader));
                    if (source.Smoke != null && source.Smoke.Enabled && asset.SmokeShader != null)
                        image.material = MaterialAsset(asset.name + source.Id + "Smoke", () =>
                        {
                            var material = new Material(asset.SmokeShader);
                            material.SetFloat("_Amount", source.Smoke.Distortion);
                            material.SetFloat("_Speed", source.Smoke.Speed);
                            material.SetFloat("_Opacity", source.Smoke.Opacity);
                            material.SetVector("_Flow", new Vector4(source.Smoke.Flow.x, source.Smoke.Flow.y, 0, 0));
                            material.SetColor("_Color", Color.white);
                            material.SetTexture("_MotionMask", source.Smoke.MotionMask != null ? source.Smoke.MotionMask : SmokeTexture(false));
                            material.SetTexture("_NoiseTex", source.Smoke.FlowNoise != null ? source.Smoke.FlowNoise : SmokeTexture(true));
                            return material;
                        });
                    layers[i] = image;
                }
                Set(component, "portraitRoot", layersRoot); SetArray(component, "authoredLayers", layers);
                portrait = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<SpiritHostPortrait>();
                Object.DestroyImmediate(root.gameObject);
            }
            if (asset.PortraitPrefab == null) { asset.PortraitPrefab = portrait; EditorUtility.SetDirty(asset); }
            path = UiFolder + "/" + asset.name + "Sigil.prefab";
            var sigil = AssetDatabase.LoadAssetAtPath<Graphic>(path);
            if (sigil == null)
            {
                RequireVacant(path);
                var root = Rect("Host Sigil", null);
                if (asset.SigilLines != null && asset.SigilLines.Length > 0)
                {
                    var lines = root.gameObject.AddComponent<SpiritHostSigilGraphic>();
                    lines.Configure(asset.SigilLines, asset.SigilWidth); sigil = lines;
                }
                else sigil = root.gameObject.AddComponent<SpiritModifierSigilGraphic>();
                sigil.color = asset.SigilColor; sigil.raycastTarget = false;
                sigil = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<Graphic>();
                Object.DestroyImmediate(root.gameObject);
            }
            if (asset.SigilPrefab == null) { asset.SigilPrefab = sigil; EditorUtility.SetDirty(asset); }
        }

        private static void CreateHistoryUi()
        {
            string tokenPath = UiFolder + "/ModifierHistoryHit.prefab";
            var token = AssetDatabase.LoadAssetAtPath<Button>(tokenPath);
            if (token == null)
            {
                RequireVacant(tokenPath);
                var rect = Rect("Modifier Hit", null);
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.sizeDelta = new Vector2(40, 40);
                var graphic = rect.gameObject.AddComponent<Image>(); graphic.color = Color.clear;
                token = rect.gameObject.AddComponent<Button>(); token.targetGraphic = graphic;
                token.transition = Selectable.Transition.None; token.navigation = new Navigation { mode = Navigation.Mode.None };
                token = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, tokenPath).GetComponent<Button>();
                Object.DestroyImmediate(rect.gameObject);
            }
            string path = UiFolder + "/ModifierHistoryTray.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            RequireVacant(path);
            var root = Rect("Modifier History Tray", null);
            var tray = root.gameObject.AddComponent<SpiritModifierHistoryTray>();
            var frame = Rect("History Scroll", root);
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", frame);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Tokens", viewport);
            content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, .5f);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42; scroll.decelerationRate = .12f;
            Set(tray, "root", root); Set(tray, "content", content); Set(tray, "frame", frame); Set(tray, "scroll", scroll);
            Set(tray, "tokenPrefab", token);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
        }

        public static void AddAudioSources(SpiritEncounterAudio audio)
        {
            var owner = Child("Audio Sources", audio.transform);
            var sources = new AudioSource[7];
            for (int i = 0; i < sources.Length; ++i)
            {
                sources[i] = Child(i == 0 ? "Ambience" : i == 1 ? "Creak" : i == 2 ? "Roll" : "Cue " + (i - 3), owner.transform).AddComponent<AudioSource>();
                sources[i].playOnAwake = false; sources[i].spatialBlend = 0; sources[i].loop = i == 0;
                sources[i].priority = i == 0 ? 192 : 128;
            }
            Set(audio, "ambienceSource", sources[0]); Set(audio, "creakSource", sources[1]); Set(audio, "rollSource", sources[2]);
            SetArray(audio, "cueSources", new Object[] { sources[3], sources[4], sources[5], sources[6] });
        }

        public static void AddActionCues(SpiritActionModifierCue component, Button reroll, Button play)
        {
            AddCue(component, "reroll", reroll);
            AddCue(component, "play", play);
        }

        private static void AddCue(SpiritActionModifierCue component, string property, Button button)
        {
            var root = Rect(property + " Modifier Cue", button.transform);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.sizeDelta = new Vector2(44, 44);
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
            var backing = Rect("Backing", root).gameObject.AddComponent<Image>(); backing.color = new Color(.07f, .09f, .07f, .94f); backing.raycastTarget = false;
            var border = backing.gameObject.AddComponent<Outline>(); border.effectDistance = new Vector2(1, -1);
            var artwork = Rect("Artwork", root).gameObject.AddComponent<Image>(); artwork.preserveAspect = true; artwork.raycastTarget = false;
            artwork.rectTransform.offsetMin = Vector2.one * 3; artwork.rectTransform.offsetMax = Vector2.one * -3;
            var sigil = Rect("Sigil", root).gameObject.AddComponent<SpiritModifierSigilGraphic>(); sigil.raycastTarget = false;
            sigil.rectTransform.offsetMin = Vector2.one * 4; sigil.rectTransform.offsetMax = Vector2.one * -4;
            var category = SpiritCharmPrefabAuthoring.AddCategoryBadge("Charm Category", artwork);
            var text = button.GetComponentInChildren<Text>(true);
            var label = text.GetComponent<CanvasGroup>();
            if (label == null) label = text.gameObject.AddComponent<CanvasGroup>();
            if (button is SpiritUiButtonVisual) Set(button, "untintedContent", root);
            var serialized = new SerializedObject(component);
            var cue = serialized.FindProperty(property);
            cue.FindPropertyRelative("Group").objectReferenceValue = group;
            cue.FindPropertyRelative("Artwork").objectReferenceValue = artwork;
            cue.FindPropertyRelative("Sigil").objectReferenceValue = sigil;
            cue.FindPropertyRelative("Category").objectReferenceValue = category;
            cue.FindPropertyRelative("Border").objectReferenceValue = border;
            cue.FindPropertyRelative("Label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
        }

        private static Material Surface(SpiritTrayPresentationAsset asset, string name, Color color, int kind,
            bool transparent = false, int queue = 3000, bool cullFront = false, float seed = 0, bool modifier = false)
            => MaterialAsset(asset.name + name, () =>
            {
                var material = new Material(asset.DiceShader);
                material.SetColor("_BaseColor", color); material.SetFloat("_SurfaceKind", kind);
                material.SetFloat("_Smoothness", .23f); material.SetFloat("_Seed", seed);
                if (kind == 0) material.SetTexture("_SurfaceTexture", asset.BoneTexture);
                if (modifier) { material.SetFloat("_ShowPips", 0); material.SetFloat("_ShowFaceIcon", 1); }
                if (transparent)
                {
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0); material.renderQueue = queue;
                }
                if (cullFront) material.SetFloat("_Cull", 1);
                return material;
            });

        private static Material MaterialAsset(string name, Func<Material> create)
        {
            string path = RigFolder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            RequireVacant(path);
            material = create(); material.name = name; AssetDatabase.CreateAsset(material, path); return material;
        }
        private static Mesh MeshAsset(string name, Func<Mesh> create)
        {
            string path = RigFolder + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            RequireVacant(path);
            mesh = create(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static MeshRenderer MeshObject(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            bool transparent = material.renderQueue >= 3000;
            renderer.shadowCastingMode = transparent ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = !transparent; return renderer;
        }
        private static Texture2D SmokeTexture(bool noise)
        {
            string path = RigFolder + "/Materials/Smoke" + (noise ? "Noise" : "Mask") + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;
            RequireVacant(path);
            int size = noise ? 128 : 256;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = noise ? "Smoke Noise" : "Smoke Mask", wrapMode = noise ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    float u = (float)x / (size - 1), v = (float)y / (size - 1);
                    float motion = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .36f, Mathf.Abs(u - .5f)));
                    pixels[y * size + x] = noise ? new Color(Noise(u, v, 17), Noise(u, v, 83), 0, 1) : new Color(motion, motion, motion, 1);
                }
            texture.SetPixels(pixels); texture.Apply(false, true); AssetDatabase.CreateAsset(texture, path); return texture;
        }
        private static float Noise(float u, float v, float offset)
        {
            float x = u * 4 + offset, y = v * 4 + offset;
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - 4, y), u),
                Mathf.Lerp(Mathf.PerlinNoise(x, y - 4), Mathf.PerlinNoise(x - 4, y - 4), u), v);
        }
        private static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("An authored asset already exists at " + path + ". Assign its required component explicitly.");
        }
        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name); child.layer = SpiritDiceTray.RenderLayer; child.transform.SetParent(parent, false); return child;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; return rect;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(Object target, string name, Object[] values)
        {
            var serialized = new SerializedObject(target); var array = serialized.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; ++i) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
