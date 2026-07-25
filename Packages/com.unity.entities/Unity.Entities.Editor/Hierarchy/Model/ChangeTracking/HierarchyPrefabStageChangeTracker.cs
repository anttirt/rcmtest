using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Editor.Bridge;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Entities.Editor
{
    struct HierarchyPrefabStageChanges : IDisposable
    {
        public NativeList<GameObjectChangeTrackerEvent> GameObjectChangeTrackerEvents;

        public HierarchyPrefabStageChanges(Allocator allocator)
        {
            GameObjectChangeTrackerEvents = new NativeList<GameObjectChangeTrackerEvent>(allocator);
        }

        public void Dispose()
        {
            GameObjectChangeTrackerEvents.Dispose();
        }

        public void Clear()
        {
            GameObjectChangeTrackerEvents.Clear();
        }
    }

    class HierarchyPrefabStageChangeTracker : IDisposable
    {
        NativeParallelHashSet<EntityId> m_EntityId;
        NativeParallelHashMap<EntityId, EntityId> m_Parents;
        NativeParallelHashSet<EntityId> m_Existing;

        List<EntityId> m_Removed = new List<EntityId>();

        public HierarchyPrefabStageChangeTracker(Allocator allocator)
        {
            m_EntityId = new NativeParallelHashSet<EntityId>(16, allocator);
            m_Parents = new NativeParallelHashMap<EntityId, EntityId>(16, allocator);
            m_Existing = new NativeParallelHashSet<EntityId>(16, allocator);
        }

        public void Clear()
        {
            m_EntityId.Clear();
            m_Parents.Clear();
            m_Existing.Clear();
        }

        public void Dispose()
        {
            m_EntityId.Dispose();
            m_Parents.Dispose();
            m_Existing.Dispose();
        }

        public void GetChanges(HierarchyPrefabStageChanges changes)
        {
            changes.Clear();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            m_Existing.Clear();
            m_Removed.Clear();

            var events = changes.GameObjectChangeTrackerEvents;

            if (null != stage)
            {
                var root = stage.prefabContentsRoot.transform.parent ? stage.prefabContentsRoot.transform.parent.gameObject : stage.prefabContentsRoot;
                GatherChangesRecursive(root, events, m_Existing);
            }

            foreach (var id in m_EntityId)
            {
                if (!m_Existing.Contains(id))
                    m_Removed.Add(id);
            }

            foreach (var id in m_Removed)
            {
                m_EntityId.Remove(id);
                events.Add(new GameObjectChangeTrackerEvent(id, GameObjectChangeTrackerEventType.Destroyed));
            }

            if (null == stage)
            {
                m_EntityId.Clear();
                m_Parents.Clear();
            }
        }

        void GatherChangesRecursive(GameObject obj, NativeList<GameObjectChangeTrackerEvent> events, NativeParallelHashSet<EntityId> existing)
        {
            var entityId = obj.GetEntityId();

            existing.Add(entityId);

            if (!m_EntityId.Contains(entityId))
            {
                events.Add(new GameObjectChangeTrackerEvent(entityId, GameObjectChangeTrackerEventType.CreatedOrChanged));
                m_EntityId.Add(entityId);
            }

            var parentId = obj.transform.parent ? obj.transform.parent.gameObject.GetEntityId() : EntityId.None;

            if (m_Parents.TryGetValue(entityId, out var currentParentId))
            {
                if (currentParentId != parentId)
                {
                    events.Add(new GameObjectChangeTrackerEvent(entityId, GameObjectChangeTrackerEventType.ChangedParent));
                    m_Parents[entityId] = parentId;
                }
            }
            else if (parentId != EntityId.None)
            {
                events.Add(new GameObjectChangeTrackerEvent(entityId, GameObjectChangeTrackerEventType.ChangedParent));
                m_Parents.Add(entityId, parentId);
            }

            foreach (Transform child in obj.transform)
                GatherChangesRecursive(child.gameObject, events, existing);
        }
    }
}
