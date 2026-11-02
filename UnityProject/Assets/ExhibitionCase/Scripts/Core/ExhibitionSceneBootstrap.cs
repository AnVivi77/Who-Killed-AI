using UnityEngine;

namespace UnityMysteryExhibition
{
    public sealed class ExhibitionSceneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (FindObjectOfType<ExhibitionApp>() == null)
            {
                var root = new GameObject("ExhibitionApp");
                root.AddComponent<ExhibitionApp>();
            }
        }
    }
}
