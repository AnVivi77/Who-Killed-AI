using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnityMysteryExhibition.Editor
{
    public static class DossierCanvasPreviewBuilder
    {
        private const string ConfigPath = "Assets/ExhibitionCase/Resources/CaseConfig.asset";
        private const string LayoutPath = "Assets/ExhibitionCase/Resources/DossierLayout.asset";
        private const string ScenePath = "Assets/ExhibitionCase/Scenes/04_DossierPreview.unity";
        private const float CanvasWidth = 3840f;
        private const float CanvasHeight = 1152f;
        private const float StageWidth = 2048f;
        private const float StageHeight = 1152f;

        [MenuItem("Tools/Exhibition/Build Dossier Canvas Preview")]
        public static void BuildAndOpen()
        {
            var config = AssetDatabase.LoadAssetAtPath<CaseConfigAsset>(ConfigPath);
            if (config == null)
            {
                EditorUtility.DisplayDialog("Dossier Canvas Preview", "CaseConfig.asset was not found. Run Tools/Exhibition/Setup Demo first.", "OK");
                return;
            }

            var layout = EnsureLayoutAsset(config);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene can unload the previously cached asset wrapper while the
            // editor changes scene. Resolve the source assets again afterwards
            // so both the in-memory build and serialized scene use live objects.
            config = AssetDatabase.LoadAssetAtPath<CaseConfigAsset>(ConfigPath);
            layout = AssetDatabase.LoadAssetAtPath<DossierLayoutAsset>(LayoutPath) ?? layout;
            var canvasObject = new GameObject("DossierCanvasPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(DossierCanvasPreview));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            SetCenteredRect(canvasRect, CanvasWidth, CanvasHeight);
            canvasObject.transform.localScale = Vector3.one * 0.001f;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var preview = canvasObject.GetComponent<DossierCanvasPreview>();
            preview.caseConfig = config;
            preview.layout = layout;
            preview.suspectId = config.suspects != null && config.suspects.Length > 0 ? config.suspects[0].id : "S01";

            // Pass the freshly loaded asset explicitly. During a scene swap Unity
            // can temporarily report a newly assigned ScriptableObject field as
            // null while serialized scene state is being established.
            Rebuild(preview, config);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Selection.activeGameObject = canvasObject;
            EditorGUIUtility.PingObject(canvasObject);
            Debug.Log("[Exhibition] Dossier Canvas Preview built at " + ScenePath + ". This is an edit-mode Canvas preview; Play Mode was not used.");
        }

        internal static DossierLayoutAsset EnsureLayoutAsset(CaseConfigAsset config)
        {
            var layout = AssetDatabase.LoadAssetAtPath<DossierLayoutAsset>(LayoutPath);
            if (layout == null || ExhibitionSetupTool.IsMissingScriptAsset(LayoutPath))
            {
                if (ExhibitionSetupTool.IsMissingScriptAsset(LayoutPath)) AssetDatabase.DeleteAsset(LayoutPath);
                layout = ScriptableObject.CreateInstance<DossierLayoutAsset>();
                layout.clues = BuildDefaultLayouts(config).ToArray();
                layout.textLayouts = BuildTextLayouts(config).ToArray();
                AssetDatabase.CreateAsset(layout, LayoutPath);
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                return layout;
            }

            // Add only missing stable IDs. Existing Inspector edits remain intact.
            var entries = (layout.clues ?? Array.Empty<DossierClueLayout>()).Where(c => c != null).ToList();
            var changed = false;
            foreach (var candidate in BuildDefaultLayouts(config))
            {
                var existing = entries.FirstOrDefault(c => c.clueId == candidate.clueId);
                if (existing == null)
                {
                    entries.Add(candidate);
                    changed = true;
                    continue;
                }

                if (existing.titleTextRect.width <= 0f || existing.bodyTextRect.width <= 0f || existing.unknownTextRect.width <= 0f || existing.titleFontSize <= 0 || existing.bodyFontSize <= 0)
                {
                    DossierLayoutDefaults.NormalizeClueTextDefaults(existing, existing.clueId);
                    changed = true;
                }
            }

            var textEntries = (layout.textLayouts ?? Array.Empty<DossierTextLayout>()).Where(t => t != null).ToList();
            foreach (var candidate in BuildTextLayouts(config))
            {
                var existing = textEntries.FirstOrDefault(t => t.suspectId == candidate.suspectId);
                if (existing == null)
                {
                    textEntries.Add(candidate);
                    changed = true;
                    continue;
                }

                if (existing.titleRect.width <= 0f || existing.profileRect.width <= 0f || existing.progressRect.width <= 0f || existing.titleFontSize <= 0 || existing.profileFontSize <= 0 || existing.progressFontSize <= 0)
                {
                    var defaults = DossierLayoutDefaults.ForSuspect(existing.suspectId);
                    existing.titleRect = defaults.titleRect;
                    existing.profileRect = defaults.profileRect;
                    existing.progressRect = defaults.progressRect;
                    existing.titleFontSize = defaults.titleFontSize;
                    existing.profileFontSize = defaults.profileFontSize;
                    existing.progressFontSize = defaults.progressFontSize;
                    existing.profileLineSpacing = defaults.profileLineSpacing;
                    existing.profileAlignment = defaults.profileAlignment;
                    changed = true;
                }
            }

            if (changed)
            {
                layout.clues = entries.ToArray();
                layout.textLayouts = textEntries.ToArray();
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
            }
            return layout;
        }

        internal static void Rebuild(DossierCanvasPreview preview)
        {
            Rebuild(preview, preview == null ? null : preview.caseConfig);
        }

        private static void Rebuild(DossierCanvasPreview preview, CaseConfigAsset config)
        {
            if (preview == null || config == null)
            {
                Debug.LogError("[Exhibition] Dossier Canvas Preview cannot rebuild: CaseConfig reference is missing.");
                return;
            }
            preview.caseConfig = config;
            var layout = preview.layout == null ? EnsureLayoutAsset(config) : preview.layout;
            preview.layout = layout;

            var oldContent = preview.transform.Find("PreviewContent");
            if (oldContent != null) UnityEngine.Object.DestroyImmediate(oldContent.gameObject);
            var content = new GameObject("PreviewContent", typeof(RectTransform), typeof(Image));
            content.transform.SetParent(preview.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            SetFullRect(contentRect);
            content.GetComponent<Image>().color = Color.black;

            var stageObject = new GameObject("DossierStage_" + preview.suspectId, typeof(RectTransform));
            stageObject.transform.SetParent(content.transform, false);
            var stage = stageObject.GetComponent<RectTransform>();
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(StageWidth, StageHeight);
            stage.anchoredPosition = Vector2.zero;

            var suspect = config.FindSuspect(preview.suspectId);
            if (suspect == null)
            {
                Debug.LogError("[Exhibition] Dossier Canvas Preview cannot rebuild: suspect ID not found: " + preview.suspectId);
                return;
            }
            var baseTexture = config.FindTexture(suspect.dossierAssetId);
            var baseImage = CreateRawImage("DossierArtwork_Base", stage, baseTexture);
            SetFullRect(baseImage.rectTransform);
            baseImage.raycastTarget = false;

            var textLayout = layout.FindText(suspect.id) ?? DossierLayoutDefaults.ForSuspect(suspect.id);
            BuildDossierText(stage, suspect, textLayout);

            var clues = config.GetCluesForSuspect(suspect.id);
            foreach (var clue in clues.OrderBy(c => c.slotIndex))
            {
                var slot = layout.Find(clue.id) ?? DossierLayoutDefaults.ForSlot(clue.slotIndex);
                BuildClue(stage, config, clue, slot);
            }

            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            Canvas.ForceUpdateCanvases();
        }

        private static void BuildClue(RectTransform stage, CaseConfigAsset config, ClueData clue, DossierClueLayout slot)
        {
            var frameObject = new GameObject("DossierClueImageFrame_" + clue.slotIndex, typeof(RectTransform), typeof(RectMask2D));
            frameObject.transform.SetParent(stage, false);
            var frame = frameObject.GetComponent<RectTransform>();
            Anchor(frame, slot.imageRect.xMin, slot.imageRect.yMin, slot.imageRect.xMax, slot.imageRect.yMax);
            frame.localEulerAngles = new Vector3(0f, 0f, slot.rotationDegrees);

            var texture = config.FindTexture(clue.displayImageAssetId);
            var imageObject = new GameObject("DossierClueImage_" + clue.slotIndex, typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(frameObject.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            image.texture = texture;
            image.uvRect = slot.uvRect;
            SetFitSize(image.rectTransform, frame, texture, slot.uvRect);

            var cardObject = new GameObject("DossierClueCard_" + clue.slotIndex, typeof(RectTransform));
            cardObject.transform.SetParent(stage, false);
            var card = cardObject.GetComponent<RectTransform>();
            Anchor(card, slot.cardRect.xMin, slot.cardRect.yMin, slot.cardRect.xMax, slot.cardRect.yMax);
            card.localEulerAngles = new Vector3(0f, 0f, slot.rotationDegrees);

            var title = CreateText("DossierClueTitle_" + clue.slotIndex, card, clue.title == null ? string.Empty : clue.title.ToUpperInvariant(), DossierLayoutDefaults.TitleFontSize(slot, clue.id), Color.white, TextAnchor.MiddleCenter);
            AnchorLocal(title.rectTransform, DossierLayoutDefaults.TitleTextRect(slot).xMin, DossierLayoutDefaults.TitleTextRect(slot).yMin, DossierLayoutDefaults.TitleTextRect(slot).xMax, DossierLayoutDefaults.TitleTextRect(slot).yMax);
            title.fontStyle = FontStyle.Bold;

            var body = CreateText("DossierClueBody_" + clue.slotIndex, card, clue.body ?? string.Empty, DossierLayoutDefaults.BodyFontSize(slot, clue.id), new Color(0.08f, 0.08f, 0.08f), TextAnchor.UpperLeft);
            AnchorLocal(body.rectTransform, DossierLayoutDefaults.BodyTextRect(slot).xMin, DossierLayoutDefaults.BodyTextRect(slot).yMin, DossierLayoutDefaults.BodyTextRect(slot).xMax, DossierLayoutDefaults.BodyTextRect(slot).yMax);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.lineSpacing = DossierLayoutDefaults.BodyLineSpacing(slot);

            var unknown = CreateText("DossierClueUnknown_" + clue.slotIndex, card, "? NOT FOUND", DossierLayoutDefaults.UnknownFontSize(slot), new Color(0.55f, 0.02f, 0.08f), TextAnchor.MiddleCenter);
            AnchorLocal(unknown.rectTransform, DossierLayoutDefaults.UnknownTextRect(slot).xMin, DossierLayoutDefaults.UnknownTextRect(slot).yMin, DossierLayoutDefaults.UnknownTextRect(slot).xMax, DossierLayoutDefaults.UnknownTextRect(slot).yMax);
            unknown.gameObject.SetActive(false);
        }

        private static IEnumerable<DossierClueLayout> BuildDefaultLayouts(CaseConfigAsset config)
        {
            foreach (var clue in config.clues ?? Array.Empty<ClueData>())
            {
                var layout = DossierLayoutDefaults.ForSlot(clue.slotIndex);
                layout.clueId = clue.id;
                DossierLayoutDefaults.ApplyClueTextDefaults(layout, clue.id);
                layout.uvRect = WatermarkCropFor(clue.displayImageAssetId);
                layout.editorNote = CropNote(clue.displayImageAssetId, layout.editorNote);
                yield return layout;
            }
        }

        private static IEnumerable<DossierTextLayout> BuildTextLayouts(CaseConfigAsset config)
        {
            foreach (var suspect in config.suspects ?? Array.Empty<SuspectData>())
                yield return DossierLayoutDefaults.ForSuspect(suspect.id);
        }

        private static void BuildDossierText(RectTransform stage, SuspectData suspect, DossierTextLayout textLayout)
        {
            var title = CreateText("DossierProfileTitle", stage, (suspect.displayName ?? suspect.id).ToUpperInvariant(), textLayout.titleFontSize, Color.white, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, textLayout.titleRect.xMin, textLayout.titleRect.yMin, textLayout.titleRect.xMax, textLayout.titleRect.yMax);
            title.fontStyle = FontStyle.Bold;

            var profile = CreateText("DossierProfileText", stage, suspect.profile ?? string.Empty, textLayout.profileFontSize, new Color(0.08f, 0.08f, 0.08f), textLayout.profileAlignment);
            Anchor(profile.rectTransform, textLayout.profileRect.xMin, textLayout.profileRect.yMin, textLayout.profileRect.xMax, textLayout.profileRect.yMax);
            profile.horizontalOverflow = HorizontalWrapMode.Wrap;
            profile.verticalOverflow = VerticalWrapMode.Truncate;
            profile.lineSpacing = textLayout.profileLineSpacing;

            var progress = CreateText("DossierProgress", stage, "2 / 2 CLUES", textLayout.progressFontSize, Color.white, TextAnchor.MiddleCenter);
            Anchor(progress.rectTransform, textLayout.progressRect.xMin, textLayout.progressRect.yMin, textLayout.progressRect.xMax, textLayout.progressRect.yMax);
            progress.fontStyle = FontStyle.Bold;
        }

        private static Rect WatermarkCropFor(string assetId)
        {
            switch (assetId)
            {
                case "clue_s01_behavior": return new Rect(0f, 0.06f, 0.93f, 0.92f);
                case "clue_s01_information": return new Rect(0f, 0.04f, 0.94f, 0.94f);
                case "clue_s02_behavior": return new Rect(0f, 0.04f, 0.94f, 0.94f);
                case "clue_s03_behavior": return new Rect(0f, 0.04f, 0.94f, 0.94f);
                case "clue_s04_behavior": return new Rect(0f, 0.05f, 0.94f, 0.93f);
                case "clue_s05_information": return new Rect(0f, 0.08f, 0.84f, 0.82f);
                default: return new Rect(0f, 0f, 1f, 1f);
            }
        }

        private static string CropNote(string assetId, string baseNote)
        {
            var crop = WatermarkCropFor(assetId);
            return crop.width < 0.999f || crop.y > 0.001f
                ? baseNote + " Lower-right source watermark trimmed with RawImage.uvRect; original file preserved."
                : baseNote + " No source watermark crop required."
                ;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = texture == null ? Color.black : Color.white;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            // Create a real OS font for the edit-mode preview. Unity 2022.3
            // removed the old Arial.ttf built-in resource alias.
            text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static void SetFitSize(RectTransform image, RectTransform frame, Texture texture, Rect uv)
        {
            var frameWidth = StageWidth * (frame.anchorMax.x - frame.anchorMin.x);
            var frameHeight = StageHeight * (frame.anchorMax.y - frame.anchorMin.y);
            var aspect = texture == null || texture.height == 0 || uv.height <= 0f
                ? 1f
                : texture.width * uv.width / (texture.height * uv.height);
            var width = frameWidth;
            var height = width / aspect;
            if (height > frameHeight)
            {
                height = frameHeight;
                width = height * aspect;
            }
            image.anchorMin = image.anchorMax = new Vector2(0.5f, 0.5f);
            image.pivot = new Vector2(0.5f, 0.5f);
            image.sizeDelta = new Vector2(width, height);
            image.anchoredPosition = Vector2.zero;
        }

        private static RectTransform FindChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            return child == null ? null : child.GetComponent<RectTransform>();
        }

        private static void SetCenteredRect(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetFullRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorLocal(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    [CustomEditor(typeof(DossierCanvasPreview))]
    internal sealed class DossierCanvasPreviewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var preview = (DossierCanvasPreview)target;
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("Edit-mode preview only. Choose a suspect, then rebuild the Canvas to inspect the image crop, frame rotation, and English dossier text without entering Play Mode.", MessageType.Info);
            if (GUILayout.Button("Rebuild Current Canvas"))
            {
                DossierCanvasPreviewBuilder.Rebuild(preview);
            }

            var config = preview.caseConfig;
            foreach (var suspect in config == null ? Array.Empty<SuspectData>() : config.suspects ?? Array.Empty<SuspectData>())
            {
                if (!GUILayout.Button("Preview " + suspect.id + "  ·  " + suspect.displayName)) continue;
                Undo.RecordObject(preview, "Change dossier preview suspect");
                preview.suspectId = suspect.id;
                DossierCanvasPreviewBuilder.Rebuild(preview);
            }
        }
    }
}
