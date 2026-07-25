using System;
using Unity.Entities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Entities.Tests
{
    struct ChunkBoundsComponent : IComponentData
    {
        public float3 boundsMin;
        public float3 boundsMax;
    }
    struct BoundsComponent : IComponentData
    {
        public float3 boundsMin;
        public float3 boundsMax;
    }

    struct CleanupChunkComponent : ICleanupComponentData
    {
    }

    public partial class ChunkComponentTests : ECSTestsFixture
    {
        [Test]
        public void CreateChunkComponentArchetype()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            Assert.IsTrue(m_Manager.HasComponent(entity, ComponentType.ChunkComponent<EcsTestData>()));
            Assert.IsFalse(m_Manager.HasComponent(entity, ComponentType.ReadWrite<EcsTestData>()));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public unsafe void ArchetypeChunk_GetAndSetChunkComponent_ThrowWhenMetaChunkEntityMissing()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var chunkComponentType = m_Manager.GetComponentTypeHandle<EcsTestData2>(false);
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.m_Chunk.MetaChunkEntity, Entity.Null);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.Throws<ArgumentException>(() => chunk.SetChunkComponentData(ref chunkComponentType, new EcsTestData2(12)));
            Assert.Throws<ArgumentException>(() => chunk.GetChunkComponentData(ref chunkComponentType));
            #pragma warning restore 0618
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public unsafe void ArchetypeChunk_GetAndSetChunkComponent_ThrowWhenComponentMissing()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            var chunkComponentType = m_Manager.GetComponentTypeHandle<EcsTestData2>(false);
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreNotEqual(chunk.m_Chunk.MetaChunkEntity, Entity.Null);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.Throws<ArgumentException>(() => chunk.GetChunkComponentData(ref chunkComponentType));
            Assert.Throws<ArgumentException>(() => chunk.SetChunkComponentData(ref chunkComponentType, new EcsTestData2(12)));
            #pragma warning restore 0618
        }

        [Test]
        public void SetChunkComponent()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new EcsTestData {value = 7});
            #pragma warning restore 0618
            Assert.IsTrue(m_Manager.HasComponent(entity, ComponentType.ChunkComponent<EcsTestData>()));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var val0 = m_Manager.GetChunkComponentData<EcsTestData>(entity).value;
            #pragma warning restore 0618
            Assert.AreEqual(7, val0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var val1 = m_Manager.GetChunkComponentData<EcsTestData>(m_Manager.GetChunk(entity)).value;
            #pragma warning restore 0618
            Assert.AreEqual(7, val1);
        }

        [Test]
        public unsafe void GetChunkComponentDataRW_ModifiesValue()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new EcsTestData { value = 1 });
            #pragma warning restore 0618

            var handle = m_Manager.GetComponentTypeHandle<EcsTestData>(isReadOnly: false);
            var chunk = m_Manager.GetChunk(entity);

            ref var value = ref chunk.GetChunkComponentDataRW(ref handle);
            value.value = 99;

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(99, m_Manager.GetChunkComponentData<EcsTestData>(entity).value);
            #pragma warning restore 0618
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires read-only handle validation")]
        public unsafe void GetChunkComponentDataRW_ReadOnlyHandle_Throws()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            var readOnlyHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(isReadOnly: true);
            var chunk = m_Manager.GetChunk(entity);

            Assert.Throws<InvalidOperationException>(() =>
            {
                chunk.GetChunkComponentDataRW(ref readOnlyHandle);
            });
        }

        [Test]
        public void AddChunkComponentMovesEntity()
        {
            var entity0 = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            var entity1 = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            var chunk0 = m_Manager.GetChunk(entity0);
            var chunk1 = m_Manager.GetChunk(entity1);

            Assert.AreEqual(chunk0, chunk1);

            Assert.IsFalse(m_Manager.HasChunkComponent<EcsTestData2>(entity0));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddChunkComponentData<EcsTestData2>(entity0);
            #pragma warning restore 0618
            Assert.IsTrue(m_Manager.HasChunkComponent<EcsTestData2>(entity0));
            chunk0 = m_Manager.GetChunk(entity0);

            Assert.AreNotEqual(chunk0, chunk1);

            Assert.IsTrue(m_Manager.HasComponent(entity0, ComponentType.ChunkComponent<EcsTestData2>()));
            Assert.IsFalse(m_Manager.HasComponent(entity1, ComponentType.ChunkComponent<EcsTestData2>()));
            Assert.IsTrue(m_Manager.Exists(m_Manager.Debug.GetMetaChunkEntity(entity0)));
            Assert.IsTrue(m_Manager.Debug.GetMetaChunkEntity(entity1) == default(Entity));
        }

        // Adding both of these components to the same entity should fail; the resulting entity wouldn't fit in a 16KB chunk.
        unsafe struct LargeComponent1 : IComponentData
        {
            public fixed byte Value[10*1024];
        }
        unsafe struct LargeComponent2 : IComponentData
        {
            public fixed byte Value[10*1024];
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test explicitly validates debug checks")]
        public void AddChunkComponent_LargeComponent_Works([Values] bool existingMetaChunk)
        {
            var entity = m_Manager.CreateSingleton<LargeComponent1>();
            if (existingMetaChunk)
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                m_Manager.AddChunkComponentData<EcsTestData2>(entity); // this creates a meta-chunk for the entity
                #pragma warning restore 0618
            // Adding a large regular component should fail
            Assert.That(() => m_Manager.AddComponent<LargeComponent2>(entity),
                Throws.InvalidOperationException.With.Message.Contains("data is too large."));
            // Adding a large chunk component should work
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.DoesNotThrow(() => m_Manager.AddChunkComponentData<LargeComponent2>(entity));
            #pragma warning restore 0618
            // Adding a second large chunk component should now fail
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.That(() => m_Manager.AddChunkComponentData<LargeComponent1>(entity),
            #pragma warning restore 0618
                Throws.InvalidOperationException.With.Message.Contains("data is too large."));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test explicitly validates debug checks")]
        public void AddChunkComponents_LargeComponent_Works([Values] bool existingMetaChunk)
        {
            var entity = m_Manager.CreateEntity();
            if (existingMetaChunk)
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                m_Manager.AddChunkComponentData<EcsTestData2>(entity); // this creates a meta-chunk for the entity
                #pragma warning restore 0618
            // Adding two large regular components at once should fail
            var typeSet1 = new ComponentTypeSet(ComponentType.ReadWrite<LargeComponent1>(),
                ComponentType.ReadWrite<LargeComponent2>());
            Assert.That(() => m_Manager.AddComponent(entity, typeSet1),
                Throws.InvalidOperationException.With.Message.Contains("data is too large."));
            // Adding a large chunk component should work
            var typeSet2 = new ComponentTypeSet(ComponentType.ReadWrite<LargeComponent1>(),
                ComponentType.ChunkComponent<LargeComponent2>());
            Assert.DoesNotThrow(() => m_Manager.AddComponent(entity, typeSet2));
            // Adding a second large chunk component should now fail
            var typeSet3 = new ComponentTypeSet(ComponentType.ChunkComponent<LargeComponent1>());
            Assert.That(() => m_Manager.AddComponent(entity, typeSet3),
                Throws.InvalidOperationException.With.Message.Contains("data is too large."));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test explicitly validates debug checks")]
        public void AddChunkComponentsToQuery_LargeComponent_Works([Values] bool existingMetaChunk)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            if (existingMetaChunk)
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                m_Manager.AddChunkComponentData<EcsTestData2>(entity); // this creates a meta-chunk for the entity
                #pragma warning restore 0618
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            // Adding two large regular components at once should fail
            var typeSet1 = new ComponentTypeSet(ComponentType.ReadWrite<LargeComponent1>(),
                ComponentType.ReadWrite<LargeComponent2>());
            Assert.That(() => m_Manager.AddComponent(query, typeSet1),
                Throws.InvalidOperationException.With.Message.Contains("data is too large."));
            // Adding a large chunk component should work
            var typeSet2 = new ComponentTypeSet(ComponentType.ReadWrite<LargeComponent1>(),
                ComponentType.ChunkComponent<LargeComponent2>());
            Assert.DoesNotThrow(() => m_Manager.AddComponent(query, typeSet2));
            // Adding a second large chunk component should now fail
            var typeSet3 = new ComponentTypeSet(ComponentType.ChunkComponent<LargeComponent1>());
            Assert.That(() => m_Manager.AddComponent(query, typeSet3),
                Throws.InvalidOperationException.With.Message.Contains("data is too large."));
        }

        [Test]
        public void RemoveChunkComponent()
        {
            var arch0 = m_Manager.CreateArchetype(ComponentType.ChunkComponent<EcsTestData>(), typeof(EcsTestData2));
            var arch1 = m_Manager.CreateArchetype(typeof(EcsTestData2));

            var entity0 = m_Manager.CreateEntity(arch0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity0), new EcsTestData { value = 7 });
            #pragma warning restore 0618
            m_Manager.SetComponentData(entity0, new EcsTestData2 { value0 = 1, value1 = 2 });
            var metaEntity0 = m_Manager.Debug.GetMetaChunkEntity(entity0);

            var entity1 = m_Manager.CreateEntity(arch1);
            m_Manager.SetComponentData(entity1, new EcsTestData2 { value0 = 2, value1 = 3 });

            m_Manager.RemoveChunkComponent<EcsTestData>(entity0);

            Assert.IsFalse(m_Manager.HasComponent(entity0, ComponentType.ChunkComponent<EcsTestData>()));
            Assert.IsFalse(m_Manager.Exists(metaEntity0));
        }

        [Test]
        public void UpdateChunkComponent()
        {
            var arch0 = m_Manager.CreateArchetype(ComponentType.ChunkComponent<EcsTestData>(), typeof(EcsTestData2));
            EntityQuery group0 = m_Manager.CreateEntityQuery(typeof(ChunkHeader), typeof(EcsTestData));

            var entity0 = m_Manager.CreateEntity(arch0);
            var chunk0 = m_Manager.GetChunk(entity0);
            EcsTestData testData = new EcsTestData { value = 7 };

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(chunk0, testData);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(7, m_Manager.GetChunkComponentData<EcsTestData>(entity0).value);
            #pragma warning restore 0618

            m_Manager.SetComponentData(entity0, new EcsTestData2 { value0 = 1, value1 = 2 });

            var entity1 = m_Manager.CreateEntity(arch0);
            var chunk1 = m_Manager.GetChunk(entity1);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(7, m_Manager.GetChunkComponentData<EcsTestData>(entity0).value);
            Assert.AreEqual(7, m_Manager.GetChunkComponentData<EcsTestData>(entity1).value);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(chunk1, testData);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());

            m_Manager.SetComponentData(entity1, new EcsTestData2 { value0 = 2, value1 = 3 });

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData<EcsTestData>(chunk0, new EcsTestData { value = 10 });
            #pragma warning restore 0618

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(10, m_Manager.GetChunkComponentData<EcsTestData>(entity0).value);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());
        }

        [Test]
        public void ProcessMetaChunkComponent()
        {
            var entity0 = m_Manager.CreateEntity(typeof(BoundsComponent), ComponentType.ChunkComponent<ChunkBoundsComponent>());
            m_Manager.SetComponentData(entity0, new BoundsComponent {boundsMin = new float3(-10, -10, -10), boundsMax = new float3(0, 0, 0)});
            var entity1 = m_Manager.CreateEntity(typeof(BoundsComponent), ComponentType.ChunkComponent<ChunkBoundsComponent>());
            m_Manager.SetComponentData(entity1, new BoundsComponent {boundsMin = new float3(0, 0, 0), boundsMax = new float3(10, 10, 10)});
            var metaGroup = m_Manager.CreateEntityQuery(typeof(ChunkBoundsComponent), typeof(ChunkHeader));
            var metaBoundsCount = metaGroup.CalculateEntityCount();
            var metaChunkHeaders = metaGroup.ToComponentDataArray<ChunkHeader>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, metaBoundsCount);
            for (int i = 0; i < metaBoundsCount; ++i)
            {
                var curBounds = new ChunkBoundsComponent { boundsMin = new float3(1000, 1000, 1000), boundsMax = new float3(-1000, -1000, -1000)};
                var boundsChunk = metaChunkHeaders[i].ArchetypeChunk;
                var boundsType = m_Manager.GetComponentTypeHandle<BoundsComponent>(true);
                var bounds = boundsChunk.GetNativeArray(ref boundsType);
                for (int j = 0; j < bounds.Length; ++j)
                {
                    curBounds.boundsMin = math.min(curBounds.boundsMin, bounds[j].boundsMin);
                    curBounds.boundsMax = math.max(curBounds.boundsMax, bounds[j].boundsMax);
                }

                var chunkBoundsType = m_Manager.GetComponentTypeHandle<ChunkBoundsComponent>(false);

                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                boundsChunk.SetChunkComponentData(ref chunkBoundsType, curBounds);
                Assert.AreEqual(curBounds, boundsChunk.GetChunkComponentData(ref chunkBoundsType));
                #pragma warning restore 0618
            }
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var val = m_Manager.GetChunkComponentData<ChunkBoundsComponent>(entity0);
            #pragma warning restore 0618
            Assert.AreEqual(new float3(-10, -10, -10), val.boundsMin);
            Assert.AreEqual(new float3(10, 10, 10), val.boundsMax);
        }

        partial struct UpdateChunkBoundsJob : IJobEntity
        {
            [ReadOnly] public ComponentTypeHandle<BoundsComponent> ChunkComponentTypeHandle;

            internal void Execute(ref ChunkBoundsComponent chunkBounds, in ChunkHeader chunkHeader)
            {
                var curBounds = new ChunkBoundsComponent { boundsMin = new float3(1000, 1000, 1000), boundsMax = new float3(-1000, -1000, -1000)};
                var boundsChunk = chunkHeader.ArchetypeChunk;
                var bounds = boundsChunk.GetNativeArray(ref ChunkComponentTypeHandle);
                for (int j = 0; j < bounds.Length; ++j)
                {
                    curBounds.boundsMin = math.min(curBounds.boundsMin, bounds[j].boundsMin);
                    curBounds.boundsMax = math.max(curBounds.boundsMax, bounds[j].boundsMax);
                }

                chunkBounds = curBounds;
            }
        }

        partial class ChunkBoundsUpdateSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var job = new UpdateChunkBoundsJob{ ChunkComponentTypeHandle = EntityManager.GetComponentTypeHandle<BoundsComponent>(true)};
                job.Run();
            }
        }

        [Test]
        public void SystemProcessMetaChunkComponent()
        {
            var chunkBoundsUpdateSystem = World.GetOrCreateSystemManaged<ChunkBoundsUpdateSystem>();

            var entity0 = m_Manager.CreateEntity(typeof(BoundsComponent), ComponentType.ChunkComponent<ChunkBoundsComponent>());
            m_Manager.SetComponentData(entity0, new BoundsComponent {boundsMin = new float3(-10, -10, -10), boundsMax = new float3(0, 0, 0)});

            var entity1 = m_Manager.CreateEntity(typeof(BoundsComponent), ComponentType.ChunkComponent<ChunkBoundsComponent>());
            m_Manager.SetComponentData(entity1, new BoundsComponent {boundsMin = new float3(0, 0, 0), boundsMax = new float3(10, 10, 10)});

            chunkBoundsUpdateSystem.Update();

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var val = m_Manager.GetChunkComponentData<ChunkBoundsComponent>(entity0);
            #pragma warning restore 0618
            Assert.AreEqual(new float3(-10, -10, -10), val.boundsMin);
            Assert.AreEqual(new float3(10, 10, 10), val.boundsMax);
        }

        [Test]
        public void ChunkHeaderMustBeQueriedExplicitly()
        {
            var arch0 = m_Manager.CreateArchetype(ComponentType.ChunkComponent<EcsTestData>(), typeof(EcsTestData2));
            var entity0 = m_Manager.CreateEntity(arch0);

            EntityQuery group0 = m_Manager.CreateEntityQuery(typeof(ChunkHeader), typeof(EcsTestData));
            EntityQuery group1 = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.AreEqual(1, group0.CalculateEntityCount());
            Assert.AreEqual(0, group1.CalculateEntityCount());
        }

        [Test]
        public void DestroyEntityDestroysMetaChunk()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ChunkComponent<ChunkBoundsComponent>());
            var metaEntity = m_Manager.Debug.GetMetaChunkEntity(entity);
            m_Manager.DestroyEntity(entity);
            Assert.IsFalse(m_Manager.Exists(metaEntity));
        }

        [Test]
        public void NewChunkGetsDefaultChunkComponentValue()
        {
            var entity = m_Manager.CreateEntity
                (
                    ComponentType.ChunkComponent<EcsTestData>(),
                    ComponentType.ReadWrite<EcsTestSharedComp>()
                );

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(123));
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new EcsTestData(123));
            #pragma warning restore 0618

            var other = m_Manager.Instantiate(entity);

            Assert.AreEqual(m_Manager.GetChunk(entity), m_Manager.GetChunk(other));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(123, m_Manager.GetChunkComponentData<EcsTestData>(other).value);
            #pragma warning restore 0618

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetSharedComponentManaged(other, new EcsTestSharedComp(456));
            #pragma warning restore 0618

            Assert.AreNotEqual(m_Manager.GetChunk(entity), m_Manager.GetChunk(other));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(0, m_Manager.GetChunkComponentData<EcsTestData>(other).value);
            #pragma warning restore 0618
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void SetChunkComponent_ManagedComponents()
        {
            var entity = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestManagedComponent>());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new EcsTestManagedComponent { value = "SomeString" });
            #pragma warning restore 0618
            Assert.IsTrue(m_Manager.HasComponent(entity, ComponentType.ChunkComponent<EcsTestManagedComponent>()));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var classVal0 = m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity).value;
            #pragma warning restore 0618
            Assert.AreEqual("SomeString", classVal0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var classVal1 = m_Manager.GetChunkComponentData<EcsTestManagedComponent>(m_Manager.GetChunk(entity)).value;
            #pragma warning restore 0618
            Assert.AreEqual("SomeString", classVal1);
        }

        [Test]
        public void AddChunkComponentMovesEntity_ManagedComponents()
        {
            var entity0 = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            var entity1 = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            var chunk0 = m_Manager.GetChunk(entity0);
            var chunk1 = m_Manager.GetChunk(entity1);

            Assert.AreEqual(chunk0, chunk1);

            Assert.IsFalse(m_Manager.HasChunkComponent<EcsTestManagedComponent>(entity0));
            m_Manager.Debug.CheckInternalConsistency();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddChunkComponentData<EcsTestManagedComponent>(entity0);
            #pragma warning restore 0618
            m_Manager.Debug.CheckInternalConsistency();
            Assert.IsTrue(m_Manager.HasChunkComponent<EcsTestManagedComponent>(entity0));
            chunk0 = m_Manager.GetChunk(entity0);

            Assert.AreNotEqual(chunk0, chunk1);

            Assert.IsTrue(m_Manager.HasComponent(entity0, ComponentType.ChunkComponent<EcsTestManagedComponent>()));
            Assert.IsFalse(m_Manager.HasComponent(entity1, ComponentType.ChunkComponent<EcsTestManagedComponent>()));
            Assert.IsTrue(m_Manager.Exists(m_Manager.Debug.GetMetaChunkEntity(entity0)));
            Assert.IsTrue(m_Manager.Debug.GetMetaChunkEntity(entity1) == default(Entity));
        }

        [Test]
        public void RemoveChunkComponent_ManagedComponents()
        {
            var arch0 = m_Manager.CreateArchetype(ComponentType.ChunkComponent<EcsTestManagedComponent>(), typeof(EcsTestData2));
            var arch1 = m_Manager.CreateArchetype(typeof(EcsTestData2));

            var entity0 = m_Manager.CreateEntity(arch0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity0), new EcsTestManagedComponent { value = "SomeString" });
            #pragma warning restore 0618
            m_Manager.SetComponentData(entity0, new EcsTestData2 { value0 = 1, value1 = 2 });
            var metaEntity0 = m_Manager.Debug.GetMetaChunkEntity(entity0);

            var entity1 = m_Manager.CreateEntity(arch1);
            m_Manager.SetComponentData(entity1, new EcsTestData2 { value0 = 2, value1 = 3 });

            m_Manager.RemoveChunkComponent<EcsTestManagedComponent>(entity0);

            Assert.IsFalse(m_Manager.HasComponent(entity0, ComponentType.ChunkComponent<EcsTestManagedComponent>()));
            Assert.IsFalse(m_Manager.Exists(metaEntity0));
        }

        [Test]
        public void UpdateChunkComponent_ManagedComponents()
        {
            var arch0 = m_Manager.CreateArchetype(ComponentType.ChunkComponent<EcsTestManagedComponent>(), typeof(EcsTestData2));
            EntityQuery group0 = m_Manager.CreateEntityQuery(typeof(ChunkHeader), typeof(EcsTestManagedComponent));

            var entity0 = m_Manager.CreateEntity(arch0);
            var chunk0 = m_Manager.GetChunk(entity0);
            EcsTestManagedComponent testData = new EcsTestManagedComponent { value = "SomeString" };

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(chunk0, testData);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual("SomeString", m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity0).value);
            #pragma warning restore 0618

            m_Manager.SetComponentData(entity0, new EcsTestData2 { value0 = 1, value1 = 2 });

            var entity1 = m_Manager.CreateEntity(arch0);
            var chunk1 = m_Manager.GetChunk(entity1);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual("SomeString", m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity0).value);
            Assert.AreEqual("SomeString", m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity1).value);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData(chunk1, testData);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());

            m_Manager.SetComponentData(entity1, new EcsTestData2 { value0 = 2, value1 = 3 });

            Assert.AreEqual(1, group0.CalculateEntityCount());

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetChunkComponentData<EcsTestManagedComponent>(chunk0, new EcsTestManagedComponent { value = "SomeOtherString" });
            #pragma warning restore 0618

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual("SomeOtherString", m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity0).value);
            #pragma warning restore 0618

            Assert.AreEqual(1, group0.CalculateEntityCount());
        }

        [Test]
        public void NewChunkGetsDefaultChunkComponentValue_ManagedComponents()
        {
            var entity = m_Manager.CreateEntity
                (
                    ComponentType.ChunkComponent<EcsTestManagedComponent>(),
                    ComponentType.ReadWrite<EcsTestSharedComp>()
                );

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(123));
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new EcsTestManagedComponent() { value = "SomeString" });
            #pragma warning restore 0618

            var other = m_Manager.Instantiate(entity);

            Assert.AreEqual(m_Manager.GetChunk(entity), m_Manager.GetChunk(other));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual("SomeString", m_Manager.GetChunkComponentData<EcsTestManagedComponent>(other).value);
            #pragma warning restore 0618

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.SetSharedComponentManaged(other, new EcsTestSharedComp(456));
            #pragma warning restore 0618

            Assert.AreNotEqual(m_Manager.GetChunk(entity), m_Manager.GetChunk(other));
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.IsNull(m_Manager.GetChunkComponentData<EcsTestManagedComponent>(other));
            #pragma warning restore 0618
        }

#endif
    }
}
