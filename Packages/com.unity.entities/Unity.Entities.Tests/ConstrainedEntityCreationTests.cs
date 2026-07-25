using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    struct Component01 : IComponentData
    {
        public int Value;

        public Component01(int value)
        {
            Value = value;
        }
    }

    struct Component02 : IComponentData
    {
        public int Value;
    }

    partial class TestSystemWithEmptyJob : SystemBase
    {
        public JobHandle ScheduledJobHandle;

        struct EmptyJob : IJob
        {
            public void Execute()
            {
                // Do nothing.
            }
        }

        protected override void OnUpdate()
        {
            var job = new EmptyJob();
            ScheduledJobHandle = job.Schedule(Dependency);
            Dependency = ScheduledJobHandle;
        }
    }

    struct EmptyJob : IJobChunk
    {
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // Do nothing.
        }
    }

    // Helper system to run a delegate on each entity that has EcsTestData. This lets us avoid defining a new system for each IFE test case.
    partial struct OnUpdateCallbackSystem : ISystem
    {
        public delegate void OnUpdateDelegate(ref SystemState state, RefRW<EcsTestData> data);
        public static OnUpdateDelegate OnUpdateAction;

        public void OnUpdate(ref SystemState state)
        {
            foreach (var data in SystemAPI.Query<RefRW<EcsTestData>>())
            {
                OnUpdateAction?.Invoke(ref state, data);
            }
        }
    }

    [TestFixture]
    class ConstrainedEntityCreationTests : ECSTestsFixture
    {
        EntityQuery m_Component01Query;
        EntityQuery m_EcsTestDataQuery;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            var emptySystem = World.GetOrCreateSystemManaged<EmptySystem>();
            m_Component01Query = new EntityQueryBuilder(Allocator.Temp).WithAll<Component01>().Build(ref emptySystem.CheckedStateRef);
            m_EcsTestDataQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(ref emptySystem.CheckedStateRef);
        }

        [Test]
        public void CreateEntity_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);
            var notMatchingArchetype = World.EntityManager.CreateArchetype(typeof(Component01));

            // We enumerate with a query for EcsTestData, but create an entity with Component01.
            // Since those two will never match each other, it is safe to create an entity in this loop
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                state.EntityManager.CreateEntity();
                state.EntityManager.CreateEntity(notMatchingArchetype);
                state.EntityManager.CreateEntity(typeof(Component01));
                state.EntityManager.CreateEntity(notMatchingArchetype, TmpNA(2));
                state.EntityManager.CreateEntity(notMatchingArchetype, 2, state.WorldUpdateAllocator);
                state.EntityManager.CreateEntity(notMatchingArchetype, 2);
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 8, Component01EntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CreateEntity_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var entityManager = World.EntityManager;
            var allocator = World.UpdateAllocator.ToAllocator;
            var matchingArchetype = entityManager.CreateArchetype(typeof(EcsTestData));

            // We enumerate with a query for EcsTestData, and then also create an entity with EcsTestData
            // If we were to allow this, it would mean that depending on where you are in the iteration
            // (the new entity gets added to a new chunk vs the chunk we are currently iterating over) you get different behaviour.
            // While this is fully deterministic, it is quite unexpected and not controlled behaviour.
            // So instead we throw an exception when creating an entity whose archetype matches what we are currently enumerating
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState _, RefRW<EcsTestData> _) =>
            {
                Assert.Throws<InvalidOperationException>(() => entityManager.CreateEntity(matchingArchetype));
                Assert.Throws<InvalidOperationException>(() => entityManager.CreateEntity(typeof(EcsTestData)));
                Assert.Throws<InvalidOperationException>(() => entityManager.CreateEntity(matchingArchetype, TmpNA(2)));
                Assert.Throws<InvalidOperationException>(() => entityManager.CreateEntity(matchingArchetype, 2, allocator));
                Assert.Throws<InvalidOperationException>(() => entityManager.CreateEntity(matchingArchetype, 2));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        public void CreateEntity_WithArchetypeNotMatchingQuery_CompletesAllScheduledJobs()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateEntity(notMatchingArchetype));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateEntity());
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateEntity(typeof(Component01)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateEntity(notMatchingArchetype, TmpNA(2)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateEntity(notMatchingArchetype, 2, World.UpdateAllocator.ToAllocator));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateEntity(notMatchingArchetype, 2));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 8, Component01EntitiesCount);
        }

        [Test]
        public void Instantiate_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = World.EntityManager.CreateEntity(typeof(Component01));

            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                state.EntityManager.Instantiate(toInstantiate);
                state.EntityManager.Instantiate(toInstantiate, TmpNA(2));
                state.EntityManager.Instantiate(toInstantiate, 2, state.WorldUpdateAllocator);
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 5 + 1, Component01EntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CreateEntity_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);

            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateEntity(notMatchingArchetype));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateEntity());
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateEntity(typeof(EcsTestData)));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateEntity(notMatchingArchetype, TmpNA(2)));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateEntity(notMatchingArchetype, 2, World.UpdateAllocator.ToAllocator));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateEntity(notMatchingArchetype, 2));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void Instantiate_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = World.EntityManager.GetAllEntities()[0];
            var entityManager = World.EntityManager;
            var allocator = World.UpdateAllocator.ToAllocator;

            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> data) =>
            {
                AssertValueConsistency(data);
                Assert.Throws<InvalidOperationException>(() => entityManager.Instantiate(toInstantiate));
                Assert.Throws<InvalidOperationException>(() => entityManager.Instantiate(toInstantiate, TmpNA(2)));
                Assert.Throws<InvalidOperationException>(() => entityManager.Instantiate(toInstantiate, 2, allocator));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void Instantiate_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.Instantiate(toInstantiate));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.Instantiate(toInstantiate, TmpNA(2)));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.Instantiate(toInstantiate, 2, World.UpdateAllocator.ToAllocator));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void Instantiate_WithArchetypeNotMatchingQuery_CompletesAllScheduledJobs()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.Instantiate(toInstantiate));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.Instantiate(toInstantiate, TmpNA(2)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.Instantiate(toInstantiate, 2, World.UpdateAllocator.ToAllocator));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 5 + 1, Component01EntitiesCount);
        }

        [Test]
        public void CopyEntities_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toCopy = m_Manager.CreateEntity(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                m_Manager.CopyEntitiesInternal(TmpNA(toCopy, toCopy), TmpNA(2));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 2 + 1, Component01EntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CopyEntities_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toCopy = m_Manager.GetAllEntities()[0];
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> data) =>
            {
                AssertValueConsistency(data);
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(toCopy));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CopyEntities_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toCopy = m_Manager.CreateEntity(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);

            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CopyEntitiesInternal(TmpNA(toCopy, toCopy), TmpNA(2)));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        public unsafe void CreateEntityOnlyArchetype_Works()
        {
            var archetype = m_Manager.CreateArchetypeWithoutSimulateComponent(null, 0);
            Assert.AreEqual(1, archetype.TypesCount);
            Assert.AreEqual(TypeManager.GetTypeIndex<Entity>(), archetype.Types[0].TypeIndex);
        }

        [Test]
        public void CreateArchetype_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                m_Manager.CreateArchetype(typeof(Component01));
                m_Manager.CreateArchetype(TmpNA(typeof(Component01)));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CreateArchetype_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateArchetype(typeof(EcsTestData)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateArchetype(TmpNA(typeof(EcsTestData))));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        public void CreateArchetype_WithArchetypeNotMatchingQuery_CompletesAllScheduledJobs()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateArchetype(typeof(Component01)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(() => m_Manager.CreateArchetype(TmpNA(typeof(Component01))));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CreateArchetype_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateArchetype(typeof(Component01)));
            ScheduleJobAndAssertCodeThrows(query, () => m_Manager.CreateArchetype(TmpNA(typeof(Component01))));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void Instantiate_Prefab_WithArchetypeMatchingQuery_Throws()
        {
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab));
            m_Manager.CreateEntity(typeof(EcsTestData));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(prefabEntity));
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(prefabEntity, TmpNA(2)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(prefabEntity, 2, World.UpdateAllocator.ToAllocator));
            };
            systemRef.Update(World.Unmanaged);
        }

        [Test]
        public void CopyEntities_Prefab_WithArchetypeNotMatchingQuery_Works()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            var prefabs = new[]
            {
                m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab)),
                m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab))
            };

            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
                m_Manager.CopyEntitiesInternal(TmpNA(prefabs), TmpNA(2));
            systemRef.Update(World.Unmanaged);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void AddComponent_InForeach_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                var entity = m_Manager.CreateEntity(notMatchingArchetype);
                Assert.Throws<InvalidOperationException>(() => m_Manager.AddComponent(entity, typeof(Component02)));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void RemoveComponent_InForeach_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                var entity = m_Manager.CreateEntity(notMatchingArchetype);
                Assert.Throws<InvalidOperationException>(() => m_Manager.RemoveComponent(entity, typeof(Component01)));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void DestroyEntity_InForeach_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                var entity = m_Manager.CreateEntity(notMatchingArchetype);
                Assert.Throws<InvalidOperationException>(() => m_Manager.DestroyEntity(entity));
            };
            systemRef.Update(World.Unmanaged);

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CommandBuffer_Playback_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var commandBuffer = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState state, RefRW<EcsTestData> _) =>
            {
                var entity = commandBuffer.Instantiate(toInstantiate);
                commandBuffer.SetComponent(entity, new Component01());

                Assert.Throws<InvalidOperationException>(() => commandBuffer.Playback(m_Manager));
            };
            systemRef.Update(World.Unmanaged);
        }

        private const int OriginalEntitiesCount = 256;
        private const int EcsTestDataValue = 0xCAFFE;

        private NativeArray<Entity> TmpNA(int count)
        {
            return CollectionHelper.CreateNativeArray<Entity>(count, World.UpdateAllocator.ToAllocator);
        }

        private NativeArray<Entity> TmpNA(params Entity[] entities)
        {
            return CollectionHelper.CreateNativeArray(entities, World.UpdateAllocator.ToAllocator);
        }

        private NativeArray<ComponentType> TmpNA(params ComponentType[] componentTypes)
        {
            return CollectionHelper.CreateNativeArray<ComponentType>(componentTypes, World.UpdateAllocator.ToAllocator);
        }

        private static void AssertValueConsistency(RefRW<EcsTestData> data)
        {
            Assert.AreEqual(
                EcsTestDataValue, data.ValueRO.value,
                "EcsTestData value is not consistent with the expected one. Entities may have been shuffled or unexpected entities where added to the manager.");
        }

        private void SetupEntitiesForConsistencyCheck(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var entity = m_Manager.CreateEntity(typeof(EcsTestData));
                m_Manager.SetComponentData(entity, new EcsTestData(EcsTestDataValue));
            }
        }

        private int Component01EntitiesCount => m_Component01Query.CalculateEntityCount();

        private int EcsTestDataEntitiesCount => m_EcsTestDataQuery.CalculateEntityCount();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Uses the job debugger to check that the job has been explicitly completed.
        private static bool IsJobExplicitlyCompleted(JobHandle handle)
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled);
            return JobHandle.CheckFenceIsDependencyOrDidSyncFence(handle, default);
        }
#endif

        private void CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(Action code)
        {
            var system = World.CreateSystemManaged<TestSystemWithEmptyJob>();
            system.SystemHandle.Update(World.Unmanaged);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(IsJobExplicitlyCompleted(system.ScheduledJobHandle));
#endif

            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();
            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState _, RefRW<EcsTestData> _) =>
            {
                code();
            };
            systemRef.Update(World.Unmanaged);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsTrue(IsJobExplicitlyCompleted(system.ScheduledJobHandle));
#endif
            World.DestroySystemManaged(system);
        }

        private void ScheduleJobAndAssertCodeThrows(EntityQuery query, Action code)
        {
            var systemRef = World.GetOrCreateSystem<OnUpdateCallbackSystem>();

            var job = new EmptyJob();
            var handle = job.Schedule(query, default);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(IsJobExplicitlyCompleted(handle));
#endif

            OnUpdateCallbackSystem.OnUpdateAction = (ref SystemState _, RefRW<EcsTestData> _) =>
            {
                Assert.Throws<InvalidOperationException>(() => code());
            };
            systemRef.Update(World.Unmanaged);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(IsJobExplicitlyCompleted(handle));
#endif

            handle.Complete();
        }
    }
}
