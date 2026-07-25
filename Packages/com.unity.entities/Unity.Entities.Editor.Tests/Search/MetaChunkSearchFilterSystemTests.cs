using System.Collections.Generic;
using NUnit.Framework;

namespace Unity.Entities.Editor.Tests.Search
{
    struct MetaChunkFilterTestDataA : IComponentData { public int Value; }
    struct MetaChunkFilterTestDataB : IComponentData { public int Value; }
    struct MetaChunkFilterTestChunkComponent : IComponentData { public int Value; }

    public class MetaChunkSearchFilterSystemTests
    {
        World m_PreviousWorld;
        World m_World;
        MetaChunkSearchFilterSystem m_System;

        readonly List<World> m_Events = new();
        System.Action<World> m_Handler;

        [SetUp]
        public void SetUp()
        {
            // The search provider subscribes to OnFilterChanged the first time SetQueryContext is
            // called and only unsubscribes via ClearQueryContext. Ensure a clean slate so a prior
            // test's subscription doesn't observe events fired here.
            MetaChunkEntitySearchProvider.ClearQueryContext();

            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World("MetaChunkSearchFilterSystemTests");
            m_System = m_World.GetOrCreateSystemManaged<MetaChunkSearchFilterSystem>();

            m_Events.Clear();
            m_Handler = world => m_Events.Add(world);
            MetaChunkSearchFilterSystem.OnFilterChanged += m_Handler;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Handler != null)
            {
                MetaChunkSearchFilterSystem.OnFilterChanged -= m_Handler;
                m_Handler = null;
            }

            MetaChunkEntitySearchProvider.ClearQueryContext();

            if (m_World != null && m_World.IsCreated)
                m_World.Dispose();
            m_World = null;
            m_System = null;
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
        }

        static EntityQueryDesc[] DescsAllOf<T>() where T : struct, IComponentData
            => new[] { new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<T>() } } };

        [Test]
        public void SetFilterQuery_SetsHasFilterAndStoresQuery()
        {
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());

            Assert.That(m_System.HasFilter, Is.True);
            Assert.That(m_System.Query, Is.Not.EqualTo(default(EntityQuery)));
        }

        [Test]
        public void ClearFilterQuery_ResetsHasFilter()
        {
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());
            m_System.ClearFilterQuery();

            Assert.That(m_System.HasFilter, Is.False);
        }

        [Test]
        public void ClearFilterQuery_WhenNoFilter_IsNoOp()
        {
            Assert.That(() => m_System.ClearFilterQuery(), Throws.Nothing);
            Assert.That(m_System.HasFilter, Is.False);
        }

        [Test]
        public void OnFilterChanged_NotFired_WhenNoFilter()
        {
            m_World.EntityManager.CreateEntity(typeof(MetaChunkFilterTestDataA));
            m_System.Update();

            Assert.That(m_Events, Is.Empty);
        }

        [Test]
        public void OnFilterChanged_FiresWhenMatchingEntityAdded()
        {
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());
            // Drain the initial diff (no matches yet).
            m_System.Update();
            m_Events.Clear();

            m_World.EntityManager.CreateEntity(typeof(MetaChunkFilterTestDataA));
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0], Is.SameAs(m_World));
        }

        [Test]
        public void OnFilterChanged_FiresWhenMatchingEntityRemoved()
        {
            var entity = m_World.EntityManager.CreateEntity(typeof(MetaChunkFilterTestDataA));
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());
            // Drain the initial diff (entity reported as added).
            m_System.Update();
            m_Events.Clear();

            m_World.EntityManager.DestroyEntity(entity);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0], Is.SameAs(m_World));
        }

        [Test]
        public void OnFilterChanged_IgnoresNonMatchingChanges()
        {
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());
            m_System.Update();
            m_Events.Clear();

            var unrelated = m_World.EntityManager.CreateEntity(typeof(MetaChunkFilterTestDataB));
            m_System.Update();
            m_World.EntityManager.DestroyEntity(unrelated);
            m_System.Update();

            Assert.That(m_Events, Is.Empty);
        }

        [Test]
        public void OnFilterChanged_NotFired_WhenNothingChangedBetweenUpdates()
        {
            m_World.EntityManager.CreateEntity(typeof(MetaChunkFilterTestDataA));
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());
            m_System.Update();
            m_Events.Clear();

            m_System.Update();

            Assert.That(m_Events, Is.Empty);
        }

        [Test]
        public void OnFilterChanged_FiresForChunkComponentQuery()
        {
            // The system's only job is to diff a query; a chunk-component query is the use case
            // it exists for, so verify the chunk-component flavor end-to-end.
            var entity = m_World.EntityManager.CreateEntity(typeof(MetaChunkFilterTestDataA));
            m_World.EntityManager.AddChunkComponentData<MetaChunkFilterTestChunkComponent>(entity);

            m_System.SetFilterQuery(new[]
            {
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<MetaChunkFilterTestDataA>(),
                        ComponentType.ChunkComponent<MetaChunkFilterTestChunkComponent>()
                    }
                }
            });
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void SetFilterQuery_CalledTwice_ReplacesPriorQuery()
        {
            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataA>());
            var firstQuery = m_System.Query;

            m_System.SetFilterQuery(DescsAllOf<MetaChunkFilterTestDataB>());

            Assert.That(m_System.HasFilter, Is.True);
            Assert.That(m_System.Query, Is.Not.EqualTo(firstQuery));
        }
    }
}
