using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMysteryExhibition
{
    public sealed class ExhibitionRuntimeUI : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject backgroundVideoLayer;
        private GameObject narrativeVideoLayer;
        private RawImage backgroundImage;
        private RawImage narrativeImage;
        private AspectRatioFitter narrativeFitter;
        private GameObject dossierRoot;
        private Text dossierTitle;
        private Text dossierProfile;
        private RectTransform dossierStage;
        private AspectRatioFitter dossierStageFitter;
        private RawImage dossierArtImage;
        private readonly List<DossierClueUi> dossierClues = new List<DossierClueUi>();
        private Font uiFont;
        private CaseConfigAsset config;
        private DossierLayoutAsset dossierLayout;

        public RawImage BackgroundImage => backgroundImage;
        public RawImage NarrativeImage => narrativeImage;

        public void Build(CaseConfigAsset caseConfig)
        {
            config = caseConfig;
            dossierLayout = Resources.Load<DossierLayoutAsset>("DossierLayout");
            uiFont = Font.CreateDynamicFontFromOSFont("Arial", 32);
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840, 1152);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            backgroundVideoLayer = new GameObject("BackgroundVideoLayer", typeof(RectTransform));
            backgroundVideoLayer.transform.SetParent(transform, false);
            SetFullScreen(backgroundVideoLayer.GetComponent<RectTransform>());
            backgroundImage = CreateRawImage("BackgroundVideo", backgroundVideoLayer.transform, Color.black);
            SetFullScreen(backgroundImage.rectTransform);
            backgroundImage.raycastTarget = false;

            narrativeVideoLayer = new GameObject("NarrativeVideoLayer", typeof(RectTransform));
            narrativeVideoLayer.transform.SetParent(transform, false);
            SetFullScreen(narrativeVideoLayer.GetComponent<RectTransform>());
            narrativeImage = CreateRawImage("NarrativeVideo", narrativeVideoLayer.transform, Color.white);
            SetFullScreen(narrativeImage.rectTransform);
            narrativeImage.raycastTarget = false;
            narrativeFitter = narrativeImage.gameObject.AddComponent<AspectRatioFitter>();
            // Preserve the complete frame. The 3840x1152 canvas is ultra-wide, so
            // narrower videos receive side bars instead of being cropped or stretched.
            narrativeFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            narrativeFitter.aspectRatio = 16f / 9f;
            narrativeVideoLayer.SetActive(false);

            CreateDossier();
            HideDossier();
            SetNarrativeVisible(false);
        }

        public void SetNarrativeAspect(float aspect)
        {
            if (narrativeFitter != null && aspect > 0.01f) narrativeFitter.aspectRatio = aspect;
        }

        public void SetBackgroundTexture(Texture texture)
        {
            if (backgroundImage != null) backgroundImage.texture = texture;
            if (backgroundImage != null && texture == null) backgroundImage.color = Color.black;
        }

        public void SetNarrativeTexture(Texture texture)
        {
            if (narrativeImage != null) narrativeImage.texture = texture;
        }

        public void SetBackgroundVisible(bool visible)
        {
            if (backgroundImage == null) return;
            if (backgroundVideoLayer != null) backgroundVideoLayer.SetActive(true);
            else backgroundImage.gameObject.SetActive(true);
            backgroundImage.color = visible ? Color.white : Color.black;
        }

        public void SetNarrativeVisible(bool visible)
        {
            if (narrativeVideoLayer != null) narrativeVideoLayer.SetActive(visible);
            else if (narrativeImage != null) narrativeImage.gameObject.SetActive(visible);
        }

        public void ShowDossier(SuspectData suspect, CaseProgressService progress)
        {
            if (suspect == null || config == null) return;
            dossierRoot.SetActive(true);
            dossierTitle.text = suspect.displayName.ToUpperInvariant();
            dossierProfile.text = suspect.profile ?? string.Empty;
            ApplyDossierTextLayout(dossierLayout == null ? null : dossierLayout.FindText(suspect.id) ?? DossierLayoutDefaults.ForSuspect(suspect.id));
            var dossierArtwork = config.FindTexture(suspect.dossierAssetId);
            // The dossier file is the base composition. It must stay full-frame;
            // only the two clue images/cards are overlaid on top of it.
            dossierArtImage.texture = dossierArtwork;
            dossierArtImage.color = dossierArtwork == null ? Color.black : Color.white;
            if (dossierStageFitter != null && dossierArtwork != null && dossierArtwork.height > 0)
                dossierStageFitter.aspectRatio = (float)dossierArtwork.width / dossierArtwork.height;
            Debug.Log($"[Exhibition] Dossier artwork bound: {suspect.id} / {suspect.dossierAssetId} / {(dossierArtwork == null ? "MISSING" : dossierArtwork.width + "x" + dossierArtwork.height)}");
            var clues = config.GetCluesForSuspect(suspect.id);
            for (var i = 0; i < dossierClues.Count; i++)
            {
                var clue = i < clues.Count ? clues[i] : null;
                var found = clue != null && progress.HasDiscovered(clue.id);
                var layout = clue == null ? null : dossierLayout == null ? null : dossierLayout.Find(clue.id);
                dossierClues[i].ApplyLayout(layout ?? DossierLayoutDefaults.ForSlot(i + 1));
                dossierClues[i].Apply(clue, found, config, layout ?? DossierLayoutDefaults.ForSlot(i + 1));
            }
        }

        public void HideDossier()
        {
            if (dossierRoot != null) dossierRoot.SetActive(false);
        }

        private void CreateDossier()
        {
            dossierRoot = CreatePanel("Dossier", transform, new Color(0.01f, 0.01f, 0.015f, 0.97f));
            Anchor(dossierRoot.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            // The supplied dossier artwork is a complete 16:9 composition. Keep a
            // single aspect-fitted stage so every overlay uses the artwork's own
            // normalized coordinates and never drifts on the 3840x1152 canvas.
            var stageObject = new GameObject("DossierStage", typeof(RectTransform));
            stageObject.transform.SetParent(dossierRoot.transform, false);
            dossierStage = stageObject.GetComponent<RectTransform>();
            SetFullScreen(dossierStage);
            dossierStageFitter = stageObject.AddComponent<AspectRatioFitter>();
            dossierStageFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            dossierStageFitter.aspectRatio = 16f / 9f;

            dossierArtImage = CreateRawImage("DossierArtwork_Base", dossierStage, Color.white);
            SetFullScreen(dossierArtImage.rectTransform);
            dossierArtImage.raycastTarget = false;

            dossierTitle = CreateText("DossierTitle", dossierStage, "", 28, Color.white, TextAnchor.MiddleCenter);
            dossierProfile = CreateText("DossierProfile", dossierStage, "", 16, new Color(0.08f, 0.08f, 0.08f), TextAnchor.UpperLeft);
            dossierProfile.horizontalOverflow = HorizontalWrapMode.Wrap;
            dossierProfile.verticalOverflow = VerticalWrapMode.Truncate;
            ApplyDossierTextLayout(DossierLayoutDefaults.ForSuspect("S01"));

            // These coordinates are stored in DossierLayout.asset so the editor
            // preview and runtime use exactly the same artwork-specific layout.
            CreateDossierClue(dossierStage, 0, "BEHAVIOR", DossierLayoutDefaults.ForSlot(1), ClueIdForSlot(1));
            CreateDossierClue(dossierStage, 1, "INFORMATION", DossierLayoutDefaults.ForSlot(2), ClueIdForSlot(2));
        }

        private void ApplyDossierTextLayout(DossierTextLayout textLayout)
        {
            if (textLayout == null) textLayout = DossierLayoutDefaults.ForSuspect("S01");

            Anchor(dossierTitle.rectTransform, textLayout.titleRect.xMin, textLayout.titleRect.yMin, textLayout.titleRect.xMax, textLayout.titleRect.yMax);
            dossierTitle.fontSize = textLayout.titleFontSize;
            dossierTitle.fontStyle = FontStyle.Bold;
            dossierTitle.alignment = TextAnchor.MiddleCenter;

            Anchor(dossierProfile.rectTransform, textLayout.profileRect.xMin, textLayout.profileRect.yMin, textLayout.profileRect.xMax, textLayout.profileRect.yMax);
            dossierProfile.fontSize = textLayout.profileFontSize;
            dossierProfile.lineSpacing = textLayout.profileLineSpacing;
            dossierProfile.alignment = textLayout.profileAlignment;
            dossierProfile.horizontalOverflow = HorizontalWrapMode.Wrap;
            dossierProfile.verticalOverflow = VerticalWrapMode.Truncate;

        }

        private string ClueIdForSlot(int slotIndex)
        {
            if (config != null && config.clues != null)
            {
                foreach (var clue in config.clues)
                    if (clue != null && clue.slotIndex == slotIndex) return clue.id;
            }
            return slotIndex == 1 ? "CLUE_BEHAVIOR" : "CLUE_INFORMATION";
        }

        private void CreateDossierClue(Transform parent, int index, string label, DossierClueLayout initialLayout, string clueId)
        {
            var frameObject = new GameObject("DossierClueImageFrame_" + (index + 1), typeof(RectTransform), typeof(RectMask2D));
            frameObject.transform.SetParent(parent, false);
            var frame = frameObject.GetComponent<RectTransform>();
            Anchor(frame, initialLayout.imageRect.xMin, initialLayout.imageRect.yMin, initialLayout.imageRect.xMax, initialLayout.imageRect.yMax);
            frame.localEulerAngles = new Vector3(0f, 0f, initialLayout.rotationDegrees);

            // Keep the clue image inside its printed photo frame. The aspect
            // fitter belongs to this child, never to the anchored frame itself.
            var imageObject = new GameObject("DossierClueImage_" + (index + 1), typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(frameObject.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            SetFullScreen(image.rectTransform);

            var cardObject = new GameObject("DossierClueCard_" + (index + 1), typeof(RectTransform));
            cardObject.transform.SetParent(parent, false);
            var card = cardObject.GetComponent<RectTransform>();
            Anchor(card, initialLayout.cardRect.xMin, initialLayout.cardRect.yMin, initialLayout.cardRect.xMax, initialLayout.cardRect.yMax);
            card.localEulerAngles = new Vector3(0f, 0f, initialLayout.rotationDegrees);

            var title = CreateText("Title", card, label, DossierLayoutDefaults.TitleFontSize(initialLayout, clueId), Color.white, TextAnchor.MiddleCenter);
            // The supplied cards have a red title strip at the bottom. Put the
            // English label on that strip instead of floating over blank paper.
            var titleRect = DossierLayoutDefaults.TitleTextRect(initialLayout);
            Anchor(title.rectTransform, titleRect.xMin, titleRect.yMin, titleRect.xMax, titleRect.yMax);
            title.fontStyle = FontStyle.Bold;
            var body = CreateText("Body", card, "", DossierLayoutDefaults.BodyFontSize(initialLayout, clueId), new Color(0.08f, 0.08f, 0.08f), TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.lineSpacing = DossierLayoutDefaults.BodyLineSpacing(initialLayout);
            var bodyRect = DossierLayoutDefaults.BodyTextRect(initialLayout);
            Anchor(body.rectTransform, bodyRect.xMin, bodyRect.yMin, bodyRect.xMax, bodyRect.yMax);
            var unknown = CreateText("Unknown", card, "? NOT FOUND", DossierLayoutDefaults.UnknownFontSize(initialLayout), new Color(0.55f, 0.02f, 0.08f), TextAnchor.MiddleCenter);
            var unknownRect = DossierLayoutDefaults.UnknownTextRect(initialLayout);
            Anchor(unknown.rectTransform, unknownRect.xMin, unknownRect.yMin, unknownRect.xMax, unknownRect.yMax);
            var ui = new DossierClueUi
            {
                frame = frame,
                card = card,
                clueId = clueId,
                image = image,
                title = title,
                body = body,
                unknown = unknown
            };
            dossierClues.Add(ui);
        }

        private Text CreateText(string name, Transform parent, string text, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var value = go.AddComponent<Text>();
            value.font = uiFont;
            value.fontSize = size;
            value.color = color;
            value.alignment = alignment;
            value.raycastTarget = false;
            value.text = text;
            value.supportRichText = false;
            value.resizeTextForBestFit = false;
            return value;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            go.GetComponent<RawImage>().color = color;
            return go.GetComponent<RawImage>();
        }

        private static void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetTexturePreservingAspect(RawImage target, Texture texture)
        {
            SetTexturePreservingAspect(target, texture, new Rect(0f, 0f, 1f, 1f));
        }

        private static void SetTexturePreservingAspect(RawImage target, Texture texture, Rect uvRect)
        {
            target.texture = texture;
            target.uvRect = uvRect;
            var fitter = target.GetComponent<AspectRatioFitter>() ?? target.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (texture != null && texture.height > 0 && uvRect.height > 0)
                fitter.aspectRatio = (float)texture.width * uvRect.width / (texture.height * uvRect.height);
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private sealed class DossierClueUi
        {
            public RectTransform frame;
            public RectTransform card;
            public string clueId;
            public Text title;
            public Text body;
            public RawImage image;
            public Text unknown;

            public void ApplyLayout(DossierClueLayout layout)
            {
                if (layout == null) return;
                Anchor(frame, layout.imageRect.xMin, layout.imageRect.yMin, layout.imageRect.xMax, layout.imageRect.yMax);
                frame.localEulerAngles = new Vector3(0f, 0f, layout.rotationDegrees);
                Anchor(card, layout.cardRect.xMin, layout.cardRect.yMin, layout.cardRect.xMax, layout.cardRect.yMax);
                card.localEulerAngles = new Vector3(0f, 0f, layout.rotationDegrees);

                var titleRect = DossierLayoutDefaults.TitleTextRect(layout);
                Anchor(title.rectTransform, titleRect.xMin, titleRect.yMin, titleRect.xMax, titleRect.yMax);
                title.fontSize = DossierLayoutDefaults.TitleFontSize(layout, clueId);
                title.fontStyle = FontStyle.Bold;
                var bodyRect = DossierLayoutDefaults.BodyTextRect(layout);
                Anchor(body.rectTransform, bodyRect.xMin, bodyRect.yMin, bodyRect.xMax, bodyRect.yMax);
                body.fontSize = DossierLayoutDefaults.BodyFontSize(layout, clueId);
                body.lineSpacing = DossierLayoutDefaults.BodyLineSpacing(layout);
                var unknownRect = DossierLayoutDefaults.UnknownTextRect(layout);
                Anchor(unknown.rectTransform, unknownRect.xMin, unknownRect.yMin, unknownRect.xMax, unknownRect.yMax);
                unknown.fontSize = DossierLayoutDefaults.UnknownFontSize(layout);
            }

            public void Apply(ClueData clue, bool found, CaseConfigAsset config, DossierClueLayout layout)
            {
                if (!found || clue == null)
                {
                    title.gameObject.SetActive(false);
                    body.gameObject.SetActive(false);
                    unknown.gameObject.SetActive(true);
                    SetTexturePreservingAspect(image, null, new Rect(0f, 0f, 1f, 1f));
                    image.gameObject.SetActive(false);
                    return;
                }

                title.gameObject.SetActive(true);
                body.gameObject.SetActive(true);
                unknown.gameObject.SetActive(false);
                title.text = clue.title;
                body.text = clue.body;
                SetTexturePreservingAspect(image, config.FindTexture(clue.displayImageAssetId), layout == null ? new Rect(0f, 0f, 1f, 1f) : layout.uvRect);
                image.gameObject.SetActive(image.texture != null);
            }
        }
    }
}
