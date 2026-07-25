using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Search;
using UnityEngine.TestTools;

namespace Unity.Entities.Editor.Tests.Search
{
    struct MetaChunkSearchTestComponentA : IComponentData
    {
        public int Value;
    }

    struct MetaChunkSearchTestComponentB : IComponentData
    {
        public int Value;
    }

    struct MetaChunkSearchTestChunkComponentA : IComponentData
    {
        public int Value;
    }

    struct MetaChunkSearchTestChunkComponentB : IComponentData
    {
        public int Value;
    }

    public class MetaChunkEntitySearchProviderTests : QuickSearchTests
    {
        const string k_WorldName = "MetaChunkSearchTestWorld";

        World m_PreviousWorld;
        World m_World;
        EntityManager m_Manager;

        [SetUp]
        public void Setup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World(k_WorldName);
            m_World.UpdateAllocatorEnableBlockFree = true;
            m_Manager = m_World.EntityManager;
            MetaChunkEntitySearchProvider.ClearQueryContext();
        }

        [TearDown]
        public void Teardown()
        {
            MetaChunkEntitySearchProvider.ClearQueryContext();
            if (m_World != null && m_World.IsCreated)
            {
                m_World.Dispose();
                m_World = null;
                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }
        }

        [UnityTest]
        public IEnumerator FetchItems_NoChunkComponentsInWorld_ReturnsEmpty()
        {
            m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));

            var results = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type, $"world:\"{k_WorldName}\"", results);
            Assert.AreEqual(0, results.Count);
        }

        [UnityTest]
        public IEnumerator FetchItems_WithChunkComponents_ReturnsOneItemPerChunk()
        {
            var e1 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e1);

            var e2 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentB));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e2);

            var results = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type, $"world:\"{k_WorldName}\"", results);
            Assert.AreEqual(2, results.Count);
        }

        [UnityTest]
        public IEnumerator FetchItems_ChunkFilter_FiltersByChunkComponentTypeOnChunkArchetype()
        {
            var e1 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e1);

            var e2 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentB));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentB>(e2);

            var matches = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type,
                $"world:\"{k_WorldName}\" chunk:MetaChunkSearchTestChunkComponentA", matches);
            Assert.AreEqual(1, matches.Count);

            var nonMatches = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type,
                $"world:\"{k_WorldName}\" chunk:MetaChunkSearchTestComponentA", nonMatches);
            Assert.AreEqual(0, nonMatches.Count,
                "chunk: filter must match the chunk archetype's chunk components, not the meta entity's own archetype or the chunk's plain components.");
        }

        [UnityTest]
        public IEnumerator FetchItems_WithQueryContext_AlsoEnumeratesRealEntitiesMatchingQuery()
        {
            var e1 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e1);
            var e2 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e2);

            var descs = new[]
            {
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<MetaChunkSearchTestComponentA>(),
                        ComponentType.ChunkComponent<MetaChunkSearchTestChunkComponentA>()
                    }
                }
            };
            MetaChunkEntitySearchProvider.SetQueryContext(m_World, descs);

            var results = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type, $"world:\"{k_WorldName}\"", results);

            // 1 meta entity (per chunk) + 2 real entities matching the full query.
            Assert.AreEqual(3, results.Count);
        }

        [UnityTest]
        public IEnumerator FetchItems_WithoutQueryContext_ReturnsOnlyMetaEntities()
        {
            var e1 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e1);
            var e2 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e2);

            MetaChunkEntitySearchProvider.ClearQueryContext();

            var results = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type, $"world:\"{k_WorldName}\"", results);

            // Only the single meta entity (the two real entities share the same chunk).
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void HasChunkComponents_ReturnsTrueForAllChunkComponent()
        {
            using var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ChunkComponent<MetaChunkSearchTestChunkComponentA>() }
            });
            Assert.IsTrue(EntityQueryToSearchString.HasChunkComponents(query));
        }

        [Test]
        public void HasChunkComponents_ReturnsTrueForNoneChunkComponent()
        {
            using var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MetaChunkSearchTestComponentA>() },
                None = new[] { ComponentType.ChunkComponent<MetaChunkSearchTestChunkComponentA>() }
            });
            Assert.IsTrue(EntityQueryToSearchString.HasChunkComponents(query));
        }

        [Test]
        public void HasChunkComponents_ReturnsFalseForNonChunkOnly()
        {
            using var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MetaChunkSearchTestComponentA>() }
            });
            Assert.IsFalse(EntityQueryToSearchString.HasChunkComponents(query));
        }

        [UnityTest]
        public IEnumerator Build_FilterFedToProviderMatchesQueryTargets_ForAllChunkComponent()
        {
            var e1 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e1);
            var e2 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentB));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentB>(e2);

            using var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MetaChunkSearchTestComponentA>(),
                    ComponentType.ChunkComponent<MetaChunkSearchTestChunkComponentA>()
                }
            });

            var filter = EntityQueryToSearchString.Build(query, m_World);
            var results = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type, filter, results);

            // Only the meta whose chunk has ChunkComponentA is expected — a Build that
            // leaked the non-chunk ComponentA from All into the filter would return 0.
            Assert.AreEqual(1, results.Count);
        }

        [UnityTest]
        public IEnumerator Build_FilterFedToProviderExcludesQueryTargets_ForNoneChunkComponent()
        {
            var e1 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentA>(e1);
            var e2 = m_Manager.CreateEntity(typeof(MetaChunkSearchTestComponentA));
            m_Manager.AddChunkComponentData<MetaChunkSearchTestChunkComponentB>(e2);

            using var query = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MetaChunkSearchTestComponentA>() },
                None = new[] { ComponentType.ChunkComponent<MetaChunkSearchTestChunkComponentA>() }
            });

            var filter = EntityQueryToSearchString.Build(query, m_World);
            var results = new List<SearchItem>();
            yield return FetchItems(MetaChunkEntitySearchProvider.type, filter, results);

            // The meta for the ChunkComponentA chunk is excluded; only the B one remains.
            Assert.AreEqual(1, results.Count);
        }
    }
}
