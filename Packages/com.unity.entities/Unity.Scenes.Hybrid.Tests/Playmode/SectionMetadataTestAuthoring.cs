using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities.Tests;
#endif

public struct TestMetadata : IComponentData
{
    public int SectionIndex;
    public int Value;
}

public struct TestMetadataTag : IComponentData
{
}

public struct TestMetadataWithEntity : IComponentData
{
    public Entity Entity;
}

public struct TestMetadataWithBlobAsset : IComponentData
{
    public BlobAssetReference<int> BlobAsset;
}
#if UNITY_EDITOR
public class SectionMetadataTestAuthoring : MonoBehaviour
{
    public int Value;
    class Baker : Baker<SectionMetadataTestAuthoring>
    {
        public override void Bake(SectionMetadataTestAuthoring authoring)
        {
            // TODO: Port this test
            /*
            var sectionEntity = conversionSystem.GetSceneSectionEntity(entity);
            //Second call to GetSceneSectionEntity should return same entity, else bail out making the test fail
            if (conversionSystem.GetSceneSectionEntity(entity) != sectionEntity)
                return;

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            int sectionIndex = dstManager.GetSharedComponentManaged<SceneSection>(entity).Section;
            #pragma warning restore 0618
            dstManager.AddComponentData(sectionEntity, new TestMetadata {SectionIndex = sectionIndex, Value = Value});

        dstManager.AddComponentData(sectionEntity, new TestMetadataWithEntity());
        dstManager.AddComponentData(sectionEntity, new TestMetadataWithBlobAsset());
        #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
        dstManager.AddSharedComponentManaged(sectionEntity, new EcsTestSharedComp());
        #pragma warning restore 0618
        dstManager.AddBuffer<EcsIntElement>(sectionEntity);
        dstManager.AddComponentData(sectionEntity, new EcsCleanup1());
        dstManager.AddComponent<TestMetadataTag>(sectionEntity);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        dstManager.AddComponentData(sectionEntity, new EcsTestManagedComponent());
#endif
    */
        }
    }
}
#endif
