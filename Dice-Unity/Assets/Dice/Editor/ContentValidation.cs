using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Editor
{
    /**
     * Checks authored content without playing: the game's own load-time validation, host and clip loops and host
     * transitions, data texture imports, what the Application scene references and missing scripts. Checks 2 to 4 report
     * magnitudes for judgement rather than deciding pass or fail. Every check collects all it finds: a failure for one asset
     * is recorded against that asset and the check continues with the next.
     *
     * It only reads. Scenes and prefabs are inspected in preview scenes, which are closed again, so nothing is modified,
     * saved, reimported or dirtied and the editor's scene setup stays as it was. Scenes are read as saved.
     */
    public static class ContentValidation
    {
        /** The folders whose assets, prefabs and scenes the project-wide checks read. */
        public static readonly string[] ContentRoots = { "Assets/Dice", "Assets/Scenes" };
        public const string ApplicationScenePath = "Assets/Scenes/Application.unity";
        /** Value differences at or below this, in the property's own unit, are noise and not listed. */
        public const float ValueNoiseFloor = .0001f;
        /** Slope differences at or below this, in the property's unit per second, are noise and not listed. */
        public const float SlopeNoiseFloor = .001f;
        /** The command line argument naming the text file RunFromCommandLine writes the report to. */
        public const string ReportArgument = "-contentValidationReport";
        // Any seed: starting the encounter checks its content, not a particular opening.
        private const ulong ValidationSeed = 1;
        private const string PreviewClipPrefix = "__preview__";

        /** Runs every check and returns what they found. */
        public static ContentValidationReport Run()
        {
            var game = new ContentValidationCheck("1. Game validation",
                "The checks the game makes at startup, when a journey is validated, when a table scene opens and when its encounter " +
                "starts: application configuration, catalog, charm skin, journeys, and for each table a journey uses, its encounter " +
                "scene's screen, host and sigil, tray and rig, piece lights, ambient activity bindings, audio and rule and piece " +
                "presenters, with errors logged while the scene opens.");
            var content = LoadContent(game);
            var tables = JourneyTables(content.Configuration);
            foreach (var use in tables) ValidateTable(game, content, use);
            int journeys = content.Configuration?.JourneyCatalog?.Entries.Count(journey => journey != null) ?? 0;
            game.Coverage = journeys + " journeys; tables " + Names(tables.Select(use => use.Table.name +
                (use.Encounters.Count > 0 ? " with " + string.Join(", ", use.Encounters.Select(encounter => encounter.name)) : "")));
            var hosts = tables.Select(use => use.Table.HostPresentation).Where(host => host != null).Distinct().ToList();
            var checks = new List<ContentValidationCheck>
            {
                game,
                HostIdleLoops(content.Configuration, hosts),
                HostTransitions(content.Configuration, hosts),
                LoopingClips(),
                DataTextureImports(),
                ApplicationSceneReferences(),
                MissingScripts()
            };
            return new ContentValidationReport(checks, DateTime.Now);
        }

        /**
         * Batch mode entry point: runs every check and writes the report as text to the path following -contentValidationReport,
         * for example: Unity.exe -batchmode -projectPath Dice-Unity -executeMethod Dice.Editor.ContentValidation.RunFromCommandLine
         * -contentValidationReport report.txt -quit
         */
        public static void RunFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, ReportArgument);
            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("Content validation requires " + ReportArgument + " <path>.");
            string path = Path.GetFullPath(arguments[index + 1]);
            string text = Run().ToText();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            Debug.Log("Content validation report written to " + path);
        }

        private sealed class LoadedContent
        {
            public DiceApplicationConfig Configuration;
            public MechanicalContent Content;
        }

        /** A table scene a journey uses and the encounters its Encounter beats play there. */
        private sealed class TableUse
        {
            public EncounterTablePresentationAsset Table;
            public readonly List<EncounterAsset> Encounters = new List<EncounterAsset>();
        }

        // Check 1: startup's configuration, catalog and skin, then each journey and the journey catalog against the catalog.
        private static LoadedContent LoadContent(ContentValidationCheck check)
        {
            var loaded = new LoadedContent();
            var configurations = ApplicationConfigurationAssets.FindAll();
            if (configurations.Count != 1)
            {
                check.Add("", "The project requires exactly one application configuration (DiceApplicationConfig); it has " +
                    configurations.Count + ". The checks that need it are skipped.");
                return loaded;
            }
            var (path, configuration) = configurations[0];
            loaded.Configuration = configuration;
            Record(check, path, configuration.Validate);
            if (configuration.CatalogAsset != null)
                Record(check, AssetPath(configuration.CatalogAsset), () => loaded.Content = configuration.CatalogAsset.Build());
            if (loaded.Content == null) return loaded;
            if (configuration.ModifierSkin != null)
                Record(check, AssetPath(configuration.ModifierSkin),
                    () => new ModifierPresentationCatalog(configuration.ModifierSkin, loaded.Content));
            var catalog = configuration.JourneyCatalog;
            if (catalog == null) return loaded;
            var journeyFailures = new HashSet<string>();
            foreach (var journey in catalog.Entries)
                if (journey != null)
                {
                    string failure = Failure(() => journey.Validate(loaded.Content.Catalog));
                    if (failure == null) continue;
                    journeyFailures.Add(failure);
                    check.Add(AssetPath(journey), failure);
                }
            // The catalog's own checks run after its journeys', so a journey's failure is not listed twice.
            string catalogFailure = Failure(() => catalog.Validate(loaded.Content.Catalog));
            if (catalogFailure != null && !journeyFailures.Contains(catalogFailure)) check.Add(AssetPath(catalog), catalogFailure);
            return loaded;
        }

        /** Every table scene the configuration's journeys use, in catalog order, with the encounters played there. */
        private static List<TableUse> JourneyTables(DiceApplicationConfig configuration)
        {
            var uses = new List<TableUse>();
            if (configuration == null || configuration.JourneyCatalog == null) return uses;
            TableUse Use(EncounterTablePresentationAsset table)
            {
                var use = uses.FirstOrDefault(candidate => candidate.Table == table);
                if (use == null) uses.Add(use = new TableUse { Table = table });
                return use;
            }
            foreach (var journey in configuration.JourneyCatalog.Entries)
            {
                if (journey == null) continue;
                for (int index = 0; index < journey.Beats.Count; ++index)
                {
                    // A malformed journey is reported by the journey validation; its tables are taken as far as they resolve.
                    EncounterTablePresentationAsset table, played;
                    try
                    {
                        table = journey.TableSceneAt(index);
                        played = journey.SceneAt(index)?.Table;
                    }
                    catch (Exception) { continue; }
                    if (table != null) Use(table);
                    var encounter = journey.EncounterBeatAt(index)?.Encounter;
                    if (played != null && encounter != null && !Use(played).Encounters.Contains(encounter))
                        Use(played).Encounters.Add(encounter);
                }
            }
            return uses;
        }

        // Check 1: each table's scene opened and its screen validated, once per encounter played there or once without one.
        private static void ValidateTable(ContentValidationCheck check, LoadedContent content, TableUse use)
        {
            if (use.Encounters.Count == 0)
            {
                ValidateTableScene(check, use.Table, null, null);
                return;
            }
            foreach (var encounter in use.Encounters)
            {
                EncounterDefinition definition = null;
                if (content.Content != null)
                {
                    // Accepting the invitation starts the encounter from the catalog, as the controller does.
                    Record(check, AssetPath(encounter), () =>
                    {
                        definition = content.Content.Catalog.GetEncounter(encounter.Identity);
                        new EncounterApplication(content.Content.Catalog, encounter.Identity, ValidationSeed);
                    });
                }
                else check.Add(AssetPath(encounter), "Not started, and its table's rule and piece presenters not created: " +
                    "the catalog did not build.");
                ValidateTableScene(check, use.Table, encounter, definition);
            }
        }

        private static void ValidateTableScene(ContentValidationCheck check, EncounterTablePresentationAsset table,
            EncounterAsset encounter, EncounterDefinition definition)
        {
            string label = "Table " + table.name + (encounter != null ? ", encounter " + encounter.name : "") + ": ";
            string path;
            try { path = SpiritEncounterPreviewScene.PathFor(table); }
            catch (Exception exception)
            {
                check.Add(AssetPath(table), label + Describe(exception));
                return;
            }
            var logged = new List<string>();
            void Capture(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) logged.Add(condition);
            }
            Scene scene = default;
            SpiritEncounterView view = null;
            Application.logMessageReceived += Capture;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(path);
                var views = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).ToArray();
                if (views.Length != 1)
                    throw new InvalidOperationException("The scene requires exactly one encounter screen; it has " + views.Length + ".");
                view = views[0];
                // A scene copy never plays sound; the audio's checks read its sources, not whether they are enabled.
                foreach (var source in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioSource>(true)))
                    source.enabled = false;
                view.ValidateContent(definition);
            }
            catch (Exception exception) { check.Add(path, label + Describe(exception)); }
            finally
            {
                Application.logMessageReceived -= Capture;
                if (view != null && view.TrayScene != null)
                {
                    view.TrayScene.DiceTray?.ReleasePresentation();
                    view.TrayScene.HistoryRenderer?.ReleasePresentation();
                    view.TrayScene.ReleasePresentation();
                }
                // Building the screen built its host, which receives no OnDestroy in edit mode.
                if (view != null && view.HostPortrait != null) view.HostPortrait.ReleaseOwnedResources();
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
            foreach (string message in logged) check.Add(path, label + "logged while opening and validating: " + message);
        }

        // Check 2: each looping sequence track of the hosts the tables use, its end against its start.
        private static ContentValidationCheck HostIdleLoops(DiceApplicationConfig configuration,
            IReadOnlyList<SpiritHostPresentationAsset> hosts)
        {
            var check = new ContentValidationCheck("2. Host loop ends",
                "For each host a journey table uses, every track of a looping sequence: the difference between its value at the " +
                "loop's end and at its start, and between its slopes there. Magnitudes for judgement, largest value jump first, " +
                "listed above the noise floor of " + Format(ValueNoiseFloor) + " (value) or " + Format(SlopeNoiseFloor) +
                " per second (slope). Units: position and size in units, rotation in degrees, alpha, deformation weight.");
            if (configuration == null) check.Add("", "No hosts measured: the application configuration did not load.");
            int measured = 0;
            foreach (var host in hosts)
            {
                string path = AssetPath(host);
                Record(check, path, () =>
                {
                    foreach (var sequence in host.Sequences)
                    {
                        if (sequence == null || !sequence.Loop) continue;
                        foreach (var track in sequence.Tracks)
                        {
                            ++measured;
                            var join = CurveLoopJoin.Measure(track.Curve, sequence.Duration);
                            if (join.ValueJump <= ValueNoiseFloor && join.SlopeJump <= SlopeNoiseFloor) continue;
                            string unit = HostTransitionJumps.Unit(track.Property);
                            check.Add(new ContentValidationEntry(path, null, host.name + " " + sequence.State + " sequence " +
                                sequence.Id + ", layer " + host.Layers[track.Layer].Id + ", " +
                                HostTransitionJumps.Describe(host, track.Layer, track.Property, track.Channel) + ": value jump " +
                                Format(join.ValueJump) + " " + unit + ", slope jump " + Format(join.SlopeJump) + " " + unit + "/s",
                                join.ValueJump));
                        }
                    }
                });
            }
            check.Coverage = measured + " looping tracks of the hosts " + Names(hosts.Select(host => host.name));
            check.SortByMagnitude();
            return check;
        }

        // Check 3: each host's portrait built in a preview scene and its transitions measured.
        private static ContentValidationCheck HostTransitions(DiceApplicationConfig configuration,
            IReadOnlyList<SpiritHostPresentationAsset> hosts)
        {
            var check = new ContentValidationCheck("3. Host transitions",
                "For each host a journey table uses, its portrait prefab built in a preview scene: the largest jump of each layer " +
                "value as Arrival starts (from Waiting, which precedes it in the journeys), a Reaction starts and ends, and each " +
                "outcome state is entered (from Idle), for the states the host authors, sampled at " +
                HostTransitionJumps.IdlePhaseCount + " idle phases across its longest idle loop. A layer's position, size, " +
                "rotation and deformation jumps count only where it shows (alpha of at least half an 8-bit step) before and after. " +
                "Magnitudes for judgement, largest first, listed above the noise floor of " + Format(ValueNoiseFloor) +
                ". Units: position and size in units, rotation in degrees, alpha (unclamped), deformation weight.");
            if (configuration == null) check.Add("", "No hosts measured: the application configuration did not load.");
            check.Coverage = "the hosts " + Names(hosts.Select(host => host.name));
            foreach (var host in hosts)
            {
                string path = AssetPath(host);
                Scene scene = default;
                SpiritHostPortrait portrait = null;
                try
                {
                    if (host.PortraitPrefab == null) throw new InvalidOperationException("The host has no portrait prefab.");
                    scene = EditorSceneManager.NewPreviewScene();
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(host.PortraitPrefab.transform.root.gameObject, scene);
                    instance.SetActive(true);
                    portrait = instance.GetComponentsInChildren<SpiritHostPortrait>(true).Single();
                    portrait.Build(host);
                    portrait.SetVisible(true);
                    foreach (var jump in HostTransitionJumps.Measure(portrait, host))
                    {
                        if (jump.Jump <= ValueNoiseFloor) continue;
                        check.Add(new ContentValidationEntry(path, null, host.name + " " + jump.Transition + ", layer " +
                            host.Layers[jump.Layer].Id + ", " + HostTransitionJumps.Describe(host, jump.Layer, jump.Property, jump.Channel) +
                            ": jump " + Format(jump.Jump) + " " + HostTransitionJumps.Unit(jump.Property) +
                            " (largest from idle time " + Format(jump.Phase) + " s)", jump.Jump));
                    }
                }
                catch (Exception exception) { check.Add(path, Describe(exception)); }
                finally
                {
                    // An edit-mode portrait receives no OnDestroy; a check that failed before finding one has none.
                    if (portrait != null) portrait.ReleaseOwnedResources();
                    if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            check.SortByMagnitude();
            return check;
        }

        // Check 4: every animation clip with loop time on, each curve's end against its start.
        private static ContentValidationCheck LoopingClips()
        {
            var check = new ContentValidationCheck("4. Looping animation clips",
                "Every animation clip under " + string.Join(", ", ContentRoots) + " with Loop Time on: for each curve, the " +
                "difference between its value at the clip's length and at 0, and between its slopes there. Magnitudes for " +
                "judgement, largest value jump first, listed above the noise floor of " + Format(ValueNoiseFloor) + " (value) or " +
                Format(SlopeNoiseFloor) + " per second (slope), in the animated property's own unit.");
            var looping = new List<string>();
            foreach (string path in AssetPaths("t:AnimationClip"))
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith(PreviewClipPrefix, StringComparison.Ordinal)) continue;
                    Record(check, path, () =>
                    {
                        if (!AnimationUtility.GetAnimationClipSettings(clip).loopTime) return;
                        looping.Add(clip.name);
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        {
                            var join = CurveLoopJoin.Measure(AnimationUtility.GetEditorCurve(clip, binding), clip.length);
                            if (join.ValueJump <= ValueNoiseFloor && join.SlopeJump <= SlopeNoiseFloor) continue;
                            string unit = ClipUnit(binding.propertyName);
                            check.Add(new ContentValidationEntry(path, null, clip.name + " " +
                                (string.IsNullOrEmpty(binding.path) ? "" : binding.path + " ") + binding.type.Name + "." +
                                binding.propertyName + ": value jump " + Format(join.ValueJump) + " " + unit + ", slope jump " +
                                Format(join.SlopeJump) + " " + unit + "/s (length " + Format(clip.length) + " s)", join.ValueJump));
                        }
                    });
                }
            check.Coverage = "the looping clips " + Names(looping);
            check.SortByMagnitude();
            return check;
        }

        private static string ClipUnit(string property)
        {
            if (property.IndexOf("EulerAngles", StringComparison.OrdinalIgnoreCase) >= 0) return "degrees";
            if (property.IndexOf("weight", StringComparison.OrdinalIgnoreCase) >= 0) return "weight";
            if (property.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0 && property.EndsWith(".a", StringComparison.Ordinal))
                return "alpha";
            if (property.IndexOf("Position", StringComparison.OrdinalIgnoreCase) >= 0) return "position";
            return "(property value)";
        }

        // Check 5: data textures (response maps and depth) that import as sRGB color.
        private static ContentValidationCheck DataTextureImports()
        {
            var check = new ContentValidationCheck("5. Response and depth map imports",
                "Every texture used as a response map or depth map, by a scene lighting light or a lit layer (in assets, prefabs " +
                "and scenes) or held in those slots by a lit-layer material, whose import has sRGB on. The lighting shaders read " +
                "them as linear data.");
            var referrers = new Dictionary<Texture, List<string>>();
            var order = new List<Texture>();
            void Note(Texture texture, string referrer)
            {
                if (texture == null) return;
                if (!referrers.TryGetValue(texture, out var list))
                {
                    referrers.Add(texture, list = new List<string>());
                    order.Add(texture);
                }
                if (!list.Contains(referrer)) list.Add(referrer);
            }
            foreach (string path in AssetPaths("t:ScriptableObject"))
                Record(check, path, () =>
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>())
                        FindDataTextures(asset, path, Note);
                });
            foreach (string path in AssetPaths("t:Prefab"))
                Record(check, path, () =>
                {
                    foreach (var component in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Component>(true))
                        if (component != null) FindDataTextures(component, path + " > " + HierarchyPath(component.transform), Note);
                });
            foreach (string path in AssetPaths("t:Material"))
                Record(check, path, () =>
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!SceneLitLayerMaterial.UsesLitShader(material)) return;
                    foreach (int id in SceneLitLayerMaterial.ResponseMapIds.Append(SceneLitLayerMaterial.DepthTextureId))
                        if (material.HasProperty(id)) Note(material.GetTexture(id), path);
                });
            ForEachScene(check, (path, scene) =>
            {
                foreach (var component in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)))
                    if (component != null) FindDataTextures(component, path + " > " + HierarchyPath(component.transform), Note);
            });
            check.Coverage = order.Count + " data textures: " + Names(order.Select(texture => texture.name));
            foreach (var texture in order)
            {
                string path = AssetDatabase.GetAssetPath(texture);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || !importer.sRGBTexture) continue;
                check.Add(path, "Imports with sRGB on and is read as data by: " + string.Join("; ", referrers[texture]));
            }
            return check;
        }

        // Response maps and depth of every scene light and lit layer the object serializes, wherever they are nested.
        private static void FindDataTextures(UnityEngine.Object owner, string referrer, Action<Texture, string> note)
        {
            using var serialized = new SerializedObject(owner);
            var property = serialized.GetIterator();
            bool enter = true;
            while (property.Next(enter))
            {
                enter = Contains(property);
                string field = property.type == nameof(SceneLitLayer) ? nameof(SceneLitLayer.Depth)
                    : property.type == nameof(SceneLightLayer) ? nameof(SceneLightLayer.ResponseMap)
                    : property.type == nameof(SceneLitLayerLight) ? nameof(SceneLitLayerLight.ResponseMap) : null;
                if (field == null) continue;
                var texture = property.FindPropertyRelative(field)?.objectReferenceValue as Texture;
                note(texture, referrer + " (" + owner.GetType().Name + " " + property.propertyPath + ")");
            }
        }

        // Whether a property can hold a nested class: arrays of plain values, such as mesh offsets, are skipped whole.
        private static bool Contains(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference) return true;
            if (property.propertyType != SerializedPropertyType.Generic) return false;
            if (!property.isArray) return true;
            if (property.arraySize == 0) return false;
            var element = property.GetArrayElementAtIndex(0).propertyType;
            return element == SerializedPropertyType.Generic || element == SerializedPropertyType.ManagedReference;
        }

        // Check 6: presentation content the Application scene would load at startup through a direct reference.
        private static ContentValidationCheck ApplicationSceneReferences()
        {
            var check = new ContentValidationCheck("6. Application scene references",
                "The dependencies of " + ApplicationScenePath + ", followed recursively: any journey scene (table or picture), host " +
                "presentation, tray presentation, prefab with an encounter screen or other scene, which would load at startup.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ApplicationScenePath) == null)
            {
                check.Add(ApplicationScenePath, "The Application scene is missing.");
                return check;
            }
            // Each dependency is listed once, through the first direct reference that reaches it.
            var reached = new HashSet<string>();
            foreach (string direct in AssetDatabase.GetDependencies(ApplicationScenePath, false))
            {
                if (direct == ApplicationScenePath) continue;
                foreach (string path in AssetDatabase.GetDependencies(direct, true))
                    if (reached.Add(path)) Record(check, path, () =>
                    {
                        string kind = PresentationKind(path);
                        if (kind == null) return;
                        check.Add(path, "The Application scene depends on " + kind +
                            (path == direct ? " directly." : " through " + direct + "."));
                    });
            }
            check.Coverage = reached.Count + " dependencies";
            return check;
        }

        private static string PresentationKind(string path)
        {
            if (path == ApplicationScenePath) return null;
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == null) return null;
            if (type == typeof(SceneAsset)) return "another scene";
            if (typeof(JourneySceneAsset).IsAssignableFrom(type)) return "a journey scene asset (" + type.Name + ")";
            if (typeof(SpiritHostPresentationAsset).IsAssignableFrom(type)) return "a host presentation";
            if (typeof(SpiritTrayPresentationAsset).IsAssignableFrom(type)) return "a tray presentation";
            if (type == typeof(GameObject) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<SpiritEncounterView>(true) != null)
                return "a prefab with an encounter screen";
            return null;
        }

        // Check 7: missing scripts on every object of every prefab and scene.
        private static ContentValidationCheck MissingScripts()
        {
            var check = new ContentValidationCheck("7. Missing scripts",
                "Every object of every prefab and scene under " + string.Join(", ", ContentRoots) + " with a component whose " +
                "script is missing; its serialized data is silently dropped.");
            void Count(string path, IEnumerable<GameObject> roots)
            {
                foreach (var transform in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missing > 0)
                        check.Add(new ContentValidationEntry(path, HierarchyPath(transform),
                            missing + (missing == 1 ? " missing script." : " missing scripts.")));
                }
            }
            int prefabs = 0, scenes = 0;
            foreach (string path in AssetPaths("t:Prefab"))
            {
                ++prefabs;
                Record(check, path, () => Count(path, new[] { AssetDatabase.LoadAssetAtPath<GameObject>(path) }));
            }
            ForEachScene(check, (path, scene) =>
            {
                ++scenes;
                Count(path, scene.GetRootGameObjects());
            });
            check.Coverage = prefabs + " prefabs and " + scenes + " scenes";
            return check;
        }

        /** Opens each scene under the content roots in a preview scene for reading, and closes it again. */
        private static void ForEachScene(ContentValidationCheck check, Action<string, Scene> visit)
        {
            foreach (string path in AssetPaths("t:Scene"))
            {
                Scene scene = default;
                try
                {
                    scene = EditorSceneManager.OpenPreviewScene(path);
                    visit(path, scene);
                }
                catch (Exception exception) { check.Add(path, Describe(exception)); }
                finally
                {
                    if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static IEnumerable<string> AssetPaths(string filter)
            => AssetDatabase.FindAssets(filter, ContentRoots).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path,
                StringComparer.Ordinal);

        /** Runs one asset's part of a check, recording its failure against the asset. */
        private static void Record(ContentValidationCheck check, string path, Action action)
        {
            string failure = Failure(action);
            if (failure != null) check.Add(path, failure);
        }

        private static string Failure(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception exception) { return Describe(exception); }
        }

        private static string Describe(Exception exception) => exception.GetType().Name + ": " + exception.Message;

        private static string AssetPath(UnityEngine.Object asset) => asset != null ? AssetDatabase.GetAssetPath(asset) : "";

        internal static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        private static string Format(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

        private static string Names(IEnumerable<string> names)
        {
            var list = names.ToList();
            return list.Count == 0 ? "none" : string.Join("; ", list);
        }
    }
}
