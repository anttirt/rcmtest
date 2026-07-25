using UnityEngine;

namespace Unity.Scenes.Editor.Tests
{
    // Test-only ScriptableObject mirroring the shape of RenderMeshArrayHost: a serialized object,
    // referenced from a component via UnityObjectRef, that itself holds a reference to another
    // UnityEngine.Object. Lives in its own file matching the class name so its MonoScript binding
    // resolves correctly across the serialization round-trip exercised by the regression test.
    public class NestedObjectRefTestHost : ScriptableObject
    {
        public Mesh nestedMesh;
    }
}
