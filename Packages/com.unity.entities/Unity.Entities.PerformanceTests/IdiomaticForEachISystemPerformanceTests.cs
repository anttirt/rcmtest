using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace Unity.Entities.PerformanceTests
{
    public struct SpeedModifier : IComponentData
    {
        public float Value;
    }

    [BurstCompile(CompileSynchronously = true)]
    partial struct IterateAndUseComponentsSystem : ISystem
    {
        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.Time.DeltaTime;
            foreach (var (transform, speedModifierRef) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<SpeedModifier>>())
            {
                transform.ValueRW.Rotation =
                    math.mul(
                        math.normalize(transform.ValueRO.Rotation),
                        quaternion.AxisAngle(math.up(), time * speedModifierRef.ValueRO.Value));
            }
        }
    }

    [TestFixture]
    public class IdiomaticForEachISystemPerformanceTests : ECSTestsFixture
    {
        EntityArchetype _archetype;

        [SetUp]
        public void SetUp() => _archetype = m_Manager.CreateArchetype(typeof(LocalTransform), typeof(SpeedModifier));

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IterateAndUseComponents([Values(100, 100000)] int entityCount)
        {
            var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);

            var system = World.GetOrCreateSystem<IterateAndUseComponentsSystem>();
            var systemPtr = &system;

            m_Manager.CreateEntity(_archetype, entities);

            Measure.Method(() => systemPtr->Update(World.Unmanaged))
                .WarmupCount(5)
                .MeasurementCount(100)
                .Run();

            entities.Dispose();
        }
    }
}
