using System.Collections;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Unity.Entities.Hybrid.Tests;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemScheduleWindowIntegrationTests
    {
        PlayerLoopSystem m_PrevPlayerLoop;
        TestWithCustomDefaultGameObjectInjectionWorld m_CustomInjectionWorld;
        // m_Configuration.ShowUnityNamespaceSystems is loaded from EditorUserSettings and
        // persists across editor sessions, so each test captures the user's setting in SetUp,
        // forces it ON for a known starting state, and restores the captured value in TearDown.
        bool m_PreviousShowUnityNamespaceSystems;
        World m_DefaultWorld;
        World m_TestWorld;
        ComponentSystemGroup m_TestSystemGroup;
        ComponentSystemBase m_TestSystem1;
        ComponentSystemBase m_TestSystem2;
        SystemScheduleWindow m_SystemScheduleWindow;
        WorldProxy m_WorldProxy;
        Scene m_Scene;
        SubScene m_SubScene;
        GameObject m_SubSceneRoot;
        bool m_PreviousLiveConversionState;
        const string k_SystemScheduleEditorWorld = "SystemWindow Test World";
        const string k_SystemScheduleTestWorld = "SystemScheduleTestWorld";
        const string k_AssetsFolderRoot = "Assets";
        const string k_SceneExtension = "unity";
        const string k_SceneName = "SystemsWindowTests";
        const string k_SubSceneName = "SubScene";
        [SerializeField]
        string m_TestAssetsDirectory;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!EditorApplication.isPlaying)
            {
                m_PreviousLiveConversionState = LiveConversionEditorSettings.LiveConversionEnabled;
                LiveConversionEditorSettings.LiveConversionEnabled = true;

                var guid = AssetDatabase.CreateFolder(k_AssetsFolderRoot, nameof(SystemScheduleWindowIntegrationTests));
                m_TestAssetsDirectory = AssetDatabase.GUIDToAssetPath(guid);

                m_Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var mainScenePath = Path.Combine(m_TestAssetsDirectory, $"{k_SceneName}.{k_SceneExtension}");

                EditorSceneManager.SaveScene(m_Scene, mainScenePath);
                SceneManager.SetActiveScene(m_Scene);

                // Temp context GameObject, necessary to create an empty subscene
                var targetGO = new GameObject(k_SubSceneName);

                var subsceneArgs = new SubSceneContextMenu.NewSubSceneArgs(targetGO, m_Scene, SubSceneContextMenu.NewSubSceneMode.EmptyScene);
                m_SubScene = SubSceneContextMenu.CreateNewSubScene(targetGO.name, subsceneArgs, InteractionMode.AutomatedAction);
                m_SubSceneRoot = m_SubScene.gameObject;

                UnityEngine.Object.DestroyImmediate(targetGO);
                EditorSceneManager.SaveScene(m_Scene);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_SubSceneRoot);
            AssetDatabase.DeleteAsset(m_TestAssetsDirectory);
            SceneWithBuildConfigurationGUIDs.ClearBuildSettingsCache();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            LiveConversionEditorSettings.LiveConversionEnabled = m_PreviousLiveConversionState;
        }

        [SetUp]
        public void SetUp()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_CustomInjectionWorld.Setup();
            DefaultWorldInitialization.Initialize(k_SystemScheduleEditorWorld, true);
            m_DefaultWorld = World.DefaultGameObjectInjectionWorld;

            CreateTestSystems(m_DefaultWorld);
            m_SystemScheduleWindow = !EditorApplication.isPlaying ? SystemScheduleTestUtilities.CreateSystemsWindow() : EditorWindow.GetWindow<SystemScheduleWindow>();
            m_SystemScheduleWindow.WorldProxyManager.RebuildWorldProxyForGivenWorld(m_DefaultWorld);
            m_SystemScheduleWindow.SelectedWorld = m_DefaultWorld;
            m_WorldProxy = m_SystemScheduleWindow.WorldProxyManager.GetWorldProxyForGivenWorld(m_DefaultWorld);

            m_PreviousShowUnityNamespaceSystems = m_SystemScheduleWindow.m_Configuration.ShowUnityNamespaceSystems;
            m_SystemScheduleWindow.m_Configuration.ShowUnityNamespaceSystems = true;
            var setupTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            if (setupTreeView != null)
                setupTreeView.ShowUnityNamespaceSystems = true;
        }

        [TearDown]
        public void TearDown()
        {
            m_CustomInjectionWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);

            if (m_SystemScheduleWindow != null && m_SystemScheduleWindow.m_Configuration != null)
                m_SystemScheduleWindow.m_Configuration.ShowUnityNamespaceSystems = m_PreviousShowUnityNamespaceSystems;

            if (!EditorApplication.isPlaying)
                SystemScheduleTestUtilities.DestroySystemsWindow(m_SystemScheduleWindow);

            if (EditorWindow.HasOpenInstances<SystemScheduleWindow>())
                EditorWindow.GetWindow<SystemScheduleWindow>().Close();

            if (m_TestWorld is { IsCreated: true })
                m_TestWorld.Dispose();
        }

        void CreateTestSystems(World world)
        {
            m_TestSystemGroup = world.GetOrCreateSystemManaged<SystemScheduleTestGroup>();
            m_TestSystem1 = world.GetOrCreateSystemManaged<SystemScheduleTestSystem1>();
            m_TestSystem2 = world.GetOrCreateSystemManaged<SystemScheduleTestSystem2>();
            m_TestSystemGroup.AddSystemToUpdateList(m_TestSystem1);
            m_TestSystemGroup.AddSystemToUpdateList(m_TestSystem2);
            m_TestSystemGroup.SortSystems();
            world.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_TestSystemGroup);
            world.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();

            if (m_SystemScheduleWindow != null)
                m_SystemScheduleWindow.SelectedWorld = world;
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForSingleComponent()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            
            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("c=SystemScheduleTestData1");
            
            while (searchView.results.pending)
                yield return null; 

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.m_ListViewFilteredItems.Count, Is.EqualTo(1));
            Assert.That(systemTreeView.m_ListViewFilteredItems.FirstOrDefault().data?.Node.Name, Is.EqualTo("System Schedule Test System 1"));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForSystemName()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            
            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("SystemScheduleTestSystem1");

            while (searchView.results.pending)
                yield return null; 
            
            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.m_ListViewFilteredItems.Count, Is.EqualTo(1));
            Assert.That(systemTreeView.m_ListViewFilteredItems.FirstOrDefault().data?.Node.Name, Is.EqualTo("System Schedule Test System 1"));

            var result = m_SystemScheduleWindow.WorldProxyManager.GetWorldProxyForGivenWorld(m_DefaultWorld);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.AllSystems.Any(s => s.NicifiedDisplayName.Equals("System Schedule Test System 1")), Is.True);
            Assert.That(result.AllSystems.Any(s => s.NicifiedDisplayName.Equals("System Schedule Test System 2")), Is.True);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NonStandardRootSystem_SearchForSystemName()
        {
            var rootSystemGroup = m_DefaultWorld.CreateSystemManaged<ManualCreationSystemGroup>();
            var testPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(rootSystemGroup, ref testPlayerLoop,
                typeof(UnityEngine.PlayerLoop.EarlyUpdate.XRUpdate)); // random player loop stage outside of where DOTS usually puts things
            PlayerLoop.SetPlayerLoop(testPlayerLoop);

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(ManualCreationSystemGroup));
            
            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("ManualCreationSystemGroup");
            
            while (searchView.results.pending)
                yield return null;
            
            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.m_ListViewFilteredItems.Count, Is.EqualTo(1));
            Assert.That(systemTreeView.m_ListViewFilteredItems.FirstOrDefault().data?.Node.Name, Is.EqualTo("Manual Creation System Group"));

            var result = m_SystemScheduleWindow.WorldProxyManager.GetWorldProxyForGivenWorld(m_DefaultWorld);
            Assert.That(result, Is.Not.Null);
        }
        
        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForNamespace()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("ns=Unity.Entities.Editor.Tests");

            while (searchView.results.pending)
                yield return null;

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            var items = systemTreeView.m_ListViewFilteredItems;

            Assert.That(items.Count, Is.GreaterThan(0));
            var foundIndex = items.FindIndex(x => ((ComponentGroupNode) (x.data?.Node))?.FullName == typeof(SystemScheduleTestGroup).FullName);
            Assert.That(foundIndex, Is.Not.EqualTo(-1), $"Should find a system of type {nameof(SystemScheduleTestGroup)}, but failed to do so.");
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForParent()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("parent=SystemScheduleTestGroup");

            while (searchView.results.pending)
                yield return null;

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            var items = systemTreeView.m_ListViewFilteredItems;

            Assert.That(items.Count, Is.EqualTo(2), "Should find exactly 2 systems with SystemScheduleTestGroup as parent");
            Assert.That(items.Any(x => x.data?.Node.Name == "System Schedule Test System 1"), Is.True, "Should find SystemScheduleTestSystem1");
            Assert.That(items.Any(x => x.data?.Node.Name == "System Schedule Test System 2"), Is.True, "Should find SystemScheduleTestSystem2");
        }

        [Test]
        public void SystemScheduleWindow_VerifyScheduledSystemData()
        {
            var wsd = new ScheduledSystemData(m_TestSystem1, -1);
            Assert.That(wsd.Category, Is.EqualTo(SystemCategory.SystemBase));
            Assert.That(wsd.NicifiedDisplayName, Is.EqualTo("System Schedule Test System 1"));
            Assert.That(wsd.Managed, Is.EqualTo(m_TestSystem1));
            Assert.That(wsd.ChildCount, Is.EqualTo(0));
            Assert.That(wsd.ParentIndex, Is.EqualTo(-1));

            var wsdUnmanaged = new ScheduledSystemData(m_DefaultWorld.GetOrCreateSystem<SystemScheduleTestUnmanagedSystem>(), m_DefaultWorld, -1);
            Assert.That(wsdUnmanaged.Category, Is.EqualTo(SystemCategory.Unmanaged));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForNonExistingSystem()
        {
            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("raasdfasd");
            
            while (searchView.results.pending)
                yield return null;
            
            Assert.That(searchView.results.Count, Is.EqualTo(0));
        }

        [Test]
        public void SystemScheduleWindow_GetSelectedWorld()
        {
            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.EqualTo(k_SystemScheduleEditorWorld));
        }

        [Test]
        public void SystemScheduleWindow_WorldVersionChange()
        {
            var previousWorldVersion = m_DefaultWorld.Version;
            m_DefaultWorld.DestroySystemManaged(m_TestSystem1);
            Assert.That(m_DefaultWorld.Version, Is.Not.EqualTo(previousWorldVersion));

            previousWorldVersion = m_DefaultWorld.Version;
            m_DefaultWorld.GetOrCreateSystemManaged<SystemScheduleTestSystem1>();
            Assert.That(m_DefaultWorld.Version, Is.Not.EqualTo(previousWorldVersion));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemSearchView_ParseSearchString()
        {
            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText("c=Com1 C=Com2 randomName Sd:System1");
            
            while (searchView.results.pending)
                yield return null;            
            
            var parseResult = searchView.results;
            
            Assert.That(parseResult.context.searchText, Is.EqualTo( "c=Com1 C=Com2 randomName Sd:System1"));
            Assert.That(parseResult.context.searchWords, Is.EquivalentTo(new[] { "c=com1", "c=com2", "randomname" }));
            Assert.That(parseResult.context.textFilters, Is.EquivalentTo(new[] { "sd:system1" }));
            
            searchView.SetSearchText("c=   com1 C=Com2");
            
            while (searchView.results.pending)
                yield return null;
            
            Assert.That(parseResult.context.searchWords, Is.EquivalentTo(new[] { "c=", "com1", "c=com2" }));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemSearchView_ParseSearchString_EmptyString()
        {
            var searchView = m_SystemScheduleWindow.SystemSearchView;
            searchView.SetSearchText(string.Empty);
            
            while (searchView.results.pending)
                yield return null;            
            
            var parseResult = searchView.results;
            
            Assert.That(parseResult.context.searchText, Is.EqualTo(string.Empty));
            Assert.That(parseResult.context.searchWords, Is.Empty);
            Assert.That(parseResult.context.textFilters, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ContainsThisComponentType()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            var componentTypesInQuery1 = EntityQueryUtility.CollectComponentTypesFromSystemQuery(new SystemProxy(m_TestSystem1, m_WorldProxy));
            var typesInQuery1 = componentTypesInQuery1 as string[] ?? componentTypesInQuery1.ToArray();
            Assert.That(typesInQuery1.Contains(nameof(SystemScheduleTestData1)));
            Assert.That(typesInQuery1.Contains(nameof(SystemScheduleTestData2)));

            var componentTypesInQuery2 = EntityQueryUtility.CollectComponentTypesFromSystemQuery(new SystemProxy(m_TestSystem2, m_WorldProxy));
            var typesInQuery2 = componentTypesInQuery2 as string[] ?? componentTypesInQuery2.ToArray();
            Assert.That(!typesInQuery2.Contains(nameof(SystemScheduleTestData1)));
            Assert.That(!typesInQuery2.Contains(nameof(SystemScheduleTestData2)));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_CustomWorld()
        {
            var previousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

            m_TestWorld = new World(k_SystemScheduleTestWorld);
            CreateTestSystems(m_TestWorld);

            var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(m_TestWorld, ref playerLoop);
            PlayerLoop.SetPlayerLoop(playerLoop);

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.EqualTo(k_SystemScheduleTestWorld));
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out _), Is.True);

            m_TestWorld.Dispose();
            PlayerLoop.SetPlayerLoop(previousPlayerLoop);

            m_SystemScheduleWindow.Update();
            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.Not.Null);
            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.Not.EqualTo(k_SystemScheduleTestWorld));

            if (m_TestWorld is { IsCreated: true })
                m_TestWorld.Dispose();
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_HashCodeForTwoSystemsWithSameType()
        {
            var previousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_SystemScheduleWindow.m_Configuration.ShowAllWorlds = false;

            // Create test system in default world.
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            CreateTestSystems(defaultWorld);

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testSystemItemDefaultWorld), Is.True);
            var testSystemInDefaultWorldHash = testSystemItemDefaultWorld.Node.Hash;

            // Create test system in test world.
            m_TestWorld = new World(k_SystemScheduleTestWorld);
            CreateTestSystems(m_TestWorld);

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(m_TestWorld, ref playerLoop);
            PlayerLoop.SetPlayerLoop(playerLoop);

            // Wait a frame for the window to update.
            yield return null;

            m_SystemScheduleWindow.RebuildTreeView();

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testSystemItemTestWorld), Is.True);

            var testSystemInTestWorldHash = testSystemItemTestWorld.Node.Hash;

            if (m_TestWorld.IsCreated)
                m_TestWorld.Dispose();
            PlayerLoop.SetPlayerLoop(previousPlayerLoop);

            Assert.That(testSystemInDefaultWorldHash, Is.Not.EqualTo(testSystemInTestWorldHash));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ScheduleSystemInDifferentWorld()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var oldSystemCount = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().MultiColumnTreeViewElement.GetTreeCount();

            m_TestWorld = new World(k_SystemScheduleTestWorld);
            var managedSystem = m_TestWorld.GetOrCreateSystemManaged<SystemScheduleTestSystem>();

            try
            {
                var simulationSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
                simulationSystemGroup.AddSystemToUpdateList(managedSystem);
                simulationSystemGroup.SortSystems();

                yield return null;
                yield return null;

                var newSystemCount = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().MultiColumnTreeViewElement.GetTreeCount();

                Assert.That(oldSystemCount, Is.EqualTo(newSystemCount));
            }
            finally
            {
                if (managedSystem != null)
                    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().RemoveSystemFromUpdateList(managedSystem);

                if (m_TestWorld.IsCreated)
                    m_TestWorld.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_AllEnabled()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = true;
            m_TestSystem2.Enabled = true;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItemData.SystemToggleState.AllEnabled));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_MixedStateWithOneChildDisabled()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = false;
            m_TestSystem2.Enabled = true;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItemData.SystemToggleState.Mixed));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_MixedStateWithAllChildrenDisabled()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = false;
            m_TestSystem2.Enabled = false;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItemData.SystemToggleState.Mixed));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_ParentDisabled()
        {
            m_TestSystemGroup.Enabled = false;
            m_TestSystem1.Enabled = true;
            m_TestSystem2.Enabled = true;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItemData.SystemToggleState.Disabled));
        }

        [Test]
        public void SystemScheduleWindow_ShowUnityNamespaceSystems_DefaultsToTrue()
        {
            var config = new SystemScheduleWindow.SystemsWindowConfiguration();
            Assert.That(config.ShowUnityNamespaceSystems, Is.True);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ShowUnityNamespaceSystems_WhenEnabled_ShowsUnityNamespacedSystems()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var treeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(treeView.ShowUnityNamespaceSystems, Is.True, "Filter should default to on (show Unity systems).");
            Assert.That(treeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out _), Is.True,
                "Unity-namespaced test systems should be visible when the toggle is on.");
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ShowUnityNamespaceSystems_WhenDisabled_HidesUnityNamespacedSystems()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var treeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(treeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out _), Is.True,
                "Pre-condition: test group should be in the tree while the toggle is on.");

            m_SystemScheduleWindow.m_Configuration.ShowUnityNamespaceSystems = false;
            treeView.ShowUnityNamespaceSystems = false;
            m_SystemScheduleWindow.RebuildTreeView();
            yield return null;

            Assert.That(treeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out _), Is.False,
                "SystemScheduleTestGroup (Unity.Entities.Editor.Tests namespace) should be hidden when the toggle is off.");
            Assert.That(treeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestSystem1), out _), Is.False,
                "Child systems of a hidden group should also be filtered out.");
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ShowUnityNamespaceSystems_WhenDisabled_HidesGroupWhoseDescendantsAreAllUnityNamespaced()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var treeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            m_SystemScheduleWindow.m_Configuration.ShowUnityNamespaceSystems = false;
            treeView.ShowUnityNamespaceSystems = false;
            m_SystemScheduleWindow.RebuildTreeView();
            yield return null;

            // SystemScheduleTestGroup is Unity-namespaced and its only descendants in this fixture
            // (SystemScheduleTestSystem1/2) are also Unity-namespaced, so the whole subtree should
            // collapse out. We deliberately don't assert on SimulationSystemGroup here because the
            // default editor world may pull in non-Unity-namespaced systems from other loaded test
            // assemblies (e.g. DocCodeSamples), which would keep that ancestor visible.
            Assert.That(treeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out _), Is.False,
                "A Unity-namespaced group whose descendants are all Unity-namespaced should collapse when the filter is off.");
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_RepeatedSearch_MaintainsCorrectToggleState()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = true;
            m_TestSystem2.Enabled = false;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var searchView = m_SystemScheduleWindow.SystemSearchView;
            var treeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();

            searchView.SetWorld(m_DefaultWorld);

            // Repeatedly search and clear to verify enabled/disabled state remains consistent
            for (int iteration = 0; iteration < 5; iteration++)
            {
                searchView.SetSearchText("SystemScheduleTestSystem");

                while (searchView.results.pending)
                    yield return null;

                yield return null;

                var items = treeView.m_ListViewFilteredItems;
                TreeViewItemData<SystemTreeViewItemData> system1Item = default;
                TreeViewItemData<SystemTreeViewItemData> system2Item = default;

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].data?.Node.Name == "System Schedule Test System 1")
                        system1Item = items[i];
                    else if (items[i].data?.Node.Name == "System Schedule Test System 2")
                        system2Item = items[i];
                }

                Assert.That(system1Item.data, Is.Not.Null,
                    $"Iteration {iteration}: Should find System1 in search results");
                Assert.That(system2Item.data, Is.Not.Null,
                    $"Iteration {iteration}: Should find System2 in search results");

                Assert.That(system1Item.data.SystemProxy.Enabled, Is.True,
                    $"Iteration {iteration}: System1 enabled state should remain consistent");
                Assert.That(system2Item.data.SystemProxy.Enabled, Is.False,
                    $"Iteration {iteration}: System2 enabled state should remain consistent");

                Assert.That(system1Item.data.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItemData.SystemToggleState.AllEnabled),
                    $"Iteration {iteration}: System1 toggle state should be AllEnabled");
                Assert.That(system2Item.data.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItemData.SystemToggleState.Disabled),
                    $"Iteration {iteration}: System2 toggle state should be Disabled");

                searchView.SetSearchText("");

                while (searchView.results.pending)
                    yield return null;

                yield return null;
            }
        }

    }
}
