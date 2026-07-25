using NUnit.Framework;
using Unity.Entities.Hybrid.Tests;
using UnityEditor;
using UnityEngine.LowLevel;
using UnityEngine.Pool;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemInspectorTests
    {
        PlayerLoopSystem m_PrevPlayerLoop;
        TestWithCustomDefaultGameObjectInjectionWorld m_CustomInjectionWorld;
        World m_World;
        SystemInspectorTestSystem m_SystemInspectorTestSystem;
        SystemScheduleTestSystem1 m_SystemInspectorTestSystem1;
        SystemScheduleTestSystem2 m_SystemInspectorTestSystem2;

        WorldProxyManager m_WorldProxyManager;
        WorldProxy m_WorldProxy;

        partial class SystemInspectorTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                foreach (var (data1, data2) in
                         SystemAPI.Query<RefRW<SystemScheduleTestData1>, RefRO<SystemScheduleTestData2>>()) { }

                foreach (var data1 in
                         SystemAPI.Query<RefRO<SystemScheduleTestData1>>().WithNone<SystemScheduleTestData2>()) { }
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_CustomInjectionWorld.Setup();
            DefaultWorldInitialization.Initialize("SystemInspectorTestWorld", false);
            m_World = World.DefaultGameObjectInjectionWorld;

            m_SystemInspectorTestSystem = m_World.GetOrCreateSystemManaged<SystemInspectorTestSystem>();
            m_SystemInspectorTestSystem1 = m_World.GetOrCreateSystemManaged<SystemScheduleTestSystem1>();
            m_SystemInspectorTestSystem2 = m_World.GetOrCreateSystemManaged<SystemScheduleTestSystem2>();

            m_World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_SystemInspectorTestSystem1);
            m_World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_SystemInspectorTestSystem2);
            m_World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_SystemInspectorTestSystem);

            m_WorldProxyManager = new WorldProxyManager();
            m_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            m_WorldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(m_World);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_CustomInjectionWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
            if (EditorWindow.HasOpenInstances<SystemScheduleWindow>())
            {
                EditorWindow.GetWindow<SystemScheduleWindow>().Close();
            }
        }

        [Test]
        public void SystemInspector_QueriesTab_MatchingQueries()
        {
            var systemQueriesTab = new SystemQueriesTab(m_World, new SystemProxy(m_SystemInspectorTestSystem, m_WorldProxy));

            Assert.That(systemQueriesTab.QueriesFromSystem.Length, Is.EqualTo(2));
            // TODO(ECSB-387): Queries can have multiple descriptions
            Assert.That(systemQueriesTab.QueriesFromSystem[0].GetEntityQueryDescs()[0].All, Is.EquivalentTo(new[] { ComponentType.ReadWrite<SystemScheduleTestData1>(), ComponentType.ReadOnly<SystemScheduleTestData2>() }));
            Assert.That(systemQueriesTab.QueriesFromSystem[1].GetEntityQueryDescs()[0].All, Is.EquivalentTo(new[] { ComponentType.ReadOnly<SystemScheduleTestData1>()}));
            // `None` components are silently given `ReadOnly` access during query creation
            Assert.That(systemQueriesTab.QueriesFromSystem[1].GetEntityQueryDescs()[0].None, Is.EquivalentTo(new[] { ComponentType.ReadOnly<SystemScheduleTestData2>()}));
        }

        [Test]
        public void SystemInspector_QueriesTab_MatchingEntities()
        {
            var systemEntities = new SystemEntities(m_World, new SystemProxy(m_SystemInspectorTestSystem, m_WorldProxy));
            Assert.That(systemEntities.EntitiesFromQueries.Count, Is.EqualTo(2));
            Assert.That(systemEntities.EntitiesFromQueries[0].SystemProxy.NicifiedDisplayName, Is.EqualTo("System Inspector Tests | System Inspector Test System"));
            Assert.That(systemEntities.EntitiesFromQueries[0].QueryOrder, Is.EqualTo(1));
            Assert.That(systemEntities.EntitiesFromQueries[0].World.Name, Is.EqualTo("SystemInspectorTestWorld"));
            Assert.That(systemEntities.EntitiesFromQueries[0].Query.GetEntityQueryDescs()[0].All,
                Is.EquivalentTo(new[]
                {
                    ComponentType.ReadWrite<SystemScheduleTestData1>(),
                    ComponentType.ReadOnly<SystemScheduleTestData2>()
                }));

            Assert.That(systemEntities.EntitiesFromQueries[1].SystemProxy.NicifiedDisplayName, Is.EqualTo("System Inspector Tests | System Inspector Test System"));
            Assert.That(systemEntities.EntitiesFromQueries[1].QueryOrder, Is.EqualTo(2));
            Assert.That(systemEntities.EntitiesFromQueries[1].World.Name, Is.EqualTo("SystemInspectorTestWorld"));
            Assert.That(systemEntities.EntitiesFromQueries[1].Query.GetEntityQueryDescs()[0].All,
                Is.EquivalentTo(new[]
                {
                    ComponentType.ReadOnly<SystemScheduleTestData1>()
                }));
            Assert.That(systemEntities.EntitiesFromQueries[1].Query.GetEntityQueryDescs()[0].None,
                Is.EquivalentTo(new[]
                {
                    // `None` components are silently given `ReadOnly` access during query creation
                    ComponentType.ReadOnly<SystemScheduleTestData2>()
                }));
        }

        [Test]
        public void SystemInspector_DependenciesTab_MatchingComponents()
        {
            var systemDependenciesTab = new SystemDependenciesTab(new SystemProxy(m_SystemInspectorTestSystem, m_WorldProxy));

            using var _ = ListPool<ComponentViewData>.Get(out var readingComponents);
            using var __ = ListPool<ComponentViewData>.Get(out var writingComponents);
            systemDependenciesTab.SystemProxy.FillListWithJobDependencyForReadingSystems(readingComponents);
            systemDependenciesTab.SystemProxy.FillListWithJobDependencyForWritingSystems(writingComponents);

            Assume.That(readingComponents.Count, Is.EqualTo(1));
            Assume.That(writingComponents.Count, Is.EqualTo(1));

            Assert.That(readingComponents[0].InComponentType, Is.EqualTo(typeof(SystemScheduleTestData2)));
            Assert.That(writingComponents[0].InComponentType, Is.EqualTo(typeof(SystemScheduleTestData1)));
        }
    }
}

