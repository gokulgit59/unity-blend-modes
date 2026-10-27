using UnityEngine;

namespace BlendMode.Showcase
{
    // A sticky note for the Hierarchy: select the "README" object to read how this scene is put together.
    public class SceneNotes : MonoBehaviour
    {
        [TextArea(12, 60)] public string notes;
    }
}
