using System;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor.Tests
{
    [AddComponentMenu("")]
    public class SubSceneLoadTestSharedAuthoring : MonoBehaviour
    {
        public int Int;
        public Object Asset;
        public string String;
    }

    #pragma warning disable EA0017 // intentionally a managed shared component
    public struct SubSceneLoadTestSharedComponent : ISharedComponentData, IEquatable<SubSceneLoadTestSharedComponent>
    {
        // Shared components do not support Entity or BlobAssetReference typed fields, hence not tested
        public int Int;
        public Object Asset;
        public string String;

        public bool Equals(SubSceneLoadTestSharedComponent other)
        {
            return Int == other.Int && Equals(Asset, other.Asset) && String == other.String;
        }

        public override bool Equals(object obj)
        {
            return obj is SubSceneLoadTestSharedComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Int;
                hashCode = (hashCode * 397) ^ (Asset != null ? Asset.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (String != null ? String.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
    #pragma warning restore EA0017

    public class SubSceneLoadTestBaker : Baker<SubSceneLoadTestSharedAuthoring>
    {
        public override void Bake(SubSceneLoadTestSharedAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            AddSharedComponentManaged(entity, new SubSceneLoadTestSharedComponent()
            #pragma warning restore 0618
            {
                Int = authoring.Int,
                Asset = authoring.Asset,
                String = authoring.String
            });
        }
    }
}
