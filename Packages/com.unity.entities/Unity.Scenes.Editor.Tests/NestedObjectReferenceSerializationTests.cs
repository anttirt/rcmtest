using System.IO;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEditor;
using UnityEngine;
using World = Unity.Entities.World;

namespace Unity.Scenes.Editor.Tests
{
    public class NestedObjectReferenceSerializationTests : ECSTestsFixture
    {
        public struct NestedHostRefComponent : IComponentData
        {
            public UnityObjectRef<NestedObjectRefTestHost> Host;
        }

        // Regression test for EditorEntityScenes.CollectNestedNonPersistentReferences.
        // A non-persistent UnityEngine.Object (here a procedural Mesh) referenced only through a
        // serialized ScriptableObject - the shape RenderMeshArray uses via RenderMeshArrayHost - must
        // survive entity-scene serialization. Without the fix the nested object is written as null,
        // because only the host is a top-level objRefs.Array entry and SaveToSerializedFileAndForget
        // drops references to non-persistent objects that aren't themselves in the serialized set.
        [Test]
        public void NestedNonPersistentReference_SurvivesEntitySceneRoundTrip()
        {
            const string binPath = "Temp/nested-objref-test.bin";
            const string binRefPath = "Temp/nested-objref-test.bin.ref";

            var mesh = new Mesh { name = "ProceduralTestMesh" };
            Assert.IsFalse(EditorUtility.IsPersistent(mesh),
                "The mesh must be non-persistent for this test to exercise the fix.");

            var host = ScriptableObject.CreateInstance<NestedObjectRefTestHost>();
            host.nestedMesh = mesh;

            try
            {
                var entity = m_Manager.CreateEntity();
                m_Manager.AddComponentData(entity, new NestedHostRefComponent { Host = host });

                using var dstWorld = new World("dst");
                var dstManager = dstWorld.EntityManager;

                EditorEntityScenes.Write(m_Manager, binPath, binRefPath);
                EditorEntityScenes.Read(dstManager, binPath, binRefPath);

                var dstEntity = dstManager.UniversalQuery.GetSingletonEntity();
                var dstHost = dstManager.GetComponentData<NestedHostRefComponent>(dstEntity).Host.Value;

                // Use Unity's overloaded != so a destroyed/"fake null" object is treated as null.
                Assert.IsTrue(dstHost != null, "The host ScriptableObject did not survive serialization.");
                Assert.IsTrue(dstHost.nestedMesh != null, "The nested non-persistent mesh was lost during serialization.");
                Assert.AreEqual("ProceduralTestMesh", dstHost.nestedMesh.name);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(host);
                if (File.Exists(binPath)) File.Delete(binPath);
                if (File.Exists(binRefPath)) File.Delete(binRefPath);
            }
        }
    }
}
