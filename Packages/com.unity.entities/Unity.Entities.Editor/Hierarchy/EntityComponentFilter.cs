using System;
using Unity.Collections;
using Unity.Hierarchy;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Filters entities based on their component types (IComponentData).
    /// </summary>
    internal class EntityComponentFilter : IDisposable
    {
        struct ComponentTypeQuery
        {
            public TypeIndex TypeIndex;
            public bool IsValid;
        }

        NativeList<ComponentTypeQuery> m_ComponentTypes = new (2, Allocator.Persistent);
        NativeHashSet<Entity> m_MatchingEntities;

        int m_EntityIndex = -1;
        int m_EntityVersion = -1;

        // Explicit EntityQuery used by the Queries tab of the System window
        EntityQuery m_ExplicitQuery;
        World m_ExplicitWorld;
        ulong m_ExplicitWorldSequenceNumber;
        bool m_UsingExplicitFilter;

        public bool IsValid { get; private set; }

        public void SetQuery(HierarchySearchQueryDescriptor query, EntityQuery explicitQuery, World explicitWorld)
        {
            if (explicitWorld != null && explicitWorld.IsCreated && explicitWorld.EntityManager.IsQueryValid(explicitQuery))
            {
                IsValid = true;
                m_ComponentTypes.Clear();
                if (m_MatchingEntities.IsCreated)
                    m_MatchingEntities.Dispose();
                m_ExplicitQuery = explicitQuery;
                m_ExplicitWorld = explicitWorld;
                m_ExplicitWorldSequenceNumber = explicitWorld.Unmanaged.SequenceNumber;
                m_UsingExplicitFilter = true;
                return;
            }

            SetQuery(query);
        }

        /// <summary>
        /// Parse a search query and extract component type filters.
        /// </summary>
        public void SetQuery(HierarchySearchQueryDescriptor query)
        {
            IsValid = true;
            m_ComponentTypes.Clear();
            m_UsingExplicitFilter = false;
            m_ExplicitQuery = default;
            m_ExplicitWorld = null;
            m_EntityIndex = -1;
            m_EntityVersion = -1;

            foreach (var filter in query.Filters)
            {
                if (filter.Name == "id")
                {
                    if (filter.Op != HierarchySearchFilterOperator.Equal &&
                        filter.Op != HierarchySearchFilterOperator.Contains)
                        continue;

                    if (string.IsNullOrEmpty(filter.Value))
                        continue;

                    if (!TryParseEntityId(filter.Value, out m_EntityIndex, out m_EntityVersion))
                    {
                        IsValid = false;
                        return;
                    }
                    continue;
                }

                if (filter.Name != "t")
                    continue;

                if (filter.Op != HierarchySearchFilterOperator.Equal &&
                    filter.Op != HierarchySearchFilterOperator.Contains)
                    continue;

                if (string.IsNullOrEmpty(filter.Value))
                    continue;

                var componentType = ResolveComponentType(filter.Value);

                if (!componentType.IsValid)
                {
                    // Unknown type - entire query becomes invalid
                    IsValid = false;
                    m_ComponentTypes.Clear();
                    return;
                }

                m_ComponentTypes.Add(componentType);
            }

            BuildBatchQuery();
        }

        void BuildBatchQuery()
        {
            if (!IsValid || m_ComponentTypes.Count == 0)
                return;

            if (m_MatchingEntities.IsCreated)
                m_MatchingEntities.Dispose();

            m_MatchingEntities = new NativeHashSet<Entity>(1024, Allocator.Persistent);

            var componentTypes = new ComponentType[m_ComponentTypes.Length];
            for (int i = 0; i < m_ComponentTypes.Length; i++)
            {
                componentTypes[i] = ComponentType.ReadOnly(m_ComponentTypes[i].TypeIndex);
            }

            foreach (var world in World.All)
            {
                if (!world.IsCreated)
                    continue;

                using (var query = world.EntityManager.CreateEntityQuery(componentTypes))
                {
                    var count = query.CalculateEntityCount();
                    if (count == 0)
                        continue;

                    using (var entities = query.ToEntityArray(Allocator.Temp))
                    {
                        foreach (var entity in entities)
                        {
                            m_MatchingEntities.Add(entity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if an entity matches the filter (has all required components).
        /// </summary>
        public bool IsMatch(Entity entity, WorldUnmanaged entityWorld)
        {
            if (!IsValid)
                return false;

            if (m_UsingExplicitFilter)
            {
                if (m_ExplicitWorld == null || !m_ExplicitWorld.IsCreated ||
                    !m_ExplicitWorld.EntityManager.IsQueryValid(m_ExplicitQuery))
                {
                    m_UsingExplicitFilter = false;
                    m_ExplicitQuery = default;
                    m_ExplicitWorld = null;
                    m_ExplicitWorldSequenceNumber = 0;
                    return false;
                }

                // Entities outside the explicit filter's world must be hidden — entity IDs are
                // world-scoped, so EntityQuery.Matches on a foreign entity is undefined behavior.
                if (entityWorld.SequenceNumber != m_ExplicitWorldSequenceNumber)
                    return false;

                return m_ExplicitQuery.Matches(entity);
            }

            // Check entity ID filter
            if (m_EntityIndex >= 0)
            {
                if (entity.Index != m_EntityIndex)
                    return false;

                // If version was specified, check it too
                if (m_EntityVersion >= 0 && entity.Version != m_EntityVersion)
                    return false;
            }

            // Empty component filter matches everything
            if (m_ComponentTypes.Count == 0)
                return true;

            return m_MatchingEntities.Contains(entity);
        }

        /// <summary>
        /// Resolve a type name string to a ComponentTypeQuery with TypeIndex.
        /// </summary>
        static ComponentTypeQuery ResolveComponentType(string typeName)
        {
            var result = new ComponentTypeQuery { IsValid = false };

            foreach (var typeInfo in TypeManager.AllTypes)
            {
                if (!IsValidComponentCategory(typeInfo.Category))
                    continue;

                var componentType = typeInfo.Type;
                if (componentType == null)
                    continue;

                var fullTypeName = componentType.FullName ?? componentType.Name;
                if (fullTypeName.Equals(typeName, StringComparison.Ordinal))
                {
                    result.TypeIndex = typeInfo.TypeIndex;
                    result.IsValid = true;
                    return result;
                }

                if (componentType.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    result.TypeIndex = typeInfo.TypeIndex;
                    result.IsValid = true;
                    return result;
                }
            }

            return result;
        }

        internal static bool IsValidComponentCategory(TypeManager.TypeCategory category)
        {
            return category == TypeManager.TypeCategory.ComponentData ||
                   category == TypeManager.TypeCategory.ISharedComponentData ||
                   category == TypeManager.TypeCategory.BufferData ||
                   category == TypeManager.TypeCategory.TransformData;
        }

        static bool TryParseEntityId(string value, out int index, out int version)
        {
            index = -1;
            version = -1;

            var colonIndex = value.IndexOf(':');
            if (colonIndex < 0)
            {
                return int.TryParse(value, out index);
            }

            var indexPart = value.Substring(0, colonIndex);
            var versionPart = value.Substring(colonIndex + 1);

            if (!int.TryParse(indexPart, out index))
                return false;

            if (!int.TryParse(versionPart, out version))
                return false;

            return true;
        }

        public void Reset()
        {
            IsValid = false;
            m_ComponentTypes.Clear();
            m_UsingExplicitFilter = false;
            m_ExplicitQuery = default;
            m_ExplicitWorld = null;
            m_ExplicitWorldSequenceNumber = 0;
            m_EntityIndex = -1;
            m_EntityVersion = -1;

            if (m_MatchingEntities.IsCreated)
                m_MatchingEntities.Dispose();
        }

        public void Dispose()
        {
            Reset();
            m_ComponentTypes.Dispose();
        }
    }
}
