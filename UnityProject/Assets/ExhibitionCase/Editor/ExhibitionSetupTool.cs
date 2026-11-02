using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMysteryExhibition.Editor
{
    public static class ExhibitionSetupTool
    {
        private const string CaseAssetPath = "Assets/ExhibitionCase/Resources/CaseConfig.asset";
        private const string ReportPath = "Assets/ExhibitionCase/Data/Generated/asset-import-report.json";

        [MenuItem("Tools/Exhibition/Setup Demo")]
        public static void SetupDemo()
        {
            SetupDemoInternal(true);
        }

        internal static void SetupDemoInternal(bool showDialog)
        {
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var packageRoot = Directory.GetParent(projectRoot).FullName;
                EnsureFolders();
                CopyManifestAssets(packageRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                ConfigureImageImportSettings();
                var config = ImportCaseConfig(packageRoot);
                DossierCanvasPreviewBuilder.EnsureLayoutAsset(config);
                AssetDatabase.SaveAssets();
                CreatePrefabs(config);
                CreateScenes(config);
                ConfigurePlayerSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("[Exhibition] Setup Demo completed. Generated CaseConfig, scenes, prefabs, and the asset mapping report." );
                if (showDialog) EditorUtility.DisplayDialog("Exhibition Setup Demo", "Build complete. Open 00_Boot or press Play to run the Mock flow.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Exhibition] Setup Demo failed: " + ex);
                if (showDialog) EditorUtility.DisplayDialog("Exhibition Setup Demo", "Failed: " + ex.Message, "OK");
            }
        }

        [MenuItem("Tools/Exhibition/Validate Case")]
        public static void ValidateCase()
        {
            var config = AssetDatabase.LoadAssetAtPath<CaseConfigAsset>(CaseAssetPath);
            if (config == null)
            {
                EditorUtility.DisplayDialog("Validate Case", "CaseConfig.asset was not found. Run Setup Demo first.", "OK");
                return;
            }
            config.Validate(out var errors, out var warnings);
            var report = new List<string>();
            report.Add(errors.Count == 0 ? "PASS: no structural errors." : "FAIL: structural errors were found." );
            report.AddRange(errors.Select(e => "ERROR: " + e));
            report.AddRange(warnings.Select(w => "WARN: " + w));
            var missing = config.assets == null ? new List<AssetBinding>() : config.assets.Where(a => a != null && a.kind != AssetKind.database && ((a.kind == AssetKind.video && a.video == null) || (a.kind != AssetKind.video && a.texture == null))).ToList();
            report.AddRange(missing.Select(a => "MISSING ASSET: " + a.id + " <- " + a.sourcePath));
            Debug.Log("[Exhibition] Validate Case\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Validate Case", string.Join("\n", report.Take(16)), "OK");
        }

        private static void EnsureFolders()
        {
            var folders = new[]
            {
                "Assets/ExhibitionCase", "Assets/ExhibitionCase/Resources", "Assets/ExhibitionCase/Data", "Assets/ExhibitionCase/Data/Generated",
                "Assets/ExhibitionCase/Imported", "Assets/ExhibitionCase/Imported/Images", "Assets/ExhibitionCase/Imported/Videos",
                "Assets/ExhibitionCase/Scenes", "Assets/ExhibitionCase/Prefabs", "Assets/ExhibitionCase/Prefabs/Systems",
                "Assets/ExhibitionCase/Prefabs/Vuforia", "Assets/ExhibitionCase/Scripts", "Assets/ExhibitionCase/Tests/EditMode"
            };
            foreach (var folder in folders) EnsureAssetFolder(folder);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            var absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absolute)) Directory.CreateDirectory(absolute);
        }

        private static void CopyManifestAssets(string packageRoot)
        {
            var manifestPath = Path.Combine(packageRoot, "content", "assets.manifest.json");
            var manifest = JsonUtility.FromJson<AssetManifestDocument>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.assets == null) throw new InvalidOperationException("assets.manifest.json could not be parsed or contains no assets." );

            var report = new List<string> { "{", "  \"generatedBy\": \"Tools/Exhibition/Setup Demo\",", "  \"assets\": [" };
            var rows = new List<string>();
            foreach (var entry in manifest.assets)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                if (string.IsNullOrWhiteSpace(entry.sourcePath))
                {
                    rows.Add($"    {{\"id\":\"{Escape(entry.id)}\",\"status\":\"unassigned\",\"sourcePath\":null}}" );
                    continue;
                }
                var sourceAbsolute = Path.GetFullPath(Path.Combine(packageRoot, entry.sourcePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(sourceAbsolute))
                {
                    rows.Add($"    {{\"id\":\"{Escape(entry.id)}\",\"status\":\"missing\",\"sourcePath\":\"{Escape(entry.sourcePath)}\"}}" );
                    continue;
                }
                var extension = Path.GetExtension(sourceAbsolute);
                var targetFolder = entry.kind == "video" ? "Assets/ExhibitionCase/Imported/Videos" : "Assets/ExhibitionCase/Imported/Images";
                var targetAssetPath = targetFolder + "/" + entry.id + extension.ToLowerInvariant();
                var targetAbsolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, targetAssetPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(targetAbsolute) || File.GetLastWriteTimeUtc(sourceAbsolute) > File.GetLastWriteTimeUtc(targetAbsolute))
                    File.Copy(sourceAbsolute, targetAbsolute, true);
                rows.Add($"    {{\"id\":\"{Escape(entry.id)}\",\"status\":\"copied\",\"sourcePath\":\"{Escape(entry.sourcePath)}\",\"importedPath\":\"{Escape(targetAssetPath)}\"}}" );
            }
            report.Add(string.Join(",\n", rows));
            report.Add("  ]");
            report.Add("}");
            var reportAbsolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(reportAbsolute, string.Join("\n", report));
        }

        private static void ConfigureImageImportSettings()
        {
            // Preserve the source pixel aspect ratio. Unity's default NPOT
            // setting can pad/resize the supplied dossier and clue images to
            // independent power-of-two dimensions, which visibly distorts the
            // artwork when it is composited on the dossier stage.
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/ExhibitionCase/Imported/Images" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.npotScale == TextureImporterNPOTScale.None) continue;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        private static CaseConfigAsset ImportCaseConfig(string packageRoot)
        {
            var casePath = Path.Combine(packageRoot, "content", "case.json");
            var manifestPath = Path.Combine(packageRoot, "content", "assets.manifest.json");
            var json = File.ReadAllText(casePath);
            var document = JsonUtility.FromJson<CaseJsonDocument>(json);
            if (document == null) throw new InvalidOperationException("case.json could not be parsed." );

            var config = AssetDatabase.LoadAssetAtPath<CaseConfigAsset>(CaseAssetPath);
            // Older generated packages may contain a ScriptableObject YAML
            // with m_Script: {fileID: 0}. Unity can display that object but it
            // cannot serialize a typed reference to it into a scene. Recreate
            // only this generated config from content/case.json so the source
            // content remains authoritative and existing source assets stay
            // untouched.
            if (config == null || IsMissingScriptAsset(CaseAssetPath))
            {
                if (File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, CaseAssetPath.Replace('/', Path.DirectorySeparatorChar))))
                    AssetDatabase.DeleteAsset(CaseAssetPath);
                config = null;
            }
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CaseConfigAsset>();
                AssetDatabase.CreateAsset(config, CaseAssetPath);
            }
            JsonUtility.FromJsonOverwrite(json, config);
            var manifest = JsonUtility.FromJson<AssetManifestDocument>(File.ReadAllText(manifestPath));
            var bindings = new List<AssetBinding>();
            foreach (var entry in manifest.assets ?? Array.Empty<AssetManifestEntry>())
            {
                var binding = new AssetBinding
                {
                    id = entry.id,
                    sourcePath = entry.sourcePath,
                    kind = ParseKind(entry.kind),
                    importedPath = FindImportedAssetPath(entry.id, entry.kind)
                };
                if (!string.IsNullOrWhiteSpace(binding.importedPath))
                {
                    if (binding.kind == AssetKind.video) binding.video = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(binding.importedPath);
                    else if (binding.kind == AssetKind.image || binding.kind == AssetKind.targetImage) binding.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(binding.importedPath);
                }
                bindings.Add(binding);
            }
            config.assets = bindings.ToArray();
            if (config.settings == null) config.settings = new CaseSettings();
            EditorUtility.SetDirty(config);
            return config;
        }

        internal static bool IsMissingScriptAsset(string assetPath)
        {
            var absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(absolute) && File.ReadAllText(absolute).Contains("m_Script: {fileID: 0}");
        }

        private static string FindImportedAssetPath(string id, string kind)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var folder = kind == "video" ? "Assets/ExhibitionCase/Imported/Videos" : "Assets/ExhibitionCase/Imported/Images";
            var guids = AssetDatabase.FindAssets(id, new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).Equals(id, StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }

        private static AssetKind ParseKind(string value)
        {
            if (Enum.TryParse(value, true, out AssetKind kind)) return kind;
            return AssetKind.image;
        }

        private static void CreatePrefabs(CaseConfigAsset config)
        {
            var appObject = new GameObject("ExhibitionAppPrefab");
            appObject.AddComponent<ExhibitionApp>().config = config;
            PrefabUtility.SaveAsPrefabAsset(appObject, "Assets/ExhibitionCase/Prefabs/Systems/ExhibitionApp.prefab");
            UnityEngine.Object.DestroyImmediate(appObject);

            var targetObject = new GameObject("VuforiaClueTargetPrefab");
            targetObject.AddComponent<VuforiaClueTarget>();
            targetObject.AddComponent<VuforiaScannerAdapter>();
            PrefabUtility.SaveAsPrefabAsset(targetObject, "Assets/ExhibitionCase/Prefabs/Vuforia/VuforiaClueTarget.prefab");
            UnityEngine.Object.DestroyImmediate(targetObject);
        }

        private static void CreateScenes(CaseConfigAsset config)
        {
            var sceneSpecs = new[] { "00_Boot", "01_Intro", "02_Investigation", "03_Finale" };
            var scenePaths = new List<EditorBuildSettingsScene>();
            foreach (var sceneName in sceneSpecs)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var marker = new GameObject("SceneBootstrap");
                marker.AddComponent<ExhibitionSceneBootstrap>();
                var cameraObject = new GameObject("ExhibitionCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                cameraObject.AddComponent<AudioListener>();
                if (sceneName == "00_Boot")
                {
                    var app = new GameObject("ExhibitionApp");
                    app.AddComponent<ExhibitionApp>().config = config;
                }
                if (sceneName == "02_Investigation")
                {
                    var scanner = new GameObject("MockScanner");
                    scanner.AddComponent<MockClueScanner>();
                    var vuforiaAdapter = new GameObject("VuforiaScannerAdapter");
                    vuforiaAdapter.AddComponent<VuforiaScannerAdapter>();
                    foreach (var clue in config.clues ?? Array.Empty<ClueData>())
                    {
                        var target = new GameObject("Target_" + clue.targetName);
                        var binding = target.AddComponent<VuforiaClueTarget>();
                        binding.clueId = clue.id;
                        binding.targetName = clue.targetName;
                    }
                }
                var path = "Assets/ExhibitionCase/Scenes/" + sceneName + ".unity";
                EditorSceneManager.SaveScene(scene, path);
                scenePaths.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenePaths.ToArray();
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/ExhibitionCase/Scenes/00_Boot.unity");
            if (boot != null) EditorSceneManager.OpenScene("Assets/ExhibitionCase/Scenes/00_Boot.unity");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Mystery Exhibition";
            PlayerSettings.companyName = "Mystery Exhibition";
            PlayerSettings.defaultScreenWidth = ExhibitionApp.TargetWidth;
            PlayerSettings.defaultScreenHeight = ExhibitionApp.TargetHeight;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault());
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    [InitializeOnLoad]
    internal static class ExhibitionAutoSetupOnOpen
    {
        private const string SessionKey = "UnityMysteryExhibition.SetupDemoAttempted.20260907.v2";

        static ExhibitionAutoSetupOnOpen()
        {
            EditorApplication.delayCall += TrySetup;
        }

        private static void TrySetup()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            if (AssetDatabase.LoadAssetAtPath<CaseConfigAsset>("Assets/ExhibitionCase/Resources/CaseConfig.asset") != null) return;
            ExhibitionSetupTool.SetupDemoInternal(false);
        }
    }
}
