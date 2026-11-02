using UnityEngine;

namespace UnityMysteryExhibition
{
    [DisallowMultipleComponent]
    public sealed class VuforiaClueTarget : MonoBehaviour
    {
        public string clueId;
        public string targetName;
        public bool directTracking;

        public void OnDirectTrackingFound()
        {
            directTracking = true;
            var adapter = FindObjectOfType<VuforiaScannerAdapter>();
            if (adapter != null && adapter.requireDirectTracking) adapter.ForwardDirectTracking(clueId);
        }

        public void OnTrackingLost()
        {
            directTracking = false;
            var adapter = FindObjectOfType<VuforiaScannerAdapter>();
            if (adapter != null) adapter.ForwardTrackingLost(clueId);
        }
    }
}
