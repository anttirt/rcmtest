#if !UNITY_DISABLE_MANAGED_COMPONENTS
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Hybrid.EndToEnd.Tests;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests.Conversion;
using UnityEngine;

namespace Unity.Entities.Tests
{
    class CompanionGameObjectActivationTests : BakingTestFixture
    {
        static (GameObject go, CompanionComponentTestAuthoring component) GetCompanion(EntityManager em, Entity entity)
        {
            var component = CompanionComponentTestFixture.AssertCompanionReadersAgree<CompanionComponentTestAuthoring>(em, entity);
            return (component.gameObject, component);
        }

        [Test]
        public void CompanionGameObject_DisabledToggle_FlipsActive()
        {
            var authoring = CreateGameObject("source", typeof(CompanionComponentTestAuthoring));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new[] { authoring }, bakingSettings);

            TestUtilities.RegisterSystems(World, TestUtilities.SystemCategories.CompanionComponents);

            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            var entity = query.GetSingletonEntity();
            var (companion, companionComponent) = GetCompanion(m_Manager, entity);

            World.Update();
            Assume.That(companion.activeSelf, Is.True, "companion should activate on first update");

            const int cycles = 3;
            for (int i = 0; i < cycles; i++)
            {
                int prevOnEnable = companionComponent.OnEnableCount;
                int prevOnDisable = companionComponent.OnDisableCount;

                m_Manager.AddComponent<Disabled>(entity);
                World.Update();
                Assert.IsFalse(companion.activeSelf,
                    $"cycle {i}: companion should deactivate after Disabled added");
                Assert.AreEqual(prevOnEnable, companionComponent.OnEnableCount,
                    $"cycle {i}: OnEnable should not fire on deactivation");
                Assert.AreEqual(prevOnDisable + 1, companionComponent.OnDisableCount,
                    $"cycle {i}: OnDisable should fire exactly once on deactivation");

                m_Manager.RemoveComponent<Disabled>(entity);
                World.Update();
                Assert.IsTrue(companion.activeSelf,
                    $"cycle {i}: companion should reactivate after Disabled removed");
                Assert.AreEqual(prevOnEnable + 1, companionComponent.OnEnableCount,
                    $"cycle {i}: OnEnable should fire exactly once on reactivation");
                Assert.AreEqual(prevOnDisable + 1, companionComponent.OnDisableCount,
                    $"cycle {i}: OnDisable should not fire on reactivation");
            }
        }

        [Test]
        public void CompanionGameObject_PrefabToggle_FlipsActive()
        {
            var authoring = CreateGameObject("source", typeof(CompanionComponentTestAuthoring));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new[] { authoring }, bakingSettings);

            TestUtilities.RegisterSystems(World, TestUtilities.SystemCategories.CompanionComponents);

            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            var entity = query.GetSingletonEntity();
            var (companion, companionComponent) = GetCompanion(m_Manager, entity);

            World.Update();
            Assume.That(companion.activeSelf, Is.True, "companion should activate on first update");

            const int cycles = 3;
            for (int i = 0; i < cycles; i++)
            {
                int prevOnEnable = companionComponent.OnEnableCount;
                int prevOnDisable = companionComponent.OnDisableCount;

                m_Manager.AddComponent<Prefab>(entity);
                World.Update();
                Assert.IsFalse(companion.activeSelf,
                    $"cycle {i}: companion should deactivate after Prefab added");
                Assert.AreEqual(prevOnEnable, companionComponent.OnEnableCount,
                    $"cycle {i}: OnEnable should not fire on deactivation");
                Assert.AreEqual(prevOnDisable + 1, companionComponent.OnDisableCount,
                    $"cycle {i}: OnDisable should fire exactly once on deactivation");

                m_Manager.RemoveComponent<Prefab>(entity);
                World.Update();
                Assert.IsTrue(companion.activeSelf,
                    $"cycle {i}: companion should reactivate after Prefab removed");
                Assert.AreEqual(prevOnEnable + 1, companionComponent.OnEnableCount,
                    $"cycle {i}: OnEnable should fire exactly once on reactivation");
                Assert.AreEqual(prevOnDisable + 1, companionComponent.OnDisableCount,
                    $"cycle {i}: OnDisable should not fire on reactivation");
            }
        }

        [Test]
        public void CompanionGameObject_BulkActivation_AllCompanionsActivateInOneUpdate()
        {
            const int count = 10;
            var sources = new GameObject[count];
            for (int i = 0; i < count; i++)
                sources[i] = CreateGameObject($"src{i}", typeof(CompanionComponentTestAuthoring));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, sources, bakingSettings);

            TestUtilities.RegisterSystems(World, TestUtilities.SystemCategories.CompanionComponents);

            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            using var entities = query.ToEntityArray(Allocator.Temp);
            Assume.That(entities.Length, Is.EqualTo(count),
                "expected one companion entity per authoring GameObject");

            var companions = new (GameObject go, CompanionComponentTestAuthoring component)[count];
            var prevOnEnable = new int[count];
            for (int i = 0; i < count; i++)
            {
                companions[i] = GetCompanion(m_Manager, entities[i]);
                prevOnEnable[i] = companions[i].component.OnEnableCount;
            }

            // Companions are inactive after bake; CompanionGameObjectUpdateSystem.OnUpdate batches
            // them into a single GameObject.SetGameObjectsActive(..., true) call.
            for (int i = 0; i < count; i++)
                Assume.That(companions[i].go.activeSelf, Is.False, $"companion {i} should start inactive");

            World.Update();

            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(companions[i].go.activeSelf, $"companion {i} should be active after one Update");
                Assert.AreEqual(prevOnEnable[i] + 1, companions[i].component.OnEnableCount,
                    $"companion {i}: OnEnable should fire exactly once during bulk activation");
            }
        }
    }
}
#endif
