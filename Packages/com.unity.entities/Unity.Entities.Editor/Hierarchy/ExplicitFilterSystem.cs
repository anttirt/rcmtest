#if UNITY_EDITOR
using Unity.Collections;

namespace Unity.Entities.Editor
{
    // Tracks entities matching an explicit EntityQuery and raises OnExplicitFilterChanged whenever
    // the set changes. Currently driven from the Queries tab of the System window via the
    // "See all..." button on a query's entity list; the hierarchy window consumes the event to
    // refresh its filter.
    [UnityEngine.ExecuteAlways]
    [DisableAutoCreation]
    [UpdateAfter(typeof(UpdateHierarchySystem))]
    partial class ExplicitFilterSystem : SystemBase
    {
        public static System.Action<World, NativeList<Entity>, NativeList<Entity>> OnExplicitFilterChanged;

        EntityDiffer m_Differ;
        EntityQuery m_Query;
        bool m_HasFilter;
        NativeList<Entity> m_Added;
        NativeList<Entity> m_Removed;

        public bool HasExplicitFilter => m_HasFilter;
        public EntityQuery ExplicitFilterQuery => m_Query;

        public void SetExplicitFilterQuery(EntityQuery query)
        {
            // Only one explicit filter may be active across all worlds at a time. Clear any other
            // ExplicitFilterSystem's filter so a "See all..." click in a new world doesn't get
            // silently overridden by a stale filter left set on a previously-used world.
            foreach (var w in World.All)
            {
                if (!w.IsCreated || w == World)
                    continue;
                var other = w.GetExistingSystemManaged<ExplicitFilterSystem>();
                if (other != null && other.m_HasFilter)
                    other.ClearExplicitFilterQuery();
            }

            m_Query = query;
            m_HasFilter = true;
            m_Differ ??= new EntityDiffer(World);
        }

        public void ClearExplicitFilterQuery()
        {
            if (!m_HasFilter)
                return;
            m_HasFilter = false;
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
                ClearExplicitFilterQuery();
                return;
            }

            m_Added.Clear();
            m_Removed.Clear();
            m_Differ
                .GetEntityQueryMatchDiffAsync(m_Query, m_Added, m_Removed)
                .Complete();

            if (m_Added.Length == 0 && m_Removed.Length == 0)
                return;

            OnExplicitFilterChanged?.Invoke(World, m_Added, m_Removed);
        }
    }
}
#endif
