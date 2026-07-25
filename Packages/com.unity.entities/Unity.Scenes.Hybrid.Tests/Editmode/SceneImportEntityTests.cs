#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Scenes.Editor.Tests;

namespace Unity.Scenes.Hybrid.Tests
{
    // End-to-end behavioural tests for RequestSceneLoaded.ImportEntity: set the carrier
    // entity on a scene or section meta entity (typically via LoadParameters.ImportEntity
    // at load time), load the scene, verify the referenced data entity's components were
    // copied into the streaming world (and consumed by a ProcessAfterLoad system) without
    // leaking into the main world.
    [TestFixture]
    partial class SceneImportEntityTests : SubSceneTestFixture
    {
        TestLiveConversionSettings m_Settings;

        public SceneImportEntityTests()
        {
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/SubSceneSectionTestScene.unity";
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_Settings.Setup(true);
            SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TearDownOnce();
            m_Settings.TearDown();
        }

        public struct ImportProbe : IComponentData
        {
            public int Value;
        }

        // ProcessAfterLoad system whose presence in a streaming world proves the import path
        // works end-to-end: it captures the imported value into a static buffer so the test
        // can assert on it after the load completes.
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        public partial class CaptureImportProbeSystem : SystemBase
        {
            public static int LastSeenValue;
            public static int Sightings;

            protected override void OnUpdate()
            {
                foreach (var probe in SystemAPI.Query<RefRO<ImportProbe>>())
                {
                    LastSeenValue = probe.ValueRO.Value;
                    Sightings++;
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            CaptureImportProbeSystem.LastSeenValue = 0;
            CaptureImportProbeSystem.Sightings = 0;
        }

        [Test]
        public void ImportEntity_ViaLoadParameters_CopiesIntoStreamingWorld()
        {
            using var world = TestWorldSetup.CreateEntityWorld("World", false);

            // Build a data entity in the main world carrying ImportProbe.
            var dataEntity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(dataEntity, new ImportProbe { Value = 42 });

            // Single LoadSceneAsync call carrying the import directive inline via LoadParameters.
            var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn,
                ImportEntity = dataEntity,
            });
            world.Update();

            // The test scene has multiple sections; how many auto-load is governed by per-section
            // RequestSceneLoaded flags that the loader populates by default. We don't pin a count
            // here — what matters is that at least one section streamed in, saw the imported
            // ImportProbe with the right value, and its imported copy survived into the main world.
            Assert.Greater(CaptureImportProbeSystem.Sightings, 0, "ProcessAfterLoad system should have seen the imported ImportProbe in at least one section's streaming world.");
            Assert.AreEqual(42, CaptureImportProbeSystem.LastSeenValue);

            // With no ProcessAfterLoad system destroying it, the imported entity receives a
            // SceneTag and is moved into the main world along with the rest of the section's
            // entities. Expect at least one imported copy (per loaded section) plus the
            // user-owned source.
            using var probeInMain = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ImportProbe>());
            Assert.GreaterOrEqual(probeInMain.CalculateEntityCount(), 2, "At least one imported copy should survive into the main world alongside the user-owned source.");
            Assert.IsTrue(world.EntityManager.Exists(dataEntity), "User-owned source entity should still exist in the main world after the load.");

            // Cleanup: destroying the source entity is the user's responsibility.
            world.EntityManager.DestroyEntity(dataEntity);
        }

        [Test]
        public void ImportEntity_OnSectionEntity_CopiesIntoStreamingWorld()
        {
            using var world = TestWorldSetup.CreateEntityWorld("World", false);

            var dataEntity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(dataEntity, new ImportProbe { Value = 7 });

            // Resolve the scene without auto-loading sections so we can drive an individual
            // section's load request ourselves with a per-section ImportEntity.
            var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.DisableAutoLoad,
            });
            world.Update();

            // Add RequestSceneLoaded with our per-section ImportEntity to one specific section.
            // The section was created without RequestSceneLoaded (DisableAutoLoad), so AddComponentData
            // both signals "load me" and carries the import directive in one structural change.
            var sectionEntity = world.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity)[0].SectionEntity;
            world.EntityManager.AddComponentData(sectionEntity, new RequestSceneLoaded
            {
                LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn,
                ImportEntity = dataEntity,
            });
            world.Update();

            Assert.Greater(CaptureImportProbeSystem.Sightings, 0);
            Assert.AreEqual(7, CaptureImportProbeSystem.LastSeenValue);

            world.EntityManager.DestroyEntity(dataEntity);
        }

        [Test]
        public void ImportEntity_WithNullValue_SkippedSilently()
        {
            using var world = TestWorldSetup.CreateEntityWorld("World", false);

            Assert.DoesNotThrow(() =>
            {
                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn,
                    ImportEntity = Entity.Null,
                });
                world.Update();
            });

            Assert.AreEqual(0, CaptureImportProbeSystem.Sightings, "Null import value must not produce any imported entity in the streaming world.");
        }

        [Test]
        public void ImportEntity_WithDestroyedSource_LogsErrorAndSkips()
        {
            using var world = TestWorldSetup.CreateEntityWorld("World", false);

            var dataEntity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(dataEntity, new ImportProbe { Value = 99 });

            // Resolve the scene without auto-loading sections so we can target one section,
            // not all of them. A scene-meta-level destroyed-source would fire the error once
            // per loaded section, requiring an equal number of LogAssert.Expect calls — by
            // attaching at section level we get a clean one-error/one-expect pairing.
            var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.DisableAutoLoad,
            });
            world.Update();

            var sectionEntity = world.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity)[0].SectionEntity;
            world.EntityManager.AddComponentData(sectionEntity, new RequestSceneLoaded
            {
                LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn,
                ImportEntity = dataEntity,
            });

            // Destroy the source entity BEFORE the section streams in. A destroyed (non-null)
            // source is a programming error: the contract is "keep carrier entities alive until
            // the scene finishes loading."
            world.EntityManager.DestroyEntity(dataEntity);

            LogAssert.Expect(LogType.Error, new Regex("RequestSceneLoaded\\.ImportEntity source entity .* (does not exist|was destroyed)"));
            Assert.DoesNotThrow(() =>
            {
                world.Update();
            });

            Assert.AreEqual(0, CaptureImportProbeSystem.Sightings, "Destroyed source entity must not produce an imported entity in the streaming world.");
        }
    }
}
#endif
