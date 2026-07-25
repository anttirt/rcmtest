using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Hierarchy;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    public class HierarchyEntityHandlerTests
    {
        HierarchyEntityHandler m_Handler;
        HierarchyWorldHandler m_WorldHandler;
        Unity.Hierarchy.Hierarchy m_Hierarchy;
        World m_PreviousWorld;
        World m_World;
        BakingSystem m_BakingSystem;
        BakingSettings m_Settings;
        GameObject m_Prefab;

        [SetUp]
        public void SetUp()
        {
            // Load the prefab
            var path = $"Packages/com.unity.entities/Unity.Entities.Editor.Tests/Content/Prefab_Hierarchy.prefab";
            m_Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World("Test World");
            m_Hierarchy = new Unity.Hierarchy.Hierarchy();
            m_Handler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyEntityHandler>();
            m_WorldHandler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyWorldHandler>();

            m_BakingSystem = m_World.GetOrCreateSystemManaged<BakingSystem>();
            m_Settings = new BakingSettings
            {
                BakingFlags = BakingUtility.BakingFlags.AssignName | BakingUtility.BakingFlags.AddEntityGUID
            };

            m_BakingSystem.BakingSettings = m_Settings;

            UpdateHierarchy(m_Hierarchy);
        }

        [TearDown]
        public void TearDown()
        {
            m_Handler = null;
            m_WorldHandler = null;
            m_Hierarchy.Dispose();
            m_Hierarchy = null;
            m_World.Dispose();
            m_World = null;
            m_BakingSystem = null;
            m_Settings = null;
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
        }

        public static void UpdateHierarchy(Unity.Hierarchy.Hierarchy hierarchy)
        {
            int count = 100;
            while (hierarchy.UpdateNeeded && count-- > 0)
                hierarchy.Update();
            Assert.IsFalse(hierarchy.UpdateNeeded);
        }

        [Test]
        public void CreateEntityNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);
            Assert.That(node1, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
            Assert.That(node2, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));

            Assert.IsTrue(m_Hierarchy.Exists(node1));
            Assert.IsTrue(m_Hierarchy.Exists(node2));
        }

        [Test]
        public void RemoveEntityNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);

            Assert.IsTrue(m_Hierarchy.Exists(node1));
            Assert.IsTrue(m_Hierarchy.Exists(node2));

            m_World.EntityManager.DestroyEntity(entity1);
            m_World.EntityManager.DestroyEntity(entity2);

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            Assert.IsFalse(m_Hierarchy.Exists(node1));
            Assert.IsFalse(m_Hierarchy.Exists(node2));
        }

        [Test]
        public void CreateEntityChildrenNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child(){Value = child});
            m_World.EntityManager.AddComponentData(child, new Parent {Value = parent});

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));

            Assert.IsTrue(m_Hierarchy.GetChildrenCount(childNode) == 0);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 1);

            var childrenNode = m_Hierarchy.GetChildren(parentNode);
            Assert.IsTrue(childrenNode[0] == childNode);
        }

        [Test]
        public void ReparentEntityNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var parent2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children1 = m_World.EntityManager.AddBuffer<Child>(parent1);
            children1.Add(new Child(){Value = child});
            m_World.EntityManager.AddComponentData(child, new Parent {Value = parent1});

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode1 = m_Handler.GetNode(parent1);
            var parentNode2 = m_Handler.GetNode(parent2);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode1));
            Assert.IsTrue(m_Hierarchy.Exists(parentNode2));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(childNode) == 0);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode1) == 1);

            // Reparent
            m_World.EntityManager.RemoveComponent<Child>(parent1);
            var children2 = m_World.EntityManager.AddBuffer<Child>(parent2);
            children2.Add(new Child(){Value = child});
            m_World.EntityManager.AddComponentData(child, new Parent {Value = parent2});

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

             parentNode2 = m_Handler.GetNode(parent2);
             childNode = m_Handler.GetNode(child);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(childNode) == 0);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode2) == 1);
        }

        [Test]
        public void RemoveParentComponent_ReparentsEntityToWorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child });
            m_World.EntityManager.AddComponentData(child, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 1);
            Assert.AreEqual(parentNode, m_Hierarchy.GetParent(childNode));

            // Remove parent relationship entirely
            m_World.EntityManager.RemoveComponent<Parent>(child);
            m_World.EntityManager.RemoveComponent<Child>(parent);

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            childNode = m_Handler.GetNode(child);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.Exists(worldNode));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode));
        }

        [Test]
        public void ReparentEntityToNullParent_IsParentedUnderWorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child });
            m_World.EntityManager.AddComponentData(child, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.AreEqual(parentNode, m_Hierarchy.GetParent(childNode));

            // Reparent to Entity.Null (parent still has Child buffer, but Parent.Value is null)
            m_World.EntityManager.SetComponentData(child, new Parent { Value = Entity.Null });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            childNode = m_Handler.GetNode(child);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.Exists(worldNode));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode));
        }

        [Test]
        public void RemoveParentFromMultipleEntities_AllReparentedToWorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child3 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child1 });
            children.Add(new Child { Value = child2 });
            children.Add(new Child { Value = child3 });
            m_World.EntityManager.AddComponentData(child1, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child2, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child3, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 3);

            // Remove parent from all children simultaneously
            m_World.EntityManager.RemoveComponent<Parent>(child1);
            m_World.EntityManager.RemoveComponent<Parent>(child2);
            m_World.EntityManager.RemoveComponent<Parent>(child3);
            m_World.EntityManager.RemoveComponent<Child>(parent);

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            parentNode = m_Handler.GetNode(parent);
            var childNode1 = m_Handler.GetNode(child1);
            var childNode2 = m_Handler.GetNode(child2);
            var childNode3 = m_Handler.GetNode(child3);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 0);
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode1));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode2));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode3));
        }

        [Test]
        public void DestroyEntityWithParentChange_DoesNotCrash()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child3 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child1 });
            children.Add(new Child { Value = child2 });
            children.Add(new Child { Value = child3 });
            m_World.EntityManager.AddComponentData(child1, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child2, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child3, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            Assume.That(m_Hierarchy.GetChildrenCount(parentNode), Is.EqualTo(3));

            // Remove parent component from all children AND destroy child2 in the same frame
            // This tests CleanupRemovedEntities: child2 will be in both RemovedParentEntities and DestroyedEntities
            m_World.EntityManager.RemoveComponent<Parent>(child1);
            m_World.EntityManager.RemoveComponent<Parent>(child2);
            m_World.EntityManager.RemoveComponent<Parent>(child3);
            m_World.EntityManager.DestroyEntity(child2);
            m_World.EntityManager.RemoveComponent<Child>(parent);

            // This should not crash - CleanupRemovedEntities should handle child2 being destroyed
            Assert.That(() =>
            {
                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
            }, Throws.Nothing);

            // Verify child1 and child3 were reparented to world node
            var childNode1 = m_Handler.GetNode(child1);
            var childNode3 = m_Handler.GetNode(child3);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.That(m_Hierarchy.Exists(childNode1), Is.True);
            Assert.That(m_Hierarchy.Exists(childNode3), Is.True);
            Assert.That(m_Hierarchy.GetParent(childNode1), Is.EqualTo(worldNode));
            Assert.That(m_Hierarchy.GetParent(childNode3), Is.EqualTo(worldNode));

            // Verify child2 node no longer exists
            var childNode2 = m_Handler.GetNode(child2);
            Assert.That(m_Hierarchy.Exists(childNode2), Is.False);
        }

        [Test]
        public void DestroyEntityDuringReparenting_DoesNotCrash()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var parent2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children1 = m_World.EntityManager.AddBuffer<Child>(parent1);
            children1.Add(new Child { Value = child1 });
            children1.Add(new Child { Value = child2 });
            m_World.EntityManager.AddComponentData(child1, new Parent { Value = parent1 });
            m_World.EntityManager.AddComponentData(child2, new Parent { Value = parent1 });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode1 = m_Handler.GetNode(parent1);
            Assume.That(m_Hierarchy.GetChildrenCount(parentNode1), Is.EqualTo(2));

            // Reparent both children to parent2, but destroy child1 in the same frame
            // This tests CleanupRemovedEntities: child1 will be in both AddedParentEntities and DestroyedEntities
            m_World.EntityManager.RemoveComponent<Child>(parent1);
            var children2 = m_World.EntityManager.AddBuffer<Child>(parent2);
            children2.Add(new Child { Value = child1 });
            children2.Add(new Child { Value = child2 });
            m_World.EntityManager.SetComponentData(child1, new Parent { Value = parent2 });
            m_World.EntityManager.SetComponentData(child2, new Parent { Value = parent2 });
            m_World.EntityManager.DestroyEntity(child1);

            // This should not crash - CleanupRemovedEntities should handle child1 being destroyed
            Assert.That(() =>
            {
                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
            }, Throws.Nothing);

            // Verify child2 was reparented to parent2
            var parentNode2 = m_Handler.GetNode(parent2);
            var childNode2 = m_Handler.GetNode(child2);

            Assert.That(m_Hierarchy.Exists(childNode2), Is.True);
            Assert.That(m_Hierarchy.GetParent(childNode2), Is.EqualTo(parentNode2));
            Assert.That(m_Hierarchy.GetChildrenCount(parentNode2), Is.EqualTo(1));

            // Verify child1 node no longer exists
            var childNode1 = m_Handler.GetNode(child1);
            Assert.That(m_Hierarchy.Exists(childNode1), Is.False);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ShowHiddenEntitiesSetting_WorksAsExpected(bool showHiddenEntities)
        {
            var oldSetting = HierarchyEntitiesSettings.GetShowHiddenEntities();
            HierarchyEntitiesSettings.SetShowHiddenEntities(showHiddenEntities);

            try
            {
                var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
                var visibleEntity = m_World.EntityManager.CreateEntity();
                var hiddenEntity = m_World.EntityManager.CreateEntity(typeof(HideInHierarchy));

                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);

                Assert.That(m_Handler.GetNode(visibleEntity), Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
                Assert.That(m_Handler.GetNode(hiddenEntity),
                    showHiddenEntities
                        ? Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null)
                        : Is.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
            }
            finally
            {
                HierarchyEntitiesSettings.SetShowHiddenEntities(oldSetting);
            }
        }

        [Test]
        public void CreateEntityPrefabNodes()
        {
            var go = Object.Instantiate(m_Prefab);
            m_Settings.PrefabRoot = go;
            BakingUtility.BakeGameObjects(m_World, Array.Empty<GameObject>(), m_Settings);

            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<LinkedEntityGroup>().WithOptions(EntityQueryOptions.IncludePrefab).Build(m_World.EntityManager);
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(1));
            var entities = query.ToEntityArray(Allocator.Temp);
            var prefabNode = m_Handler.GetNode(entities[0]);

            // The prefab should have exactly one child
            Assert.IsTrue(m_Hierarchy.Exists(prefabNode));
            Assert.That(m_Hierarchy.GetChildrenCount(prefabNode), Is.EqualTo(1));

            entities.Dispose();
            query.Dispose();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CreatePrefabNodes_WithAdditionalEntitiesInLinkedEntityGroup()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            // Create a prefab entity with LinkedEntityGroup
            var prefabEntity = m_World.EntityManager.CreateEntity(typeof(Prefab));
            var linkedEntityGroup = m_World.EntityManager.AddBuffer<LinkedEntityGroup>(prefabEntity);

            // Add the prefab itself to the LinkedEntityGroup
            linkedEntityGroup.Add(new LinkedEntityGroup { Value = prefabEntity });

            // Create additional entities and add them to the LinkedEntityGroup.
            // This simulates entities being added to a LinkedEntityGroup buffer after baking.
            const int additionalEntityCount = 5;
            var additionalEntities = new Entity[additionalEntityCount];
            for (int i = 0; i < additionalEntityCount; i++)
            {
                additionalEntities[i] = m_World.EntityManager.CreateEntity();
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = additionalEntities[i] });
            }

            // Update the hierarchy - this should not throw due to capacity issues.
            Assert.That(() =>
            {
                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
            }, Throws.Nothing);

            // Verify the prefab node exists
            var prefabNode = m_Handler.GetNode(prefabEntity);
            Assert.IsTrue(m_Hierarchy.Exists(prefabNode));

            // All additional entities should be children of the prefab node
            Assert.That(m_Hierarchy.GetChildrenCount(prefabNode), Is.EqualTo(additionalEntityCount));

            // Verify all additional entities are parented under the prefab root
            foreach (var entity in additionalEntities)
            {
                var childNode = m_Handler.GetNode(entity);
                Assert.IsTrue(m_Hierarchy.Exists(childNode));
                Assert.That(m_Hierarchy.GetParent(childNode), Is.EqualTo(prefabNode));
            }
        }

        [Test]
        public void SearchMatch_FiltersEntitiesByComponent()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(LocalToWorld));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(LocalTransform));
            var entity3 = m_World.EntityManager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);
            var node3 = m_Handler.GetNode(entity3);

            Assume.That(node1, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null), "Entity1 should have a hierarchy node");
            Assume.That(node2, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null), "Entity2 should have a hierarchy node");
            Assume.That(node3, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null), "Entity3 should have a hierarchy node");

            var query = CreateSearchQuery("LocalToWorld");
            m_Handler.Internal_SearchBegin(query);

            Assert.That(m_Handler.Internal_SearchMatch(node1), Is.True, "Entity with LocalToWorld should match");
            Assert.That(m_Handler.Internal_SearchMatch(node2), Is.False, "Entity without LocalToWorld should not match");
            Assert.That(m_Handler.Internal_SearchMatch(node3), Is.True, "Entity with LocalToWorld should match");
        }

        [Test]
        public void SearchMatch_MultipleComponents_RequiresAllComponents()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(LocalToWorld));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);

            Assume.That(node1, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null), "Entity1 should have a hierarchy node");
            Assume.That(node2, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null), "Entity2 should have a hierarchy node");

            var query = CreateSearchQuery("LocalToWorld", "LocalTransform");
            m_Handler.Internal_SearchBegin(query);

            Assert.That(m_Handler.Internal_SearchMatch(node1), Is.False, "Entity missing LocalTransform should not match");
            Assert.That(m_Handler.Internal_SearchMatch(node2), Is.True, "Entity with both components should match");
        }

        [Test]
        public void ClearMappings_WithEntryReferencingDisposedWorld_DoesNotThrow()
        {
            // A world's SequenceNumber remains stable and non-recycled once assigned,
            // so a disposed world's SN cannot match any live world.
            var deadWorld = new World("Dead World");
            var deadSequenceNumber = deadWorld.SequenceNumber;
            deadWorld.Dispose();

            var orphanEntity = new Entity { Index = int.MaxValue, Version = 1 };
            m_Handler.Internal_AddEntityToWorld(orphanEntity, new Unity.Hierarchy.HierarchyNode(), deadSequenceNumber);

            Assert.That(() => m_Handler.ClearMappings(m_World), Throws.Nothing);
        }

        [Test]
        public void SearchMatch_FiltersByEntityId()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity();
            var entity2 = m_World.EntityManager.CreateEntity();

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);

            Assume.That(node1, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
            Assume.That(node2, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));

            var query = CreateIdSearchQuery(entity1.Index.ToString());
            m_Handler.Internal_SearchBegin(query);

            Assert.That(m_Handler.Internal_SearchMatch(node1), Is.True, "Entity with matching index should match");
            Assert.That(m_Handler.Internal_SearchMatch(node2), Is.False, "Entity with different index should not match");
        }

        [Test]
        public void SearchMatch_FiltersByEntityIdWithVersion()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity = m_World.EntityManager.CreateEntity();

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node = m_Handler.GetNode(entity);
            Assume.That(node, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));

            // Test with correct version
            var queryCorrect = CreateIdSearchQuery($"{entity.Index}:{entity.Version}");
            m_Handler.Internal_SearchBegin(queryCorrect);
            Assert.That(m_Handler.Internal_SearchMatch(node), Is.True, "Entity with matching index:version should match");

            // Test with wrong version
            var queryWrong = CreateIdSearchQuery($"{entity.Index}:{entity.Version + 1}");
            m_Handler.Internal_SearchBegin(queryWrong);
            Assert.That(m_Handler.Internal_SearchMatch(node), Is.False, "Entity with wrong version should not match");
        }

        [Test]
        public void SearchMatch_EntityIdCombinedWithComponent_RequiresBoth()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entityWithComponent = m_World.EntityManager.CreateEntity(typeof(LocalToWorld));
            var entityWithoutComponent = m_World.EntityManager.CreateEntity();

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var nodeWith = m_Handler.GetNode(entityWithComponent);
            var nodeWithout = m_Handler.GetNode(entityWithoutComponent);

            Assume.That(nodeWith, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
            Assume.That(nodeWithout, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));

            // Search for entity with specific id AND LocalToWorld component
            var filters = new[]
            {
                new HierarchySearchFilter { Name = "id", Value = entityWithComponent.Index.ToString(), Op = HierarchySearchFilterOperator.Equal },
                new HierarchySearchFilter { Name = "t", Value = "LocalToWorld", Op = HierarchySearchFilterOperator.Equal }
            };
            var query = new HierarchySearchQueryDescriptor(filters);
            m_Handler.Internal_SearchBegin(query);

            Assert.That(m_Handler.Internal_SearchMatch(nodeWith), Is.True, "Entity with matching id and component should match");
            Assert.That(m_Handler.Internal_SearchMatch(nodeWithout), Is.False, "Entity with different id should not match");
        }

        HierarchySearchQueryDescriptor CreateSearchQuery(params string[] componentTypeNames)
        {
            var filters = new HierarchySearchFilter[componentTypeNames.Length];
            for (int i = 0; i < componentTypeNames.Length; i++)
            {
                filters[i] = new HierarchySearchFilter
                {
                    Name = "t",
                    Value = componentTypeNames[i],
                    Op = HierarchySearchFilterOperator.Equal
                };
            }
            return new HierarchySearchQueryDescriptor(filters);
        }

        HierarchySearchQueryDescriptor CreateIdSearchQuery(string idValue)
        {
            var filters = new[]
            {
                new HierarchySearchFilter
                {
                    Name = "id",
                    Value = idValue,
                    Op = HierarchySearchFilterOperator.Equal
                }
            };
            return new HierarchySearchQueryDescriptor(filters);
        }
    }
}
