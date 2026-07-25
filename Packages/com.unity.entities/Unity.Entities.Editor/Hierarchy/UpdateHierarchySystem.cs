#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;

namespace Unity.Entities.Editor
{
    internal enum HierarchyPrefabType
    {
        None,
        PrefabRoot,
        PrefabPart,
    }

    // Entity cached data required to create an entity node
    internal struct HierarchyEntityNodeData
    {
        public Entity Entity;
        public FixedString64Bytes EntityName;
        public HierarchyPrefabType PrefabType;
    }

    [UnityEngine.ExecuteAlways]
    [UpdateAfter(typeof(TransformSystemGroup))]
    [DisableAutoCreation]
    [BurstCompile]
    partial class UpdateHierarchySystem : SystemBase
    {
        public static System.Action<World, Entity, NativeArray<HierarchyEntityNodeData>> OnAddEntityNodes;
        public static System.Action<NativeList<Entity>> OnRemoveEntityNodes;
        public static System.Action<World, NativeArray<HierarchyEntityNodeData>, string[]> OnAddSubSceneNodes;
        public static System.Action<World> OnRemoveWorldNode;
        public static System.Action<World, NativeList<Entity>, NativeList<Entity>> OnSetParentNode;
        public static System.Action<int> OnResizeEntityHandlerMappingsCapacity;

        static readonly ProfilerMarker k_RemoveEntityNodesMarker = new ProfilerMarker("UpdateHierarchySystem.RemoveEntityNodes");
        static readonly ProfilerMarker k_CreateRootEntityNodesMarker = new ProfilerMarker("UpdateHierarchySystem.CreateRootEntityNodes");
        static readonly ProfilerMarker k_CreateChildrenEntityNodesMarker = new ProfilerMarker("UpdateHierarchySystem.CreateChildrenEntityNodes");
        static readonly ProfilerMarker k_ReparentEntityNodesMarker = new ProfilerMarker("UpdateHierarchySystem.ReparentEntityNodes");

        EntityDiffer m_EntityDiffer;
        HierarchyEntityChanges m_HierarchyEntityChanges;
        ComponentDataDiffer m_ParentChangeTracker;
        NativeHashMap<Entity, int> m_DistinctBuffer;
        EntityQuery m_SubsceneEntityQuery;

        protected override unsafe void OnCreate()
        {
            base.OnCreate();

            m_EntityDiffer = new EntityDiffer(World);
            m_HierarchyEntityChanges = new HierarchyEntityChanges(Allocator.Persistent);
            var ecs = EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            m_ParentChangeTracker = new ComponentDataDiffer(ecs, ComponentType.ReadOnly<Parent>());

            m_DistinctBuffer = new NativeHashMap<Entity, int>(16, Allocator.Persistent);
            m_SubsceneEntityQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<SceneReference>().WithNone<SceneSectionData>().Build(EntityManager);
        }

        protected override void OnDestroy()
        {
            // Must run regardless of Enabled: tests (and user code) may disable systems
            // before disposing the world, and skipping cleanup here leaks the world node
            // in the Hierarchy view and the native buffers below.
            OnRemoveWorldNode?.Invoke(World);

            m_EntityDiffer?.Dispose();
            m_HierarchyEntityChanges.Dispose();
            m_ParentChangeTracker?.Dispose();
            m_DistinctBuffer.Dispose();
            m_SubsceneEntityQuery.Dispose();

            base.OnDestroy();
        }

        // Must be called on the main thread (EditorUtility.EntityIdToObject that can only be called on the main thread)
        static HierarchyPrefabType GetPrefabType(EntityGuid entityGuid, bool hasPrefabComponent, bool hasLinkedEntityGroupComponent)
        {
            if (hasPrefabComponent && hasLinkedEntityGroupComponent)
                return HierarchyPrefabType.PrefabRoot;

            if (entityGuid != EntityGuid.Null)
            {
                var gameObject = EditorUtility.EntityIdToObject(entityGuid.OriginatingEntityId) as UnityEngine.GameObject;
                if (gameObject)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
                        return HierarchyPrefabType.PrefabRoot;

                    if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                        return HierarchyPrefabType.PrefabPart;
                }
            }

            if (hasPrefabComponent)
                return HierarchyPrefabType.PrefabPart;

            return HierarchyPrefabType.None;
        }

        HierarchyPrefabType ComputePrefabTypeForEntity(Entity entity)
        {
            bool hasPrefabComponent = EntityManager.HasComponent<Prefab>(entity);
            bool hasLinkedEntityGroupComponent = EntityManager.HasBuffer<LinkedEntityGroup>(entity);
            var guid = EntityGuid.Null;
            if (EntityManager.HasComponent<EntityGuid>(entity))
                guid = EntityManager.GetComponentData<EntityGuid>(entity);
            return GetPrefabType(guid, hasPrefabComponent, hasLinkedEntityGroupComponent);
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [BurstCompile]
        static void GetNodeName(ref EntityNameStoreAccess nameAccess, ref Entity entity, ref FixedString64Bytes name)
        {
            var entityName = EntityNameStorage.GetEntityName(entity);
            entityName.ToFixedString(ref name);
            if (name == null || name.IsEmpty)
                name = entity.ToFixedString();
        }
#endif

        // Entity comparer used to sort entity by their depth level in the hierarchy
        struct EntityLevelComparer : IComparer<Entity>
        {
            public NativeParallelHashMap<Entity, int> Levels; //Entity and their hierarchy depth

            public int Compare(Entity a, Entity b)
            {
                return Levels[a].CompareTo(Levels[b]);
            }
        }

        // Computes and returns entity depth level in the hierarchy for each entity
        void ComputeHierarchyDepthLevels(NativeList<Entity> allNodesToCreate, out NativeParallelHashMap<Entity, int> entityLevels)
        {
            entityLevels =
                new NativeParallelHashMap<Entity, int>(allNodesToCreate.Length, Allocator.Temp);
            if (allNodesToCreate.Length == 0)
                return;

            NativeQueue<Entity> queue = new NativeQueue<Entity>(Allocator.Temp);
            for (int i = 0; i < allNodesToCreate.Length; i++)
            {
                Entity root = allNodesToCreate[i];
                entityLevels[root] = 0;
                queue.Enqueue(root);
            }
            while (queue.Count > 0)
            {
                Entity current = queue.Dequeue();
                int currentLevel = entityLevels[current];

                if (!EntityManager.HasBuffer<Child>(current))
                    continue;

                var children = EntityManager.GetBuffer<Child>(current);
                for (int i = 0; i < children.Length; i++)
                {
                    Entity child = children[i].Value;

                    if (!entityLevels.ContainsKey(child))
                    {
                        entityLevels[child] = currentLevel + 1;
                        queue.Enqueue(child);
                    }
                }
            }
            queue.Dispose();
        }

        bool IsSubsceneEntity(Entity entity)
        {
            return EntityManager.HasComponent<SubScene>(entity) && !EntityManager.HasComponent<SceneSectionData>(entity);
        }

        internal static string GetSubSceneName(EntityManager entityManager, Entity entity)
        {
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            var subScene = entityManager.GetComponentObject<SubScene>(entity);
            #pragma warning restore 0618
            return subScene.SceneName;
        }

        /// <summary>
        /// Returns the subscene entity that an entity belongs to
        /// </summary>
        Entity GetSubsceneEntity(Entity entity)

        {
            // Section entities reference the entity scene they belong in SceneEntityReference
            if (EntityManager.HasComponent<SceneEntityReference>(entity))
                return EntityManager.GetComponentData<SceneEntityReference>(entity).SceneEntity;

            // Regular entities have either a SceneReference or a SceneSection component referencing the subscene
            // It is not worth checking the SceneTag component as it doesn't always point to the subscene but the section entity
            Hash128 sceneGuid = default;
            if (EntityManager.HasComponent<SceneReference>(entity))
                sceneGuid = EntityManager.GetComponentData<SceneReference>(entity).SceneGUID;
            else if (EntityManager.HasComponent<SceneSection>(entity))
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                sceneGuid = EntityManager.GetSharedComponentManaged<SceneSection>(entity).SceneGUID;
                #pragma warning restore 0618

            var subsceneEntities = m_SubsceneEntityQuery.ToEntityArray(Allocator.Temp);
            Entity subsceneEntity = Entity.Null;
            foreach (var e in subsceneEntities)
            {
                var subsceneReference = EntityManager.GetComponentData<SceneReference>(e);
                if (sceneGuid == subsceneReference.SceneGUID)
                {
                    subsceneEntity =  e;
                }
            }
            subsceneEntities.Dispose();
            return subsceneEntity;
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            // Retrieve all changes (new created and destroyed entities) from the EntityDiffer
            m_HierarchyEntityChanges.Clear();
            var entityChangesJobHandle = m_EntityDiffer.GetEntityQueryMatchDiffAsync(
                EntityManager.UniversalQueryWithSystems, m_HierarchyEntityChanges.CreatedEntities,
                m_HierarchyEntityChanges.DestroyedEntities);
            var parentQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent>().Build(EntityManager);
            var parentChanges = m_ParentChangeTracker.GatherComponentChangesAsync(parentQuery, Allocator.TempJob, out var parentComponentChangesJobHandle);
            JobHandle.CombineDependencies(entityChangesJobHandle, parentComponentChangesJobHandle).Complete();

            parentChanges.GetAddedComponentEntities(m_HierarchyEntityChanges.AddedParentEntities);
            parentChanges.GetRemovedComponentEntities(m_HierarchyEntityChanges.RemovedParentEntities);
            parentChanges.GetAddedComponentData(m_HierarchyEntityChanges.AddedParentComponents);
            parentChanges.GetRemovedComponentData(m_HierarchyEntityChanges.RemovedParentComponents);

            parentChanges.Dispose();
            parentQuery.Dispose();

            // If there are no changes no need to update the hierarchy
            if (!m_HierarchyEntityChanges.HasChanges())
                return;

            // Remove duplicate changes
            new DistinctJob { Changes = m_HierarchyEntityChanges, DistinctBuffer = m_DistinctBuffer }.Run();

            using (k_RemoveEntityNodesMarker.Auto())
            {
                // Remove first any destroyed entities
                if (!m_HierarchyEntityChanges.DestroyedEntities.IsEmpty)
                {
                    OnRemoveEntityNodes?.Invoke(m_HierarchyEntityChanges.DestroyedEntities);
                }
            }

            if (!m_HierarchyEntityChanges.CreatedEntities.IsEmpty)
                CreateNewNodes();

            using (k_ReparentEntityNodesMarker.Auto())
            {
                // Reparenting
                if (m_HierarchyEntityChanges.HasReparentingChanges())
                {
                    var children = new NativeList<Entity>(m_HierarchyEntityChanges.AddedParentEntities.Length + m_HierarchyEntityChanges.RemovedParentEntities.Length, Allocator.Temp);
                    var newParents = new NativeList<Entity>(m_HierarchyEntityChanges.AddedParentEntities.Length + m_HierarchyEntityChanges.RemovedParentEntities.Length, Allocator.Temp);

                    // First, add all entities that got a new parent
                    children.AddRange(m_HierarchyEntityChanges.AddedParentEntities);
                    for (var i = 0; i < m_HierarchyEntityChanges.AddedParentEntities.Length; i++)
                        newParents.Add(m_HierarchyEntityChanges.AddedParentComponents[i].Value);

                    // Create a set of entities that are getting a new parent (to avoid processing them twice)
                    var addedEntitiesSet = new NativeHashSet<Entity>(m_HierarchyEntityChanges.AddedParentEntities.Length, Allocator.Temp);
                    for (var i = 0; i < m_HierarchyEntityChanges.AddedParentEntities.Length; i++)
                        addedEntitiesSet.Add(m_HierarchyEntityChanges.AddedParentEntities[i]);

                    // Then, add entities that lost their parent (but only if they didn't also get a new parent)
                    foreach (var entity in m_HierarchyEntityChanges.RemovedParentEntities)
                    {
                        if (!addedEntitiesSet.Contains(entity))
                        {
                            children.Add(entity);
                            var subsceneEntity = GetSubsceneEntity(entity);
                            newParents.Add(subsceneEntity != default ? subsceneEntity : default);
                        }
                    }

                    OnSetParentNode?.Invoke(World, children, newParents);

                    addedEntitiesSet.Dispose();
                    children.Dispose();
                    newParents.Dispose();
                }
            }
        }

        unsafe void CreateNewNodes()
        {
#if !DOTS_DISABLE_DEBUG_NAMES
            var entityNameStoreAccess = EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->NameStoreAccess;
#endif
            var parents = new NativeList<Entity>(m_HierarchyEntityChanges.CreatedEntities.Length, Allocator.TempJob);
            var looseChildren = new NativeList<HierarchyEntityNodeData>(m_HierarchyEntityChanges.CreatedEntities.Length, Allocator.Temp);
            // Count of entities with Parent component (children in Child buffers)
            var childrenEntityCount = 0;
            // Count of additional entities in LinkedEntityGroups that don't have Parent component
            var additionalLinkedEntityCount = 0;

            var showHiddenEntities = HierarchyEntitiesSettings.GetShowHiddenEntities();

            // Creates first subscene entity nodes
            var subsceneEntityNodes = new NativeList<HierarchyEntityNodeData>(m_HierarchyEntityChanges.CreatedEntities.Length,Allocator.Temp);
            var subsceneNodeNames = new List<string>();
            // Then creates any other entity nodes that doesn't have a parent and must be parented below a scene node it belongs to
            var rootEntityNodesPerSubscene = new NativeParallelMultiHashMap<Entity, HierarchyEntityNodeData>(m_HierarchyEntityChanges.CreatedEntities.Length, Allocator.TempJob);
            // Array of unique keys (subscenes) of the NativeParallelMultiHashMap rootEntityNodesPerSubscene.
            // GetUniqueKeyArray only return unique pairs not unique keys
            var rootEntityCountPerSubscene =  new NativeHashMap<Entity, int>(m_HierarchyEntityChanges.CreatedEntities.Length, Allocator.Temp);

            // Set of entities that are in LinkedEntityGroups of prefab parents.
            // These entities should not be added as root entities since they will be added as children of the prefab.
            var linkedEntityGroupChildren = new NativeHashSet<Entity>(m_HierarchyEntityChanges.CreatedEntities.Length, Allocator.Temp);
            // Candidate root entities - will be filtered after we know all LinkedEntityGroup children
            var candidateRootEntities = new NativeList<(Entity sceneEntity, HierarchyEntityNodeData data)>(m_HierarchyEntityChanges.CreatedEntities.Length, Allocator.Temp);

            for (var i = 0; i < m_HierarchyEntityChanges.CreatedEntities.Length; i++)
            {
                var e = m_HierarchyEntityChanges.CreatedEntities[i];
                if (!showHiddenEntities && EntityManager.HasComponent<HideInHierarchy>(e))
                    continue;

#if DOTS_DISABLE_DEBUG_NAMES
                var name = e.ToFixedString();
#else
                var name = default(FixedString64Bytes);
                GetNodeName(ref entityNameStoreAccess, ref e, ref name);
#endif
                var prefabType = ComputePrefabTypeForEntity(e);

                // Collect LinkedEntityGroup children from prefab parents
                if (EntityManager.HasComponent<Prefab>(e) && EntityManager.HasBuffer<LinkedEntityGroup>(e))
                {
                    var leg = EntityManager.GetBuffer<LinkedEntityGroup>(e);
                    for (var j = 0; j < leg.Length; j++)
                    {
                        if (leg[j].Value != e)
                            linkedEntityGroupChildren.Add(leg[j].Value);
                    }
                }

                // Root entities can be subscene, section, prefab roots, root entities
                if (!SystemAPI.TryGetComponent(e, out Parent parentComp))
                {
                    if (IsSubsceneEntity(e))
                    {
                        var data = new HierarchyEntityNodeData()
                        {
                            Entity = e,
                            EntityName = default,
                            PrefabType = prefabType
                        };
                        subsceneNodeNames.Add(GetSubSceneName(EntityManager, e));
                        subsceneEntityNodes.Add(data);
                    }
                    else
                    {
                        // Collect as candidate root entity, will filter later
                        var data = new HierarchyEntityNodeData()
                        {
                            Entity = e, EntityName = name, PrefabType = prefabType
                        };
                        var sceneEntity = GetSubsceneEntity(e);
                        candidateRootEntities.Add((sceneEntity, data));
                    }
                }
                else
                {
                    // Loose child. Can happen when the parent is disabled and not picked up by the ParentSystem.
                    if (!IsParent(parentComp.Value))
                    {
                        var data = new HierarchyEntityNodeData()
                        {
                            Entity = e, EntityName = name, PrefabType = prefabType
                        };
                        looseChildren.Add(data);
                    }

                    childrenEntityCount++;
                }

                // Parents can be entities with a Child component or a LinkedEntityGroup for Prefabs
                if (IsParent(e))
                {
                    parents.Add(e);
                }
            }

            // Filter candidate root entities - skip those that are in a LinkedEntityGroup of a prefab parent
            for (var i = 0; i < candidateRootEntities.Length; i++)
            {
                var (sceneEntity, data) = candidateRootEntities[i];
                if (linkedEntityGroupChildren.Contains(data.Entity))
                    continue;

                rootEntityNodesPerSubscene.Add(sceneEntity, data);
                if (rootEntityCountPerSubscene.TryGetValue(sceneEntity, out var entityCount))
                {
                    entityCount++;
                    rootEntityCountPerSubscene[sceneEntity] = entityCount;
                }
                else
                {
                    rootEntityCountPerSubscene.TryAdd(sceneEntity, 1);
                }
            }
            candidateRootEntities.Dispose();

            // Add any new subscene entity nodes below the world node
            OnAddSubSceneNodes?.Invoke(World, subsceneEntityNodes.AsArray(), subsceneNodeNames.ToArray());
            subsceneEntityNodes.Dispose();
            linkedEntityGroupChildren.Dispose();

            OnAddEntityNodes?.Invoke(World, Entity.Null, looseChildren.AsArray());
            looseChildren.Dispose();

            using (k_CreateRootEntityNodesMarker.Auto())
            {
                var subsceneEntitiesArray = rootEntityCountPerSubscene.GetKeyArray(Allocator.Temp);

                // Add all new entity root nodes below their scene entity node (including section entities)
                for (var i = 0; i < subsceneEntitiesArray.Length; i++)
                {
                    var sceneEntity = subsceneEntitiesArray[i];
                    var count = rootEntityCountPerSubscene[sceneEntity];
                    var nodeArray = new NativeArray<HierarchyEntityNodeData>(count, Allocator.TempJob);
                    if (rootEntityNodesPerSubscene.TryGetFirstValue(sceneEntity, out var child, out var it))
                    {
                        var j = 0;
                        do
                        {
                            nodeArray[j] = child;
                            j++;
                        } while (rootEntityNodesPerSubscene.TryGetNextValue(out child, ref it));
                    }

                    OnAddEntityNodes?.Invoke(World, sceneEntity, nodeArray);
                    nodeArray.Dispose();
                }

                subsceneEntitiesArray.Dispose();
                rootEntityNodesPerSubscene.Dispose();
                rootEntityCountPerSubscene.Dispose();
            }

            // If there are only root nodes, no more nodes to create
            if (parents.Length == 0)
            {
                parents.Dispose();
                return;
            }

            // Populate the HierarchyEntityNodeData buffer used to create hierarchy entity nodes
            var childLookup = GetBufferLookup<Child>(true);
            var hideInHierarchyLookup = GetComponentLookup<HideInHierarchy>(true);
            var linkedEntityGroupLookup = GetBufferLookup<LinkedEntityGroup>(true);
            var prefabLookup = GetComponentLookup<Prefab>(true);
            var parentLookup = GetComponentLookup<Parent>(true);

            // Pre-compute prefab types on main thread
            var prefabTypeLookup = new NativeHashMap<Entity, HierarchyPrefabType>(childrenEntityCount, Allocator.TempJob);
            for (var i = 0; i < parents.Length; i++)
            {
                var parent = parents[i];
                if (EntityManager.HasBuffer<Child>(parent))
                {
                    var children = EntityManager.GetBuffer<Child>(parent);
                    for (var j = 0; j < children.Length; j++)
                    {
                        var childEntity = children[j].Value;
                        if (!showHiddenEntities && EntityManager.HasComponent<HideInHierarchy>(childEntity))
                            continue;
                        prefabTypeLookup[childEntity] = ComputePrefabTypeForEntity(childEntity);
                    }
                }

                // For prefabs, also pre-compute types for additional LinkedEntityGroup entities (without Parent component)
                if (EntityManager.HasComponent<Prefab>(parent) && EntityManager.HasBuffer<LinkedEntityGroup>(parent))
                {
                    var linkedEntities = EntityManager.GetBuffer<LinkedEntityGroup>(parent);
                    for (var j = 0; j < linkedEntities.Length; j++)
                    {
                        var childEntity = linkedEntities[j].Value;
                        if (childEntity == parent)
                            continue;
                        if (EntityManager.HasComponent<HideInHierarchy>(childEntity))
                            continue;
                        // Only count entities without Parent component - these are additional entities added after baking
                        if (EntityManager.HasComponent<Parent>(childEntity))
                            continue;
                        if (!prefabTypeLookup.ContainsKey(childEntity))
                        {
                            prefabTypeLookup[childEntity] = ComputePrefabTypeForEntity(childEntity);
                            additionalLinkedEntityCount++;
                        }
                    }
                }
            }

            var childrenEntityNodeData =
                new NativeParallelMultiHashMap<Entity, HierarchyEntityNodeData>(parents.Length + childrenEntityCount + additionalLinkedEntityCount, Allocator.TempJob);
            var populateBufferJob = new PopulateParentBuffersJob
            {
                Entities = parents,
                ChildLookup = childLookup,
                ShowHiddenEntities = showHiddenEntities,
                HideInHierarchyLookup = hideInHierarchyLookup,
                LinkedEntityGroupLookup = linkedEntityGroupLookup,
                PrefabLookup = prefabLookup,
                ParentLookup = parentLookup,
                PrefabTypeLookup = prefabTypeLookup,
                OutputBuffers = childrenEntityNodeData.AsParallelWriter(),
#if !DOTS_DISABLE_DEBUG_NAMES
                NameStoreAccess = EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->NameStoreAccess,
#endif
            };
            Dependency = populateBufferJob.Schedule(parents.Length, 64, Dependency);
            Dependency.Complete();

            // Sort all parents by their level, to make sure we will create parent node before their children
            ComputeHierarchyDepthLevels(parents, out NativeParallelHashMap<Entity, int> entityLevels);
            parents.Sort(new EntityLevelComparer { Levels = entityLevels });

            using (k_CreateChildrenEntityNodesMarker.Auto())
            {
                // Update the HierarchyEntityHandler mappings capacity first
                var newChildrenNodesCount = 0;
                for (var i = 0; i < parents.Length; i++)
                {
                    newChildrenNodesCount += childrenEntityNodeData.CountValuesForKey(parents[i]);
                }

                OnResizeEntityHandlerMappingsCapacity?.Invoke(newChildrenNodesCount);

                // Add each parent and their children to the hierarchy together in a single command
                for (var i = 0; i < parents.Length; i++)
                {
                    var parent = parents[i];
                    var nodeDataArray =
                        new NativeList<HierarchyEntityNodeData>(childrenEntityNodeData.CountValuesForKey(parent),
                            Allocator.Temp);
                    if (childrenEntityNodeData.TryGetFirstValue(parent, out HierarchyEntityNodeData data, out var it))
                    {
                        do
                        {
                            nodeDataArray.Add(data);
                        } while (childrenEntityNodeData.TryGetNextValue(out data, ref it));
                    }

                    OnAddEntityNodes?.Invoke(World, parent, nodeDataArray.AsArray());
                    nodeDataArray.Dispose();
                }
            }

            entityLevels.Dispose();
            childrenEntityNodeData.Dispose();
            prefabTypeLookup.Dispose();
            parents.Dispose();
        }

        bool IsParent(Entity e)
        {
            return EntityManager.HasComponent<Child>(e) ||
                   (EntityManager.HasBuffer<LinkedEntityGroup>(e) &&
                    EntityManager.HasComponent<Prefab>(e));
        }

        [BurstCompile]
        struct PopulateParentBuffersJob : IJobParallelFor
        {
            [ReadOnly] public NativeList<Entity> Entities;
            [ReadOnly] public BufferLookup<Child> ChildLookup;
            [ReadOnly] public bool ShowHiddenEntities;
            [ReadOnly] public ComponentLookup<HideInHierarchy> HideInHierarchyLookup;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> LinkedEntityGroupLookup;
            [ReadOnly] public ComponentLookup<Prefab> PrefabLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public NativeHashMap<Entity, HierarchyPrefabType> PrefabTypeLookup;

#if !DOTS_DISABLE_DEBUG_NAMES
            [ReadOnly] public EntityNameStoreAccess NameStoreAccess;
#endif

            [NativeDisableParallelForRestriction]
            public NativeParallelMultiHashMap<Entity, HierarchyEntityNodeData>.ParallelWriter OutputBuffers;

            public void Execute(int index)
            {
                var e = Entities[index];

                if (ChildLookup.HasBuffer(e))
                {
                    var children = ChildLookup[e];

                    for (var i = 0; i < children.Length; i++)
                    {
                        var childEntity = children[i].Value;
                        if (!ShowHiddenEntities && HideInHierarchyLookup.HasComponent(childEntity))
                            continue;

                        AddChildNode(e, childEntity);
                    }
                }

                // For prefabs, also add additional entities from LinkedEntityGroup.
                // These are entities added to the LinkedEntityGroup after baking that don't have a Parent component.
                // Entities with Parent component will be handled by their actual parent's Child buffer.
                if (PrefabLookup.HasComponent(e) && LinkedEntityGroupLookup.HasBuffer(e))
                {
                    var linkedEntities = LinkedEntityGroupLookup[e];
                    for (var i = 0; i < linkedEntities.Length; i++)
                    {
                        var linkedEntity = linkedEntities[i].Value;

                        // Skip the parent entity itself
                        if (linkedEntity == e)
                            continue;

                        if (HideInHierarchyLookup.HasComponent(linkedEntity))
                            continue;

                        // Skip entities that have a Parent component - they will be added by their actual parent if they have a child component
                        if (ParentLookup.HasComponent(linkedEntity) && ChildLookup.HasBuffer(e))
                            continue;

                        AddChildNode(e, linkedEntity);
                    }
                }
            }

            void AddChildNode(Entity parent, Entity childEntity)
            {
                var prefabType = HierarchyPrefabType.None;
                if (PrefabTypeLookup.TryGetValue(childEntity, out var precomputedType))
                    prefabType = precomputedType;

#if DOTS_DISABLE_DEBUG_NAMES
                var name = childEntity.ToFixedString();
#else
                var name = default(FixedString64Bytes);
                GetNodeName(ref NameStoreAccess, ref childEntity, ref name);
#endif

                var data = new HierarchyEntityNodeData
                {
                    Entity = childEntity, EntityName = name,
                    PrefabType = prefabType
                };
                OutputBuffers.Add(parent, data);
            }
        }

        [BurstCompile]
        struct DistinctJob : IJob
        {
            public HierarchyEntityChanges Changes;
            public NativeHashMap<Entity, int> DistinctBuffer;

            public void Execute()
            {
                if (Changes.CreatedEntities.Length > 0 && Changes.DestroyedEntities.Length > 0)
                    RemoveDuplicate(DistinctBuffer, Changes.CreatedEntities, Changes.DestroyedEntities);

                CleanupRemovedEntities(ref Changes);

                if (Changes.AddedParentEntities.Length > 0 && Changes.RemovedParentEntities.Length > 0)
                    RemoveDuplicate(DistinctBuffer, Changes.AddedParentEntities, Changes.RemovedParentEntities, Changes.AddedParentComponents, Changes.RemovedParentComponents);

                if (Changes.AddedSceneTagWithoutParentEntities.Length > 0 && Changes.RemovedSceneTagWithoutParentEntities.Length > 0)
                    RemoveDuplicate(DistinctBuffer, Changes.AddedSceneTagWithoutParentEntities, Changes.RemovedSceneTagWithoutParentEntities, Changes.AddedSceneTagWithoutParentComponents);
            }

            static void RemoveDuplicate(NativeHashMap<Entity, int> index,
                NativeList<Entity> added,
                NativeList<Entity> removed)
            {
                index.Clear();

                var addedLength = added.Length;
                var removedLength = removed.Length;

                for (var i = 0; i < addedLength; i++)
                    index[added[i]] = i + 1;

                for (var i = 0; i < removedLength; i++)
                {
                    // TryGetValue returns default is the value is missing.
                    index.TryGetValue(removed[i], out var addIndex);
                    addIndex -= 1;

                    if (addIndex < 0)
                        continue;

                    // An entity was recorded as added AND removed with the same index.
                    var addedEntity = added[addIndex];
                    var removedEntity = removed[i];

                    if (addedEntity.Version != removedEntity.Version)
                        continue;

                    // Swap back
                    added[addIndex] = added[addedLength - 1];
                    removed[i] = removed[removedLength - 1];

                    index[added[addIndex]] = addIndex + 1;

                    addedLength--;
                    removedLength--;
                    i--;
                }

                added.ResizeUninitialized(addedLength);
                removed.ResizeUninitialized(removedLength);
            }

            static void CleanupRemovedEntities(ref HierarchyEntityChanges changes)
            {
                var added = changes.CreatedEntities;
                var destroyed = changes.DestroyedEntities;
                var addedParents = changes.AddedParentEntities;
                var addedParentComponents = changes.AddedParentComponents;
                var removedParents = changes.RemovedParentEntities;
                var removedParentComponents = changes.RemovedParentComponents;
                var addedSceneTag = changes.AddedSceneTagWithoutParentEntities;
                var addedSceneTagComponents = changes.AddedSceneTagWithoutParentComponents;
                var removedSceneTag = changes.RemovedSceneTagWithoutParentEntities;

                var addedSet = new NativeHashSet<Entity>(added.Count, Allocator.Temp);
                for (var i = 0; i < added.Count; ++i)
                    addedSet.Add(added[i]);

                var destroyedSet = new NativeHashSet<Entity>(destroyed.Count, Allocator.Temp);
                for (var i = 0; i < destroyed.Count; ++i)
                    destroyedSet.Add(destroyed[i]);

                for (var i = addedParents.Count - 1; i >= 0; --i)
                {
                    if (addedSet.Contains(addedParents[i]) || destroyedSet.Contains(addedParents[i]))
                    {
                        addedParents.RemoveAtSwapBack(i);
                        addedParentComponents.RemoveAtSwapBack(i);
                    }
                }
                for (var i = removedParents.Count - 1; i >= 0; --i)
                {
                    if (addedSet.Contains(removedParents[i]) || destroyedSet.Contains(removedParents[i]))
                    {
                        removedParents.RemoveAtSwapBack(i);
                        removedParentComponents.RemoveAtSwapBack(i);
                    }
                }
                for (var i = addedSceneTag.Count - 1; i >= 0; --i)
                {
                    if (addedSet.Contains(addedSceneTag[i]) || destroyedSet.Contains(addedSceneTag[i]))
                    {
                        addedSceneTag.RemoveAtSwapBack(i);
                        addedSceneTagComponents.RemoveAtSwapBack(i);
                    }
                }
                for (var i = removedSceneTag.Count - 1; i >= 0; --i)
                {
                    if (addedSet.Contains(removedSceneTag[i]) || destroyedSet.Contains(removedSceneTag[i]))
                        removedSceneTag.RemoveAtSwapBack(i);
                }

                addedSet.Dispose();
                destroyedSet.Dispose();
            }

            static void RemoveDuplicate<TData>(NativeHashMap<Entity, int> index,
                NativeList<Entity> added,
                NativeList<Entity> removed,
                NativeList<TData> data) where TData : unmanaged
            {
                index.Clear();

                var addedLength = added.Length;
                var removedLength = removed.Length;

                for (var i = 0; i < addedLength; i++)
                    index[added[i]] = i + 1;

                for (var i = 0; i < removedLength; i++)
                {
                    // TryGetValue returns default is the value is missing.
                    index.TryGetValue(removed[i], out var addIndex);
                    addIndex -= 1;

                    if (addIndex < 0)
                        continue;

                    // An entity was recorded as added AND removed with the same index.
                    var addedEntity = added[addIndex];
                    var removedEntity = removed[i];

                    if (addedEntity.Version != removedEntity.Version)
                        continue;

                    // Swap back
                    added[addIndex] = added[addedLength - 1];
                    data[addIndex] = data[addedLength - 1];
                    removed[i] = removed[removedLength - 1];

                    index[added[addIndex]] = addIndex + 1;

                    addedLength--;
                    removedLength--;
                    i--;
                }

                added.ResizeUninitialized(addedLength);
                data.ResizeUninitialized(addedLength);
                removed.ResizeUninitialized(removedLength);
            }

            static unsafe void RemoveDuplicate<TData>(
                NativeHashMap<Entity, int> index,
                NativeList<Entity> addedEntities,
                NativeList<Entity> removedEntities,
                NativeList<TData> addedData,
                NativeList<TData> removedData) where TData : unmanaged
            {
                index.Clear();

                var addedLength = addedEntities.Length;
                var removedLength = removedEntities.Length;

                for (var i = 0; i < addedLength; i++)
                    index[addedEntities[i]] = i + 1;

                for (var i = 0; i < removedLength; i++)
                {
                    // TryGetValue returns default is the value is missing.
                    index.TryGetValue(removedEntities[i], out var addIndex);
                    addIndex -= 1;

                    if (addIndex < 0)
                        continue;

                    var a = addedData[addIndex];
                    var b = removedData[i];

                    // Only filter if the data is the same.
                    if (UnsafeUtility.MemCmp(&a, &b, sizeof(TData)) != 0)
                        continue;

                    // An entity was recorded as added AND removed with the same index.
                    var addedEntity = addedEntities[addIndex];
                    var removedEntity = removedEntities[i];

                    if (addedEntity.Version != removedEntity.Version)
                        continue;

                    // Swap back
                    addedEntities[addIndex] = addedEntities[addedLength - 1];
                    addedData[addIndex] = addedData[addedLength - 1];
                    removedEntities[i] = removedEntities[removedLength - 1];
                    removedData[i] = removedData[removedLength - 1];

                    index[addedEntities[addIndex]] = addIndex + 1;

                    addedLength--;
                    removedLength--;
                }

                addedEntities.ResizeUninitialized(addedLength);
                addedData.ResizeUninitialized(addedLength);
                removedEntities.ResizeUninitialized(removedLength);
            }
        }
    }
#endif
}
