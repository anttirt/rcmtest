using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class AssetReferenceTests : ECSTestsFixture
    {
        #pragma warning disable EA0017 // intentionally a managed shared component
        struct SharedComponentWithAssetReference : ISharedComponentData , IEquatable<SharedComponentWithAssetReference>
        {
            public TextAsset Target;

            public bool Equals(SharedComponentWithAssetReference other)
            {
                return Target == other.Target;
            }

            public override int GetHashCode()
            {
                return ReferenceEquals(Target, null) ? 0 : Target.GetHashCode();
            }
        }
        #pragma warning restore EA0017

        [Test]
        public void SharedComponents_ReferencingAssets_PreventUnloadBy_UnloadUnusedAssets()
        {
            var e = m_Manager.CreateEntity();
            var sharedComponent = new SharedComponentWithAssetReference {Target = new TextAsset()};
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddSharedComponentManaged(e, sharedComponent);
            #pragma warning restore 0618
            sharedComponent.Target = null;
            EditorUtility.UnloadUnusedAssetsImmediate();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.IsFalse(m_Manager.GetSharedComponentManaged<SharedComponentWithAssetReference>(e).Target == null);
            #pragma warning restore 0618
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class ManagedComponentWithAssetReference : IComponentData
        {
            public TextAsset Target;
        }

        [Test]
        public void ManagedComponents_ReferencingAssets_PreventUnloadBy_UnloadUnusedAssets()
        {
            var e = m_Manager.CreateEntity();
            var managedComponent = new ManagedComponentWithAssetReference {Target = new TextAsset()};
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddComponentData(e, managedComponent);
            #pragma warning restore 0618
            managedComponent = null;
            EditorUtility.UnloadUnusedAssetsImmediate();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.IsFalse(m_Manager.GetComponentData<ManagedComponentWithAssetReference>(e).Target == null);
            #pragma warning restore 0618
        }
#endif
    }
}
