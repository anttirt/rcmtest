#nullable enable
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public sealed partial class ComponentLifecycleCallbacksPerformanceTests : EntityPerformanceTestFixture
    {
        [Test, Performance]
        [TestRequiresDotsDebugOrCollectionChecks("Component lifecycle callbacks only fire in debug/collection-checks builds")]
        public void BatchInstantiate_OnAdded_MainThread([Values(10, 1000, 100000)] int entityCount,
            [Values(typeof(EcsTestFloatData), typeof(EcsTestFloatDataWithDebugOnAdded))] System.Type type)
        {
            var componentType = ComponentType.ReadOnly(type);
            var prefabEntity = m_Manager.CreateEntity(componentType, typeof(Prefab));
            using var query = m_Manager.CreateEntityQuery(componentType);
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            Measure.Method(
                    () =>
                    {
                        m_Manager.Instantiate(prefabEntity, entities);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() => { })
                .CleanUp(() => { m_Manager.DestroyEntity(query); })
                .Run();
        }
    }
}
