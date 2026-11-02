using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMysteryExhibition.Editor
{
    /// <summary>
    /// Validates the real local Vuforia package and all stable-ID mappings.
    /// This deliberately does not claim that a PNG/JPG was recognised by Vuforia:
    /// Vuforia's Play Mode image source is a webcam, simulator or .vuforia recording.
    /// </summary>
    public static class VuforiaLocalTargetValidationTool
    {
        private const string CaseConfigPath = "Assets/ExhibitionCase/Resources/CaseConfig.asset";
        private const string InvestigationScenePath = "Assets/ExhibitionCase/Scenes/02_Investigation.unity";
        private const string StreamingXmlPath = "Assets/StreamingAssets/Vuforia/CaseTargets.xml";
        private const string StreamingDatPath = "Assets/StreamingAssets/Vuforia/CaseTargets.dat";
        private const string ReportPath = "Assets/ExhibitionCase/Data/Generated/vuforia-local-target-validation.txt";

        [MenuItem("Tools/Exhibition/Validate Local Target Package")]
        public static void ValidateLocalTargetPackage()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var packageRoot = Directory.GetParent(projectRoot).FullName;
            var report = new List<string>
            {
                "Vuforia local target/package validation",
                "Run time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ""
            };
            var errors = new List<string>();
            var warnings = new List<string>();

            var config = AssetDatabase.LoadAssetAtPath<CaseConfigAsset>(CaseConfigPath);
            if (config == null)
            {
                errors.Add("CaseConfig.asset is missing.");
            }
            else
            {
                config.Validate(out var configErrors, out var configWarnings);
                errors.AddRange(configErrors.Select(e => "CaseConfig: " + e));
                warnings.AddRange(configWarnings.Select(w => "CaseConfig: " + w));
            }

            var sourceCasePath = Path.Combine(packageRoot, "content", "case.json");
            var sourceManifestPath = Path.Combine(packageRoot, "content", "assets.manifest.json");
            var sourceCase = ReadJson<CaseJsonDocument>(sourceCasePath, errors, "content/case.json");
            var manifest = ReadJson<AssetManifestDocument>(sourceManifestPath, errors, "content/assets.manifest.json");

            var expectedTargetNames = GetTargetNames(config);
            var sourceTargetNames = sourceCase == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : GetTargetNames(sourceCase);
            CompareSets("content/case.json", sourceTargetNames, expectedTargetNames, errors);

            var xmlAbsolute = Path.Combine(projectRoot, StreamingXmlPath.Replace('/', Path.DirectorySeparatorChar));
            var xmlTargetNames = ReadDatabaseTargetNames(xmlAbsolute, errors);
            CompareSets("StreamingAssets/Vuforia/CaseTargets.xml", xmlTargetNames, expectedTargetNames, errors);

            var sceneAbsolute = Path.Combine(projectRoot, InvestigationScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sceneAbsolute))
            {
                errors.Add("Investigation scene is missing: " + InvestigationScenePath);
            }
            else
            {
                var sceneText = File.ReadAllText(sceneAbsolute);
                foreach (var targetName in expectedTargetNames.OrderBy(n => n))
                {
                    if (!sceneText.Contains("m_Name: ImageTarget_" + targetName, StringComparison.Ordinal))
                        errors.Add("Scene ImageTarget object is missing: " + targetName);
                    if (!sceneText.Contains("mTrackableName: " + targetName, StringComparison.Ordinal))
                        errors.Add("Scene Vuforia mapping is missing: " + targetName);
                }
                var sceneTargetCount = sceneText.Split(new[] { "mTrackableName:" }, StringSplitOptions.None).Length - 1;
                if (sceneTargetCount != expectedTargetNames.Count)
                    errors.Add("Scene has " + sceneTargetCount + " mTrackableName entries; expected " + expectedTargetNames.Count + ".");
                else
                    report.Add("PASS: scene contains the expected " + sceneTargetCount + " ImageTarget mappings.");
            }

            var packageAbsolute = Path.Combine(packageRoot, "SourceAssets/06_Vuforia/Database/CaseTargets.unitypackage".Replace('/', Path.DirectorySeparatorChar));
            CheckFile(packageAbsolute, "SourceAssets/06_Vuforia/Database/CaseTargets.unitypackage", errors, report);
            CheckFile(Path.Combine(projectRoot, StreamingXmlPath.Replace('/', Path.DirectorySeparatorChar)), StreamingXmlPath, errors, report);
            CheckFile(Path.Combine(projectRoot, StreamingDatPath.Replace('/', Path.DirectorySeparatorChar)), StreamingDatPath, errors, report);

            ValidateManifestSources(packageRoot, manifest, report, errors);
            ValidateImportedTargetTextures(config, report, errors);

            if (errors.Count == 0)
                report.Add("PASS: stable IDs, target names, source files, imported target textures and database files are consistent.");
            else
                report.Add("FAIL: the local package/mapping check found errors; see ERROR lines below.");

            report.Add("");
            report.Add("NOT RUN: direct Vuforia recognition from a static PNG/JPG.");
            report.Add("Reason: Vuforia Play Mode accepts a webcam, simulator or a .vuforia session recording; it does not expose a Texture2D/PNG-as-camera-frame API.");
            report.Add("Real recognition still requires a webcam or a Vuforia .vuforia recording file. The current machine has no present camera device and no recording file in this workspace.");
            report.Add("This report therefore separates package integrity from camera/recording recognition and does not mark scanning as passed.");

            var output = string.Join("\n", report.Concat(errors.Select(e => "ERROR: " + e)).Concat(warnings.Select(w => "WARN: " + w)));
            var reportAbsolute = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(reportAbsolute));
            File.WriteAllText(reportAbsolute, output, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("[Exhibition] " + output);

            var dialog = (errors.Count == 0 ? "PACKAGE CHECK PASS\n\n" : "PACKAGE CHECK HAS ERRORS\n\n") +
                         string.Join("\n", report.Take(12).Concat(errors.Take(4).Select(e => "ERROR: " + e)));
            EditorUtility.DisplayDialog("Vuforia Local Target Package", dialog, "OK");
        }

        private static T ReadJson<T>(string path, List<string> errors, string label) where T : class
        {
            if (!File.Exists(path))
            {
                errors.Add(label + " is missing: " + path);
                return null;
            }
            try
            {
                var result = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (result == null) errors.Add(label + " could not be parsed.");
                return result;
            }
            catch (Exception ex)
            {
                errors.Add(label + " could not be parsed: " + ex.Message);
                return null;
            }
        }

        private static HashSet<string> GetTargetNames(CaseConfigAsset config)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config != null)
            {
                foreach (var clue in config.clues ?? Array.Empty<ClueData>())
                    if (clue != null && !string.IsNullOrWhiteSpace(clue.targetName)) names.Add(clue.targetName.Trim());
                if (config.detective != null && !string.IsNullOrWhiteSpace(config.detective.targetName))
                    names.Add(config.detective.targetName.Trim());
            }
            return names;
        }

        private static HashSet<string> GetTargetNames(CaseJsonDocument document)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var clue in document.clues ?? Array.Empty<ClueData>())
                if (clue != null && !string.IsNullOrWhiteSpace(clue.targetName)) names.Add(clue.targetName.Trim());
            if (document.detective != null && !string.IsNullOrWhiteSpace(document.detective.targetName))
                names.Add(document.detective.targetName.Trim());
            return names;
        }

        private static HashSet<string> ReadDatabaseTargetNames(string path, List<string> errors)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                errors.Add("Vuforia XML database is missing: " + path);
                return names;
            }
            try
            {
                var document = XDocument.Load(path);
                foreach (var element in document.Descendants("ImageTarget"))
                {
                    var name = (string)element.Attribute("name");
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
                }
            }
            catch (Exception ex)
            {
                errors.Add("Vuforia XML database could not be parsed: " + ex.Message);
            }
            return names;
        }

        private static void CompareSets(string label, HashSet<string> actual, HashSet<string> expected, List<string> errors)
        {
            var missing = expected.Except(actual, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray();
            var extra = actual.Except(expected, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray();
            if (missing.Length > 0) errors.Add(label + " is missing target(s): " + string.Join(", ", missing));
            if (extra.Length > 0) errors.Add(label + " contains unexpected target(s): " + string.Join(", ", extra));
        }

        private static void CheckFile(string path, string label, List<string> errors, List<string> report)
        {
            if (!File.Exists(path))
            {
                errors.Add("Missing file: " + label);
                return;
            }
            var length = new FileInfo(path).Length;
            if (length <= 0) errors.Add("Empty file: " + label);
            else report.Add("PASS: " + label + " (" + length + " bytes).");
        }

        private static void ValidateManifestSources(string packageRoot, AssetManifestDocument manifest, List<string> report, List<string> errors)
        {
            var targetEntries = (manifest == null ? Array.Empty<AssetManifestEntry>() : manifest.assets ?? Array.Empty<AssetManifestEntry>())
                .Where(a => a != null && string.Equals(a.kind, "targetImage", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (targetEntries.Length != 11)
                errors.Add("assets.manifest.json has " + targetEntries.Length + " targetImage entries; expected 11.");
            var missing = targetEntries
                .Where(a => string.IsNullOrWhiteSpace(a.sourcePath) || !File.Exists(Path.Combine(packageRoot, a.sourcePath.Replace('/', Path.DirectorySeparatorChar))))
                .Select(a => a.id)
                .ToArray();
            if (missing.Length > 0) errors.Add("Missing target source image(s): " + string.Join(", ", missing));
            else report.Add("PASS: all " + targetEntries.Length + " target source images exist at their manifest paths.");
        }

        private static void ValidateImportedTargetTextures(CaseConfigAsset config, List<string> report, List<string> errors)
        {
            if (config == null) return;
            var ids = (config.clues ?? Array.Empty<ClueData>()).Where(c => c != null).Select(c => c.targetImageAssetId)
                .Concat(new[] { config.detective == null ? null : config.detective.targetImageAssetId })
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var missing = new List<string>();
            foreach (var id in ids)
            {
                var binding = config.FindAsset(id);
                if (binding == null || binding.texture == null || string.IsNullOrWhiteSpace(binding.importedPath)) missing.Add(id);
            }
            if (missing.Count > 0) errors.Add("Imported target texture binding is missing: " + string.Join(", ", missing));
            else report.Add("PASS: all " + ids.Length + " stable-ID target textures are imported and bound in CaseConfig.asset.");
        }
    }
}
