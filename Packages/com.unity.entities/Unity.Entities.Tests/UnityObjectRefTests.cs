#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    [Serializable]
    public class UnityObjectRefTests : ECSTestsCommonBase
    {
        private string TempAssetDir;
        private string TempAssetPath;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var guid = AssetDatabase.CreateFolder("Assets", nameof(UnityObjectRefTests));
            TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);
            TempAssetPath = $"{TempAssetDir}/TempTextAsset.asset";
            var textAsset = new TextAsset("Foo");
            AssetDatabase.CreateAsset(textAsset, TempAssetPath);
            AssetDatabase.Refresh();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(TempAssetDir);
        }

        struct StructWithUnityObjectRef : IComponentData
        {
            public UnityObjectRef<UnityEngine.Object> UnityObjectRef;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class ClassWithUnityObjectRef : IComponentData
        {
            public UnityObjectRef<UnityEngine.Object> UnityObjectRef;
        }

        #pragma warning disable EA0017 // intentionally a managed shared component
        struct SharedComponentManagedWithUnityObjectRef : ISharedComponentData, IEquatable<SharedComponentManagedWithUnityObjectRef>
        {
            public UnityObjectRef<UnityEngine.Object> UnityObjectRef;
            public UnityEngine.Object DummyManagedField;

            public bool Equals(SharedComponentManagedWithUnityObjectRef other)
            {
                return UnityObjectRef.Equals(other.UnityObjectRef) && Equals(DummyManagedField, other.DummyManagedField);
            }

            public override bool Equals(object obj)
            {
                return obj is SharedComponentManagedWithUnityObjectRef other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(UnityObjectRef, DummyManagedField);
            }
        }
        #pragma warning restore EA0017
#endif // !UNITY_DISABLE_MANAGED_COMPONENTS

        struct SharedComponentWithUnityObjectRef : ISharedComponentData
        {
            public UnityObjectRef<UnityEngine.Object> UnityObjectRef;
        }

        [TypeManager.TypeOverrides(hasNoBlobReferences:true, hasNoEntityReferences:true, hasNoUnityObjectReferences:true)]
        struct StructWithUnityObjectRefOverride : IComponentData
        {
            public UnityObjectRef<UnityEngine.Object> UnityObjectRef;
        }

        [UnityTest]
        public IEnumerator AssetGC_StructWithUnityObjectRefOverride_AssetReleased()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TempAssetPath);
            var entityId = textAsset.GetEntityId();

            using var world = new World("TestWorld");
            var entity = world.EntityManager.CreateEntity(new ComponentType(typeof(StructWithUnityObjectRefOverride)));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            world.EntityManager.SetComponentData(entity, new StructWithUnityObjectRefOverride{UnityObjectRef = textAsset});
            #pragma warning restore 0618

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));

            textAsset = null;

            yield return Resources.UnloadUnusedAssets();

            Assert.IsFalse(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));
        }

        [UnityTest]
        public IEnumerator AssetGC_StructComponent_AssetNotReleased()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TempAssetPath);
            var entityId = textAsset.GetEntityId();

            using var world = new World("TestWorld");
            var entity = world.EntityManager.CreateEntity(new ComponentType(typeof(StructWithUnityObjectRef)));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            world.EntityManager.SetComponentData(entity, new StructWithUnityObjectRef{UnityObjectRef = textAsset});
            #pragma warning restore 0618

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));

            textAsset = null;

            yield return Resources.UnloadUnusedAssets();

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [UnityTest]
        public IEnumerator AssetGC_ClassComponent_AssetNotReleased()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TempAssetPath);
            var entityId = textAsset.GetEntityId();

            using var world = new World("TestWorld");
            var entity = world.EntityManager.CreateEntity(new ComponentType(typeof(ClassWithUnityObjectRef)));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            world.EntityManager.SetComponentData(entity, new ClassWithUnityObjectRef{UnityObjectRef = textAsset});
            #pragma warning restore 0618

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));

            textAsset = null;

            yield return Resources.UnloadUnusedAssets();

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));
        }

        [UnityTest]
        public IEnumerator AssetGC_SharedComponentManaged_AssetNotReleased()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TempAssetPath);
            var entityId = textAsset.GetEntityId();

            using var world = new World("TestWorld");
            var entity = world.EntityManager.CreateEntity(new ComponentType(typeof(SharedComponentManagedWithUnityObjectRef)));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            world.EntityManager.SetSharedComponentManaged(entity, new SharedComponentManagedWithUnityObjectRef{UnityObjectRef = textAsset});
            #pragma warning restore 0618

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));

            textAsset = null;

            yield return Resources.UnloadUnusedAssets();

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));
        }
#endif

        [UnityTest]
        public IEnumerator AssetGC_SharedComponent_AssetNotReleased()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TempAssetPath);
            var entityId = textAsset.GetEntityId();

            using var world = new World("TestWorld");
            var entity = world.EntityManager.CreateEntity(new ComponentType(typeof(SharedComponentWithUnityObjectRef)));
            world.EntityManager.SetSharedComponent(entity, new SharedComponentWithUnityObjectRef{UnityObjectRef = textAsset});

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));

            textAsset = null;

            yield return Resources.UnloadUnusedAssets();

            Assert.IsTrue(AssetDatabase.IsMainAssetAtPathLoaded(TempAssetPath));
        }
    }
}
#endif
