#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Scenes.Editor.Tests
{
    class CompanionComponentSubSceneTests : SubSceneConversionAndBakingTests
    {
        // Distinctive value set on the authoring component so we can assert it survives bake/load.
        const int ExpectedValue = 42;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_Settings.Setup(true);
            base.OneTimeSetUp();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.OneTimeTearDown();
            m_Settings.TearDown();
        }

        SubScene CreateCompanionSubScene()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject("CompanionAuthoring");
                // CompanionComponentTestAuthoring (Unity.Entities.TestComponents) is registered as a companion
                // type via [InitializeOnLoadMethod] so the registration also reaches the AssetImportWorker that
                // bakes the subscene.
                var companion = go.AddComponent<CompanionComponentTestAuthoring>();
                companion.Value = ExpectedValue;
                return new List<GameObject> { go };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);
            return subScene;
        }

        static List<CompanionComponentTestAuthoring> CaptureCompanions(EntityManager em, EntityQuery query, int expectedCount)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            Assume.That(entities.Length, Is.EqualTo(expectedCount),
                $"expected {expectedCount} CompanionLink entit{(expectedCount == 1 ? "y" : "ies")}");

            var companions = new List<CompanionComponentTestAuthoring>(entities.Length);
            for (int i = 0; i < entities.Length; i++)
            {
                companions.Add(CompanionComponentTestFixture
                    .AssertCompanionReadersAgree<CompanionComponentTestAuthoring>(em, entities[i]));
            }
            return companions;
        }

        static void AssertAuthoredValues(IEnumerable<CompanionComponentTestAuthoring> companions)
        {
            foreach (var companion in companions)
                Assert.AreEqual(ExpectedValue, companion.Value, "companion.Value");
        }

        // Use Resources.FindObjectsOfTypeAll, not Object.FindObjectsByType — companion GameObjects
        // live in a hidden preview scene (CompanionGameObjectUtility.CreateCompanionScenes), which
        // FindObjectsByType is not guaranteed to traverse; FindObjectsOfTypeAll captures them.
        static int CountAllCompanions() => Resources.FindObjectsOfTypeAll<CompanionComponentTestAuthoring>().Length;

        [UnityTest]
        public IEnumerator SubScene_WithCompanionComponent_LoadUnloadCycle_DoesNotLeak()
        {
            var subScene = CreateCompanionSubScene();
            const int iterations = 3;

            using (var world = TestWorldSetup.CreateEntityWorld("World", TestWorldSetup.TestWorldSystemFilterFlags.Default))
            using (var query = world.EntityManager.CreateEntityQuery(typeof(CompanionLink)))
            {
                int baseline = CountAllCompanions();

                for (int iter = 0; iter < iterations; iter++)
                {
                    var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                        new SceneSystem.LoadParameters
                        {
                            Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                        });
                    world.Update();
                    Assume.That(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), Is.True,
                        $"Iteration {iter}: scene failed to load");
                    Assume.That(CountAllCompanions(), Is.EqualTo(baseline + 1),
                        $"Iteration {iter}: expected exactly one new companion after load");

                    AssertAuthoredValues(CaptureCompanions(world.EntityManager, query, 1));

                    SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
                    world.Update();
                    yield return null; // flush deferred Object.Destroy in play-mode runs

                    Assume.That(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), Is.False,
                        $"Iteration {iter}: scene failed to unload");
                    Assert.AreEqual(baseline, CountAllCompanions(),
                        $"Iteration {iter}: companion count did not return to baseline after unload (leak).");
                }
            }
        }

        [UnityTest]
        public IEnumerator SubScene_WithCompanionComponent_NewInstances_UnloadDestroysOnlyThatInstance()
        {
            var subScene = CreateCompanionSubScene();
            const int instances = 3;
            var sceneEntities = new Entity[instances];

            using (var world = TestWorldSetup.CreateEntityWorld("World", TestWorldSetup.TestWorldSystemFilterFlags.Default))
            using (var query = world.EntityManager.CreateEntityQuery(typeof(CompanionLink)))
            {
                int baseline = CountAllCompanions();

                for (int i = 0; i < instances; i++)
                {
                    sceneEntities[i] = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                        new SceneSystem.LoadParameters
                        {
                            Flags = SceneLoadFlags.NewInstance | SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                        });
                }
                world.Update();

                for (int i = 0; i < instances; i++)
                    Assume.That(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntities[i]), Is.True,
                        $"Instance {i}: scene failed to load");
                Assume.That(CountAllCompanions(), Is.EqualTo(baseline + instances),
                    "expected one new companion per loaded instance");

                var companions = CaptureCompanions(world.EntityManager, query, instances);
                CollectionAssert.AllItemsAreUnique(companions,
                    "Instances unexpectedly share companion instances.");
                AssertAuthoredValues(companions);

                // Unload in non-sequential order to make accidental "destroy them all" or "destroy by index"
                // bugs surface more readably than a 0,1,2 sweep would.
                int[] unloadOrder = { 1, 0, 2 };
                for (int step = 0; step < unloadOrder.Length; step++)
                {
                    SceneSystem.UnloadScene(world.Unmanaged, sceneEntities[unloadOrder[step]]);
                    world.Update();
                    yield return null; // flush deferred Object.Destroy in play-mode runs

                    int expected = baseline + instances - (step + 1);
                    Assert.AreEqual(expected, CountAllCompanions(),
                        $"After unloading instance {unloadOrder[step]}: expected {expected} companions.");
                }
            }
        }
    }
}
#endif
