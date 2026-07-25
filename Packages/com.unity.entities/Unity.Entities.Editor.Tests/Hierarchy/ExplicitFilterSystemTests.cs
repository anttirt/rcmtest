using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    struct ExplicitFilterTestDataExtra : IComponentData
    {
        public int value;
    }

    struct ExplicitFilterTestDataEnableable : IComponentData, IEnableableComponent
    {
        public int value;
    }

    struct ExplicitFilterTestTag : IComponentData { }

    struct ExplicitFilterTestTag2 : IComponentData { }

    public class ExplicitFilterSystemTests
    {
        World m_PreviousWorld;
        World m_World;
        ExplicitFilterSystem m_System;

        struct ExplicitFilterEvent
        {
            public World World;
            public Entity[] Added;
            public Entity[] Removed;
        }

        List<ExplicitFilterEvent> m_Events;
        System.Action<World, NativeList<Entity>, NativeList<Entity>> m_Handler;

        [SetUp]
        public void SetUp()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World("ExplicitFilterSystemTests");
            m_System = m_World.GetOrCreateSystemManaged<ExplicitFilterSystem>();

            m_Events = new List<ExplicitFilterEvent>();
            m_Handler = (world, added, removed) =>
            {
                m_Events.Add(new ExplicitFilterEvent
                {
                    World = world,
                    Added = CopyToArray(added),
                    Removed = CopyToArray(removed)
                });
            };
            ExplicitFilterSystem.OnExplicitFilterChanged += m_Handler;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Handler != null)
            {
                ExplicitFilterSystem.OnExplicitFilterChanged -= m_Handler;
                m_Handler = null;
            }

            if (m_World != null && m_World.IsCreated)
                m_World.Dispose();
            m_World = null;
            m_System = null;
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
        }

        static Entity[] CopyToArray(NativeList<Entity> list)
        {
            var array = new Entity[list.Length];
            for (var i = 0; i < list.Length; i++)
                array[i] = list[i];
            return array;
        }

        [Test]
        public void SetExplicitFilterQuery_SetsFlagAndStoresQuery()
        {
            var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(query);

            Assert.That(m_System.HasExplicitFilter, Is.True);
            Assert.That(m_System.ExplicitFilterQuery, Is.EqualTo(query));
        }

        [Test]
        public void ClearExplicitFilterQuery_ResetsFlag()
        {
            var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(query);
            m_System.ClearExplicitFilterQuery();

            Assert.That(m_System.HasExplicitFilter, Is.False);
        }

        [Test]
        public void OnExplicitFilterChanged_NotFired_WhenNoExplicitFilter()
        {
            m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            m_System.Update();

            Assert.That(m_Events, Is.Empty);
        }

        [Test]
        public void OnExplicitFilterChanged_FiresWhenMatchingEntityAdded()
        {
            var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(query);
            // Drain the initial diff (no matches yet)
            m_System.Update();
            m_Events.Clear();

            var newEntity = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { newEntity }));
            Assert.That(m_Events[0].Removed, Is.Empty);
        }

        [Test]
        public void OnExplicitFilterChanged_FiresWhenMatchingEntityRemoved()
        {
            var entity = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(query);
            // Drain the initial diff (matches: entity)
            m_System.Update();
            m_Events.Clear();

            m_World.EntityManager.DestroyEntity(entity);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.Empty);
            Assert.That(m_Events[0].Removed, Is.EquivalentTo(new[] { entity }));
        }

        [Test]
        public void OnExplicitFilterChanged_IgnoresNonMatchingEntityChanges()
        {
            var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();
            m_Events.Clear();

            // Adding/destroying an entity that doesn't match the filter should not trigger the event
            var unrelated = m_World.EntityManager.CreateEntity(typeof(EcsTestData2));
            m_System.Update();
            m_World.EntityManager.DestroyEntity(unrelated);
            m_System.Update();

            Assert.That(m_Events, Is.Empty);
        }

        [Test]
        public void Update_WithDisposedExplicitFilterQuery_AutoClearsFilter()
        {
            var query = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(query);
            // Drain so we have a stable baseline
            m_System.Update();

            query.Dispose();

            Assert.That(() => m_System.Update(), Throws.Nothing);
            Assert.That(m_System.HasExplicitFilter, Is.False);
        }

        // --- Query shape coverage ---

        [Test]
        public void OnExplicitFilterChanged_WithAllMultipleComponents_RequiresEveryComponent()
        {
            var hasBoth = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            // Has only one of the required components
            m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            m_World.EntityManager.CreateEntity(typeof(EcsTestData2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithAll<EcsTestData2>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { hasBoth }));
        }

        [Test]
        public void OnExplicitFilterChanged_WithNone_ExcludesEntitiesWithDisallowedComponent()
        {
            var onlyA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            // Has the excluded component
            m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithNone<EcsTestData2>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { onlyA }));
        }

        [Test]
        public void OnExplicitFilterChanged_WithAny_MatchesEntitiesWithAtLeastOneComponent()
        {
            var withA = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var withB = m_World.EntityManager.CreateEntity(typeof(EcsTestData2));
            var withBoth = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            // Has neither — should not match
            m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestDataExtra));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAny<EcsTestData>()
                .WithAny<EcsTestData2>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { withA, withB, withBoth }));
        }

        // --- Query option coverage ---

        [Test]
        public void OnExplicitFilterChanged_WithoutIncludePrefab_ExcludesPrefabEntities()
        {
            var normal = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Prefab));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { normal }));
        }

        [Test]
        public void OnExplicitFilterChanged_WithIncludePrefab_IncludesPrefabEntities()
        {
            var normal = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var prefab = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Prefab));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { normal, prefab }));
        }

        [Test]
        public void OnExplicitFilterChanged_WithoutIncludeDisabledEntities_ExcludesDisabledEntities()
        {
            var enabled = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Disabled));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { enabled }));
        }

        [Test]
        public void OnExplicitFilterChanged_WithIncludeDisabledEntities_IncludesDisabledEntities()
        {
            var enabled = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var disabled = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Disabled));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { enabled, disabled }));
        }

        [Test]
        public void OnExplicitFilterChanged_EnableableComponent_EnabledEntityMatches()
        {
            var entity = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestDataEnableable));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ExplicitFilterTestDataEnableable>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { entity }));
        }

        [Test]
        public void OnExplicitFilterChanged_EnableableComponent_StructuralChangesAreTracked()
        {
            // Tracks entity creation/destruction for enableable-component queries, since the
            // hierarchy diff is chunk/structure based.
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ExplicitFilterTestDataEnableable>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();
            m_Events.Clear();

            var added = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestDataEnableable));
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { added }));
            Assert.That(m_Events[0].Removed, Is.Empty);

            m_Events.Clear();
            m_World.EntityManager.DestroyEntity(added);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.Empty);
            Assert.That(m_Events[0].Removed, Is.EquivalentTo(new[] { added }));
        }

        [Test]
        public void OnExplicitFilterChanged_IgnoreComponentEnabledState_MatchesEntitiesRegardlessOfState()
        {
            var entity = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestDataEnableable));
            m_World.EntityManager.SetComponentEnabled<ExplicitFilterTestDataEnableable>(entity, false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ExplicitFilterTestDataEnableable>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { entity }));
        }

        [Test]
        public void OnExplicitFilterChanged_CombinedOptions_IncludePrefabAndDisabled()
        {
            var normal = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var prefab = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Prefab));
            var disabled = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Disabled));
            var prefabAndDisabled = m_World.EntityManager.CreateEntity(typeof(EcsTestData), typeof(Prefab), typeof(Disabled));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added,
                Is.EquivalentTo(new[] { normal, prefab, disabled, prefabAndDisabled }));
        }

        [Test]
        public void OnExplicitFilterChanged_TagComponent_MatchesEntitiesWithTag()
        {
            var withTag1 = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestTag));
            var withTagAndData = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestTag), typeof(EcsTestData));
            // No tag — should not match
            m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ExplicitFilterTestTag>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { withTag1, withTagAndData }));
        }

        [Test]
        public void OnExplicitFilterChanged_TagComponent_StructuralChangesAreTracked()
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ExplicitFilterTestTag>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();
            m_Events.Clear();

            var added = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestTag));
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { added }));

            m_Events.Clear();
            m_World.EntityManager.DestroyEntity(added);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Removed, Is.EquivalentTo(new[] { added }));
        }

        [Test]
        public void OnExplicitFilterChanged_MultipleTagComponents_RequiresAll()
        {
            var hasBoth = m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestTag), typeof(ExplicitFilterTestTag2));
            // Only one of the two tags — should not match.
            m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestTag));
            m_World.EntityManager.CreateEntity(typeof(ExplicitFilterTestTag2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ExplicitFilterTestTag>()
                .WithAll<ExplicitFilterTestTag2>()
                .Build(m_World.EntityManager);
            m_System.SetExplicitFilterQuery(query);
            m_System.Update();

            Assert.That(m_Events, Has.Count.EqualTo(1));
            Assert.That(m_Events[0].Added, Is.EquivalentTo(new[] { hasBoth }));
        }

        [Test]
        public void SetExplicitFilterQuery_OnOtherWorld_ClearsThisWorldsFilter()
        {
            // Set a filter on m_World (the SetUp-created world)
            var queryA = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            m_System.SetExplicitFilterQuery(queryA);
            Assert.That(m_System.HasExplicitFilter, Is.True);

            // Create a second world and set a filter on its ExplicitFilterSystem
            var otherWorld = new World("ExplicitFilterSystemTests_Other");
            try
            {
                var otherSystem = otherWorld.GetOrCreateSystemManaged<ExplicitFilterSystem>();
                var queryB = otherWorld.EntityManager.CreateEntityQuery(typeof(EcsTestData));
                otherSystem.SetExplicitFilterQuery(queryB);

                // The new filter wins; the previous world's filter must be cleared so the
                // global "at most one explicit filter" invariant holds.
                Assert.That(otherSystem.HasExplicitFilter, Is.True);
                Assert.That(m_System.HasExplicitFilter, Is.False);
            }
            finally
            {
                otherWorld.Dispose();
            }
        }
    }
}
