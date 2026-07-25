using System;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor.Tests
{
    [AddComponentMenu("")]
    public class SubSceneLoadTestManagedAuthoring : MonoBehaviour
    {
        public GameObject Entity;
        public int Int;
        public string String;
        public Object Asset;

    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class SubSceneLoadTestManagedComponent : IComponentData
    {
        // Managed components do not support BlobAssetReference typed fields, hence not tested
        public int Int;
        public string String;
        public Object Asset;
        public Entity Entity;
    }
#endif

    public class SubSceneLoadTestManagedBaker : Baker<SubSceneLoadTestManagedAuthoring>
    {
        public override void Bake(SubSceneLoadTestManagedAuthoring authoring)
        {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            AddComponentObject(entity, new SubSceneLoadTestManagedComponent
            #pragma warning restore 0618
            {
                Entity = GetEntity(authoring.Entity, TransformUsageFlags.None),
                Int = authoring.Int,
                String = authoring.String,
                Asset = authoring.Asset,
            });
#endif
        }
    }
}
