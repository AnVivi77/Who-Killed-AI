using System;
using UnityEngine;
using UnityEngine.Video;

namespace UnityMysteryExhibition
{
    public enum AssetKind
    {
        video,
        image,
        targetImage,
        database,
        text
    }

    public enum ExhibitionPhase
    {
        Boot,
        Opening,
        Investigating,
        ShowingDossier,
        PlayingSuspectEvent,
        PlayingDetectiveEasterEgg,
        PlayingFinale,
        Completed,
        Error
    }

    [Serializable]
    public class CaseSettings
    {
        public string scannerMode = "Mock";
        public string eventPlaybackMode = "AfterDelay";
        public float dossierAutoPlayDelaySeconds = 3f;
        public float stableTrackingSeconds = 0.35f;
        public float rearmAfterLostSeconds = 0.5f;
        public bool saveProgress;
        public bool allowVideoSkip;
        public float autoRestartSeconds;
    }

    [Serializable]
    public class SuspectData
    {
        public string id;
        public string displayName;
        [TextArea(3, 12)] public string profile;
        public string portraitAssetId;
        public string dossierAssetId;
        public string eventVideoAssetId;
    }

    [Serializable]
    public class ClueData
    {
        public string id;
        public string suspectId;
        public int slotIndex;
        public string title;
        [TextArea(3, 12)] public string body;
        public string displayImageAssetId;
        public string targetImageAssetId;
        public string targetName;
        public float physicalWidthMeters;
    }

    [Serializable]
    public class DetectiveData
    {
        public string id = "D01";
        public string displayName = "Detective";
        public string informationClueId = "CL_DETECTIVE_INFORMATION";
        public string targetName = "Detective_Information";
        public string targetImageAssetId;
        public string easterEggVideoAssetId;
    }

    [Serializable]
    public class AssetBinding
    {
        public string id;
        public AssetKind kind;
        public string sourcePath;
        public string importedPath;
        public Texture2D texture;
        public VideoClip video;
    }

    // These DTOs are used only for JSON import; content/*.json remains the source of truth.
    [Serializable]
    public class CaseJsonDocument
    {
        public int schemaVersion;
        public string caseId;
        public string title;
        public bool isPlaceholder;
        public string introAssetId;
        public string finaleAssetId;
        public string backgroundAssetId;
        public string corpseAssetId;
        public CaseSettings settings;
        public SuspectData[] suspects;
        public ClueData[] clues;
        public DetectiveData detective;
    }

    [Serializable]
    public class AssetManifestDocument
    {
        public int schemaVersion;
        public string pathBase;
        public string note;
        public AssetManifestEntry[] assets;
    }

    [Serializable]
    public class AssetManifestEntry
    {
        public string id;
        public string kind;
        public string label;
        public string sourcePath;
        public string suggestedPath;
        public string requiredFor;
    }
}
