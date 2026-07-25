#if UNITY_EDITOR
using Unity.Collections;

namespace Unity.Entities.Editor
{
    // Tracks entities matching the query opened in the MetaChunk search window and raises
    // OnFilterChanged whenever the matched set changes, so the window can re-fetch its rows
    // promptly instead of showing stale entries from a destroyed entity. Mirrors
    // [[ExplicitFilterSystem]] but feeds the search window rather than the hierarchy.
    [UnityEngine.ExecuteAlways]
    [DisableAutoCreation]
    [UpdateAfter(typeof(UpdateHierarchySystem))]
    partial class MetaChunkSearchFilterSystem : SystemBase
    {
        public static System.Action<World> OnFilterChanged;

        EntityDiffer m_Differ;
        EntityQuery m_Query;
        bool m_HasFilter;
        NativeList<Entity> m_Added;
        NativeList<Entity> m_Removed;

        public bool HasFilter => m_HasFilter;
        public EntityQuery Query => m_Query;

        public void SetFilterQuery(EntityQueryDesc[] descs)
        {
            // Only one search filter may be active across all worlds at a time. Clear any other
            // MetaChunkSearchFilterSystem so a "See all..." click in a new world doesn't get
            // silently shadowed by a stale filter on a previously-used world.
            foreach (var w in World.All)
            {
                if (!w.IsCreated || w == World)
                    continue;
                var other = w.GetExistingSystemManaged<MetaChunkSearchFilterSystem>();
                if (other != null && other.m_HasFilter)
                    other.ClearFilterQuery();
            }

            if (m_HasFilter)
                m_Query.Dispose();

            m_Query = EntityManager.CreateEntityQuery(descs);
            m_HasFilter = true;
            m_Differ ??= new EntityDiffer(World);
        }

        public void ClearFilterQuery()
        {
            if (!m_HasFilter)
                return;
            m_HasFilter = false;
            m_Query.Dispose();
            m_Query = default;
            m_Differ?.Dispose();
            m_Differ = null;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Added = new NativeList<Entity>(Allocator.Persistent);
            m_Removed = new NativeList<Entity>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (m_HasFilter)
                m_Query.Dispose();
            m_Differ?.Dispose();
            if (m_Added.IsCreated)
                m_Added.Dispose();
            if (m_Removed.IsCreated)
                m_Removed.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (!m_HasFilter || m_Differ == null)
                return;

            if (!EntityManager.IsQueryValid(m_Query))
            {
                ClearFilterQuery();
                return;
            }

            m_Added.Clear();
            m_Removed.Clear();
            m_Differ
                .GetEntityQueryMatchDiffAsync(m_Query, m_Added, m_Removed)
                .Complete();

            if (m_Added.Length == 0 && m_Removed.Length == 0)
                return;

            OnFilterChanged?.Invoke(World);
        }
    }
}
#endif
