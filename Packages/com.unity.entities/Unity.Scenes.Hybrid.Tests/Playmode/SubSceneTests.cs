using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
#if UNITY_EDITOR
using Unity.Scenes.Editor.Tests;
#endif
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Scenes.Hybrid.Tests.Playmode
{
    public partial class SubSceneTests : SubSceneTestFixture
    {
#if UNITY_EDITOR
        private TestLiveConversionSettings m_Settings;
#endif

        public SubSceneTests()
        {
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/Subscene/TestSubScene.unity";
            BuildScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/TestScene.unity";
            BuildSceneGUID = new Unity.Entities.Hash128("785a8fb7f3d8213b9b65da9d2c45c22b");
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
#if UNITY_EDITOR
            m_Settings.Setup(true);
#endif
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.TearDownOnce();
#if UNITY_EDITOR
            m_Settings.TearDown();
#endif
        }

#if false
        [UnityTest]
        public IEnumerator LoadMultipleSubscenes_Async_WithAssetBundles()
        {
            using (var worldA = CreateEntityWorld("World A"))
            using (var worldB = CreateEntityWorld("World B"))
            {

#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);

                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, PlayModeSceneGUID);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, PlayModeSceneGUID);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var initialContentFilesNumber = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, BuildSceneGUID);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, BuildSceneGUID);
#endif

                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene));
                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene));

                while (!SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene) ||
                       !SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene))
                {
                    worldA.Update();
                    worldB.Update();
                    yield return null;
                }

                var worldAEntities = worldA.EntityManager.GetAllEntities(worldA.UpdateAllocator.ToAllocator);
                var worldBEntities = worldB.EntityManager.GetAllEntities(worldB.UpdateAllocator.ToAllocator);
                using (worldAEntities)
                using (worldBEntities)
                {
                    Assert.AreEqual(worldAEntities.Length, worldBEntities.Length);
                }

                var worldAQuery = worldA.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                var worldBQuery = worldB.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                Assert.AreEqual(worldAQuery.CalculateEntityCount(), worldBQuery.CalculateEntityCount());
                Assert.AreEqual(1, worldAQuery.CalculateEntityCount());

                // Get Material on RenderMesh
                var sharedEntitiesA = worldAQuery.ToEntityArray(worldA.UpdateAllocator.ToAllocator);
                var sharedEntitiesB = worldBQuery.ToEntityArray(worldB.UpdateAllocator.ToAllocator);

                SharedWithMaterial sharedA;
                SharedWithMaterial sharedB;
                using (sharedEntitiesA)
                using (sharedEntitiesB)
                {
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    sharedA = worldA.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesA[0]);
                    sharedB = worldB.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesB[0]);
                    #pragma warning restore 0618
                }

                Assert.AreSame(sharedA.material, sharedB.material);
                Assert.IsTrue(sharedA.material != null, "sharedA.material != null");
#if !UNITY_EDITOR
                var contentFilesNumberAfterLoadingScene = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
#endif

                SceneSystem.UnloadScene(worldA.Unmanaged, worldAScene);
                SceneSystem.UnloadScene(worldB.Unmanaged, worldBScene);

                worldA.Update();
                worldB.Update();
#if !UNITY_EDITOR
                var contentFilesNumberAfterUnLoadingScene = contentFilesNumberAfterLoadingScene - initialContentFilesNumber;
                Assert.AreEqual(2, contentFilesNumberAfterUnLoadingScene);
#endif
            }
        }
#endif
    #if false
        [UnityTest]
        public IEnumerator LoadMultipleSubscenes_Blocking_WithAssetBundles()
        {
            using (var worldA = CreateEntityWorld("World A"))
            using (var worldB = CreateEntityWorld("World B"))
            {
                var sceneSectionStreamingSystemA = worldA.GetExistingSystemManaged<SceneSectionStreamingSystem>();
                var sceneSectionStreamingSystemB = worldA.GetExistingSystemManaged<SceneSectionStreamingSystem>();

                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);

                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, PlayModeSceneGUID, loadParams);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var initialContentFilesNumber = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, BuildSceneGUID, loadParams);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, BuildSceneGUID, loadParams);
#endif

                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene));
                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene));

                worldA.Update();
                while (!sceneSectionStreamingSystemA.AllStreamsComplete)
                {
                    worldA.Update();
                    yield return null;
                }
                worldB.Update();
                while (!sceneSectionStreamingSystemB.AllStreamsComplete)
                {
                    worldB.Update();
                    yield return null;
                }

                Assert.IsTrue(SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene));
                Assert.IsTrue(SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene));

                var worldAEntities = worldA.EntityManager.GetAllEntities(worldA.UpdateAllocator.ToAllocator);
                var worldBEntities = worldB.EntityManager.GetAllEntities(worldB.UpdateAllocator.ToAllocator);
                using (worldAEntities)
                using (worldBEntities)
                {
                    Assert.AreEqual(worldAEntities.Length, worldBEntities.Length);
                }

                var worldAQuery = worldA.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                var worldBQuery = worldB.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                Assert.AreEqual(worldAQuery.CalculateEntityCount(), worldBQuery.CalculateEntityCount());
                Assert.AreEqual(1, worldAQuery.CalculateEntityCount());

                // Get Material on RenderMesh
                var sharedEntitiesA = worldAQuery.ToEntityArray(worldA.UpdateAllocator.ToAllocator);
                var sharedEntitiesB = worldBQuery.ToEntityArray(worldB.UpdateAllocator.ToAllocator);

                SharedWithMaterial sharedA;
                SharedWithMaterial sharedB;
                using (sharedEntitiesA)
                using (sharedEntitiesB)
                {
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    sharedA = worldA.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesA[0]);
                    sharedB = worldB.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesB[0]);
                    #pragma warning restore 0618
                }

                Assert.AreSame(sharedA.material, sharedB.material);
                Assert.IsTrue(sharedA.material != null, "sharedA.material != null");

#if !UNITY_EDITOR
                var contentFilesNumberAfterLoadingScene = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
#endif

                SceneSystem.UnloadScene(worldA.Unmanaged, worldAScene);
                worldA.Update();

                SceneSystem.UnloadScene(worldB.Unmanaged, worldBScene);
                worldB.Update();

#if !UNITY_EDITOR
                var contentFilesNumberAfterUnLoadingScene = contentFilesNumberAfterLoadingScene - initialContentFilesNumber;
                Assert.AreEqual(2, contentFilesNumberAfterUnLoadingScene);
#endif
            }
        }

        #endif

        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class Group1 : ComponentSystemGroup {}

        [UpdateBefore(typeof(Group1))]
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class Group2 : ComponentSystemGroup {}

        [UpdateInGroup(typeof(Group1))]
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class System1 : SystemBase
        {
            public static int CounterRead;
            protected override void OnUpdate()
            {
                CounterRead = s_Counter++;
            }
        }

        [UpdateInGroup(typeof(Group2))]
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class System2 : SystemBase
        {
            public static int CounterRead;
            protected override void OnUpdate()
            {
                CounterRead = s_Counter++;
            }
        }

        private static int s_Counter = 0;

    #if false
        [Test]
        public void PostProcessAfterLoadGroup_SupportsSystemGroups()
        {
            using (var world = CreateEntityWorld("World"))
            {
                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.NewInstance
                };
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
#endif
                world.Update();
                Assert.Greater(System1.CounterRead, System2.CounterRead);
            }
        }
    #endif

    #if false
        [Test]
        public void Load_EnableableComponentsHaveCorrectState()
        {
            using (var world = CreateEntityWorld("World"))
            {
                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.NewInstance
                };
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
#endif
                world.Update();
                using var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<SingletonTag1>()
                    .Build(world.EntityManager);
                Assert.IsTrue(query.TryGetSingletonEntity<SingletonTag1>(out Entity e));
                Assert.IsFalse(world.EntityManager.IsComponentEnabled<EnableableTag1>(e), "EnableableTag1 should be disabled");
                Assert.IsTrue(world.EntityManager.IsComponentEnabled<EnableableTag2>(e), "EnableableTag2 should be enabled");
                Assert.IsFalse(world.EntityManager.IsComponentEnabled<EnableableTag3>(e), "EnableableTag3 should be disabled");
                Assert.IsTrue(world.EntityManager.IsComponentEnabled<EnableableTag4>(e), "EnableableTag4 should be enabled");
            }
        }
    #endif

    }
}
