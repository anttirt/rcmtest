using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Editor;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

static class EntitySelectionProxyUtility
{
    [InitializeOnLoadMethod]
    static void Subscribe()
    {
        Unity.Editor.Bridge.HandleUtilityBridge.RegisterEntityIdFromIndexResolver(GetEntityIdFromIndex);
        HandleUtility.getEntityIdsForAuthoringObject += GetEntitiesForAuthoringObject;
        HandleUtility.getAuthoringObjectForEntity += GetAuthoringObjectForEntity;
    }

    /// <summary>
    /// Resolves an entity index to an EntityId.
    /// </summary>
    /// <param name="index">The entity index to resolve.</param>
    /// <returns>The EntityId of the entity at the given index, or EntityId.None if not found in any world.</returns>
    static EntityId GetEntityIdFromIndex(int index)
    {
        var allWorlds = World.All;
        for (var i = 0; i < allWorlds.Count; ++i)
        {
            var entity = allWorlds[i].EntityManager.GetEntityByEntityIndex(index);
            if (entity != Entity.Null)
                return (EntityId)entity;
        }
        return EntityId.None;
    }

    static IEnumerable<EntityId> GetEntitiesForAuthoringObject(UnityObject obj)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
        {
            yield return EntityId.None;
        }
        else if (obj is GameObject gameObject)
        {
            var debug = world.EntityManager.Debug;
            var map = debug.GetCachedEntityGUIDToEntityIndexLookup();
            var entityId = gameObject.GetEntityId();

            foreach (var entity in map.GetValuesForKey(entityId))
            {
                yield return entity;
            }
        }
        else if (obj is EntitySelectionProxy proxy)
        {
            yield return proxy.Entity;
        }
    }

    static UnityObject GetAuthoringObjectForEntity(int entityIndex)
    {
        Entity entity = Entity.Null;
        World world = null;
        foreach (var w in World.All)
        {
            entity = w.EntityManager.GetEntityByEntityIndex(entityIndex);
            if (w.EntityManager.Exists(entity))
            {
                world = w;
                break;
            }
        }

        UnityObject authoringObject = null;
        
        if (entity == Entity.Null)
            Debug.LogWarning($"Could not find the entity with index: {entityIndex}");
        else if (world == null)
            Debug.LogWarning($"Could not find a world which Entity {entity} belongs to.");
        else
            authoringObject = world.EntityManager.Debug.GetAuthoringObjectForEntity(entity);
        
        // If we did not find the GameObject associated with this entity, try to find it in the current selection.
        // We don't want to create a new EntitySelectionProxy for an Entity that is already selected. Otherwise some features like Ctrl+click to deselect an Entity won't work.
        // For example, Ctrl+click is basically checking if the newly picked object is already in the Selection.objects in list. If this is the case, then it deselects it.
        if (authoringObject == null && Selection.objects != null)
        {
            foreach (UnityObject obj in Selection.objects)
            {
                var proxy = obj as EntitySelectionProxy;
                if (proxy != null)
                {
                    if (proxy.Entity == entity)
                    {
                        authoringObject = proxy;
                        break;
                    }
                }
            }
        }

        if (authoringObject == null && world != null)
            authoringObject = EntitySelectionProxy.CreateInstance(world, entity);

        return authoringObject;
    }
}
