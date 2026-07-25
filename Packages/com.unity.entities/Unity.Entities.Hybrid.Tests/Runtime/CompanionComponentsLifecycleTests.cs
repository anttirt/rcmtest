#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Hybrid.EndToEnd.Tests;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests.Conversion;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    class CompanionComponentsLifecycleTests : BakingTestFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));
        }

        [UnityTest]
        public IEnumerator DestroyEntity_DestroysCompanionGameObject()
        {
            var authoring = CreateGameObject("source", typeof(ConversionTestCompanionComponent));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new[] { authoring }, bakingSettings);

            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            var entity = query.GetSingletonEntity();
            var companion = CompanionComponentTestFixture
                .AssertCompanionReadersAgree<ConversionTestCompanionComponent>(m_Manager, entity).gameObject;

            m_Manager.DestroyEntity(entity);
            yield return null; // flush deferred Object.Destroy in play-mode runs

            Assert.IsTrue(companion == null,
                "Companion GameObject was not destroyed when its entity was destroyed.");
        }

        [UnityTest]
        public IEnumerator WorldDispose_DestroysCompanionGameObjects()
        {
            const int count = 3;
            var sources = new GameObject[count];
            for (int i = 0; i < count; i++)
                sources[i] = CreateGameObject($"src{i}", typeof(ConversionTestCompanionComponent));

            GameObject[] companions;
            var testWorld = new World("LifecycleTestWorld");
            {
                using var blobAssetStore = new BlobAssetStore(128);
                var bakingSettings = MakeDefaultSettings();
                bakingSettings.BlobAssetStore = blobAssetStore;
                BakingUtility.BakeGameObjects(testWorld, sources, bakingSettings);

                var query = testWorld.EntityManager.CreateEntityQuery(typeof(CompanionLink));
                using var entities = query.ToEntityArray(Allocator.Temp);
                Assume.That(entities.Length, Is.EqualTo(count));

                companions = new GameObject[entities.Length];
                for (int i = 0; i < entities.Length; i++)
                    companions[i] = CompanionComponentTestFixture
                        .AssertCompanionReadersAgree<ConversionTestCompanionComponent>(testWorld.EntityManager, entities[i]).gameObject;
            }

            testWorld.Dispose();
            yield return null;

            for (int i = 0; i < companions.Length; i++)
                Assert.IsTrue(companions[i] == null,
                    $"Companion GameObject {i} survived world disposal.");
        }

        [UnityTest]
        public IEnumerator DestroyInstance_OnlyDestroysItsOwnCompanionGameObject()
        {
            var authoring = CreateGameObject("source", typeof(ConversionTestCompanionComponent));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new[] { authoring }, bakingSettings);

            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            var sourceEntity = query.GetSingletonEntity();
            var sourceCompanion = CompanionComponentTestFixture
                .AssertCompanionReadersAgree<ConversionTestCompanionComponent>(m_Manager, sourceEntity).gameObject;

            var instance1 = m_Manager.Instantiate(sourceEntity);
            var instance2 = m_Manager.Instantiate(sourceEntity);
            var companion1 = CompanionComponentTestFixture
                .AssertCompanionReadersAgree<ConversionTestCompanionComponent>(m_Manager, instance1).gameObject;
            var companion2 = CompanionComponentTestFixture
                .AssertCompanionReadersAgree<ConversionTestCompanionComponent>(m_Manager, instance2).gameObject;

            Assume.That(ReferenceEquals(companion1, companion2), Is.False,
                "Instances unexpectedly share a single companion GameObject.");
            Assume.That(ReferenceEquals(companion1, sourceCompanion), Is.False,
                "Instance unexpectedly shares the source's companion GameObject.");

            m_Manager.DestroyEntity(instance1);
            yield return null;

            Assert.IsTrue(companion1 == null,
                "Destroyed instance's companion GameObject was not destroyed.");
            Assert.IsFalse(companion2 == null,
                "Surviving instance's companion GameObject was incorrectly destroyed.");
            Assert.IsFalse(sourceCompanion == null,
                "Source's companion GameObject was incorrectly destroyed.");
        }

        [Test]
        public void RemoveCompanionLink_RemovesActiveCleanupTag()
        {
            var authoring = CreateGameObject("source", typeof(ConversionTestCompanionComponent));

            using var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;
            BakingUtility.BakeGameObjects(World, new[] { authoring }, bakingSettings);

            TestUtilities.RegisterSystems(World, TestUtilities.SystemCategories.CompanionComponents);

            var query = m_Manager.CreateEntityQuery(typeof(CompanionLink));
            var entity = query.GetSingletonEntity();

            World.Update();
            Assume.That(m_Manager.HasComponent<CompanionGameObjectActiveCleanup>(entity), Is.True,
                "Activation system did not add CompanionGameObjectActiveCleanup on first update.");

            m_Manager.RemoveComponent<CompanionLink>(entity);
            World.Update();

            Assert.IsFalse(m_Manager.HasComponent<CompanionGameObjectActiveCleanup>(entity),
                "Cleanup query did not remove orphaned CompanionGameObjectActiveCleanup tag after CompanionLink was removed.");
        }
    }
}
#endif
