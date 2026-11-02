using System;
using UnityEngine;

namespace UnityMysteryExhibition
{
    /// <summary>
    /// Artwork-specific layout for one clue on a dossier. Coordinates are
    /// normalized to the supplied 16:9 dossier artwork and use Unity's
    /// bottom-left origin. The source image is never modified; uvRect is the
    /// visible source window used to trim artwork padding or watermarks.
    /// </summary>
    [Serializable]
    public sealed class DossierClueLayout
    {
        public string clueId;
        public Rect imageRect;
        public Rect cardRect;
        public float rotationDegrees;
        public Rect uvRect = new Rect(0f, 0f, 1f, 1f);
        public Rect titleTextRect = new Rect(0.06f, 0.04f, 0.88f, 0.22f);
        public Rect bodyTextRect = new Rect(0.08f, 0.30f, 0.84f, 0.62f);
        public Rect unknownTextRect = new Rect(0.06f, 0.26f, 0.88f, 0.48f);
        public int titleFontSize = 18;
        public int bodyFontSize = 16;
        public int unknownFontSize = 26;
        public float bodyLineSpacing = 1.04f;
        [TextArea(1, 3)] public string editorNote;

        public DossierClueLayout Clone()
        {
            return new DossierClueLayout
            {
                clueId = clueId,
                imageRect = imageRect,
                cardRect = cardRect,
                rotationDegrees = rotationDegrees,
                uvRect = uvRect,
                titleTextRect = titleTextRect,
                bodyTextRect = bodyTextRect,
                unknownTextRect = unknownTextRect,
                titleFontSize = titleFontSize,
                bodyFontSize = bodyFontSize,
                unknownFontSize = unknownFontSize,
                bodyLineSpacing = bodyLineSpacing,
                editorNote = editorNote
            };
        }
    }

    /// <summary>
    /// Artwork-specific text layout for the large profile card on a dossier.
    /// These values are normalized to the supplied 16:9 dossier artwork.
    /// </summary>
    [Serializable]
    public sealed class DossierTextLayout
    {
        public string suspectId;
        public Rect titleRect;
        public Rect profileRect;
        public Rect progressRect;
        public int titleFontSize = 25;
        public int profileFontSize = 22;
        public int progressFontSize = 15;
        public float profileLineSpacing = 1.08f;
        public TextAnchor profileAlignment = TextAnchor.UpperLeft;

        public DossierTextLayout Clone()
        {
            return new DossierTextLayout
            {
                suspectId = suspectId,
                titleRect = titleRect,
                profileRect = profileRect,
                progressRect = progressRect,
                titleFontSize = titleFontSize,
                profileFontSize = profileFontSize,
                progressFontSize = progressFontSize,
                profileLineSpacing = profileLineSpacing,
                profileAlignment = profileAlignment
            };
        }
    }

    public static class DossierLayoutDefaults
    {
        // The five dossier backgrounds share the same printed frame geometry.
        // The top frame is very slightly tilted; the lower frame is visibly
        // rotated in the supplied artwork. The frame itself clips the clue so
        // no source pixels can protrude over the printed black area.
        public static DossierClueLayout ForSlot(int slotIndex)
        {
            var behavior = slotIndex == 1;
            return new DossierClueLayout
            {
                imageRect = behavior
                    ? new Rect(0.688f, 0.511f, 0.143f, 0.259f)
                    : new Rect(0.634f, 0.110f, 0.173f, 0.276f),
                cardRect = behavior
                    ? new Rect(0.841f, 0.478f, 0.129f, 0.293f)
                    : new Rect(0.811f, 0.090f, 0.151f, 0.315f),
                rotationDegrees = behavior ? -1.5f : 6.0f,
                uvRect = new Rect(0f, 0f, 1f, 1f),
                editorNote = behavior
                    ? "Measured top black frame; keep the printed white border visible."
                    : "Measured lower black frame; rotate with the printed frame."
            };
        }

        public static void ApplyClueTextDefaults(DossierClueLayout layout, string clueId)
        {
            if (layout == null) return;

            layout.titleTextRect = new Rect(0.06f, 0.04f, 0.88f, 0.22f);
            layout.bodyTextRect = new Rect(0.08f, 0.30f, 0.84f, 0.62f);
            layout.unknownTextRect = new Rect(0.06f, 0.26f, 0.88f, 0.48f);
            layout.titleFontSize = clueId != null && clueId.EndsWith("_INFORMATION", StringComparison.Ordinal) ? 17 : 18;
            layout.bodyFontSize = BodyFontSizeFor(clueId);
            layout.unknownFontSize = 26;
            layout.bodyLineSpacing = 1.04f;
        }

        public static void NormalizeClueTextDefaults(DossierClueLayout layout, string clueId)
        {
            if (layout == null) return;
            var defaults = ForSlot(layout.imageRect.width > 0.15f ? 2 : 1);
            if (layout.titleTextRect.width <= 0f || layout.titleTextRect.height <= 0f) layout.titleTextRect = defaults.titleTextRect;
            if (layout.bodyTextRect.width <= 0f || layout.bodyTextRect.height <= 0f) layout.bodyTextRect = defaults.bodyTextRect;
            if (layout.unknownTextRect.width <= 0f || layout.unknownTextRect.height <= 0f) layout.unknownTextRect = defaults.unknownTextRect;
            if (layout.titleFontSize <= 0) layout.titleFontSize = clueId != null && clueId.EndsWith("_INFORMATION", StringComparison.Ordinal) ? 17 : 18;
            if (layout.bodyFontSize <= 0) layout.bodyFontSize = BodyFontSizeFor(clueId);
            if (layout.unknownFontSize <= 0) layout.unknownFontSize = 26;
            if (layout.bodyLineSpacing <= 0f) layout.bodyLineSpacing = 1.04f;
        }

        public static DossierTextLayout ForSuspect(string suspectId)
        {
            var layout = new DossierTextLayout
            {
                suspectId = suspectId,
                titleRect = new Rect(0.445f, 0.710f, 0.170f, 0.060f),
                profileRect = new Rect(0.425f, 0.540f, 0.215f, 0.155f),
                progressRect = new Rect(0.548f, 0.478f, 0.090f, 0.050f),
                titleFontSize = 25,
                profileFontSize = 22,
                progressFontSize = 15,
                profileLineSpacing = 1.08f,
                profileAlignment = TextAnchor.UpperLeft
            };

            // The artwork shares the same geometry, but longer display names and
            // profiles get a slightly smaller type scale so every dossier stays
            // balanced inside the printed card.
            switch (suspectId)
            {
                case "S01":
                    layout.titleFontSize = 25;
                    layout.profileFontSize = 22;
                    break;
                case "S02":
                    layout.titleFontSize = 25;
                    layout.profileFontSize = 21;
                    break;
                case "S03":
                    layout.titleFontSize = 22;
                    layout.profileFontSize = 21;
                    break;
                case "S04":
                    layout.titleFontSize = 24;
                    layout.profileFontSize = 22;
                    break;
                case "S05":
                    layout.titleFontSize = 25;
                    layout.profileFontSize = 22;
                    break;
            }
            return layout;
        }

        public static Rect TitleTextRect(DossierClueLayout layout)
        {
            return layout != null && layout.titleTextRect.width > 0f && layout.titleTextRect.height > 0f
                ? layout.titleTextRect
                : new Rect(0.06f, 0.04f, 0.88f, 0.22f);
        }

        public static Rect BodyTextRect(DossierClueLayout layout)
        {
            return layout != null && layout.bodyTextRect.width > 0f && layout.bodyTextRect.height > 0f
                ? layout.bodyTextRect
                : new Rect(0.08f, 0.30f, 0.84f, 0.62f);
        }

        public static Rect UnknownTextRect(DossierClueLayout layout)
        {
            return layout != null && layout.unknownTextRect.width > 0f && layout.unknownTextRect.height > 0f
                ? layout.unknownTextRect
                : new Rect(0.06f, 0.26f, 0.88f, 0.48f);
        }

        public static int TitleFontSize(DossierClueLayout layout, string clueId)
        {
            return layout != null && layout.titleFontSize > 0
                ? layout.titleFontSize
                : clueId != null && clueId.EndsWith("_INFORMATION", StringComparison.Ordinal) ? 17 : 18;
        }

        public static int BodyFontSize(DossierClueLayout layout, string clueId)
        {
            return layout != null && layout.bodyFontSize > 0 ? layout.bodyFontSize : BodyFontSizeFor(clueId);
        }

        public static int UnknownFontSize(DossierClueLayout layout)
        {
            return layout != null && layout.unknownFontSize > 0 ? layout.unknownFontSize : 26;
        }

        public static float BodyLineSpacing(DossierClueLayout layout)
        {
            return layout != null && layout.bodyLineSpacing > 0f ? layout.bodyLineSpacing : 1.04f;
        }

        private static int BodyFontSizeFor(string clueId)
        {
            switch (clueId)
            {
                case "CL_S02_BEHAVIOR": return 13;
                case "CL_S03_BEHAVIOR": return 13;
                case "CL_S03_INFORMATION": return 12;
                case "CL_S05_BEHAVIOR": return 15;
                default: return 17;
            }
        }
    }
}
