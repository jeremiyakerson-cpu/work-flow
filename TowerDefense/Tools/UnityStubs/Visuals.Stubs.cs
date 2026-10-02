// Visuals workstream additions to the compile-only UnityEngine stubs.
// Real Unity 6 signatures only; partial types extend the shared stub file.
#pragma warning disable CS0067, CS1591

namespace UnityEngine.Rendering
{
    /// <summary>UnityEngine.Rendering.SortingGroup (CoreModule): sorts a hierarchy of renderers as one unit.</summary>
    public sealed partial class SortingGroup : Behaviour
    {
        public string sortingLayerName { get; set; }
        public int sortingLayerID { get; set; }
        public int sortingOrder { get; set; }
        public bool sortAtRoot { get; set; }
    }
}

namespace UnityEngine
{
    public sealed partial class GameObject
    {
        public UnityEngine.SceneManagement.Scene scene => default;
    }

    public partial class TrailRenderer
    {
        public float minVertexDistance { get; set; }
        public int numCapVertices { get; set; }
        public bool emitting { get; set; }
    }
}
