using System;
using System.Linq;
using UnityEngine;

namespace UnityMysteryExhibition
{
    [CreateAssetMenu(menuName = "Exhibition/Dossier Layout", fileName = "DossierLayout")]
    public sealed class DossierLayoutAsset : ScriptableObject
    {
        public int schemaVersion = 1;
        public string sourceNote = "Coordinates measured against the supplied dossier_s01-s05 artwork.";
        public DossierClueLayout[] clues = Array.Empty<DossierClueLayout>();
        public DossierTextLayout[] textLayouts = Array.Empty<DossierTextLayout>();

        public DossierClueLayout Find(string clueId)
        {
            return clues == null ? null : clues.FirstOrDefault(c => c != null && c.clueId == clueId);
        }

        public DossierTextLayout FindText(string suspectId)
        {
            return textLayouts == null ? null : textLayouts.FirstOrDefault(t => t != null && t.suspectId == suspectId);
        }
    }
}
