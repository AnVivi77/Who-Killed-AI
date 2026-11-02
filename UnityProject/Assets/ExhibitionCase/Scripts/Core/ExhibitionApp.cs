using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace UnityMysteryExhibition
{
    public sealed class ExhibitionApp : MonoBehaviour
    {
        public CaseConfigAsset config;
        public const int TargetWidth = 3840;
        public const int TargetHeight = 1152;

        private static ExhibitionApp instance;
        private CaseProgressService progress;
        private ExhibitionRuntimeUI ui;
        private VideoPlayer backgroundPlayer;
        private VideoPlayer narrativePlayer;
        private AudioSource backgroundAudio;
        private AudioSource narrativeAudio;
        private readonly Queue<string> eventQueue = new Queue<string>();
        private readonly HashSet<string> queuedOrPlayingEvents = new HashSet<string>();
        private Coroutine autoPlayRoutine;
        private int mediaRequestToken;
        private bool backgroundExpected;
        private float nextBackgroundRecoveryTime;
        private NarrativeKind narrativeKind;
        private string currentSuspectId;
        private bool initialized;
        private static readonly KeyCode[] MockClueKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        public CaseProgressService Progress => progress;
        public CaseConfigAsset Config => config;
        public ExhibitionPhase Phase => progress == null ? ExhibitionPhase.Boot : progress.state.phase;

        private enum NarrativeKind
        {
            None,
            Opening,
            SuspectEvent,
            DetectiveEgg,
            Finale
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Start()
        {
            if (initialized && Phase == ExhibitionPhase.Boot) StartOpening();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12)) ResetRound();
            HandleMockKeyboardInput();

            // Keep the permanent exhibition background alive if a Windows
            // Media Foundation backend temporarily stops after a loop or
            // reports a read error. The source texture is native and already
            // matches the 3840x1152 output, so no extra full-size copy is made.
            if (backgroundExpected && backgroundPlayer != null && backgroundPlayer.clip != null &&
                Time.unscaledTime >= nextBackgroundRecoveryTime &&
                (!backgroundPlayer.isPrepared || !backgroundPlayer.isPlaying))
            {
                nextBackgroundRecoveryTime = Time.unscaledTime + 0.5f;
                if (!backgroundPlayer.isPrepared)
                {
                    backgroundPlayer.Prepare();
                    Debug.Log("[Exhibition] background.mp4 playback watchdog requested a fresh prepare.");
                }
                else
                {
                    backgroundPlayer.Play();
                    Debug.Log("[Exhibition] background.mp4 playback watchdog restarted the background player.");
                }
            }
        }

        private void HandleMockKeyboardInput()
        {
            if (Phase != ExhibitionPhase.Investigating || config == null || config.settings == null ||
                config.settings.scannerMode != "Mock") return;

            var clues = config.clues;
            if (clues != null)
            {
                var count = Mathf.Min(clues.Length, MockClueKeys.Length);
                for (var i = 0; i < count; i++)
                {
                    if (clues[i] == null || !Input.GetKeyDown(MockClueKeys[i])) continue;
                    SubmitMockClue(clues[i].id);
                    return;
                }
            }

            // F9 is a hidden Mock-only trigger for Detective's one-shot Easter egg.
            if (Input.GetKeyDown(KeyCode.F9)) SubmitMockDetective();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (config == null) config = Resources.Load<CaseConfigAsset>("CaseConfig");
            progress = new CaseProgressService(config);

            var uiObject = new GameObject("ExhibitionRuntimeUI");
            uiObject.transform.SetParent(transform, false);
            ui = uiObject.AddComponent<ExhibitionRuntimeUI>();
            ui.Build(config);
            CreateMediaPlayers();
            SetResolutionTarget();

            if (config == null)
            {
                progress.state.phase = ExhibitionPhase.Error;
                Debug.LogError("[Exhibition] CaseConfig.asset is not loaded. Run Tools/Exhibition/Setup Demo.");
            }
            else
            {
                config.Validate(out var errors, out var warnings);
                if (errors.Count > 0)
                {
                    progress.state.phase = ExhibitionPhase.Error;
                    Debug.LogError("[Exhibition] Configuration error: " + string.Join("; ", errors));
                }
                else if (warnings.Count > 0)
                {
                    Debug.LogWarning("[Exhibition] Configuration note: " + string.Join("; ", warnings));
                }
            }
        }

        private void CreateMediaPlayers()
        {
            // Keep both players on APIOnly. Every clip is then exposed at its
            // decoder-native size and the Canvas controls placement. This is
            // especially important for background.mp4: its source is already
            // 3840x1152, so a second full-size RenderTexture would only add a
            // costly GPU copy while Vuforia is using the webcam.
            ui.SetBackgroundTexture(null);
            ui.SetNarrativeTexture(null);

            var backgroundObject = new GameObject("BackgroundVideoPlayer");
            backgroundObject.transform.SetParent(transform, false);
            backgroundPlayer = backgroundObject.AddComponent<VideoPlayer>();
            backgroundAudio = backgroundObject.AddComponent<AudioSource>();
            ConfigurePlayer(backgroundPlayer, backgroundAudio, true);
            backgroundPlayer.prepareCompleted += OnBackgroundPrepared;
            backgroundPlayer.loopPointReached += OnBackgroundLoopPointReached;
            backgroundPlayer.errorReceived += OnVideoError;

            var narrativeObject = new GameObject("NarrativeVideoPlayer");
            narrativeObject.transform.SetParent(transform, false);
            narrativePlayer = narrativeObject.AddComponent<VideoPlayer>();
            narrativeAudio = narrativeObject.AddComponent<AudioSource>();
            ConfigurePlayer(narrativePlayer, narrativeAudio, false);
            narrativePlayer.prepareCompleted += OnNarrativePrepared;
            narrativePlayer.loopPointReached += OnNarrativeFinished;
            narrativePlayer.errorReceived += OnVideoError;
        }

        private static void ConfigurePlayer(VideoPlayer player, AudioSource audio, bool loop)
        {
            player.playOnAwake = false;
            player.source = VideoSource.VideoClip;
            // The RawImage reads VideoPlayer.texture directly. This keeps the
            // source frame dimensions intact; the Canvas controls only the
            // placement and FitInParent adds side bars for the ultra-wide screen.
            player.renderMode = VideoRenderMode.APIOnly;
            player.targetTexture = null;
            player.aspectRatio = VideoAspectRatio.NoScaling;
            player.isLooping = loop;
            player.skipOnDrop = true;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.SetTargetAudioSource(0, audio);
            audio.playOnAwake = false;
            audio.loop = loop;
        }

        private static void SetResolutionTarget()
        {
            if (!Application.isBatchMode && !Application.isEditor)
                Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.FullScreenWindow);
        }

        private void StartOpening()
        {
            StopAutoPlay();
            progress.state.phase = ExhibitionPhase.Opening;
            ui.HideDossier();
            ui.SetBackgroundVisible(false);
            ui.SetNarrativeVisible(true);
            var clip = config == null ? null : config.FindVideo(config.introAssetId);
            PlayNarrative(clip, NarrativeKind.Opening, "opening");
            TryLoadScene("01_Intro");
        }

        private void StartBackground()
        {
            var clip = config == null ? null : config.FindVideo(config.backgroundAssetId);
            if (clip == null)
            {
                backgroundExpected = false;
                ui.SetBackgroundVisible(false);
                Debug.LogWarning("[Exhibition] background.mp4 is missing; the background remains black.");
                return;
            }
            backgroundPlayer.Stop();
            backgroundPlayer.clip = clip;
            ui.SetBackgroundTexture(null);
            // Let Unity's native decoder loop in place. Seeking to frame 0 from
            // loopPointReached repeatedly reopens Media Foundation's reader on
            // Windows and can cause stalls or 0xc00d36e5 read errors.
            backgroundPlayer.isLooping = true;
            backgroundExpected = true;
            nextBackgroundRecoveryTime = 0f;
            backgroundPlayer.Prepare();
        }

        private void OnBackgroundPrepared(VideoPlayer source)
        {
            if (source != backgroundPlayer || backgroundPlayer.clip == null || !backgroundExpected) return;
            ui.SetBackgroundTexture(backgroundPlayer.texture);
            Debug.Log("[Exhibition] background.mp4 prepared; background loop started. clip=" + backgroundPlayer.clip.name + " texture=" + DescribeTexture(backgroundPlayer.texture));
            ui.SetBackgroundVisible(true);
            backgroundPlayer.Play();
        }

        private void OnBackgroundLoopPointReached(VideoPlayer source)
        {
            if (source != backgroundPlayer || !backgroundExpected) return;
            // With isLooping enabled the native player is already advancing into
            // the next pass. Do not seek or call Play here; both operations can
            // interrupt the decoder exactly at the loop boundary.
            nextBackgroundRecoveryTime = Time.unscaledTime + 1f;
            Debug.Log("[Exhibition] background.mp4 reached its loop point; native looping continues.");
        }

        private void PlayNarrative(VideoClip clip, NarrativeKind kind, string label)
        {
            mediaRequestToken++;
            narrativeKind = kind;
            narrativePlayer.Stop();
            narrativePlayer.clip = clip;
            ui.SetNarrativeTexture(null);
            if (backgroundAudio != null && backgroundPlayer != null && backgroundPlayer.isPlaying) backgroundAudio.volume = 0.12f;
            ui.SetNarrativeVisible(clip != null);
            if (clip == null)
            {
                Debug.LogWarning("[Exhibition] Missing narrative media; skipping without an on-screen placeholder: " + label);
                FinishMissingNarrative(kind);
                return;
            }
            narrativePlayer.isLooping = false;
            narrativePlayer.Prepare();
        }

        private void OnNarrativePrepared(VideoPlayer source)
        {
            if (source != narrativePlayer || narrativePlayer.clip == null) return;
            var nativeTexture = narrativePlayer.texture;
            ui.SetNarrativeTexture(nativeTexture);
            var width = nativeTexture == null ? (int)narrativePlayer.width : nativeTexture.width;
            var height = nativeTexture == null ? (int)narrativePlayer.height : nativeTexture.height;
            if (width > 0 && height > 0)
                ui.SetNarrativeAspect((float)width / height);
            Debug.Log("[Exhibition] narrative prepared; playback started: " + narrativeKind + " / " + narrativePlayer.clip.name + " texture=" + DescribeTexture(nativeTexture) + " aspect=" + (height > 0 ? ((float)width / height).ToString("0.####") : "unknown"));
            narrativePlayer.Play();
        }

        private static string DescribeTexture(Texture texture)
        {
            return texture == null ? "MISSING" : texture.width + "x" + texture.height;
        }

        private void OnNarrativeFinished(VideoPlayer source)
        {
            if (source != narrativePlayer) return;
            Debug.Log("[Exhibition] narrative playback finished: " + narrativeKind);
            if (backgroundAudio != null) backgroundAudio.volume = 1f;
            switch (narrativeKind)
            {
                case NarrativeKind.Opening:
                    FinishOpening();
                    break;
                case NarrativeKind.SuspectEvent:
                    FinishSuspectEvent(currentSuspectId);
                    break;
                case NarrativeKind.DetectiveEgg:
                    FinishDetectiveEgg();
                    break;
                case NarrativeKind.Finale:
                    FinishFinale();
                    break;
            }
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            if (source == backgroundPlayer)
            {
                Debug.LogWarning("[Exhibition] background.mp4 playback error; the watchdog will attempt recovery: " + message);
                // Do not Stop() from the decoder callback. If the backend has
                // actually stopped, Update() will recover it after a short
                // cooldown; stopping here can create a prepare/seek storm.
                nextBackgroundRecoveryTime = Time.unscaledTime + 1f;
            }
            if (Phase == ExhibitionPhase.Completed) return;
            Debug.LogError("[Exhibition] Video playback error: " + message);
        }

        private void FinishOpening()
        {
            ui.SetNarrativeVisible(false);
            StartBackground();
            progress.state.phase = ExhibitionPhase.Investigating;
            TryLoadScene("02_Investigation");
        }

        private void FinishMissingNarrative(NarrativeKind kind)
        {
            ui.SetNarrativeVisible(false);
            if (backgroundAudio != null) backgroundAudio.volume = 1f;
            if (kind == NarrativeKind.Opening) FinishOpening();
            else if (kind == NarrativeKind.SuspectEvent) FinishSuspectEvent(currentSuspectId);
            else if (kind == NarrativeKind.DetectiveEgg) FinishDetectiveEgg();
            else if (kind == NarrativeKind.Finale) FinishFinale();
        }

        public void SubmitMockClue(string clueId)
        {
            if (Phase != ExhibitionPhase.Investigating)
            {
                return;
            }
            HandleClueDiscovery(clueId);
        }

        public void SubmitMockDetective()
        {
            if (Phase != ExhibitionPhase.Investigating)
            {
                return;
            }
            if (config == null || config.detective == null || !progress.TryConsumeDetectiveEgg()) return;
            progress.state.phase = ExhibitionPhase.PlayingDetectiveEasterEgg;
            PlayNarrative(config.FindVideo(config.detective.easterEggVideoAssetId), NarrativeKind.DetectiveEgg, "Detective X video");
        }

        // Vuforia and Mock scanning share this stable-ID protocol.
        public void SubmitTrackedClue(string clueId)
        {
            if (Phase != ExhibitionPhase.Investigating) return;

            // Detective is an Easter egg, not one of the mainline clues. The
            // Vuforia adapter still resolves its target to the stable clue ID
            // so the scanner pipeline remains uniform, but it must be routed
            // through the one-shot Easter egg gate instead of DiscoverClue().
            if (config != null && config.detective != null &&
                string.Equals(config.detective.informationClueId, clueId, StringComparison.OrdinalIgnoreCase))
            {
                SubmitMockDetective();
                return;
            }

            HandleClueDiscovery(clueId);
        }

        private void HandleClueDiscovery(string clueId)
        {
            var result = progress.DiscoverClue(clueId);
            Debug.Log("[Exhibition] Mock/Target scan: " + clueId + " accepted=" + result.accepted + " new=" + result.isNew + " completes=" + result.completesSuspect);
            if (!result.accepted)
            {
                Debug.LogWarning("[Exhibition] Clue was not accepted: " + result.message);
                return;
            }
            if (result.completesSuspect && result.isNew) QueueEvent(result.suspect.id);
            progress.state.phase = ExhibitionPhase.ShowingDossier;
            ui.ShowDossier(result.suspect, progress);
            StopAutoPlay();
            autoPlayRoutine = StartCoroutine(AdvanceAfterDossier(result.suspect.id));
        }

        private void QueueEvent(string suspectId)
        {
            if (string.IsNullOrWhiteSpace(suspectId) || progress.IsEventResolved(suspectId) || !queuedOrPlayingEvents.Add(suspectId)) return;
            eventQueue.Enqueue(suspectId);
        }

        private IEnumerator AdvanceAfterDossier(string suspectId)
        {
            var delay = Mathf.Max(0f, config.settings == null ? 3f : config.settings.dossierAutoPlayDelaySeconds);
            var remaining = delay;
            while (remaining > 0f && Phase == ExhibitionPhase.ShowingDossier)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (Phase == ExhibitionPhase.ShowingDossier) StartNextQueuedEvent(suspectId);
            autoPlayRoutine = null;
        }

        private void StartNextQueuedEvent(string preferredSuspectId = null)
        {
            if (Phase != ExhibitionPhase.ShowingDossier && Phase != ExhibitionPhase.Investigating) return;
            ui.HideDossier();
            string suspectId = null;
            if (!string.IsNullOrWhiteSpace(preferredSuspectId) && eventQueue.Contains(preferredSuspectId))
            {
                var kept = new Queue<string>();
                while (eventQueue.Count > 0)
                {
                    var id = eventQueue.Dequeue();
                    if (id == preferredSuspectId) suspectId = id;
                    else kept.Enqueue(id);
                }
                while (kept.Count > 0) eventQueue.Enqueue(kept.Dequeue());
            }
            if (suspectId == null && eventQueue.Count > 0) suspectId = eventQueue.Dequeue();
            if (suspectId == null)
            {
                progress.state.phase = ExhibitionPhase.Investigating;
                return;
            }

            currentSuspectId = suspectId;
            progress.state.phase = ExhibitionPhase.PlayingSuspectEvent;
            var suspect = config.FindSuspect(suspectId);
            PlayNarrative(config.FindVideo(suspect.eventVideoAssetId), NarrativeKind.SuspectEvent, suspect.displayName + " · event");
        }

        private void FinishSuspectEvent(string suspectId)
        {
            ui.SetNarrativeVisible(false);
            progress.TryMarkEventResolved(suspectId);
            if (eventQueue.Count > 0)
            {
                StartNextQueuedEvent();
                return;
            }
            if (progress.ReadyForFinale) StartFinale();
            else
            {
                progress.state.phase = ExhibitionPhase.Investigating;
            }
        }

        private void FinishDetectiveEgg()
        {
            ui.SetNarrativeVisible(false);
            progress.state.phase = ExhibitionPhase.Investigating;
        }

        private void StartFinale()
        {
            if (progress.state.finaleStarted || progress.state.finaleResolved) return;
            progress.MarkFinaleStarted();
            progress.state.phase = ExhibitionPhase.PlayingFinale;
            PlayNarrative(config.FindVideo(config.finaleAssetId), NarrativeKind.Finale, "ending");
            TryLoadScene("03_Finale");
        }

        private void FinishFinale()
        {
            ui.SetNarrativeVisible(false);
            progress.MarkFinaleResolved();
        }

        private void StopAutoPlay()
        {
            if (autoPlayRoutine != null)
            {
                StopCoroutine(autoPlayRoutine);
                autoPlayRoutine = null;
            }
        }

        public void ResetRound()
        {
            if (!initialized || config == null) return;
            StopAutoPlay();
            mediaRequestToken++;
            narrativePlayer.Stop();
            backgroundPlayer.Stop();
            backgroundExpected = false;
            eventQueue.Clear();
            queuedOrPlayingEvents.Clear();
            progress.Reset();
            ui.HideDossier();
            ui.SetNarrativeVisible(false);
            ui.SetBackgroundVisible(false);
            StartOpening();
        }

        private static void TryLoadScene(string sceneName)
        {
            if (SceneManager.GetActiveScene().name == sceneName) return;
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                if (path.EndsWith(sceneName + ".unity", StringComparison.OrdinalIgnoreCase))
                {
                    SceneManager.LoadScene(sceneName);
                    return;
                }
            }
        }
    }

}
