using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    [BurstCompile(CompileSynchronously = true)]
    public sealed partial class EntityCommandBufferPerformanceTests : EntityPerformanceTestFixture
    {
        EntityArchetype archetype1;
        EntityArchetype archetype2;
        EntityArchetype archetype3;
        EntityArchetype archetype4;
        NativeArray<Entity> entities1;
        NativeArray<Entity> entities2;
        NativeArray<Entity> entities3;
        EntityQuery query;

        const int count = 1024 * 128;

        public override void Setup()
        {
            base.Setup();

            archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestManagedComponent));
#endif
            archetype4 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTag));
            entities1 = new NativeArray<Entity>(count, Allocator.Persistent);
            entities2 = new NativeArray<Entity>(count, Allocator.Persistent);
            entities3 = new NativeArray<Entity>(count, Allocator.Persistent);
            query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
        }

        [TearDown]
        public override void TearDown()
        {
            if (m_World.IsCreated)
            {
                entities1.Dispose();
                entities2.Dispose();
                entities3.Dispose();
                query.Dispose();
            }
            base.TearDown();
        }

        struct EcsTestDataWithEntity : IComponentData
        {
            public int value;
            public Entity entity;
        }

        NativeArray<Entity> FillWithEcsTestDataWithEntity(EntityCommandBuffer cmds, int repeat)
        {
            var entities = cmds.CreateEntity(repeat, Allocator.Persistent);
            cmds.AddComponent(entities, new EcsTestDataWithEntity { value = repeat });
            return entities;
        }

        NativeArray<Entity> FillWithEcsTestData(EntityCommandBuffer cmds, int repeat)
        {
            var entities = cmds.CreateEntity(repeat, Allocator.Persistent);
            cmds.AddComponent(entities, new EcsTestData { value = repeat });
            return entities;
        }

        void FillWithCreateEntityCommands(EntityCommandBuffer cmds, int repeat)
        {
            for (int i = repeat; i != 0; --i)
            {
                cmds.CreateEntity();
            }
        }

        NativeArray<Entity> FillWithBatchedCreateEntityCommands(EntityCommandBuffer cmds, int repeat)
        {
            return cmds.CreateEntity(repeat, Allocator.Persistent);

        }

        void FillWithInstantiateEntityCommands(EntityCommandBuffer cmds, int repeat, Entity prefab)
        {
            for (int i = repeat; i != 0; --i)
            {
                cmds.Instantiate(prefab);
            }
        }

        void FillWithBatchedInstantiateEntityCommands(EntityCommandBuffer cmds, ref NativeArray<Entity> entities, Entity prefab)
        {
            cmds.Instantiate(prefab, entities);

        }

        void FillWithAddComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities, ComponentType componentType)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddComponent(entities[i], componentType);
            }
        }

        void FillWithRemoveComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.RemoveComponent(entities[i], typeof(EcsTestData));
            }
        }

        void FillWithSetComponentCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetComponent(entities[i], new EcsTestData {value = i});
            }
        }

        void FillWithDestroyEntityCommands(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.DestroyEntity(entities[i]);
            }
        }

        void FillWithEcsTestSharedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.AddSharedComponent(entities[i], new EcsTestSharedComp {value = 1});
            }
        }

        void FillWithSetEcsTestSharedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                cmds.SetSharedComponent(entities[i], new EcsTestSharedComp {value = 2});
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        void FillWithEcsTestManagedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                cmds.AddComponent(entities[i], new EcsTestManagedComponent {value = "string1"});
                #pragma warning restore 0618
            }
        }

        void FillWithSetEcsTestManagedComp(EntityCommandBuffer cmds, NativeArray<Entity> entities)
        {
            for (int i = entities.Length - 1; i != 0; i--)
            {
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                cmds.SetComponent(entities[i], new EcsTestManagedComponent {value = "string2"});
                #pragma warning restore 0618
            }
        }

#endif

        [BurstCompile(CompileSynchronously = true)]
        partial struct CreateEmptyEcbSystem : ISystem
        {
            private bool _createParallelWriters;
            private int _ecbCount;

            [BurstCompile(CompileSynchronously = true)]
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<EcsTestData2>();
                state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
                EcsTestData2 ecbParams = SystemAPI.GetSingleton<EcsTestData2>();
                _createParallelWriters = ecbParams.value0 != 0;
                _ecbCount = ecbParams.value1;
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                if (_createParallelWriters)
                    for (int i = 0; i < _ecbCount; ++i)
                    {
                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
                    }
                else
                    for (int i = 0; i < _ecbCount; ++i)
                    {
                        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                    }
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_Create([Values] bool createParallelWriters)
        {
            const int kEcbCount = 100;
            // Use a singleton to communicate whether the system should create the ECBs
            // as ParallelWriters or not, and how many.
            var e = m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.SetComponentData(e,
                new EcsTestData2 { value0 = createParallelWriters ? 1 : 0, value1 = kEcbCount });

            var createEcbSystem = World.CreateSystem<CreateEmptyEcbSystem>();
            var ecbSystem = World.CreateSystem<EndSimulationEntityCommandBufferSystem>();
            Measure.Method(() => { createEcbSystem.Update(World.Unmanaged); })
                .CleanUp(() => {
                    ecbSystem.Update(World.Unmanaged);
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup($"Create_{kEcbCount}x", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_Dispose([Values] bool createParallelWriters)
        {
            const int kEcbCount = 100;
            // Use a singleton to communicate whether the system should create the ECBs
            // as ParallelWriters or not, and how many.
            var e = m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.SetComponentData(e,
                new EcsTestData2 { value0 = createParallelWriters ? 1 : 0, value1 = kEcbCount });

            var createEcbSystem = World.CreateSystem<CreateEmptyEcbSystem>();
            var ecbSystem = World.CreateSystem<EndSimulationEntityCommandBufferSystem>();
            Measure.Method(() => { ecbSystem.Update(World.Unmanaged); })
                .SetUp(() => { createEcbSystem.Update(World.Unmanaged); })
                .CleanUp(() => { World.UpdateAllocator.Rewind(); })
                .SampleGroup(new SampleGroup($"PlaybackAndDispose_{kEcbCount}x", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_512SimpleEntities()
        {
            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;

            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            var entityArrays = new List<NativeArray<Entity>>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        entityArrays.Add(FillWithEcsTestData(cmds, kCreateLoopCount));
                        ecbs.Add(cmds);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();

            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        ecbs[repeat].Playback(m_Manager);
                    }
                })
                .SampleGroup("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .CleanUp(() =>
                {
                })
                .Run();

            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
            foreach (var arr in entityArrays)
            {
                arr.Dispose();
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_512EntitiesWithEmbeddedEntity()
        {
            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;

            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            var entityArrays = new List<NativeArray<Entity>>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        entityArrays.Add(FillWithEcsTestDataWithEntity(cmds, kCreateLoopCount));
                        ecbs.Add(cmds);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        ecbs[repeat].Playback(m_Manager);
                    }
                })
                .SampleGroup("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
            foreach (var arr in entityArrays)
            {
                arr.Dispose();
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_OneEntityWithEmbeddedEntityAnd512SimpleEntities()
        {
            // This test should not be any slower than EntityCommandBuffer_SimpleEntities_512x1000
            // It shows that adding one component that needs fix up will not make the fast
            // path any slower

            const int kCreateLoopCount = 512;
            const int kPlaybackLoopCount = 1000;


            var ecbs = new List<EntityCommandBuffer>(kPlaybackLoopCount);
            var entityArrays = new List<NativeArray<Entity>>(kPlaybackLoopCount);
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                    {
                        var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        Entity e0 = cmds.CreateEntity();
                        cmds.AddComponent(e0, new EcsTestDataWithEntity {value = -1, entity = e0 });
                        entityArrays.Add(FillWithEcsTestData(cmds, kCreateLoopCount));
                        ecbs.Add(cmds);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            Measure.Method(
                () =>
                {
                    for (int repeat = 0; repeat < kPlaybackLoopCount; ++repeat)
                        ecbs[repeat].Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(0)
                .MeasurementCount(1)
                .Run();
            foreach (var ecb in ecbs)
            {
                ecb.Dispose();
            }
            foreach (var arr in entityArrays)
            {
                arr.Dispose();
            }
        }

        // ----------------------------------------------------------------------------------------------------------
        // BLITTABLE
        // ----------------------------------------------------------------------------------------------------------
        [Test, Performance]
        public void EntityCommandBuffer_DestroyEntity([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithDestroyEntityCommands(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithDestroyEntityCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_CreateEntities([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    FillWithCreateEntityCommands(ecb, size);
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    FillWithCreateEntityCommands(ecb, size);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_CreateEntitiesBatched([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            NativeArray<Entity> createdEntities = default;
            Measure.Method(
                    () =>
                    {
                        createdEntities = FillWithBatchedCreateEntityCommands(ecb, size);
                    })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                    createdEntities.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    createdEntities = FillWithBatchedCreateEntityCommands(ecb, size);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                    createdEntities.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_InstantiateEntities([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            var prefabEntity = m_Manager.CreateEntity(archetype1);
            Measure.Method(
                () =>
                {
                    FillWithInstantiateEntityCommands(ecb, size, prefabEntity);
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    prefabEntity = m_Manager.CreateEntity(archetype1);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    prefabEntity = m_Manager.CreateEntity(archetype1);
                    FillWithInstantiateEntityCommands(ecb, size, prefabEntity);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_InstantiateEntitiesBatched([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            var prefabEntity = m_Manager.CreateEntity(archetype1);
            NativeArray<Entity> instantiatedEntities = default;
            Measure.Method(
                    () =>
                    {
                        FillWithBatchedInstantiateEntityCommands(ecb, ref instantiatedEntities, prefabEntity);
                    })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    prefabEntity = m_Manager.CreateEntity(archetype1);
                    instantiatedEntities = CollectionHelper.CreateNativeArray<Entity>(size,
                        World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                    instantiatedEntities.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    prefabEntity = m_Manager.CreateEntity(archetype1);
                    instantiatedEntities = CollectionHelper.CreateNativeArray<Entity>(size,
                        World.UpdateAllocator.ToAllocator);
                    FillWithBatchedInstantiateEntityCommands(ecb, ref instantiatedEntities, prefabEntity);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                    instantiatedEntities.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_AddComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithAddComponentCommands(ecb, entities, typeof(EcsTestData2));
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithAddComponentCommands(ecb, entities, typeof(EcsTestData2));
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_SetComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetComponentCommands(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetComponentCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_RemoveComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithRemoveComponentCommands(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithRemoveComponentCommands(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        // ----------------------------------------------------------------------------------------------------------
        // MANAGED
        // ----------------------------------------------------------------------------------------------------------
        [Test, Performance]
        public void EntityCommandBuffer_AddSharedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestSharedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestSharedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test, Performance]
        public void EntityCommandBuffer_AddManagedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestManagedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype1);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithEcsTestManagedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#endif

        [Test, Performance]
        public void EntityCommandBuffer_SetSharedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestSharedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype2);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype2);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestSharedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test, Performance]
        public void EntityCommandBuffer_SetManagedComponent([Values(10, 1000, 10000)] int size)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestManagedComp(ecb, entities);
                    }
                })
                .SampleGroup("Record")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype3);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Playback")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    for (int i = 0; i < size; i++)
                        m_Manager.CreateEntity(archetype3);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    using (var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        FillWithSetEcsTestManagedComp(ecb, entities);
                    }
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

#endif

        [Test, Performance]
        public void EntityCommandBuffer_AddComponentToEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode captureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.AddComponent(query, typeof(EcsTestTag), captureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.AddComponent(query, typeof(EcsTestTag), captureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_RemoveComponentFromEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode captureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.RemoveComponent(query, typeof(EcsTestData), captureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.RemoveComponent(query, typeof(EcsTestData), captureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_DestroyEntitiesInEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.DestroyEntity(query, queryCaptureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.DestroyEntity(query, EntityQueryCaptureMode.AtPlayback);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_AddSharedComponentToEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.AddSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.AddSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_SetSharedComponentToEntityQuery([Values(10, 1000, 10000)] int size, [Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var ecb = default(EntityCommandBuffer);
            Measure.Method(
                () =>
                {
                    ecb.SetSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .SampleGroup(new SampleGroup("Record", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype2, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                () =>
                {
                    ecb.Playback(m_Manager);
                })
                .SampleGroup(new SampleGroup("Playback", SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype2, size);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    ecb.SetSharedComponent(query, new EcsTestSharedComp {value = 1}, queryCaptureMode);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        m_Manager.DestroyEntity(entities);
                    }
                    ecb.Dispose();
                })
                .Run();
        }

        [Test, Performance]
        public void EntityCommandBuffer_AddComponent_SingleVsMultiple([Values(10, 100, 1000, 10000)] int size)
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var ecb = default(EntityCommandBuffer);
            NativeArray<Entity> entities = default;
            Measure.Method(
                () =>
                {
                    for (int i = 0; i < size; ++i)
                        ecb.AddComponent<EcsTestData2>(entities[i]);
                    ecb.Playback(m_Manager);
                })
                .SampleGroup("Individual_Packed")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    entities = m_Manager.CreateEntity(archetype1, size, World.UpdateAllocator.ToAllocator);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < size; ++i)
                            ecb.AddComponent<EcsTestData2>(entities[i]);
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Individual_Sparse")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    var allEntities = m_Manager.CreateEntity(archetype1, 2*size, World.UpdateAllocator.ToAllocator);
                    entities = CollectionHelper.CreateNativeArray<Entity>(size, World.UpdateAllocator.ToAllocator,
                        NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < size; ++i)
                        entities[i] = allEntities[2 * i];
                    allEntities.Dispose();
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(query);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.AddComponent<EcsTestData2>(entities);
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Batched_Packed")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    entities = m_Manager.CreateEntity(archetype1, size, World.UpdateAllocator.ToAllocator);
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(entities);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();

            Measure.Method(
                    () =>
                    {
                        ecb.AddComponent<EcsTestData2>(entities);
                        ecb.Playback(m_Manager);
                    })
                .SampleGroup("Batched_Sparse")
                .WarmupCount(1)
                .MeasurementCount(100)
                .SetUp(() =>
                {
                    var allEntities = m_Manager.CreateEntity(archetype1, 2*size, World.UpdateAllocator.ToAllocator);
                    entities = CollectionHelper.CreateNativeArray<Entity>(size, World.UpdateAllocator.ToAllocator,
                        NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < size; ++i)
                        entities[i] = allEntities[2 * i];
                    allEntities.Dispose();
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    m_Manager.DestroyEntity(query);
                    entities.Dispose();
                    ecb.Dispose();
                })
                .Run();
        }

#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
        [Test, Performance]
        public void EntityCommandBuffer_MultiPlayback_RemapEntity(
            [Values(10, 100, 1000)] int entityCount)
        {
            EntityCommandBuffer ecb = default;
            NativeArray<Entity> entities = new NativeArray<Entity>();

            Measure.Method(() =>
            {
                ecb.Playback(m_Manager);
            })
            .SetUp(() =>
            {
                ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator,
                    PlaybackPolicy.MultiPlayback);


                var componentTypeSet = new ComponentTypeSet(typeof(EcsTestData));
                for (int i = 0; i < entityCount; i++)
                {
                    var e = ecb.CreateEntity();
                    ecb.AddComponent(e, componentTypeSet);
                }
                ecb.Playback(m_Manager);
            })
            .CleanUp(() =>
            {
                entities.Dispose();
                ecb.Dispose();
            })
            .SampleGroup(new SampleGroup(
                $"MultiPlayback_RemapEntity_N{entityCount} With 1 Component",
                SampleUnit.Microsecond))
            .WarmupCount(1)
            .MeasurementCount(100)
            .Run();

            Measure.Method(() =>
                {
                    ecb.Playback(m_Manager);
                })
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator,
                        PlaybackPolicy.MultiPlayback);


                    var componentTypeSet = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2),typeof(EcsTestData3),typeof(EcsTestData4));
                    for (int i = 0; i < entityCount; i++)
                    {
                        var e = ecb.CreateEntity();
                        ecb.AddComponent(e, componentTypeSet);
                    }
                    ecb.Playback(m_Manager);
                })
                .CleanUp(() =>
                {
                    entities.Dispose();
                    ecb.Dispose();
                })
                .SampleGroup(new SampleGroup(
                    $"MultiPlayback_RemapEntity_N{entityCount} With 4 Components",
                    SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();

            Measure.Method(() =>
                {
                    ecb.Playback(m_Manager);
                })
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator,
                        PlaybackPolicy.MultiPlayback);

                    var componentTypeSet = new ComponentTypeSet(typeof(EcsTestData));
                    entities = ecb.CreateEntity(entityCount, Allocator.Persistent);
                    ecb.AddComponent(entities, componentTypeSet);

                    ecb.Playback(m_Manager);
                })
                .CleanUp(() =>
                {
                    entities.Dispose();
                    ecb.Dispose();
                })
                .SampleGroup(new SampleGroup(
                    $"MultiPlayback_RemapEntity_N{entityCount} Batched With 1 Component",
                    SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();

            Measure.Method(() =>
                {
                    ecb.Playback(m_Manager);
                })
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator,
                        PlaybackPolicy.MultiPlayback);


                    var componentTypeSet = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2),typeof(EcsTestData3),typeof(EcsTestData4));
                    entities = ecb.CreateEntity(entityCount, Allocator.Persistent);
                    ecb.AddComponent(entities, componentTypeSet);

                    ecb.Playback(m_Manager);
                })
                .CleanUp(() =>
                {
                    entities.Dispose();
                    ecb.Dispose();
                })
                .SampleGroup(new SampleGroup(
                    $"MultiPlayback_RemapEntity_N{entityCount} Batched With 4 Components",
                    SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();

            Entity prefab = default;
            Measure.Method(() =>
                {
                    ecb.Playback(m_Manager);
                })
                .SetUp(() =>
                {
                    prefab = m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab));
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator,
                        PlaybackPolicy.MultiPlayback);

                    for (int i = 0; i < entityCount; i++)
                        ecb.Instantiate(prefab);

                    ecb.Playback(m_Manager);
                })
                .CleanUp(() =>
                {
                    ecb.Dispose();
                    m_Manager.DestroyEntity(prefab);
                })
                .SampleGroup(new SampleGroup(
                    $"MultiPlayback_RemapEntity_N{entityCount} Instantiate",
                    SampleUnit.Microsecond))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();

        }
#pragma warning restore 618

        [Test, Performance]
        public void EntityCommandBuffer_CreateEntities_From_Job(
            [Values(100, 1000, 10000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTag));
            EntityCommandBuffer ecb = default;
            NativeArray<Entity> entities = default;

            Measure.Method(() =>
                {
                    var job = new CreateEntitiesJob { ecb = ecb, archetype = archetype, Result = entities };
                    job.Run();
                })
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                    entities = CollectionHelper.CreateNativeArray<Entity>(entityCount,
                        World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    ecb.Playback(m_Manager);
                    for (int i = 0; i < entityCount; i++)
                    {
                        Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entities[i]));
                        Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entities[i]));
                    }

                    ecb.Dispose();
                    entities.Dispose();

                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup($"BatchedCreate_N{entityCount}", SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(50)
                .Run();

                Measure.Method(() =>
                    {
                        var job = new CreateEntitiesSlowJob
                        {
                            ecb = ecb, archetype = archetype, entityCount = entityCount, Result = entities
                        };
                        job.Run();
                    })
                    .SetUp(() =>
                    {
                        ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                        entities = CollectionHelper.CreateNativeArray<Entity>(entityCount,
                            World.UpdateAllocator.ToAllocator);
                    })
                    .CleanUp(() =>
                    {
                        ecb.Playback(m_Manager);
                        for (int i = 0; i < entityCount; i++)
                        {
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entities[i]));
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entities[i]));
                        }

                        ecb.Dispose();
                        entities.Dispose();
                        World.UpdateAllocator.Rewind();
                    })
                    .SampleGroup(new SampleGroup($"NonBatchedCreate_N{entityCount}", SampleUnit.Microsecond))
                    .WarmupCount(5)
                    .MeasurementCount(50)
                    .Run();
        }

        [BurstCompile]
        public struct CreateEntitiesJob : IJob
        {
            public EntityCommandBuffer ecb;
            public EntityArchetype archetype;
            public NativeArray<Entity> Result;

            public void Execute()
            {
                ecb.CreateEntity(archetype, Result);
                ecb.AddComponent<EcsTestData2>(Result);
                ecb.RemoveComponent<EcsTestData>(Result);
            }
        }

        [BurstCompile]
        public struct CreateEntitiesSlowJob : IJob
        {
            public EntityCommandBuffer ecb;
            public EntityArchetype archetype;
            public int entityCount;
            public NativeArray<Entity> Result;

            public void Execute()
            {
                for (int i = 0; i < entityCount; i++)
                {
                    Result[i] = ecb.CreateEntity(archetype);
                }
                ecb.AddComponent<EcsTestData2>(Result);
                ecb.RemoveComponent<EcsTestData>(Result);

            }
        }

        [BurstCompile]
        public struct CreateEntitiesParallelJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public EntityArchetype archetype;
            public int perWorker;

            public void Execute(int index)
            {
                int baseSortKey = index * perWorker;
                for (int i = 0; i < perWorker; i++)
                {
                    ecb.CreateEntity(baseSortKey + i, archetype);
                }
            }
        }

        [BurstCompile]
        public struct CreateEntitiesBatchedParallelJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public EntityArchetype archetype;
            public int perWorker;

            public void Execute(int index)
            {
                var local = new NativeArray<Entity>(perWorker, Allocator.Temp);
                ecb.CreateEntity(index, archetype, local);
                local.Dispose();
            }
        }

        [Test, Performance]
        public void EntityCommandBuffer_ParallelWriter_Record_FromParallelJob(
            [Values(100, 1000)] int perWorker,
            [Values(false, true)] bool batched)
        {
            const int workerCount = 64;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            EntityCommandBuffer ecb = default;

            Measure.Method(() =>
                {
                    if (batched)
                    {
                        var job = new CreateEntitiesBatchedParallelJob
                            { ecb = ecb.AsParallelWriter(), archetype = archetype, perWorker = perWorker };
                        job.Schedule(workerCount, 1).Complete();
                    }
                    else
                    {
                        var job = new CreateEntitiesParallelJob
                            { ecb = ecb.AsParallelWriter(), archetype = archetype, perWorker = perWorker };
                        job.Schedule(workerCount, 1).Complete();
                    }
                })
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                })
                .CleanUp(() =>
                {
                    ecb.Playback(m_Manager);
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                        m_Manager.DestroyEntity(entities);
                    ecb.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup(
                    $"Record_Workers{workerCount}_PerWorker{perWorker}_{(batched ? "Batched" : "PerEntity")}",
                    SampleUnit.Microsecond))
                .WarmupCount(3)
                .MeasurementCount(30)
                .Run();
        }

#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
        [Test, Performance]
        public void EntityCommandBuffer_ParallelWriter_Playback(
            [Values(100, 1000)] int perWorker,
            [Values(PlaybackPolicy.SinglePlayback, PlaybackPolicy.MultiPlayback)] PlaybackPolicy policy)
        {
            const int workerCount = 64;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            EntityCommandBuffer ecb = default;
            bool measureSecondPlayback = policy == PlaybackPolicy.MultiPlayback;

            Measure.Method(() =>
                {
                    ecb.Playback(m_Manager);
                })
                .SetUp(() =>
                {
                    ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, policy);
                    var job = new CreateEntitiesParallelJob
                        { ecb = ecb.AsParallelWriter(), archetype = archetype, perWorker = perWorker };
                    job.Schedule(workerCount, 1).Complete();

                    if (measureSecondPlayback)
                        ecb.Playback(m_Manager);
                })
                .CleanUp(() =>
                {
                    using (var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                        m_Manager.DestroyEntity(entities);
                    ecb.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup(
                    $"Playback_Workers{workerCount}_PerWorker{perWorker}_{(measureSecondPlayback ? "MultiSecond" : "First")}",
                    SampleUnit.Microsecond))
                .WarmupCount(3)
                .MeasurementCount(30)
                .Run();
        }
#pragma warning restore 618
    }
}
