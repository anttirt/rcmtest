using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class InternalAllocatorTests : ECSTestsFixture
    {
        [Test]
        public unsafe void CreateEntitiesThenSetChunk()
        {
            var count = 10;

            // Test create
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(count, ref World.UpdateAllocator);
            ref var data = ref EntityComponentStore.s_entityStore.Data;

            //Allocate entities but do not set a chunk
            data.AllocateEntities(entities);

            var savedIndexes = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(count, ref World.UpdateAllocator);

            for (int i = 0; i < entities.Length; i++)
            {
               Assert.IsTrue(data.Exists(entities[i]));
               Assert.AreEqual(EntityInChunk.Null, data.GetEntityInChunk(entities[i]));

               savedIndexes[i] = entities[i].Index;
            }

            var archetype = m_Manager.CreateArchetype();

            //Now allocate a chunk for them (this is a structural change)
            var access = m_Manager.GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var archetypeChanges = access->BeginAdditiveStructuralChanges();

            access->AllocateAndAssignChunksToExistingEntities(archetype, (Entity*)entities.GetUnsafePtr(), count);

            access->EndStructuralChanges(ref archetypeChanges);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.AreNotEqual(EntityInChunk.Null, data.GetEntityInChunk(entities[i]));
                Assert.AreEqual(savedIndexes[i], entities[i].Index);
                Assert.IsTrue(m_Manager.Exists(entities[i]));
            }
        }


        [Test]
        public unsafe void InstantiateEntitiesThenSetChunk()
        {
            var count = 10;

            // Test create
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(count, ref World.UpdateAllocator);
            ref var data = ref EntityComponentStore.s_entityStore.Data;

            data.AllocateEntities(entities);
            var savedIndexes = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(count+1, ref World.UpdateAllocator);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(data.Exists(entities[i]));
                Assert.AreEqual(EntityInChunk.Null, data.GetEntityInChunk(entities[i]));

                savedIndexes[i] = entities[i].Index;
            }

            var archetype = m_Manager.CreateArchetype();
            var srcEntity = m_Manager.CreateEntity(archetype);
            savedIndexes[count] = srcEntity.Index;

            var access = m_Manager.GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var archetypeChanges = access->BeginAdditiveStructuralChanges();

            access->AllocateAndAssignChunksToExistingEntitiesInstantiate(srcEntity, (Entity*)entities.GetUnsafePtr(), count);

            access->EndStructuralChanges(ref archetypeChanges);

            //Get the entities of the specific archetype and check that there are count + 1
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(Simulate) }
            };
            NativeArray<Entity> queriedEntities = m_Manager.CreateEntityQuery(queryDesc).ToEntityArray(Allocator.Persistent);

            var numEntities = queriedEntities.Length;
            Assert.AreEqual(count + 1, numEntities);


            Assert.AreEqual(savedIndexes[count], queriedEntities[0].Index);
            for (int i = 0; i < numEntities-1; i++)
            {
                Assert.AreNotEqual(EntityInChunk.Null, data.GetEntityInChunk(queriedEntities[i]));
                Assert.AreEqual(savedIndexes[i], queriedEntities[i+1].Index);
                Assert.IsTrue(m_Manager.Exists(queriedEntities[i]));

            }

            queriedEntities.Dispose();
        }

        [Test]
        public unsafe void InstantiateEntitiesWithLegThenSetChunk()
        {
            var count = 10;
            var numChildren = 5;

            // Test create
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(count, ref World.UpdateAllocator);
            ref var data = ref EntityComponentStore.s_entityStore.Data;

            data.AllocateEntities(entities);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(data.Exists(entities[i]));
                Assert.AreEqual(EntityInChunk.Null, data.GetEntityInChunk(entities[i]));
            }

            var archetype = m_Manager.CreateArchetype();

            //Create source entity from which we will instantiate the entities
            //This entity should have a LinkedEntityGroup
            var srcEntity = m_Manager.CreateEntity(archetype);

            //Create array to hold leg children
            var array = m_Manager.CreateEntity(archetype, numChildren, World.UpdateAllocator.Handle);

            //Add the leg to the srcEntity
            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(srcEntity);
            linkedBuffer.Add(new LinkedEntityGroup {Value = srcEntity}); // We must make sure the src entity of the group is itself

            for (var i = 0; i < numChildren; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            //Now create the src entity and instantiate
            var access = m_Manager.GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var archetypeChanges = access->BeginAdditiveStructuralChanges();

            access->AllocateAndAssignChunksToExistingEntitiesInstantiate(srcEntity, (Entity*)entities.GetUnsafePtr(), count);

            access->EndStructuralChanges(ref archetypeChanges);

            //I will get the entities of the specific archetype
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(Simulate) }
            };
            NativeArray<Entity> queriedEntities = m_Manager.CreateEntityQuery(queryDesc).ToEntityArray(Allocator.Persistent);

            var numEntities = queriedEntities.Length;

            //Ensure we have the expected number of entities (number of total children plus number of parents. This includes the source entity)
            var expectedNumEntities = (count+1) * numChildren + count+1;
            Assert.AreEqual(expectedNumEntities, numEntities);

            // Assert that the instantiated entities and their linked entities have an associated chunk
            for (int i = 0; i < numEntities; i++)
            {
                Assert.AreNotEqual(EntityInChunk.Null, data.GetEntityInChunk(queriedEntities[i]));
                Assert.IsTrue(m_Manager.Exists(queriedEntities[i]));
            }
            queriedEntities.Dispose();
        }

        [Test]
        public unsafe void DeallocateEntitiesAndUnassignChunks()
        {
            var count = 10;

            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(count, ref World.UpdateAllocator);
            ref var data = ref EntityComponentStore.s_entityStore.Data;

            // Allocate entities and assign them to a chunk so they are fully realized.
            data.AllocateEntities(entities);

            var archetype = m_Manager.CreateArchetype();

            var access = m_Manager.GetCheckedEntityDataAccess();
            access->PrepareForAdditiveStructuralChanges();
            var archetypeChanges = access->BeginAdditiveStructuralChanges();

            access->AllocateAndAssignChunksToExistingEntities(archetype, (Entity*)entities.GetUnsafePtr(), count);

            access->EndStructuralChanges(ref archetypeChanges);

            // Sanity check: entities exist and have a chunk assigned prior to deallocation.
            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.Exists(entities[i]));
                Assert.AreNotEqual(EntityInChunk.Null, data.GetEntityInChunk(entities[i]));
            }

            // Now deallocate the entities and unassign their chunks.
            access->DeallocateAndUnAssignChunksToExistingEntities((Entity*)entities.GetUnsafePtr(), count);

            // After deallocation, the entities should no longer exist and no longer be assigned to a chunk.
            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsFalse(m_Manager.Exists(entities[i]));
                Assert.AreEqual(EntityInChunk.Null, data.GetEntityInChunk(entities[i]));
            }

            // The archetype's chunks should now be empty since every entity in them was destroyed.
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(Simulate) }
            };
            var queriedEntities = m_Manager.CreateEntityQuery(queryDesc).ToEntityArray(Allocator.Persistent);
            Assert.AreEqual(0, queriedEntities.Length);
            queriedEntities.Dispose();
        }



    }
}
