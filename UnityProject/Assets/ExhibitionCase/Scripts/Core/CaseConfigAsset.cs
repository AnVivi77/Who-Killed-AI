using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace UnityMysteryExhibition
{
    [CreateAssetMenu(menuName = "Exhibition/Case Config", fileName = "CaseConfig")]
    public sealed class CaseConfigAsset : ScriptableObject
    {
        public int schemaVersion = 1;
        public string caseId;
        public string title;
        public bool isPlaceholder = true;
        public string introAssetId;
        public string finaleAssetId;
        public string backgroundAssetId;
        public string corpseAssetId;
        public CaseSettings settings = new CaseSettings();
        public SuspectData[] suspects = Array.Empty<SuspectData>();
        public ClueData[] clues = Array.Empty<ClueData>();
        public DetectiveData detective = new DetectiveData();
        public AssetBinding[] assets = Array.Empty<AssetBinding>();

        public SuspectData FindSuspect(string id)
        {
            return suspects == null ? null : suspects.FirstOrDefault(s => s != null && s.id == id);
        }

        public ClueData FindClue(string id)
        {
            return clues == null ? null : clues.FirstOrDefault(c => c != null && c.id == id);
        }

        public List<ClueData> GetCluesForSuspect(string suspectId)
        {
            return clues == null
                ? new List<ClueData>()
                : clues.Where(c => c != null && c.suspectId == suspectId).OrderBy(c => c.slotIndex).ToList();
        }

        public AssetBinding FindAsset(string id)
        {
            return assets == null ? null : assets.FirstOrDefault(a => a != null && a.id == id);
        }

        public VideoClip FindVideo(string id)
        {
            var binding = FindAsset(id);
            return binding == null ? null : binding.video;
        }

        public Texture2D FindTexture(string id)
        {
            var binding = FindAsset(id);
            return binding == null ? null : binding.texture;
        }

        public bool Validate(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(caseId)) errors.Add("caseId is empty.");
            if (suspects == null || suspects.Length == 0) errors.Add("At least one mainline suspect is required.");
            if (clues == null || clues.Length == 0) errors.Add("At least one clue is required.");

            var suspectIds = new HashSet<string>();
            if (suspects != null)
            {
                foreach (var suspect in suspects)
                {
                    if (suspect == null || string.IsNullOrWhiteSpace(suspect.id))
                    {
                        errors.Add("A suspect or suspect ID is empty.");
                        continue;
                    }
                    if (!suspectIds.Add(suspect.id)) errors.Add("Duplicate suspect ID: " + suspect.id);
                }
            }

            var clueIds = new HashSet<string>();
            var targetNames = new HashSet<string>();
            if (clues != null)
            {
                foreach (var clue in clues)
                {
                    if (clue == null || string.IsNullOrWhiteSpace(clue.id))
                    {
                        errors.Add("A clue or clue ID is empty.");
                        continue;
                    }
                    if (!clueIds.Add(clue.id)) errors.Add("Duplicate clue ID: " + clue.id);
                    if (!suspectIds.Contains(clue.suspectId)) errors.Add($"Clue {clue.id} references missing suspect {clue.suspectId}.");
                    if (clue.slotIndex != 1 && clue.slotIndex != 2) errors.Add($"Clue {clue.id} must use slot 1 or 2.");
                    if (!targetNames.Add(clue.targetName)) errors.Add("Duplicate target name: " + clue.targetName);
                    if (clue.physicalWidthMeters <= 0) warnings.Add($"Clue {clue.id} has no physical print width; real Vuforia scanning still needs this value.");
                }
            }

            foreach (var suspect in suspects ?? Array.Empty<SuspectData>())
            {
                var count = GetCluesForSuspect(suspect.id).Count;
                if (count != 2) errors.Add($"Suspect {suspect.id} must have exactly two clues; current count is {count}.");
                if (string.IsNullOrWhiteSpace(suspect.eventVideoAssetId)) warnings.Add($"Suspect {suspect.id} has no event video configured.");
            }

            if (string.IsNullOrWhiteSpace(introAssetId)) errors.Add("Opening video is not configured.");
            if (string.IsNullOrWhiteSpace(backgroundAssetId)) warnings.Add("Background video is not configured.");
            if (string.IsNullOrWhiteSpace(finaleAssetId)) errors.Add("Ending video is not configured.");
            if (isPlaceholder) warnings.Add("This case is still marked as placeholder content and cannot pass formal acceptance.");
            return errors.Count == 0;
        }
    }
}
