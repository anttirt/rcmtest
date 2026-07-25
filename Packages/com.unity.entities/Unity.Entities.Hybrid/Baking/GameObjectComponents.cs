using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Conversion;
using UnityEngine;

namespace Unity.Entities.Baking
{
    /// <summary>
    /// Stores meta data about all game objects in the scene.
    /// This means we have a copy of the last converted state, where we can see which game objects existed and what components were attached to it.
    /// </summary>
    struct GameObjectComponents
    {
        public struct ComponentData
        {
            public ComponentData(Component component)
            {
                TypeIndex = TypeManager.GetOrCreateTypeIndex(component.GetType());
                EntityId = component.GetEntityId();
            }

            public TypeIndex TypeIndex;
            public EntityId  EntityId;
        }

        //UnsafeParallelMultiHashMap<int, TransformData>      _TransformData;
        // GameObject EntityId -> Component Type + component EntityId
        // Used to keep the last converted state of each game object.
        UnsafeParallelHashMap<EntityId, UnsafeList<ComponentData>> _GameObjectComponentMetaData;


        public GameObjectComponents(Allocator allocator)
        {
            _GameObjectComponentMetaData = new UnsafeParallelHashMap<EntityId, UnsafeList<ComponentData>>(1024, allocator);
        }

        public UnsafeList<ComponentData>.ReadOnly GetComponents(EntityId entityId)
        {
            _GameObjectComponentMetaData.TryGetValue(entityId, out var componentList);
            return componentList.AsReadOnly();
        }

        public bool HasComponent (EntityId gameObjectEntityId, EntityId componentEntityId)
        {
            if (_GameObjectComponentMetaData.TryGetValue(gameObjectEntityId, out var componentList))
            {
                foreach (var com in componentList)
                {
                    if (com.EntityId == componentEntityId)
                        return true;
                }
            }

            return false;
        }

        public EntityId GetComponent (EntityId gameObjectEntityId, TypeIndex componentType)
        {
            if (_GameObjectComponentMetaData.TryGetValue(gameObjectEntityId, out var componentList))
            {
                foreach (var com in componentList)
                {
                    if (com.TypeIndex == componentType || TypeManager.IsDescendantOf(com.TypeIndex, componentType))
                        return com.EntityId;
                }
            }
            return EntityId.None;
        }

        public void GetComponents (EntityId gameObjectEntityId, TypeIndex componentType, ref UnsafeList<EntityId> results)
        {
            if (_GameObjectComponentMetaData.TryGetValue(gameObjectEntityId, out var componentList))
            {
                foreach (var com in componentList)
                {
                    if (com.TypeIndex == componentType || TypeManager.IsDescendantOf(com.TypeIndex, componentType))
                        results.Add(com.EntityId);
                }
            }
        }

        private void GetComponentsHash(EntityId gameObjectEntityId, TypeIndex componentType, ref xxHash3.StreamingState hash)
        {
            if (_GameObjectComponentMetaData.TryGetValue(gameObjectEntityId, out var componentList))
            {
                foreach (var com in componentList)
                {
                    if (com.TypeIndex == componentType || TypeManager.IsDescendantOf(com.TypeIndex, componentType))
                        hash.Update(com.EntityId);
                }
            }
        }

        public Hash128 GetComponentsHash(EntityId gameObjectEntityId, TypeIndex componentType)
        {
            var hashGenerator = new xxHash3.StreamingState(false);
            GetComponentsHash(gameObjectEntityId, componentType, ref hashGenerator);
            return new Hash128(hashGenerator.DigestHash128());
        }

        public static EntityId GetComponentInParent(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type)
        {
            EntityId res = components.GetComponent(gameObject, type);
            if (res != EntityId.None)
                return res;


            if (!hierarchy.TryGetIndexForEntityId(gameObject, out var index))
            {
                //Debug.LogError("Invalid internal state");
                return EntityId.None;
            }

            // We already checked the first one, so skip that one to avoid a duplicate
            index = hierarchy.GetParentForIndex(index);
            while (index != -1)
            {
                gameObject = hierarchy.GetEntityIdForIndex(index);
                res = components.GetComponent(gameObject, type);
                if (res != EntityId.None)
                    return res;

                index = hierarchy.GetParentForIndex(index);
            }
            return EntityId.None;
        }

        public void AddGameObject(GameObject gameObject, List<Component> components)
        {
            var entityId = gameObject.GetEntityId();
            if (!_GameObjectComponentMetaData.TryGetValue(entityId, out var componentDataList))
            {
                componentDataList = new UnsafeList<ComponentData>(components.Count, Allocator.Persistent);
            }
            else
            {
                // Clear existing entries to avoid duplicates when the same GO is processed again
                componentDataList.Clear();
            }
            foreach (var com in components)
            {
                componentDataList.Add(new ComponentData(com));
            }
            _GameObjectComponentMetaData[entityId] = componentDataList;
        }

        private static EntityId GetComponentInChildrenInternal(ref GameObjectComponents components, ref SceneHierarchy hierarchy, int index, TypeIndex type)
        {
            EntityId res = EntityId.None;
            var childIterator = hierarchy.GetChildIndicesForIndex(index);
            while (childIterator.MoveNext())
            {
                int childIndex = childIterator.Current;
                var gameObject = hierarchy.GetEntityIdForIndex(childIndex);

                // Look up in the current object
                res = components.GetComponent(gameObject, type);
                if (res != EntityId.None)
                    break;

                // Look up in the children
                res = GetComponentInChildrenInternal(ref components, ref hierarchy, childIndex, type);
                if (res != EntityId.None)
                    break;
            }
            return res;
        }

        public static EntityId GetComponentInChildren(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type)
        {
            EntityId res = components.GetComponent(gameObject, type);
            if (res != EntityId.None)
                return res;

            if (!hierarchy.TryGetIndexForEntityId(gameObject, out var index))
            {
                return EntityId.None;
            }
            return GetComponentInChildrenInternal(ref components, ref hierarchy, index, type);
        }

        private static void GetComponentsInChildrenInternal(ref GameObjectComponents components, ref SceneHierarchy hierarchy, int index, TypeIndex type, ref UnsafeList<EntityId> results)
        {
            var childIterator = hierarchy.GetChildIndicesForIndex(index);
            while (childIterator.MoveNext())
            {
                int childIndex = childIterator.Current;
                var gameObject = hierarchy.GetEntityIdForIndex(childIndex);

                // Look up in the current object
                components.GetComponents(gameObject, type, ref results);

                // Look up in the children
                GetComponentsInChildrenInternal(ref components, ref hierarchy, childIndex, type, ref results);
            }
        }

        public static void GetComponentsInChildren(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type, ref UnsafeList<EntityId> results)
        {
            components.GetComponents(gameObject, type, ref results);

            if (hierarchy.TryGetIndexForEntityId(gameObject, out var index))
            {
                GetComponentsInChildrenInternal(ref components, ref hierarchy, index, type, ref results);
            }
        }

        private static void GetComponentsInChildrenInternalHash(ref GameObjectComponents components, ref SceneHierarchy hierarchy, int index, TypeIndex type, ref xxHash3.StreamingState hashGenerator)
        {
            var childIterator = hierarchy.GetChildIndicesForIndex(index);
            while (childIterator.MoveNext())
            {
                int childIndex = childIterator.Current;
                var gameObject = hierarchy.GetEntityIdForIndex(childIndex);

                // Look up in the current object
                components.GetComponentsHash(gameObject, type, ref hashGenerator);

                // Look up in the children
                GetComponentsInChildrenInternalHash(ref components, ref hierarchy, childIndex, type, ref hashGenerator);
            }
        }

        public static Hash128 GetComponentsInChildrenHash(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type)
        {
            var hashGenerator = new xxHash3.StreamingState(false);
            components.GetComponentsHash(gameObject, type, ref hashGenerator);

            if (hierarchy.TryGetIndexForEntityId(gameObject, out var index))
            {
                GetComponentsInChildrenInternalHash(ref components, ref hierarchy, index, type, ref hashGenerator);
            }
            return new Hash128(hashGenerator.DigestHash128());
        }

        public static void GetComponentsInParent(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type, ref UnsafeList<EntityId> results)
        {
            components.GetComponents(gameObject, type, ref results);

            if (hierarchy.TryGetIndexForEntityId(gameObject, out var index))
            {
                // We already checked the first one, so skip that one to avoid a duplicate
                index = hierarchy.GetParentForIndex(index);
                while (index != -1)
                {
                    gameObject = hierarchy.GetEntityIdForIndex(index);
                    components.GetComponents(gameObject, type, ref results);

                    index = hierarchy.GetParentForIndex(index);
                }
            }
        }

        private static void GetComponentsInParentHash(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type, ref xxHash3.StreamingState hashGenerator)
        {
            components.GetComponentsHash(gameObject, type, ref hashGenerator);

            if (hierarchy.TryGetIndexForEntityId(gameObject, out var index))
            {
                // We already checked the first one, so skip that one to avoid a duplicate
                index = hierarchy.GetParentForIndex(index);
                while (index != -1)
                {
                    gameObject = hierarchy.GetEntityIdForIndex(index);
                    components.GetComponentsHash(gameObject, type, ref hashGenerator);

                    index = hierarchy.GetParentForIndex(index);
                }
            }
        }

        public static Hash128 GetComponentsInParentHash(ref GameObjectComponents components, ref SceneHierarchy hierarchy, EntityId gameObject, TypeIndex type)
        {
            var hashGenerator = new xxHash3.StreamingState(false);
            GetComponentsInParentHash(ref components, ref hierarchy, gameObject, type, ref hashGenerator);
            return new Hash128(hashGenerator.DigestHash128());
        }

        /// <summary>
        /// Replaces state of the component meta data with the current state
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="components"></param>
        /// <returns>Returns true if the game object was created</returns>
        public unsafe bool UpdateGameObject(GameObject gameObject, List<Component> currentComponentsOnGameObject, List<Component> outAddedComponents, List<Component> outExistingComponents, ref UnsafeParallelHashSet<EntityId> removed)
        {
            //TODO: DOTS-5453

            var entityId = gameObject.GetEntityId();
            int removedCount = 0;

            if (_GameObjectComponentMetaData.TryGetValue(entityId, out var componentDataList))
            {
                // Record added components, that need to be baked
                foreach (var newComponent in currentComponentsOnGameObject)
                {
                    bool found = false;
                    foreach (var oldComponent in componentDataList)
                    {
                        if (oldComponent.EntityId == newComponent.GetEntityId())
                        {
                            outExistingComponents.Add(newComponent);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        outAddedComponents.Add(newComponent);
                }

                // Record removed components
                foreach (var oldComponent in componentDataList)
                {
                    bool found = false;
                    foreach (var newComponent in currentComponentsOnGameObject)
                    {
                        if (oldComponent.EntityId == newComponent.GetEntityId())
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        removed.Add(oldComponent.EntityId);
                }

                removedCount = componentDataList.Length;
                componentDataList.Clear();
            }
            else
            {
                // There are no previous components recorded, so all current are new
                outAddedComponents.AddRange(currentComponentsOnGameObject);
                componentDataList = new UnsafeList<ComponentData>(currentComponentsOnGameObject.Count, Allocator.Persistent);
            }

            foreach (var com in currentComponentsOnGameObject)
            {
                componentDataList.Add(new ComponentData(com));
            }
            _GameObjectComponentMetaData[entityId] = componentDataList;

            return removedCount == 0;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="gameObjectEntityId"></param>
        /// <returns>Returns true if the game object was still alive.</returns>
        public bool DestroyGameObject(EntityId gameObjectEntityId)
        {
            int length = 0;
            if (_GameObjectComponentMetaData.TryGetValue(gameObjectEntityId, out var componentDataList))
            {
                length = componentDataList.Length;
                componentDataList.Dispose();
                _GameObjectComponentMetaData.Remove(gameObjectEntityId);
            }
            return length != 0;
        }

        public void Dispose()
        {
            if (_GameObjectComponentMetaData.IsCreated)
            {
                // We need to release the individual lists
                foreach (var list in _GameObjectComponentMetaData.GetValueArray(Allocator.Temp))
                {
                    list.Dispose();
                }
                _GameObjectComponentMetaData.Dispose();
            }
        }
    }
}
