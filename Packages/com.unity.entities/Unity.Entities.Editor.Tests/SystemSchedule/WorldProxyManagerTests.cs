using NUnit.Framework;
using Unity.Entities.Hybrid.Tests;
using UnityEngine.LowLevel;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class WorldProxyManagerTests
    {
        PlayerLoopSystem m_PrevPlayerLoop;
        TestWithCustomDefaultGameObjectInjectionWorld m_CustomInjectionWorld;
        WorldProxyManager m_Manager;

        [SetUp]
        public void Setup()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_CustomInjectionWorld.Setup();

            m_Manager = new WorldProxyManager();
        }

        [TearDown]
        public void TearDown()
        {
            m_Manager?.Dispose();
            m_CustomInjectionWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }

        World CreateTestWorld(string name)
        {
            var world = new World(name);
            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            return world;
        }

        [Test]
        public void CreateWorldProxiesForAllWorlds_CreatesProxyForEachWorld()
        {
            using var worldA = CreateTestWorld("TestWorldA");
            using var worldB = CreateTestWorld("TestWorldB");

            m_Manager.CreateWorldProxiesForAllWorlds();

            // Verify proxy exists for world A
            Assert.That(m_Manager.TryGetWorldProxy(worldA, out var proxyA), Is.True, "Should create proxy for world A");
            Assert.That(proxyA, Is.Not.Null, "Proxy A should not be null");

            // Verify proxy exists for world B
            Assert.That(m_Manager.TryGetWorldProxy(worldB, out var proxyB), Is.True, "Should create proxy for world B");
            Assert.That(proxyB, Is.Not.Null, "Proxy B should not be null");
        }

        [Test]
        public void GetWorldProxyForGivenWorld_ReturnsProxyThroughUpdater()
        {
            using var world = CreateTestWorld("TestWorld");
            m_Manager.CreateWorldProxiesForAllWorlds();

            // Proxy should be accessible through the manager
            var proxy = m_Manager.GetWorldProxyForGivenWorld(world);

            Assert.That(proxy, Is.Not.Null, "Proxy should be returned through updater");
            Assert.That(proxy.SequenceNumber, Is.EqualTo(world.SequenceNumber), "Proxy should match world sequence number");
        }

        [Test]
        public void TryGetWorldProxy_ReturnsFalse_WhenWorldDoesNotExist()
        {
            using var world = CreateTestWorld("TestWorld");
            // Don't create proxies

            // Attempting to get proxy for world without creating it should fail
            var result = m_Manager.TryGetWorldProxy(world, out var proxy);

            Assert.That(result, Is.False, "Should return false when world proxy doesn't exist");
            Assert.That(proxy, Is.Null, "Proxy should be null when not found");
        }

        [Test]
        public void TryGetWorldProxy_ReturnsFalse_WhenWorldIsNull()
        {
            // Attempting to get proxy for null world should fail gracefully
            var result = m_Manager.TryGetWorldProxy(null, out var proxy);

            Assert.That(result, Is.False, "Should return false for null world");
            Assert.That(proxy, Is.Null, "Proxy should be null for null world");
        }

        [Test]
        public void GetAllWorldProxyUpdaters_ReturnsAllUpdaters()
        {
            using var worldA = CreateTestWorld("TestWorldA");
            using var worldB = CreateTestWorld("TestWorldB");

            m_Manager.CreateWorldProxiesForAllWorlds();

            var updaters = m_Manager.GetAllWorldProxyUpdaters();
            var count = 0;
            foreach (var updater in updaters)
            {
                count++;
            }

            // Should have at least 2 updaters (possibly more from default worlds)
            Assert.That(count, Is.GreaterThanOrEqualTo(2), "Should return updaters for all created worlds");
        }

        [Test]
        public void SetSelectedWorldProxy_WithFullPlayerLoopFalse_OnlyEnablesSelectedUpdater()
        {
            using var worldA = CreateTestWorld("TestWorldA");
            using var worldB = CreateTestWorld("TestWorldB");

            m_Manager.IsFullPlayerLoop = false;
            m_Manager.CreateWorldProxiesForAllWorlds();

            var proxyA = m_Manager.GetWorldProxyForGivenWorld(worldA);

            // Select world A
            m_Manager.SetSelectedWorldProxy(proxyA);

            var updaterA = FindUpdaterForWorld(worldA);
            var updaterB = FindUpdaterForWorld(worldB);

            // Only the selected updater should be active
            Assert.That(updaterA.IsActive(), Is.True, "Selected world's updater should be active");
            Assert.That(updaterB.IsActive(), Is.False, "Non-selected world's updater should be inactive");
        }

        [Test]
        public void SetSelectedWorldProxy_WithFullPlayerLoopTrue_EnablesAllUpdaters()
        {
            using var worldA = CreateTestWorld("TestWorldA");
            using var worldB = CreateTestWorld("TestWorldB");

            m_Manager.IsFullPlayerLoop = true;
            m_Manager.CreateWorldProxiesForAllWorlds();

            var proxyA = m_Manager.GetWorldProxyForGivenWorld(worldA);

            // Select world A (but full player loop mode means all should be enabled)
            m_Manager.SetSelectedWorldProxy(proxyA);

            var updaterA = FindUpdaterForWorld(worldA);
            var updaterB = FindUpdaterForWorld(worldB);

            // All updaters should be active in full player loop mode
            Assert.That(updaterA.IsActive(), Is.True, "World A updater should be active in full player loop mode");
            Assert.That(updaterB.IsActive(), Is.True, "World B updater should be active in full player loop mode");
        }

        [Test]
        public void CleanUpWorldProxyDictionary_RemovesDestroyedWorlds()
        {
            var worldA = CreateTestWorld("TestWorldA");
            using var worldB = CreateTestWorld("TestWorldB");

            m_Manager.CreateWorldProxiesForAllWorlds();

            Assume.That(m_Manager.TryGetWorldProxy(worldA, out _), Is.True, "World A proxy should exist before disposal");
            Assume.That(m_Manager.TryGetWorldProxy(worldB, out _), Is.True, "World B proxy should exist");

            // Destroy world A
            worldA.Dispose();

            // Trigger cleanup by creating proxies again
            m_Manager.CreateWorldProxiesForAllWorlds();

            // World A should be cleaned up, World B should still exist
            Assert.That(m_Manager.TryGetWorldProxy(worldA, out _), Is.False, "Destroyed world A should be cleaned up");
            Assert.That(m_Manager.TryGetWorldProxy(worldB, out _), Is.True, "World B should still exist");
        }

        [Test]
        public void CleanUpWorldProxyDictionary_DisablesUpdaterForDestroyedWorld()
        {
            var worldA = CreateTestWorld("TestWorldA");

            m_Manager.IsFullPlayerLoop = false;
            m_Manager.CreateWorldProxiesForAllWorlds();

            var proxyA = m_Manager.GetWorldProxyForGivenWorld(worldA);
            m_Manager.SetSelectedWorldProxy(proxyA);

            var updaterA = FindUpdaterForWorld(worldA);

            Assume.That(updaterA.IsActive(), Is.True, "Updater should be active before world disposal");

            // Destroy world A
            worldA.Dispose();

            // Trigger cleanup
            m_Manager.CreateWorldProxiesForAllWorlds();

            // Updater should be disabled after cleanup
            Assert.That(updaterA.IsActive(), Is.False, "Updater should be disabled after world disposal");
        }

        [Test]
        public void CreateWorldProxy_SkipsStreamingWorlds()
        {
            var streamingWorld = new World("StreamingWorld", WorldFlags.Streaming);

            m_Manager.CreateWorldProxiesForAllWorlds();

            // Streaming world should not have a proxy
            Assert.That(m_Manager.TryGetWorldProxy(streamingWorld, out _), Is.False, "Streaming worlds should not create proxies");

            streamingWorld.Dispose();
        }

        [Test]
        public void RebuildWorldProxyForGivenWorld_UpdatesProxySuccessfully()
        {
            using var world = CreateTestWorld("TestWorld");

            m_Manager.CreateWorldProxiesForAllWorlds();

            var proxyBefore = m_Manager.GetWorldProxyForGivenWorld(world);
            Assume.That(proxyBefore, Is.Not.Null, "Proxy should exist before rebuild");

            // Rebuild the proxy (should not throw)
            m_Manager.RebuildWorldProxyForGivenWorld(world);

            // Proxy should still be accessible after rebuild
            var proxyAfter = m_Manager.GetWorldProxyForGivenWorld(world);
            Assert.That(proxyAfter, Is.Not.Null, "Proxy should exist after rebuild");
            Assert.That(proxyAfter.SequenceNumber, Is.EqualTo(world.SequenceNumber), "Proxy should still match world after rebuild");
        }

        [Test]
        public void UpdaterExposesProxy_ThroughProperty()
        {
            using var world = CreateTestWorld("TestWorld");

            m_Manager.CreateWorldProxiesForAllWorlds();

            var updater = FindUpdaterForWorld(world);
            var proxyFromManager = m_Manager.GetWorldProxyForGivenWorld(world);

            // Updater should expose the same proxy
            Assert.That(updater.Proxy, Is.SameAs(proxyFromManager), "Updater should expose the same proxy instance");
            Assert.That(updater.World, Is.SameAs(world), "Updater should expose the correct world");
        }

        [Test]
        public void Dispose_DisablesAllUpdaters()
        {
            using var worldA = CreateTestWorld("TestWorldA");
            using var worldB = CreateTestWorld("TestWorldB");

            m_Manager.IsFullPlayerLoop = true;
            m_Manager.CreateWorldProxiesForAllWorlds();

            var updaterA = FindUpdaterForWorld(worldA);
            var updaterB = FindUpdaterForWorld(worldB);

            var proxyA = m_Manager.GetWorldProxyForGivenWorld(worldA);
            m_Manager.SetSelectedWorldProxy(proxyA);

            Assume.That(updaterA.IsActive(), Is.True, "Updater A should be active before dispose");
            Assume.That(updaterB.IsActive(), Is.True, "Updater B should be active before dispose");

            // Dispose manager
            m_Manager.Dispose();

            // All updaters should be disabled
            Assert.That(updaterA.IsActive(), Is.False, "Updater A should be disabled after dispose");
            Assert.That(updaterB.IsActive(), Is.False, "Updater B should be disabled after dispose");
        }

        WorldProxyUpdater FindUpdaterForWorld(World world)
        {
            foreach (var updater in m_Manager.GetAllWorldProxyUpdaters())
            {
                if (updater.World == world)
                    return updater;
            }
            return null;
        }

        partial class TestSystem : SystemBase
        {
            protected override void OnUpdate() { }
        }
    }
}
