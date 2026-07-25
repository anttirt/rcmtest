#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [DisallowMultipleComponent]
    public class WeakMaterialComponentAuthoring : MonoBehaviour
    {
        public Material mat;
    }

    #pragma warning disable EA0017 // intentionally a managed shared component
    public struct WeakMaterialComponent : ISharedComponentData, IEquatable<WeakMaterialComponent>
    {
        public Material material;

        public bool Equals(WeakMaterialComponent other)
        {
            return Equals(material, other.material);
        }

        public override bool Equals(object obj)
        {
            return obj is WeakMaterialComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (material != null ? material.GetHashCode() : 0);
        }
    }
    #pragma warning restore EA0017

    public class WeakMaterialComponentAuthoringBaker : Baker<WeakMaterialComponentAuthoring>
    {
        public override void Bake(WeakMaterialComponentAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            AddSharedComponentManaged(entity, new WeakMaterialComponent() { material = authoring.mat });
            #pragma warning restore 0618
        }
    }
}
#endif
