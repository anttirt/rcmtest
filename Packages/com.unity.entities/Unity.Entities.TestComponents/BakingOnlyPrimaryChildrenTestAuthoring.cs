using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Hybrid.Baking;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class BakingOnlyPrimaryChildrenTestAuthoring : MonoBehaviour
    {
        public int SelfValue;

        [TemporaryBakingType]
        public struct ChildrenTestComponent : IBufferElementData
        {
            public Entity entity;
        }

        public struct PrimaryBakeOnlyChildrenTestComponent : IComponentData
        {
            public int Value;
        }

        class Baker : Baker<BakingOnlyPrimaryChildrenTestAuthoring>
        {
            public override void Bake(BakingOnlyPrimaryChildrenTestAuthoring authoring)
            {
                var component = new PrimaryBakeOnlyChildrenTestComponent
                {
                    Value = authoring.SelfValue
                };
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, component);

                var childrenBuffer = AddBuffer<ChildrenTestComponent>(entity);

                foreach (var transform in GetComponentsInChildren<Transform>())
                {
                    if (transform == authoring.transform)
                        continue;
                    childrenBuffer.Add(new ChildrenTestComponent() {entity = GetEntity(transform, TransformUsageFlags.None)});
                }
            }
        }

    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
        partial class BakingOnlyPrimaryChildrenTestBakingSystem : SystemBase
        {
            [BurstCompile]
            partial struct AddTestComponentJob : IJobEntity
            {
                public EntityCommandBuffer.ParallelWriter ParallelWriter;

                void Execute([EntityIndexInChunk] int indexInChunk,
                    in DynamicBuffer<BakingOnlyPrimaryChildrenTestAuthoring.ChildrenTestComponent> childrenBuffer)
                {
                    foreach (var child in childrenBuffer)
                    {
                        ParallelWriter.AddComponent
                            <BakingOnlyPrimaryChildrenTestAuthoring.PrimaryBakeOnlyChildrenTestComponent>(
                                indexInChunk, child.entity);
                    }
                }
            }
            protected override void OnUpdate()
            {
                var ecb = new EntityCommandBuffer(Allocator.TempJob);
                var ecbP = ecb.AsParallelWriter();

                new AddTestComponentJob() { ParallelWriter = ecbP }.ScheduleParallel();

                CompleteDependency();
                ecb.Playback(EntityManager);
                ecb.Dispose();
            }
        }

}
