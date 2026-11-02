using UnityEngine;

namespace UnityMysteryExhibition
{
    public sealed class MockClueScanner : MonoBehaviour, IClueScanner
    {
        public void SubmitClue(string clueId)
        {
            var app = FindObjectOfType<ExhibitionApp>();
            if (app != null) app.SubmitMockClue(clueId);
        }
    }
}
