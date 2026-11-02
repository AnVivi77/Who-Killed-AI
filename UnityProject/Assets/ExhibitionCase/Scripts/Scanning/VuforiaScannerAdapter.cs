using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityMysteryExhibition
{
    // Reflection keeps the runtime assembly buildable in Mock mode when the
    // optional Vuforia package is not installed.
    public sealed class VuforiaScannerAdapter : MonoBehaviour, IClueScanner
    {
        [Tooltip("Accept direct TRACKED only; do not treat EXTENDED_TRACKED as a new discovery.")]
        public bool requireDirectTracking = true;

        [Tooltip("Minimum time a target must remain in direct TRACKED state before it is submitted.")]
        public float stableTrackingSeconds = 0.35f;

        [Tooltip("Discover Vuforia ObserverBehaviour components at runtime without a compile-time SDK dependency.")]
        public bool autoDiscoverVuforiaTargets = true;

        [Tooltip("Write target-status transitions to the Unity Console while integrating a camera.")]
        public bool logTrackingTransitions;

        [Tooltip("Target render rate while Vuforia is reading the webcam. 30 is the stable exhibition default.")]
        public int targetFrameRate = 30;

        [Tooltip("Read Vuforia target statuses at this interval instead of once per rendered frame.")]
        public float trackingPollInterval = 0.1f;

        [Tooltip("Disable ImageTarget observers while a dossier or video is on screen. Scanning resumes when investigation returns.")]
        public bool pauseVuforiaOutsideInvestigation = true;

        [Tooltip("The exhibition is a full-screen Canvas composition. Keep Vuforia's camera feed for tracking, but do not render it a second time.")]
        public bool optimizeForCanvasExhibition = true;

        private Type observerBehaviourType;
        private PropertyInfo targetNameProperty;
        private PropertyInfo targetStatusProperty;
        private PropertyInfo statusProperty;
        private readonly List<TargetState> targets = new List<TargetState>();
        private ExhibitionApp app;
        private float nextRefreshTime;
        private float nextTrackingPollTime;
        private bool observersEnabled = true;

        private sealed class TargetState
        {
            public Component observer;
            public string targetName;
            public string lastStatus;
            public bool wasTracked;
            public bool blockedUntilLost;
            public bool submittedForTracking;
            public float trackedSince;
        }

        private void Start()
        {
            app = FindObjectOfType<ExhibitionApp>();
            if (app != null && app.Config != null && app.Config.settings != null)
                stableTrackingSeconds = Mathf.Max(0f, app.Config.settings.stableTrackingSeconds);
            ApplyCanvasExhibitionPerformanceProfile();
            DiscoverVuforiaObservers();
            nextTrackingPollTime = 0f;
        }

        private void Update()
        {
            if (!autoDiscoverVuforiaTargets) return;
            if (app == null) app = FindObjectOfType<ExhibitionApp>();

            var shouldEnableObservers = !pauseVuforiaOutsideInvestigation || app == null || app.Phase == ExhibitionPhase.Investigating;
            if (shouldEnableObservers != observersEnabled)
                SetObserversEnabled(shouldEnableObservers);
            if (!shouldEnableObservers) return;

            // Vuforia performs the expensive camera-side recognition natively.
            // The managed bridge only needs to sample statuses often enough to
            // confirm a stable direct TRACKED transition. Polling at 10 Hz is
            // sufficient for the 0.35 s stability gate and avoids 11 reflection
            // calls plus string conversions on every Canvas frame.
            if (Time.unscaledTime < nextTrackingPollTime) return;
            nextTrackingPollTime = Time.unscaledTime + Mathf.Max(0.033f, trackingPollInterval);

            // Target objects are scene-authored and do not change during a
            // round. Re-scanning every 2 seconds makes webcam mode compete
            // with Vuforia for the main thread for no functional benefit.
            // Only retry discovery while the initial scan has not found any
            // observers, and use a slow retry in case Vuforia finishes loading
            // after this component.
            if (targets.Count == 0 && Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + 2f;
                DiscoverVuforiaObservers();
            }

            for (var i = targets.Count - 1; i >= 0; i--)
            {
                var target = targets[i];
                if (target == null || target.observer == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                var statusName = ReadStatusName(target.observer);
                if (logTrackingTransitions && !string.Equals(statusName, target.lastStatus, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[Exhibition] Vuforia target status: " + target.targetName + " -> " + statusName);
                    target.lastStatus = statusName;
                }

                var tracked = string.Equals(statusName, "TRACKED", StringComparison.OrdinalIgnoreCase);
                if (!tracked)
                {
                    if (target.wasTracked && logTrackingTransitions)
                        Debug.Log("[Exhibition] Vuforia target lost: " + target.targetName);
                    target.wasTracked = false;
                    target.submittedForTracking = false;
                    target.trackedSince = 0f;
                    target.blockedUntilLost = false;
                    continue;
                }

                if (!target.wasTracked)
                {
                    target.wasTracked = true;
                    target.trackedSince = Time.unscaledTime;
                    target.blockedUntilLost = app == null || app.Phase != ExhibitionPhase.Investigating;
                    if (logTrackingTransitions)
                        Debug.Log("[Exhibition] Vuforia target entered direct TRACKED: " + target.targetName);
                }

                if (app == null || app.Phase != ExhibitionPhase.Investigating || target.blockedUntilLost || target.submittedForTracking)
                    continue;
                if (Time.unscaledTime - target.trackedSince < Mathf.Max(0f, stableTrackingSeconds)) continue;

                var clueId = ResolveClueId(app.Config, target.targetName);
                if (string.IsNullOrWhiteSpace(clueId))
                {
                    Debug.LogWarning("[Exhibition] Vuforia target has no stable content mapping: " + target.targetName);
                    target.submittedForTracking = true;
                    continue;
                }
                target.submittedForTracking = true;
                ForwardDirectTracking(clueId);
            }
        }

        private void DiscoverVuforiaObservers()
        {
            observerBehaviourType = FindType("Vuforia.ObserverBehaviour");
            if (observerBehaviourType == null)
            {
                targetNameProperty = null;
                targetStatusProperty = null;
                statusProperty = null;
                targets.Clear();
                return;
            }

            targetNameProperty = observerBehaviourType.GetProperty("TargetName", BindingFlags.Public | BindingFlags.Instance);
            targetStatusProperty = observerBehaviourType.GetProperty("TargetStatus", BindingFlags.Public | BindingFlags.Instance);
            statusProperty = targetStatusProperty == null ? null : targetStatusProperty.PropertyType.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance);
            if (targetNameProperty == null || targetStatusProperty == null || statusProperty == null) return;

            var found = new HashSet<Component>();
            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour == null || !observerBehaviourType.IsAssignableFrom(behaviour.GetType())) continue;
                // Vuforia also exposes non-image observers such as
                // DeviceObserver. They can enter TRACKED/EXTENDED_TRACKED
                // states, but they are not exhibition clues and must never be
                // sent through the stable content-ID resolver.
                if (behaviour.GetComponent<VuforiaClueTarget>() == null) continue;
                var name = targetNameProperty.GetValue(behaviour, null) as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                found.Add(behaviour);
                var state = targets.Find(t => t.observer == behaviour);
                if (state == null)
                {
                    state = new TargetState { observer = behaviour, targetName = name };
                    targets.Add(state);
                }
                else
                {
                    state.targetName = name;
                }
            }
            targets.RemoveAll(t => t == null || t.observer == null || !found.Contains(t.observer));
        }

        private void SetObserversEnabled(bool enabled)
        {
            observersEnabled = enabled;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || target.observer == null) continue;
                var behaviour = target.observer as Behaviour;
                if (behaviour != null) behaviour.enabled = enabled;
                target.wasTracked = false;
                target.lastStatus = null;
                target.blockedUntilLost = false;
                target.submittedForTracking = false;
                target.trackedSince = 0f;
            }
            nextTrackingPollTime = Time.unscaledTime + (enabled ? 0.1f : 0f);
            if (logTrackingTransitions)
                Debug.Log("[Exhibition] Vuforia ImageTarget observers " + (enabled ? "enabled for investigation." : "paused while the Canvas shows a dossier or video."));
        }

        private string ReadStatusName(Component observer)
        {
            try
            {
                var targetStatus = targetStatusProperty.GetValue(observer, null);
                var status = statusProperty.GetValue(targetStatus, null);
                return status == null ? "UNKNOWN" : status.ToString();
            }
            catch (Exception ex)
            {
                if (logTrackingTransitions) Debug.LogWarning("[Exhibition] Could not read Vuforia target status: " + ex.Message);
                return "ERROR";
            }
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

        private static string ResolveClueId(CaseConfigAsset config, string targetName)
        {
            if (config == null || string.IsNullOrWhiteSpace(targetName)) return null;
            foreach (var clue in config.clues ?? Array.Empty<ClueData>())
                if (clue != null && string.Equals(clue.targetName, targetName, StringComparison.OrdinalIgnoreCase)) return clue.id;
            if (config.detective != null && string.Equals(config.detective.targetName, targetName, StringComparison.OrdinalIgnoreCase))
                return config.detective.informationClueId;
            return null;
        }

        public void SubmitClue(string clueId)
        {
            ForwardDirectTracking(clueId);
        }

        public void ForwardDirectTracking(string clueId)
        {
            if (app == null) app = FindObjectOfType<ExhibitionApp>();
            if (app != null) app.SubmitTrackedClue(clueId);
        }

        public void ForwardTrackingLost(string clueId)
        {
            // CaseProgressService owns progress; target loss does not undo discovery or close the dossier UI.
        }

        private void ApplyCanvasExhibitionPerformanceProfile()
        {
            if (targetFrameRate > 0)
            {
                // VSync overrides Application.targetFrameRate on standalone
                // builds. The exhibit uses a fixed 30 FPS budget so that the
                // webcam tracker and the 3840x1152 Canvas do not fight for an
                // unconstrained render loop.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }

            if (!optimizeForCanvasExhibition) return;

            // The Canvas already owns every visible pixel. Leave the ARCamera
            // and VuforiaBehaviour enabled so the webcam remains available for
            // image recognition, but make the Unity camera a cheap black pass
            // instead of rendering a redundant camera feed/scene underneath
            // the full-screen Canvas.
            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != "Vuforia.VuforiaBehaviour") continue;
                var camera = behaviour.GetComponent<Camera>();
                if (camera == null) continue;
                camera.cullingMask = 0;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                Debug.Log("[Exhibition] Vuforia webcam remains enabled for tracking; ARCamera background/scene rendering reduced because the Canvas owns the display.");
            }
        }
    }
}
