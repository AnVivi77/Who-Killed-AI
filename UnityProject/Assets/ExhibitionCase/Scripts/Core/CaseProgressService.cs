using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMysteryExhibition
{
    public sealed class CaseRuntimeState
    {
        public readonly HashSet<string> discoveredClueIds = new HashSet<string>();
        public readonly HashSet<string> resolvedEventSuspectIds = new HashSet<string>();
        public bool detectiveEggConsumed;
        public bool finaleStarted;
        public bool finaleResolved;
        public ExhibitionPhase phase = ExhibitionPhase.Boot;

        public void Clear()
        {
            discoveredClueIds.Clear();
            resolvedEventSuspectIds.Clear();
            detectiveEggConsumed = false;
            finaleStarted = false;
            finaleResolved = false;
            phase = ExhibitionPhase.Boot;
        }
    }

    public sealed class ClueDiscoveryResult
    {
        public bool accepted;
        public bool isNew;
        public bool completesSuspect;
        public string clueId;
        public ClueData clue;
        public SuspectData suspect;
        public string message;
    }

    public sealed class CaseProgressService
    {
        public readonly CaseConfigAsset config;
        public readonly CaseRuntimeState state = new CaseRuntimeState();
        public event Action<CaseRuntimeState> Changed;

        public CaseProgressService(CaseConfigAsset config)
        {
            this.config = config;
        }

        public int DiscoveredCount => state.discoveredClueIds.Count;
        public int TotalClueCount => config == null || config.clues == null ? 0 : config.clues.Length;
        public int ResolvedEventCount => state.resolvedEventSuspectIds.Count;
        public int SuspectCount => config == null || config.suspects == null ? 0 : config.suspects.Length;

        public bool HasDiscovered(string clueId) => state.discoveredClueIds.Contains(clueId);

        public bool IsSuspectComplete(string suspectId)
        {
            if (config == null) return false;
            var clues = config.GetCluesForSuspect(suspectId);
            return clues.Count == 2 && clues.All(c => state.discoveredClueIds.Contains(c.id));
        }

        public int DiscoveredCountForSuspect(string suspectId)
        {
            if (config == null) return 0;
            return config.GetCluesForSuspect(suspectId).Count(c => state.discoveredClueIds.Contains(c.id));
        }

        public bool IsEventResolved(string suspectId) => state.resolvedEventSuspectIds.Contains(suspectId);

        public bool AllSuspectsComplete
        {
            get
            {
                return config != null && config.suspects != null && config.suspects.Length > 0 &&
                       config.suspects.All(s => IsSuspectComplete(s.id));
            }
        }

        public bool AllEventsResolved
        {
            get
            {
                return config != null && config.suspects != null && config.suspects.Length > 0 &&
                       config.suspects.All(s => IsEventResolved(s.id));
            }
        }

        public bool ReadyForFinale => AllSuspectsComplete && AllEventsResolved && !state.finaleStarted && !state.finaleResolved;

        public ClueDiscoveryResult DiscoverClue(string clueId)
        {
            var result = new ClueDiscoveryResult { clueId = clueId };
            if (config == null)
            {
                result.message = "Case configuration is not loaded.";
                return result;
            }

            var clue = config.FindClue(clueId);
            if (clue == null)
            {
                result.message = "Unconfigured clue: " + clueId;
                return result;
            }

            result.clue = clue;
            result.suspect = config.FindSuspect(clue.suspectId);
            if (result.suspect == null)
            {
                result.message = "The clue references a missing suspect: " + clue.suspectId;
                return result;
            }

            result.accepted = true;
            result.isNew = state.discoveredClueIds.Add(clue.id);
            result.completesSuspect = IsSuspectComplete(clue.suspectId);
            result.message = result.isNew ? "New clue discovered: " + clue.title : "Clue already discovered. The file can be viewed again.";
            if (result.isNew) Changed?.Invoke(state);
            return result;
        }

        public bool TryConsumeDetectiveEgg()
        {
            if (state.finaleStarted || state.finaleResolved || state.detectiveEggConsumed) return false;
            state.detectiveEggConsumed = true;
            Changed?.Invoke(state);
            return true;
        }

        public bool TryMarkEventResolved(string suspectId)
        {
            if (!IsSuspectComplete(suspectId)) return false;
            var added = state.resolvedEventSuspectIds.Add(suspectId);
            if (added) Changed?.Invoke(state);
            return added;
        }

        public void MarkFinaleStarted()
        {
            state.finaleStarted = true;
            Changed?.Invoke(state);
        }

        public void MarkFinaleResolved()
        {
            state.finaleResolved = true;
            state.phase = ExhibitionPhase.Completed;
            Changed?.Invoke(state);
        }

        public void Reset()
        {
            state.Clear();
            Changed?.Invoke(state);
        }
    }
}
