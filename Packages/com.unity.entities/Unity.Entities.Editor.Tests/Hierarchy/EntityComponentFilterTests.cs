using NUnit.Framework;
using Unity.Collections;
using Unity.Transforms;
using Unity.Hierarchy;

namespace Unity.Entities.Editor.Tests
{
    public class EntityComponentFilterTests
    {
        World m_World;
        EntityManager m_EntityManager;

        [SetUp]
        public void SetUp()
        {
            m_World = new World("TestWorld");
            m_EntityManager = m_World.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_World != null && m_World.IsCreated)
                m_World.Dispose();
        }

        [Test]
        public void ResolveComponentType_ValidComponentType_ResolvesSuccessfully()
        {
            var filter = new EntityComponentFilter();
            var query = CreateQuery("LocalToWorld");

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.True, "Filter should be valid for known component type LocalToWorld");
        }

        [Test]
        public void ResolveComponentType_InvalidComponentType_MarksFilterInvalid()
        {
            var filter = new EntityComponentFilter();
            var query = CreateQuery("NonExistentComponent");

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.False, "Filter should be invalid for unknown component type");
        }

        [Test]
        public void IsMatch_EntityWithRequestedComponent_ReturnsTrue()
        {
            var entity = m_EntityManager.CreateEntity(typeof(LocalToWorld));
            var filter = new EntityComponentFilter();
            var query = CreateQuery("LocalToWorld");

            filter.SetQuery(query);

            Assume.That(filter.IsValid, Is.True, "Filter should be valid before testing IsMatch");
            Assert.That(filter.IsMatch(entity, m_World.Unmanaged), Is.True,
                "Entity with LocalToWorld component should match filter");
        }

        [Test]
        public void IsMatch_EntityWithoutRequestedComponent_ReturnsFalse()
        {
            var entity = m_EntityManager.CreateEntity(typeof(LocalTransform));
            var filter = new EntityComponentFilter();
            var query = CreateQuery("LocalToWorld");

            filter.SetQuery(query);

            Assume.That(filter.IsValid, Is.True, "Filter should be valid before testing IsMatch");
            Assert.That(filter.IsMatch(entity, m_World.Unmanaged), Is.False,
                "Entity without LocalToWorld component should not match filter");
        }

        [Test]
        public void IsMatch_EntityIdFilterByIndex_MatchesCorrectEntity()
        {
            var entity = m_EntityManager.CreateEntity();
            var filter = new EntityComponentFilter();
            var query = CreateIdQuery(entity.Index.ToString());

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.True);
            Assert.That(filter.IsMatch(entity, m_World.Unmanaged), Is.True,
                "Entity should match when filtering by its index");
        }

        [Test]
        public void IsMatch_EntityIdFilterByIndex_DoesNotMatchDifferentEntity()
        {
            var entity1 = m_EntityManager.CreateEntity();
            var entity2 = m_EntityManager.CreateEntity();
            var filter = new EntityComponentFilter();
            var query = CreateIdQuery(entity1.Index.ToString());

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.True);
            Assert.That(filter.IsMatch(entity2, m_World.Unmanaged), Is.False,
                "Entity should not match when filtering by a different index");
        }

        [Test]
        public void IsMatch_EntityIdFilterByIndexAndVersion_MatchesExactEntity()
        {
            var entity = m_EntityManager.CreateEntity();
            var filter = new EntityComponentFilter();
            var query = CreateIdQuery($"{entity.Index}:{entity.Version}");

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.True);
            Assert.That(filter.IsMatch(entity, m_World.Unmanaged), Is.True,
                "Entity should match when filtering by exact index:version");
        }

        [Test]
        public void IsMatch_EntityIdFilterByIndexAndVersion_DoesNotMatchDifferentVersion()
        {
            var entity = m_EntityManager.CreateEntity();
            var filter = new EntityComponentFilter();
            // Use a version that's different from the entity's actual version
            var wrongVersion = entity.Version + 1;
            var query = CreateIdQuery($"{entity.Index}:{wrongVersion}");

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.True);
            Assert.That(filter.IsMatch(entity, m_World.Unmanaged), Is.False,
                "Entity should not match when version differs");
        }

        [Test]
        public void IsMatch_EntityIdFilterCombinedWithComponentFilter_RequiresBoth()
        {
            var entityWithComponent = m_EntityManager.CreateEntity(typeof(LocalToWorld));
            var entityWithoutComponent = m_EntityManager.CreateEntity();
            var filter = new EntityComponentFilter();

            // Create query with both id= and t= filters
            var filters = new[]
            {
                new HierarchySearchFilter { Name = "id", Value = entityWithComponent.Index.ToString(), Op = HierarchySearchFilterOperator.Equal },
                new HierarchySearchFilter { Name = "t", Value = "LocalToWorld", Op = HierarchySearchFilterOperator.Equal }
            };
            var query = new HierarchySearchQueryDescriptor(filters);

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.True);
            Assert.That(filter.IsMatch(entityWithComponent, m_World.Unmanaged), Is.True,
                "Entity with matching id and component should match");
            Assert.That(filter.IsMatch(entityWithoutComponent, m_World.Unmanaged), Is.False,
                "Entity with different id should not match even without checking component");
        }

        [Test]
        public void SetQuery_MalformedEntityId_MarksFilterInvalid()
        {
            var filter = new EntityComponentFilter();
            var query = CreateIdQuery("abc");

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.False, "Filter should be invalid for non-numeric id");
        }

        [Test]
        public void SetQuery_MalformedEntityIdWithVersion_MarksFilterInvalid()
        {
            var filter = new EntityComponentFilter();
            var query = CreateIdQuery("123:xyz");

            filter.SetQuery(query);

            Assert.That(filter.IsValid, Is.False, "Filter should be invalid when version is non-numeric");
        }

        HierarchySearchQueryDescriptor CreateQuery(params string[] componentTypeNames)
        {
            var filters = new HierarchySearchFilter[componentTypeNames.Length];
            for (int i = 0; i < componentTypeNames.Length; i++)
            {
                filters[i] = new HierarchySearchFilter
                {
                    Name = "t",
                    Value = componentTypeNames[i],
                    Op = HierarchySearchFilterOperator.Equal
                };
            }
            return new HierarchySearchQueryDescriptor(filters);
        }

        HierarchySearchQueryDescriptor CreateIdQuery(string idValue)
        {
            var filters = new[]
            {
                new HierarchySearchFilter
                {
                    Name = "id",
                    Value = idValue,
                    Op = HierarchySearchFilterOperator.Equal
                }
            };
            return new HierarchySearchQueryDescriptor(filters);
        }
    }
}
