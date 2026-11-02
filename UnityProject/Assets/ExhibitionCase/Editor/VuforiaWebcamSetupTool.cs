using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMysteryExhibition.Editor
{
    /// <summary>
    /// Creates the real Vuforia objects in the investigation scene through the
    /// installed Vuforia editor menu. The bridge itself remains reflection-based
    /// so the main Mock runtime assembly still compiles when Vuforia is absent.
    /// </summary>
    public static class VuforiaWebcamSetupTool
    {
        private const string InvestigationScenePath = "Assets/ExhibitionCase/Scenes/02_Investigation.unity";
        private const string CaseAssetPath = "Assets/ExhibitionCase/Resources/CaseConfig.asset";
        private const string TargetsRootName = "VuforiaCaseTargets";
        private const string DatabaseName = "CaseTargets";
        private const string DatabasePath = "Vuforia/CaseTargets.xml";

        [MenuItem("Tools/Exhibition/Setup Vuforia Webcam")]
        public static void SetupVuforiaWebcam()
        {
            SetupVuforiaWebcamInternal(false);
        }

        [MenuItem("Tools/Exhibition/Repair Vuforia Generated Targets")]
        public static void RepairVuforiaGeneratedTargets()
        {
            // The target root is generated from content/case.json. Rebuilding
            // only this generated subtree repairs legacy scene-local scripts
            // without touching the UI, camera, or manually authored objects.
            SetupVuforiaWebcamInternal(true);
        }

        private static void SetupVuforiaWebcamInternal(bool rebuildGeneratedTargets)
        {
            var config = AssetDatabase.LoadAssetAtPath<CaseConfigAsset>(CaseAssetPath);
            if (config == null)
            {
                EditorUtility.DisplayDialog("Setup Vuforia Webcam", "CaseConfig.asset was not found. Run Tools/Exhibition/Setup Demo first.", "OK");
                return;
            }

            try
            {
                RefreshConfigFromSource(config);
                var scene = EditorSceneManager.OpenScene(InvestigationScenePath, OpenSceneMode.Single);
                var targetsRoot = FindOrCreateRoot(TargetsRootName).transform;
                EnsureScannerAdapter();
                var rebuilt = rebuildGeneratedTargets ? RemoveGeneratedImageTargets(targetsRoot) : 0;
                var removedDuplicates = RemoveDuplicateImageTargets(targetsRoot);
                var created = 0;
                var reused = 0;

                if (FindVuforiaComponent("Vuforia.VuforiaBehaviour") == null)
                {
                    if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/AR Camera"))
                        Debug.LogError("[Exhibition] Vuforia AR Camera menu item was not available.");
                    else
                        Debug.Log("[Exhibition] Added Vuforia AR Camera for the webcam scene.");
                }

                foreach (var clue in config.clues ?? Array.Empty<ClueData>())
                {
                    if (clue == null) continue;
                    var target = EnsureImageTarget(clue.targetName, targetsRoot, out var wasReused);
                    if (target == null) continue;
                    ConfigureTarget(target, clue.id, clue.targetName, clue.physicalWidthMeters);
                    if (wasReused) reused++; else created++;
                }

                if (config.detective != null)
                {
                    var target = EnsureImageTarget(config.detective.targetName, targetsRoot, out var wasReused);
                    if (target != null)
                    {
                        ConfigureTarget(target, config.detective.informationClueId, config.detective.targetName, 0.1f);
                        if (wasReused) reused++; else created++;
                    }
                }

                DisableLegacyAudioListener();
                DisableRedundantObserverEventHandlers();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                var observerCount = CountVuforiaImageTargets(scene);
                var message = "Scene configured.\n\n" +
                              "Image Targets: " + observerCount + "\n" +
                              "Created: " + created + "  Reused: " + reused + "  Removed duplicates: " + removedDuplicates + "\n\n" +
                              (rebuildGeneratedTargets ? "Rebuilt generated targets: " + rebuilt + "\n\n" : string.Empty) +
                              "Next required project setting: enter the Vuforia license key in Project Settings > Vuforia Engine.\n" +
                              "Then press Play on 00_Boot and point the webcam at a printed/displayed target image.";
                Debug.Log("[Exhibition] Setup Vuforia Webcam completed. " + message.Replace("\n", " "));
                EditorUtility.DisplayDialog("Setup Vuforia Webcam", message, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Exhibition] Setup Vuforia Webcam failed: " + ex);
                EditorUtility.DisplayDialog("Setup Vuforia Webcam", "Failed: " + ex.Message, "OK");
            }
        }

        [MenuItem("Tools/Exhibition/Validate Vuforia Setup")]
        public static void ValidateVuforiaSetup()
        {
            var scene = SceneManager.GetActiveScene();
            var report = new List<string>();
            var observerType = FindType("Vuforia.ImageTargetBehaviour");
            var vuforiaType = FindType("Vuforia.VuforiaBehaviour");
            report.Add(observerType == null ? "FAIL: Vuforia ImageTargetBehaviour type is not loaded." : "PASS: Vuforia ImageTargetBehaviour type is loaded.");
            report.Add(vuforiaType == null ? "FAIL: VuforiaBehaviour is not loaded." : "PASS: VuforiaBehaviour is loaded.");
            report.Add(GetLicenseKeyStatus());
            report.Add("Scene: " + scene.name);
            report.Add("Image Targets: " + CountVuforiaImageTargets(scene));
            report.Add("StreamingAssets/ Vuforia database files: " + (System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "Vuforia/CaseTargets.dat")) ? "found" : "missing"));
            Debug.Log("[Exhibition] Validate Vuforia Setup\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Validate Vuforia Setup", string.Join("\n", report), "OK");
        }

        private static string GetLicenseKeyStatus()
        {
            // Read only the presence of the key. Never log, display, or persist
            // the secret itself (or a prefix/length that could identify it).
            var configurationType = FindType("Vuforia.VuforiaConfiguration");
            if (configurationType == null)
                return "WARN: Vuforia configuration type is not available; license key was not inspected.";

            try
            {
                var instanceProperty = configurationType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProperty == null ? null : instanceProperty.GetValue(null, null);
                var vuforiaProperty = configurationType.GetProperty("Vuforia", BindingFlags.Public | BindingFlags.Instance);
                var vuforia = instance == null || vuforiaProperty == null ? null : vuforiaProperty.GetValue(instance, null);
                var licenseProperty = vuforia == null ? null : vuforia.GetType().GetProperty("LicenseKey", BindingFlags.Public | BindingFlags.Instance);
                var value = licenseProperty == null ? null : licenseProperty.GetValue(vuforia, null) as string;
                return string.IsNullOrWhiteSpace(value)
                    ? "FAIL: Vuforia license key is empty."
                    : "PASS: Vuforia license key is present (value not displayed).";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Exhibition] Could not inspect Vuforia license configuration: " + ex.Message);
                return "WARN: Vuforia license key could not be inspected; check Window > Vuforia Configuration.";
            }
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var root = GameObject.Find(name);
            if (root != null) return root;
            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create Vuforia target root");
            return root;
        }

        private static void EnsureScannerAdapter()
        {
            var adapter = UnityEngine.Object.FindObjectOfType<VuforiaScannerAdapter>();
            if (adapter == null)
            {
                var go = new GameObject("VuforiaScannerAdapter");
                Undo.RegisterCreatedObjectUndo(go, "Create Vuforia scanner adapter");
                adapter = go.AddComponent<VuforiaScannerAdapter>();
            }
            adapter.autoDiscoverVuforiaTargets = true;
            adapter.requireDirectTracking = true;
            adapter.logTrackingTransitions = false;
            adapter.targetFrameRate = 30;
            adapter.trackingPollInterval = 0.1f;
            adapter.pauseVuforiaOutsideInvestigation = true;
            adapter.optimizeForCanvasExhibition = true;
            EditorUtility.SetDirty(adapter);
        }

        private static void DisableLegacyAudioListener()
        {
            // The Vuforia AR Camera owns the listener used by the webcam scene.
            // The original full-screen camera remains useful as a layout reference,
            // but keeping its listener enabled produces Unity's two-listener warning.
            var scene = SceneManager.GetActiveScene();
            foreach (var listener in Resources.FindObjectsOfTypeAll<AudioListener>())
            {
                if (listener == null || listener.gameObject.scene != scene) continue;
                if (string.Equals(listener.gameObject.name, "ARCamera", StringComparison.OrdinalIgnoreCase)) continue;
                if (!listener.enabled) continue;

                listener.enabled = false;
                EditorUtility.SetDirty(listener);
                Debug.Log("[Exhibition] Disabled the legacy AudioListener on " + listener.gameObject.name + "; ARCamera remains the active listener.");
            }
        }

        private static void DisableRedundantObserverEventHandlers()
        {
            // This exhibition renders every visible pixel through its Canvas.
            // The Vuforia sample DefaultObserverEventHandler is therefore not
            // needed: it logs every status callback and tries to show/hide AR
            // child renderers, which adds avoidable work when a webcam image
            // is noisy. Keep ImageTargetBehaviour itself enabled for tracking.
            var scene = SceneManager.GetActiveScene();
            var disabled = 0;
            foreach (var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (component == null || component.gameObject.scene != scene) continue;
                // DefaultObserverEventHandler is declared in the global
                // namespace by the Vuforia package, so its FullName has no
                // "Vuforia." prefix.
                if (!string.Equals(component.GetType().FullName, "DefaultObserverEventHandler", StringComparison.Ordinal)) continue;
                if (!component.enabled) continue;
                component.enabled = false;
                EditorUtility.SetDirty(component);
                disabled++;
            }
            if (disabled > 0)
                Debug.Log("[Exhibition] Disabled " + disabled + " redundant Vuforia DefaultObserverEventHandler components; ImageTarget tracking remains enabled.");
        }

        private static void RefreshConfigFromSource(CaseConfigAsset config)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var packageRoot = Directory.GetParent(projectRoot).FullName;
            var sourcePath = Path.Combine(packageRoot, "content", "case.json");
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning("[Exhibition] Source case.json was not found; using the existing CaseConfig.asset.");
                return;
            }

            var sourceJson = File.ReadAllText(sourcePath);
            JsonUtility.FromJsonOverwrite(sourceJson, config);
            EditorUtility.SetDirty(config);
            Debug.Log("[Exhibition] Refreshed CaseConfig.asset from " + sourcePath);
        }

        private static GameObject EnsureImageTarget(string targetName, Transform parent, out bool reused)
        {
            reused = false;
            var existing = FindImageTargetByName(targetName);
            if (existing != null)
            {
                reused = true;
                existing.transform.SetParent(parent, false);
                return existing.gameObject;
            }

            if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/Image Target"))
            {
                Debug.LogError("[Exhibition] Could not execute the Vuforia Image Target menu item for " + targetName);
                return null;
            }

            var target = Selection.activeGameObject;
            if (target == null)
            {
                Debug.LogError("[Exhibition] Vuforia Image Target menu item did not select a created object for " + targetName);
                return null;
            }
            target.name = "ImageTarget_" + targetName;
            target.transform.SetParent(parent, false);
            return target;
        }

        private static void ConfigureTarget(GameObject target, string clueId, string targetName, float widthMeters)
        {
            // Setup can be run repeatedly while importing/updating the target
            // database. Keep exactly one bridge component per ImageTarget so a
            // repeated setup never creates duplicate callbacks or stale script
            // references in the scene.
            var bindings = target.GetComponents<VuforiaClueTarget>();
            var binding = bindings.FirstOrDefault();
            if (binding == null)
                binding = target.AddComponent<VuforiaClueTarget>();

            for (var i = 1; i < bindings.Length; i++)
            {
                if (bindings[i] == null || bindings[i] == binding) continue;
                Undo.DestroyObjectImmediate(bindings[i]);
                Debug.Log("[Exhibition] Removed duplicate VuforiaClueTarget from " + target.name + ".");
            }

            binding.clueId = clueId;
            binding.targetName = targetName;
            binding.directTracking = false;
            EditorUtility.SetDirty(binding);

            var observer = FindComponent(target, "Vuforia.ImageTargetBehaviour");
            if (observer == null)
            {
                Debug.LogError("[Exhibition] Created object has no ImageTargetBehaviour: " + target.name);
                return;
            }

            var serialized = new SerializedObject(observer);
            SetString(serialized, new[] { "mTrackableName", "trackableName", "mTargetName" }, targetName);
            SetString(serialized, new[] { "mDataSetPath", "dataSetPath", "mDatabasePath", "databasePath" }, DatabasePath);
            SetFloat(serialized, new[] { "mWidth", "width", "mPhysicalWidth" }, widthMeters > 0f ? widthMeters : 0.1f);
            SetImageTargetTypePredefined(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(observer);
            Debug.Log("[Exhibition] Bound Vuforia target " + targetName + " -> " + clueId + " database=" + DatabaseName);
        }

        private static void SetImageTargetTypePredefined(SerializedObject serialized)
        {
            var property = serialized.FindProperty("mImageTargetType");
            if (property != null && property.propertyType == SerializedPropertyType.Enum)
                property.enumValueIndex = 0; // Vuforia.ImageTargetType.PREDEFINED
        }

        private static void SetString(SerializedObject serialized, IEnumerable<string> names, string value)
        {
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property == null || property.propertyType != SerializedPropertyType.String) continue;
                property.stringValue = value;
                return;
            }
        }

        private static void SetFloat(SerializedObject serialized, IEnumerable<string> names, float value)
        {
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property == null || property.propertyType != SerializedPropertyType.Float) continue;
                property.floatValue = value;
                return;
            }
        }

        private static Component FindImageTargetByName(string targetName)
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (component == null || component.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (component.GetType().FullName != "Vuforia.ImageTargetBehaviour") continue;
                var property = component.GetType().GetProperty("TargetName", BindingFlags.Public | BindingFlags.Instance);
                var current = property == null ? null : property.GetValue(component, null) as string;
                if (string.Equals(current, targetName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(component.gameObject.name, "ImageTarget_" + targetName, StringComparison.OrdinalIgnoreCase))
                    return component;
            }
            return null;
        }

        private static int RemoveDuplicateImageTargets(Transform targetsRoot)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<GameObject>();
            foreach (var transform in targetsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (transform == targetsRoot) continue;
                var observer = FindComponent(transform.gameObject, "Vuforia.ImageTargetBehaviour");
                if (observer == null) continue;

                var key = transform.gameObject.name;
                const string prefix = "ImageTarget_";
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) key = key.Substring(prefix.Length);
                if (!seen.Add(key)) duplicates.Add(transform.gameObject);
            }

            foreach (var duplicate in duplicates)
            {
                var duplicateName = duplicate == null ? "<missing>" : duplicate.name;
                Undo.DestroyObjectImmediate(duplicate);
                Debug.Log("[Exhibition] Removed duplicate Vuforia Image Target: " + duplicateName);
            }
            return duplicates.Count;
        }

        private static int RemoveGeneratedImageTargets(Transform targetsRoot)
        {
            var generated = targetsRoot.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != targetsRoot &&
                                    transform.parent == targetsRoot &&
                                    FindComponent(transform.gameObject, "Vuforia.ImageTargetBehaviour") != null)
                .Select(transform => transform.gameObject)
                .ToList();

            foreach (var target in generated)
            {
                if (target == null) continue;
                var targetName = target.name;
                Undo.DestroyObjectImmediate(target);
                Debug.Log("[Exhibition] Rebuilt generated Vuforia Image Target: " + targetName);
            }
            return generated.Count;
        }

        private static Component FindComponent(GameObject target, string fullTypeName)
        {
            return target.GetComponents<Component>().FirstOrDefault(component => component != null && component.GetType().FullName == fullTypeName);
        }

        private static Component FindVuforiaComponent(string fullTypeName)
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (component != null && component.gameObject.scene == SceneManager.GetActiveScene() && component.GetType().FullName == fullTypeName)
                    return component;
            return null;
        }

        private static int CountVuforiaImageTargets(Scene scene)
        {
            var count = 0;
            foreach (var component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (component != null && component.gameObject.scene == scene && component.GetType().FullName == "Vuforia.ImageTargetBehaviour") count++;
            return count;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
