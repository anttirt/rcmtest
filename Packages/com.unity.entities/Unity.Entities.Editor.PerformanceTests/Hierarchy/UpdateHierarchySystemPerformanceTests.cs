using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;
using Unity.Transforms;
using UnityEditor;

namespace Unity.Entities.Editor.PerformanceTests
{
    class UpdateHierarchySystemPerformanceTests
    {
        HierarchyEntityHandler m_Handler;
        Unity.Hierarchy.Hierarchy m_Hierarchy;
        World m_PreviousWorld;
        World m_World;
        bool m_EnableEntitiesInHierarchyState;

        [SetUp]
        public void OnSetup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World("Test World");
            m_Hierarchy = new Unity.Hierarchy.Hierarchy();
            m_Handler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyEntityHandler>();

            UpdateHierarchy(m_Hierarchy);
        }

        [TearDown]
        public void TearDown()
        {
            m_Handler = null;
            m_Hierarchy.Dispose();
            m_Hierarchy = null;
            m_World.Dispose();
            m_World = null;
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
        }

        public static void UpdateHierarchy(Unity.Hierarchy.Hierarchy hierarchy)
        {
            int count = 100;
            while (hierarchy.UpdateNeeded && count-- > 0)
                hierarchy.Update();
            Assert.IsFalse(hierarchy.UpdateNeeded);
        }

        [Test, Performance]
        public void CreateNewRootNodes([Values(1000, 10_000, 100_000, 1_000_000)] int numberOfEntities)
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            // Create entities
            for (int i = 0; i < numberOfEntities; i++)
            {
                m_World.EntityManager.CreateEntity();
            }

            Measure.Method(() =>
                {
                    // This should create the corresponding entity nodes
                    hierarchySystem.Update(m_World.Unmanaged);
                }).WarmupCount(0)
                .MeasurementCount(1)
                .IterationsPerMeasurement(10)
                .Run();
        }

        [Test, Performance]
        public void CreateNewEntityNodesWithChildren([Values(1000, 10_000, 100_000, 1_000_000)] int numberOfEntities)
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            // Create numberOfEntities entities with one child
            for (int i = 0; i < numberOfEntities/2; i++)
            {
                var parent = m_World.EntityManager.CreateEntity();
                var child = m_World.EntityManager.CreateEntity();

                var children = m_World.EntityManager.AddBuffer<Child>(parent);
                children.Add(new Child(){Value = child});
                m_World.EntityManager.AddComponentData(child, new Parent {Value = parent});
            }

            Measure.Method(() =>
                {
                    // This should create the corresponding entity nodes
                    hierarchySystem.Update(m_World.Unmanaged);
                }).WarmupCount(0)
                .MeasurementCount(1)
                .IterationsPerMeasurement(10)
                .Run();
        }

        [Test, Performance]
        public void ReparentEntityNodes([Values(1000, 10_000, 100_000, 1_000_000)] int numberOfEntities)
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
            int firstHalfOfEntities = numberOfEntities/2;

            NativeArray<Entity> parents = new NativeArray<Entity>(numberOfEntities, Allocator.Temp);
            NativeArray<Entity> children = new NativeArray<Entity>(numberOfEntities/2, Allocator.Temp);

            int parentIndex;
            int childIndex;
            for (parentIndex = 0, childIndex = 0; parentIndex < numberOfEntities; parentIndex++, childIndex++)
            {
                parents[parentIndex] = m_World.EntityManager.CreateEntity();
                if (childIndex < firstHalfOfEntities)
                {
                    children[childIndex] = m_World.EntityManager.CreateEntity();
                    var childrenBuffer = m_World.EntityManager.AddBuffer<Child>(parents[parentIndex]);
                    childrenBuffer.Add(new Child(){Value = children[childIndex]});
                    m_World.EntityManager.AddComponentData(children[childIndex], new Parent {Value = parents[parentIndex]});
                }
            }

            // This will create all the entity nodes with the children being parented under the first half of created parent nodes
            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            // Reparent the children below the parent with same index
            for (parentIndex = 0, childIndex = 0; parentIndex < numberOfEntities; parentIndex++)
            {
                if (parentIndex < firstHalfOfEntities)
                {
                    m_World.EntityManager.RemoveComponent<Child>(parents[parentIndex]);
                }
                else
                {
                    var childrenBuffer = m_World.EntityManager.AddBuffer<Child>(parents[parentIndex]);
                    childrenBuffer.Add(new Child(){Value = children[childIndex]});
                    m_World.EntityManager.AddComponentData(children[childIndex], new Parent {Value = parents[parentIndex]});
                    childIndex++;
                }
            }

            Measure.Method(() =>
                {
                    // This should reparent the corresponding entity nodes
                    hierarchySystem.Update(m_World.Unmanaged);
                }).WarmupCount(0)
                .MeasurementCount(1)
                .IterationsPerMeasurement(10)
                .Run();

            parents.Dispose();
            children.Dispose();
        }

        [Test, Performance]
        public void RemoveEntityNodes([Values(1000, 10_000, 100_000, 1_000_000)] int numberOfEntities)
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            // Create entities
            NativeArray<Entity> entities = new NativeArray<Entity>(numberOfEntities, Allocator.Temp);
            for (int i = 0; i < numberOfEntities; i++)
            {
                entities[i] = m_World.EntityManager.CreateEntity();
            }
            hierarchySystem.Update(m_World.Unmanaged);

            m_World.EntityManager.DestroyEntity(entities);

            Measure.Method(() =>
                {
                    // This should create the corresponding entity nodes
                    hierarchySystem.Update(m_World.Unmanaged);
                }).WarmupCount(0)
                .MeasurementCount(1)
                .IterationsPerMeasurement(10)
                .Run();

            entities.Dispose();
        }
    }
}
