using UnityEngine;

namespace UnityMysteryExhibition
{
    /// <summary>
    /// Edit-mode-only handle for the dossier composition scene. The editor
    /// builder creates the visible Canvas children; this component stores the
    /// selected suspect and source assets so the layout can be rebuilt without
    /// entering Play Mode.
    /// </summary>
    public sealed class DossierCanvasPreview : MonoBehaviour
    {
        public CaseConfigAsset caseConfig;
        public DossierLayoutAsset layout;
        public string suspectId = "S01";
        public bool showBothClues = true;
    }
}
