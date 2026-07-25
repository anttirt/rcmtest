#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities
{
#if UNITY_EDITOR
    [InitializeOnLoad] // ensures type manager is initialized on domain reload when not playing
#endif
    static unsafe class AttachToEntityClonerInjection
    {
        static readonly Dictionary<Type, TypeIndex> s_CompanionComponentTypeIndexByType = new();

        static TypeIndex GetCompanionComponentTypeIndex(Type unityComponentType)
        {
            if (!s_CompanionComponentTypeIndexByType.TryGetValue(unityComponentType, out var typeIndex))
            {
                var closedType = typeof(CompanionComponent<>).MakeGenericType(unityComponentType);
                typeIndex = TypeManager.GetTypeIndex(closedType);
                s_CompanionComponentTypeIndexByType[unityComponentType] = typeIndex;
            }
            return typeIndex;
        }

        // Injection is used to keep everything GameObject related outside of Unity.Entities

        static AttachToEntityClonerInjection()
        {
            Initialize();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            TypeManager.Initialize();
            ManagedComponentStore.CompanionReferenceTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionReference));
            ManagedComponentStore.CompanionLinkTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionLink));
            ManagedComponentStore.CompanionLinkTransformTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionLinkTransform));
            ManagedComponentStore.InstantiateCompanionComponent = InstantiateCompanionComponentDelegate;
            ManagedComponentStore.AssignCompanionComponentsToCompanionGameObjects = AssignCompanionComponentsToCompanionGameObjectsDelegate;
        }

        /// <summary>
        /// This method will handle the cloning of Hybrid Components (if any) during the batched instantiation of an Entity
        /// </summary>
        /// <param name="srcArray">Array of source managed component indices. One per <paramref name="componentCount"/></param>
        /// <param name="componentCount">Number of component being instantiated</param>
        /// <param name="dstEntities">Array of destination entities. One per <paramref name="instanceCount"/></param>
        /// <param name="dstCompanionLinkIndices">Array of destination CompanionLink indices, can be null if the hybrid components are not owned</param>
        /// <param name="dstArray">Array of destination managed component indices. One per <paramref name="componentCount"/>*<paramref name="instanceCount"/>. All indices for the first component stored first etc.</param>
        /// <param name="instanceCount">Number of instances being created</param>
        /// <param name="managedComponentStore">Managed Store that owns the instances we create</param>
        static void InstantiateCompanionComponentDelegate(int* srcArray, int componentCount, Entity* dstEntities, int* dstCompanionLinkIndices, EntityId* dstComponentLinkIds, int* dstArray, int instanceCount, ManagedComponentStore managedComponentStore, EntityComponentStore* entityComponentStore)
        {
            if (dstCompanionLinkIndices != null)
            {
                var dstCompanionGameObjects = new GameObject[instanceCount];
                for (int i = 0; i < instanceCount; ++i)
                {
                    var companionLink = (CompanionReference)managedComponentStore.GetManagedComponent(dstCompanionLinkIndices[i]);
                    // Update referenced EntityId ID
                    companionLink.Companion.Id.entityId = dstComponentLinkIds[i];
                    dstCompanionGameObjects[i] = companionLink.Companion;
                    #if UNITY_EDITOR
                    CompanionGameObjectUtility.SetCompanionName(dstEntities[i], dstCompanionGameObjects[i]);
                    #endif
                }

                var globalSystemVersion = entityComponentStore->GlobalSystemVersion;

                for (int src = 0; src < componentCount; ++src)
                {
                    var componentType = managedComponentStore.GetManagedComponent(srcArray[src]).GetType();
                    var companionComponentTypeIndex = GetCompanionComponentTypeIndex(componentType);

                    for (int i = 0; i < instanceCount; i++)
                    {
                        var componentInInstance = dstCompanionGameObjects[i].GetComponent(componentType);
                        var dstIndex = src * instanceCount + i;
                        managedComponentStore.SetManagedComponentValue(dstArray[dstIndex], componentInInstance);

                        // Mirror the managed slot into the unmanaged CompanionComponent<T> slot.
                        // ReplicateComponents chunk-copied the source entity's stale EntityId into
                        // the dst slot; repoint it at the freshly bound component so GetCompanion<T>
                        // resolves to the clone's component, not the source's.
                        var ptr = (EntityId*)entityComponentStore->GetComponentDataWithTypeRW(dstEntities[i], companionComponentTypeIndex, globalSystemVersion);
                        *ptr = componentInInstance.GetEntityId();
                    }
                }
            }
            else
            {
                for (int src = 0; src < componentCount; ++src)
                {
                    var component = managedComponentStore.GetManagedComponent(srcArray[src]);

                    for (int i = 0; i < instanceCount; i++)
                    {
                        var dstIndex = src * instanceCount + i;
                        managedComponentStore.SetManagedComponentValue(dstArray[dstIndex], component);
                    }
                }
            }
        }

        static void AssignCompanionComponentsToCompanionGameObjectsDelegate(EntityManager entityManager, NativeArray<Entity> entities)
        {
            for (int i = 0; i < entities.Length; ++i)
            {
                var entity = entities[i];
                var companionGameObject = entityManager.GetComponentData<CompanionLink>(entity).Companion.Value;

                // Add a CompanionReference
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                entityManager.AddComponentObject(entity, new CompanionReference { Companion = companionGameObject });
                #pragma warning restore 0618

                var archetypeChunk = entityManager.GetChunk(entities[i]);
                var archetype = archetypeChunk.Archetype.Archetype;

                var types = archetype->Types;
                var firstIndex = archetype->FirstManagedComponent;
                var lastIndex = archetype->ManagedComponentsEnd;

                for (int t = firstIndex; t < lastIndex; ++t)
                {
                    ref readonly var type = ref TypeManager.GetTypeInfo(types[t].TypeIndex);

                    if (type.Category != TypeManager.TypeCategory.UnityEngineObject)
                        continue;

                    var companionComponent = companionGameObject.GetComponent(type.Type);
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    entityManager.SetComponentObject(entity, ComponentType.FromTypeIndex(type.TypeIndex), companionComponent);
                    #pragma warning restore 0618

                    // Mirror the managed slot with the unmanaged CompanionComponent<T>. Chunk-copy
                    // brought over the source entity's EntityId, which now refers to the wrong
                    // companion instance; update it to the freshly-bound component.
                    var companionComponentTypeIndex = GetCompanionComponentTypeIndex(type.Type);
                    var access = entityManager.GetCheckedEntityDataAccess();
                    var ptr = (EntityId*)access->GetComponentDataRawRW(entity, companionComponentTypeIndex);
                    *ptr = companionComponent.GetEntityId();
                }
            }

            entityManager.RemoveComponent<CompanionGameObjectActiveCleanup>(entities);
        }
    }
}
#endif
