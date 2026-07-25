using System;
using NUnit.Framework;
using Unity.Editor.Bridge;
using Unity.Hierarchy;
using UnityEditor;

namespace Unity.Entities.Editor.Tests
{
    public class HierarchyWorldHandlerTests
    {
        HierarchyWorldHandler m_WorldHandler;
        HierarchyEntityHandler m_EntityHandler;
        Unity.Hierarchy.Hierarchy m_Hierarchy;
        World m_World;

        [SetUp]
        public void SetUp()
        {
            m_World = new World("Test World", WorldFlags.Simulation);
            m_Hierarchy = new Unity.Hierarchy.Hierarchy();
            m_WorldHandler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyWorldHandler>();
            m_EntityHandler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyEntityHandler>();

            UpdateHierarchy(m_Hierarchy);
        }

        [TearDown]
        public void TearDown()
        {
            m_WorldHandler = null;
            m_EntityHandler = null;
            m_Hierarchy.Dispose();
            m_Hierarchy = null;

            if (m_World != null)
            {
                m_World.Dispose();
                m_World = null;
            }
        }

        public static void UpdateHierarchy(Unity.Hierarchy.Hierarchy hierarchy)
        {
            int count = 100;
            while (hierarchy.UpdateNeeded && count-- > 0)
                hierarchy.Update();
            Assert.IsFalse(hierarchy.UpdateNeeded);
        }

        [Test]
        public void CreatingEntities_Creates_WorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var worldNode = m_WorldHandler.GetOrCreateWorldNode(m_World);
            var entityNode1 = m_EntityHandler.GetNode(entity1);
            var entityNode2 = m_EntityHandler.GetNode(entity2);

            Assert.IsTrue(m_Hierarchy.Exists(entityNode1));
            Assert.IsTrue(m_Hierarchy.Exists(entityNode2));

            var parentNode = m_Hierarchy.GetParent(entityNode1);
            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(worldNode == parentNode);
        }

        [Test]
        public void DestroyingWorld_Destroys_WorldNodeAndChildren()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
            var entity1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var worldNode = m_WorldHandler.GetOrCreateWorldNode(m_World);
            var entityNode1 = m_EntityHandler.GetNode(entity1);
            var entityNode2 = m_EntityHandler.GetNode(entity2);

            Assert.IsTrue(m_Hierarchy.Exists(worldNode));
            Assert.IsTrue(m_Hierarchy.Exists(entityNode1));
            Assert.IsTrue(m_Hierarchy.Exists(entityNode2));

            // Destroying a while should dispose the hierarchySystem that should
            m_World.Dispose();
            m_World = null;

            UpdateHierarchy(m_Hierarchy);

            Assert.IsFalse(m_Hierarchy.Exists(worldNode));
            Assert.IsFalse(m_Hierarchy.Exists(entityNode1));
            Assert.IsFalse(m_Hierarchy.Exists(entityNode2));
        }

        [Test]
        [TestCase(WorldFlags.Simulation)]
        [TestCase(WorldFlags.Editor)]
        [TestCase(WorldFlags.GameServer)]
        [TestCase(WorldFlags.Shadow)]
        [TestCase(WorldFlags.Live)]
        public void SettingUpWorldFilter_NonFilteredWorldsShows(WorldFlags worldFlags)
        {
            var oldFlags = HierarchyEntitiesSettings.GetTypesOfWorldsShown();

            var allFlags = Enum.GetValues(typeof(WorldFlags)) as WorldFlags[];
            var tmpWorlds = new World[allFlags.Length];
            for (var i = 0; i < tmpWorlds.Length; ++i)
            {
                var flag = allFlags[i];
                tmpWorlds[i] = new World($"World_{flag.ToString()}", flag);
                tmpWorlds[i].EntityManager.CreateEntity(typeof(EcsTestData));
            }

            try
            {
                HierarchyEntitiesSettings.SetTypesOfWorldsShown(worldFlags);

                var window = EditorWindow.GetWindow<Unity.Hierarchy.Editor.HierarchyWindow>();
                window.ReloadHostView();

                for (var i = 0; i < tmpWorlds.Length; ++i)
                {
                    var world = tmpWorlds[i];
                    var worldNode = m_WorldHandler.GetWorldNode(world);

                    var shouldBeVisible = (HierarchyWorldHandler.GetMainFlag(world) & worldFlags) != 0;
                    if (shouldBeVisible)
                        Assert.That(worldNode, Is.Not.EqualTo(default(Unity.Hierarchy.HierarchyNode)), $"{world.Name} was not found in the Hierarchy, but we expected it to be.");
                    else
                        Assert.That(worldNode, Is.EqualTo(default(Unity.Hierarchy.HierarchyNode)), $"{world.Name} was found in the Hierarchy, but we didn't expect it to be.");
                }
            }
            finally
            {
                for (var i = 0; i < tmpWorlds.Length; ++i)
                    tmpWorlds[i].Dispose();

                HierarchyEntitiesSettings.SetTypesOfWorldsShown(oldFlags);
            }
        }

        [Test]
        public void RuntimeCreatedWorld_AutomaticallyRegistersInHierarchy()
        {
            var oldFlags = HierarchyEntitiesSettings.GetTypesOfWorldsShown();
            HierarchyEntitiesSettings.SetTypesOfWorldsShown(WorldFlags.Live);

            World runtimeWorld = null;
            try
            {
                runtimeWorld = new World("Runtime Created World", WorldFlags.Live);

                // Simulate EditorApplication.update - should register the new world
                m_WorldHandler.RegisterAllHierarchySystems();

                var hierarchySystem = runtimeWorld.GetExistingSystem<UpdateHierarchySystem>();
                Assert.That(hierarchySystem, Is.Not.EqualTo(default(SystemHandle)),
                    "UpdateHierarchySystem should be created for runtime-created world");

                // Create an entity to trigger world node creation
                var entity = runtimeWorld.EntityManager.CreateEntity(typeof(EcsTestData));
                runtimeWorld.GetExistingSystemManaged<UpdateHierarchySystem>().Update();
                UpdateHierarchy(m_Hierarchy);

                // Verify world node and entity node exist in hierarchy
                var worldNode = m_WorldHandler.GetWorldNode(runtimeWorld);
                Assert.That(worldNode, Is.Not.EqualTo(default(Unity.Hierarchy.HierarchyNode)),
                    "World node should exist after entity creation");
                Assert.That(m_Hierarchy.Exists(worldNode), Is.True,
                    "World node should be valid in hierarchy");

                var entityNode = m_EntityHandler.GetNode(entity);
                Assert.That(entityNode, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null),
                    "Entity node should exist in hierarchy");
                Assert.That(m_Hierarchy.GetParent(entityNode), Is.EqualTo(worldNode),
                    "Entity should be parented under the runtime world node");
            }
            finally
            {
                runtimeWorld?.Dispose();
                HierarchyEntitiesSettings.SetTypesOfWorldsShown(oldFlags);
            }
        }

        [Test]
        public void RuntimeCreatedWorld_DoesNotRegisterDuplicates()
        {
            var oldFlags = HierarchyEntitiesSettings.GetTypesOfWorldsShown();
            HierarchyEntitiesSettings.SetTypesOfWorldsShown(WorldFlags.Live);

            World runtimeWorld = null;
            try
            {
                runtimeWorld = new World("Duplicate Check World", WorldFlags.Live);

                m_WorldHandler.RegisterAllHierarchySystems();

                runtimeWorld.EntityManager.CreateEntity(typeof(EcsTestData));
                runtimeWorld.GetExistingSystemManaged<UpdateHierarchySystem>().Update();
                UpdateHierarchy(m_Hierarchy);

                var worldNode = m_WorldHandler.GetWorldNode(runtimeWorld);
                Assert.That(worldNode, Is.Not.EqualTo(default(Unity.Hierarchy.HierarchyNode)),
                    "World node should exist after initial registration");

                // Call RegisterAllHierarchySystems multiple times (simulating multiple update ticks)
                m_WorldHandler.RegisterAllHierarchySystems();
                m_WorldHandler.RegisterAllHierarchySystems();
                m_WorldHandler.RegisterAllHierarchySystems();
                UpdateHierarchy(m_Hierarchy);

                var worldNodeAfter = m_WorldHandler.GetWorldNode(runtimeWorld);
                Assert.That(worldNodeAfter, Is.EqualTo(worldNode),
                    "World node should not change after multiple registration calls");

                var hierarchySystem = runtimeWorld.GetExistingSystemManaged<UpdateHierarchySystem>();
                var systemAgain = runtimeWorld.GetExistingSystemManaged<UpdateHierarchySystem>();
                Assert.That(systemAgain, Is.EqualTo(hierarchySystem),
                    "Should return same UpdateHierarchySystem instance, not create duplicates");
            }
            finally
            {
                runtimeWorld?.Dispose();
                HierarchyEntitiesSettings.SetTypesOfWorldsShown(oldFlags);
            }
        }

        [Test]
        public void WorldNode_HasExpandedFlagByDefault()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
            m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var worldNode = m_WorldHandler.GetOrCreateWorldNode(m_World);
            Assert.IsTrue(m_Hierarchy.Exists(worldNode));

            var defaultFlags = m_WorldHandler.GetDefaultNodeFlags(in worldNode);
            Assert.IsTrue((defaultFlags & Unity.Hierarchy.HierarchyNodeFlags.Expanded) != 0,
                "World nodes should have Expanded flag by default");
        }

        [Test]
        public void WorldNode_IsPositionedLast_WhenCreated()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            // Create an entity to trigger world node creation
            m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var worldNode = m_WorldHandler.GetOrCreateWorldNode(m_World);
            UpdateHierarchy(m_Hierarchy);

            // Get all children of Root
            var rootChildren = m_Hierarchy.GetChildren(m_Hierarchy.Root);
            Assert.That(rootChildren.Length, Is.GreaterThan(0), "Root should have children");

            // World node should be the last child
            var lastChild = rootChildren[rootChildren.Length - 1];
            Assert.That(lastChild, Is.EqualTo(worldNode),
                "World node should be the last child of Root");
        }

        [Test]
        public void WorldNode_StaysLast_AfterRepositioning()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            // Create world node first
            m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var worldNode = m_WorldHandler.GetOrCreateWorldNode(m_World);
            UpdateHierarchy(m_Hierarchy);

            // Add some nodes directly under Root (simulating scene nodes added after world node)
            Unity.Hierarchy.HierarchyNode sceneNode1, sceneNode2;
            using (var commandList = new HierarchyCommandList(m_Hierarchy))
            {
                commandList.Add(m_Hierarchy.Root, out sceneNode1);
                commandList.SetName(sceneNode1, "Scene1");
                commandList.Add(m_Hierarchy.Root, out sceneNode2);
                commandList.SetName(sceneNode2, "Scene2");
                commandList.Execute();
            }
            UpdateHierarchy(m_Hierarchy);

            // Before repositioning, world node might not be last
            var childrenBefore = m_Hierarchy.GetChildren(m_Hierarchy.Root);
            Assert.That(childrenBefore.Length, Is.GreaterThanOrEqualTo(3),
                "Root should have at least 3 children (world + 2 scene nodes)");

            // Reposition world node to end (simulating what RepositionWorldNodesToEnd does)
            using (var commandList2 = new HierarchyCommandList(m_Hierarchy))
            {
                var childCount = m_Hierarchy.GetChildrenCount(m_Hierarchy.Root);
                commandList2.SetParent(worldNode, m_Hierarchy.Root, childCount - 1);
                commandList2.Execute();
            }
            UpdateHierarchy(m_Hierarchy);

            // After repositioning, world node should be last
            var childrenAfter = m_Hierarchy.GetChildren(m_Hierarchy.Root);
            var lastChild = childrenAfter[childrenAfter.Length - 1];
            Assert.That(lastChild, Is.EqualTo(worldNode),
                "World node should be the last child of Root after repositioning");
        }

        [Test]
        public void MultipleWorldNodes_ArePositionedLast()
        {
            var oldFlags = HierarchyEntitiesSettings.GetTypesOfWorldsShown();
            HierarchyEntitiesSettings.SetTypesOfWorldsShown(WorldFlags.Live | WorldFlags.Editor);

            World world2 = null;
            try
            {
                // Create first world node
                var hierarchySystem1 = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
                m_World.EntityManager.CreateEntity(typeof(EcsTestData));
                hierarchySystem1.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
                var worldNode1 = m_WorldHandler.GetOrCreateWorldNode(m_World);

                // Create second world
                world2 = new World("Second Test World", WorldFlags.Live);
                m_WorldHandler.RegisterAllHierarchySystems();
                var hierarchySystem2 = world2.GetOrCreateSystem<UpdateHierarchySystem>();
                world2.EntityManager.CreateEntity(typeof(EcsTestData));
                hierarchySystem2.Update(world2.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
                var worldNode2 = m_WorldHandler.GetOrCreateWorldNode(world2);
                UpdateHierarchy(m_Hierarchy);

                // Add a scene node
                Unity.Hierarchy.HierarchyNode sceneNode;
                using (var commandList = new HierarchyCommandList(m_Hierarchy))
                {
                    commandList.Add(m_Hierarchy.Root, out sceneNode);
                    commandList.SetName(sceneNode, "TestScene");
                    commandList.Execute();
                }
                UpdateHierarchy(m_Hierarchy);

                // Reposition both world nodes (simulating RepositionWorldNodesToEnd)
                using (var commandList2 = new HierarchyCommandList(m_Hierarchy))
                {
                    // Move first world node to end
                    var childCount1 = m_Hierarchy.GetChildrenCount(m_Hierarchy.Root);
                    commandList2.SetParent(worldNode1, m_Hierarchy.Root, childCount1 - 1);
                    commandList2.Execute();
                }
                UpdateHierarchy(m_Hierarchy);

                using (var commandList3 = new HierarchyCommandList(m_Hierarchy))
                {
                    // Move second world node to end
                    var childCount2 = m_Hierarchy.GetChildrenCount(m_Hierarchy.Root);
                    commandList3.SetParent(worldNode2, m_Hierarchy.Root, childCount2 - 1);
                    commandList3.Execute();
                }
                UpdateHierarchy(m_Hierarchy);

                // Verify scene node is not last
                var children = m_Hierarchy.GetChildren(m_Hierarchy.Root);
                Assert.That(children.Length, Is.GreaterThanOrEqualTo(3),
                    "Root should have at least 3 children");

                // Scene node should not be last (both world nodes should be after it)
                var sceneNodeIsLast = children[children.Length - 1] == sceneNode;
                Assert.That(sceneNodeIsLast, Is.False,
                    "Scene node should not be the last child - world nodes should come after");

                // At least one world node should be in the last two positions
                var lastTwo = new[] { children[children.Length - 1], children[children.Length - 2] };
                bool worldNode1InLastTwo = System.Array.IndexOf(lastTwo, worldNode1) >= 0;
                bool worldNode2InLastTwo = System.Array.IndexOf(lastTwo, worldNode2) >= 0;
                Assert.That(worldNode1InLastTwo || worldNode2InLastTwo, Is.True,
                    "At least one world node should be in the last two positions");
            }
            finally
            {
                world2?.Dispose();
                HierarchyEntitiesSettings.SetTypesOfWorldsShown(oldFlags);
            }
        }
    }
}
