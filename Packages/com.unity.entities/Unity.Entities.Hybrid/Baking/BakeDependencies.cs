using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using SceneHierarchy = Unity.Entities.Conversion.SceneHierarchy;

namespace Unity.Entities.Baking
{
    internal struct ChangedSceneTransforms
    {
        public SceneHierarchy                  Hierarchy;
        public NativeList<TransformAuthoring>  Transforms;
        public NativeList<EntityId>                 ChangedLocalToWorldIndices;
    }

    /// <summary>
    /// Bake dependencies are done in three parts
    /// * Fastpath for no structural changes. Properties of a component / asset has changed.
    /// * Structural changes  (GameObject create / destroy, component remove / add, prefab instantiate, reparent etc)
    /// * Assets on disk have changed. (Eg. a material or prefab was modified outside of Unity on disk. For example by pulling latest from version control)
    ///
    /// The fastpath is what needs to run at less than 5ms overhead even on big scenes. Eg. when using a slider or moving a transform around in the scene view.
    /// We need that to have practically no overhead, otherwise it affects the tweaking / live editor experience.
    /// When falling off the fastpath (Add component / asset changed etc...) it is ok if those changes take ~50ms for small changes. Eg. Adding a component / destroying an object,
    /// it's practically impossible to feel if you get a 50ms spike there or not since you click a button, let go of the mouse and only then does the action apply.
    ///
    ///
    /// 1. The fastpath assumes that all GetComponent / GetAncestor component will return the exact same component every time.
    /// Thus all we need is a dependency from the baker -> all the component instanceIDs it is reading from
    /// These are stored in _PropertyChangeReverseDependency and _PropertyChangeDependency. They have reverse dependencies and thus are very fast to evaluate.
    ///
    /// 2. If there are any structural changes whatsover, we re-evaluate all structural dependencies.
    /// Eg. GetComponentInParent has to result in the same component, so any added / removed component in the hierarchy can invalidate the baker
    /// This is quite difficult to express with reverse dependencies (Adding the components lets us directly know what baker to invalidate)
    ///
    /// Thus we take the easy road... If we detect that there were any structural changes.
    /// We bruteforce check all dependencies by simply recalculating if the GetComponentInAncestor will now with the current state of the world result in the same instanceID.
    /// The idea here is to just make it bruteforce fast instead of smart, meaning we use a bunch of hashtables to make GetComponentInParent etc fast.
    ///
    /// 3. Asset dependencies. AssetDatabase.GlobalArtifactDependencyVersion is a simple way to detect that no asset on disk has changed.
    /// If anything at all changed, we have to re-check all asset guids that were referenced by the scene.
    /// </summary>
    [BurstCompile]
    struct BakeDependencies : IDisposable
    {
        // NOTE:
        // Most dependents are tracked at the Unity Component (EntityId) level. This means even in the case of multiple Baker types per Unity Component type
        // we will have to re-run every Baker if any dependency triggers that Unity Component to be re-baked.
        //
        // e.g.
        // MyBaker : Baker<MyComponent>{} AND MyOtherBaker : Baker<MyComponent>{}
        // If a dependency expressed in MyBaker triggers an instance of MyComponent to be re-baked, we will re-run MyBaker AND MyOtherBaker.
        // It may make sense in future to change this to track dependencies at a Baker type AND EntityId level, so that we could reduce the work performed.

        // Unity Component (EntityId) -> Dependent Object EntityId
        UnsafeParallelMultiHashMap<EntityId, EntityId>                        _PropertyChangeDependency;
        // Dependent Object EntityId ->  Unity Component (EntityId)
        UnsafeParallelMultiHashMap<EntityId, EntityId>                        _ReversePropertyChangeDependency;
        // Is _ReversePropertyChangeDependency up to date with _PropertyChangeDependency?
        // For performance reasons we don't immediately add / remove to _ReversePropertyChangeDependency.
        // Instead if any dependencies have changed, we rebuild the _ReversePropertyChangeDependency from scratch.
        // Thus every change to _PropertyChangeDependency must set _IsReversePropertyChangeDependencyUpToDate = 0
        // NOTE: This is an int to make burst happy. (Can't marshal bools via struct in a function pointer)
        int                                                 _IsReversePropertyChangeDependencyUpToDate;

        // Baker (EntityId) -> ActiveDependency
        UnsafeParallelMultiHashMap<EntityId, ActiveDependency>               _ActiveDependencies;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, GetComponentDependency>         _StructuralGetComponentDependency;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, GetComponentsDependency>        _StructuralGetComponentsDependency;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, GetHierarchySingleDependency>   _StructuralGetHierarchySingleDependency;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, GetHierarchyDependency>         _StructuralGetHierarchyDependency;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, ObjectExistDependency>          _StructuralObjectExistDependency;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency>   _ObjectPropertyDependency;
        // Dependent Object (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency>   _ReverseObjectPropertyDependency;

        // Baker (EntityId) -> Dependency data
        UnsafeParallelMultiHashMap<EntityId, ObjectStaticDependency>     _ObjectStaticDependency;

        // Baker (EntityId) -> Dependency on Light Baking
        UnsafeHashSet<EntityId>                                              _LightBakingDependency;

#if UNITY_EDITOR
        internal struct AssetState
        {
            public GUID GUID;
            public Hash128 Hash;

            public AssetState(GUID guid, Hash128 hash)
            {
                GUID = guid;
                Hash = hash;
            }
        }

        // GUID -> Last known hash on disk
        UnsafeParallelHashSet<GUID>                                 _AssetStateKeys;
        UnsafeList<AssetState>                              _AssetState;
        // Unity Component (EntityId) -> GUID
        UnsafeParallelMultiHashMap<EntityId, GUID>                       _ComponentIdToAssetGUID;
#endif

        static readonly string CalculateDependenciesMarkerStr         = "Dependencies.CalculateDependencies";
        static readonly string AssetDependenciesMarkerStr             = "Dependencies.CalculateAssetDependencies";
        static readonly string StructuralDependenciesMarkerStr        = "Dependencies.CalculateStructuralDependencies";
        static readonly string NonStructuralDependenciesMarkerStr     = "Dependencies.CalculateNonStructuralDependencies";
        static readonly string ObjectExistDependenciesMarkerStr       = "Dependencies.CalculateObjectExistDependencies";
        static readonly string InstanceIDsToValidArrayMarkerStr       = "InstanceIDsToValidArrayMarkerStr";

        static readonly ProfilerMarker CalculateDependenciesMarker             = new ProfilerMarker(CalculateDependenciesMarkerStr);
        static readonly ProfilerMarker AssetDependenciesMarker                 = new ProfilerMarker(AssetDependenciesMarkerStr);
        static readonly ProfilerMarker StructuralDependenciesMarker            = new ProfilerMarker(StructuralDependenciesMarkerStr);
        static readonly ProfilerMarker NonStructuralDependenciesMarker         = new ProfilerMarker(NonStructuralDependenciesMarkerStr);
        static readonly ProfilerMarker ObjectExistDependenciesMarker           = new ProfilerMarker(ObjectExistDependenciesMarkerStr);
        static readonly ProfilerMarker InstanceIDsToValidArrayMarker           = new ProfilerMarker(InstanceIDsToValidArrayMarkerStr);

        internal static string[] CollectImportantProfilerMarkerStrings()
        {
            return new string [] {
                CalculateDependenciesMarkerStr,

                // Structural Dependencies
                StructuralDependenciesMarkerStr,
                BurstFunctionName(nameof(CalculateStructuralGetComponentDependencyJob)),
                BurstFunctionName(nameof(CalculateStructuralGetComponentsDependencyJob)),
                BurstFunctionName(nameof(CalculateActiveDependenciesJob)),

                // Non Structural Dependencies
                NonStructuralDependenciesMarkerStr,
                BurstFunctionName(nameof(CalculateReversePropertyChangeDependencyJob)),
                BurstFunctionName(nameof(NonStructuralChangedComponentJob)),
                BurstFunctionName(nameof(NonStructuralChangedAssetsJob)),

                // Asset Dependencies
                AssetDependenciesMarkerStr,
#if UNITY_EDITOR
                BurstFunctionName(nameof(PrepareAssetDataJob)),
                BurstFunctionName(nameof(CalculateAssetDependenciesJob)),
#endif

                // Object Exists
                ObjectExistDependenciesMarkerStr
            };
        }

        internal struct RecordedDependencies
        {
            internal UnsafeList<EntityId>                       ObjectReference;
            internal UnsafeList<EntityId>                       PersistentAsset;
            internal UnsafeList<GetComponentDependency>         GetComponent;
            internal UnsafeList<GetHierarchySingleDependency>   GetHierarchySingle;
            internal UnsafeList<GetHierarchyDependency>         GetHierarchy;
            internal UnsafeList<GetComponentsDependency>        GetComponents;
            internal UnsafeList<ObjectExistDependency>          ObjectExist;
            internal UnsafeList<ObjectPropertyDependency>       ObjectProperty;
            internal UnsafeList<ObjectStaticDependency>         ObjectStatic;
            internal UnsafeList<ActiveDependency>               Active;
            internal int                                        LightBaking;

            public RecordedDependencies(int capacity, Allocator allocator)
            {
                ObjectReference = new UnsafeList<EntityId>(capacity, allocator);
                PersistentAsset = new UnsafeList<EntityId>(capacity, allocator);
                GetComponent = new UnsafeList<GetComponentDependency>(capacity, allocator);
                GetHierarchySingle = new UnsafeList<GetHierarchySingleDependency>(capacity, allocator);
                GetHierarchy = new UnsafeList<GetHierarchyDependency>(capacity, allocator);
                GetComponents = new UnsafeList<GetComponentsDependency>(capacity, allocator);
                ObjectExist = new UnsafeList<ObjectExistDependency>(capacity, allocator);
                ObjectProperty = new UnsafeList<ObjectPropertyDependency>(capacity, allocator);
                ObjectStatic = new UnsafeList<ObjectStaticDependency>(capacity, allocator);
                Active = new UnsafeList<ActiveDependency>(capacity, allocator);
                LightBaking = 0;
            }

            public void Dispose()
            {
                ObjectReference.Dispose();
                PersistentAsset.Dispose();
                GetComponent.Dispose();
                GetComponents.Dispose();
                GetHierarchySingle.Dispose();
                GetHierarchy.Dispose();
                ObjectExist.Dispose();
                ObjectProperty.Dispose();
                ObjectStatic.Dispose();
                Active.Dispose();
            }

            public void Clear()
            {
                ObjectReference.Clear();
                PersistentAsset.Clear();
                GetComponent.Clear();
                GetComponents.Clear();
                GetHierarchySingle.Clear();
                GetHierarchy.Clear();
                ObjectExist.Clear();
                ObjectProperty.Clear();
                ObjectStatic.Clear();
                Active.Clear();
                LightBaking = 0;
            }

            public void CopyFrom(ref RecordedDependencies itemDependencies)
            {
                ObjectReference.CopyFrom(itemDependencies.ObjectReference);
                PersistentAsset.CopyFrom(itemDependencies.PersistentAsset);
                GetComponent.CopyFrom(itemDependencies.GetComponent);
                GetComponents.CopyFrom(itemDependencies.GetComponents);
                GetHierarchySingle.CopyFrom(itemDependencies.GetHierarchySingle);
                GetHierarchy.CopyFrom(itemDependencies.GetHierarchy);
                ObjectExist.CopyFrom(itemDependencies.ObjectExist);
                ObjectProperty.CopyFrom(itemDependencies.ObjectProperty);
                ObjectStatic.CopyFrom(itemDependencies.ObjectStatic);
                Active.CopyFrom(itemDependencies.Active);
                LightBaking = itemDependencies.LightBaking;
            }

            public void ReportDiff(ref RecordedDependencies other)
            {
                // Diff GetComponent
                if (GetComponent.Length != other.GetComponent.Length)
                {
                    Debug.Log($"Change - GetComponent.Length: {GetComponent.Length} -> {other.GetComponent.Length}");
                    return;
                }
                for (int i = 0; i != GetComponent.Length; i++)
                {
                    if (!GetComponent[i].Equals(other.GetComponent[i]))
                    {
                        Debug.Log($"Change - GetComponent: {GetComponent[i]} -> {other.GetComponent[i]}");
                        return;
                    }
                }

                // Diff GetComponents
                if (GetComponents.Length != other.GetComponents.Length)
                {
                    Debug.Log($"Change - GetComponents.Length: {GetComponents.Length} -> {other.GetComponents.Length}");
                    return;
                }
                for (int i = 0; i != GetComponents.Length; i++)
                {
                    if (!GetComponents[i].Equals(other.GetComponents[i]))
                    {
                        Debug.Log($"Change - GetComponents: {GetComponents[i]} -> {other.GetComponents[i]}");
                        return;
                    }
                }

                // Diff GetHierarchySingle
                if (GetHierarchySingle.Length != other.GetHierarchySingle.Length)
                {
                    Debug.Log($"Change - GetHierarchySingle.Length: {GetHierarchySingle.Length} -> {other.GetHierarchySingle.Length}");
                    return;
                }
                for (int i = 0; i != GetHierarchySingle.Length; i++)
                {
                    if (!GetHierarchySingle[i].Equals(other.GetHierarchySingle[i]))
                    {
                        Debug.Log($"Change - GetHierarchySingle: {GetHierarchySingle[i]} -> {other.GetHierarchySingle[i]}");
                        return;
                    }
                }

                // Diff GetHierarchy
                if (GetHierarchy.Length != other.GetHierarchy.Length)
                {
                    Debug.Log($"Change - GetHierarchy.Length: {GetHierarchy.Length} -> {other.GetHierarchy.Length}");
                    return;
                }
                for (int i = 0; i != GetHierarchy.Length; i++)
                {
                    if (!GetHierarchy[i].Equals(other.GetHierarchy[i]))
                    {
                        Debug.Log($"Change - GetHierarchy: {GetHierarchy[i]} -> {other.GetHierarchy[i]}");
                        return;
                    }
                }

                // Diff ObjectExist
                if (ObjectExist.Length != other.ObjectExist.Length)
                {
                    Debug.Log($"Change - ObjectExist.Length: {ObjectExist.Length} -> {other.ObjectExist.Length}");
                    return;
                }
                for (int i = 0; i != ObjectExist.Length; i++)
                {
                    if (!ObjectExist[i].Equals(other.ObjectExist[i]))
                    {
                        Debug.Log($"Change - ObjectExist: {ObjectExist[i]} -> {other.ObjectExist[i]}");
                        return;
                    }
                }

                // Diff ObjectProperty
                if (ObjectProperty.Length != other.ObjectProperty.Length)
                {
                    Debug.Log($"Change - ObjectProperty.Length: {ObjectProperty.Length} -> {other.ObjectProperty.Length}");
                    return;
                }
                for (int i = 0; i != ObjectProperty.Length; i++)
                {
                    if (!ObjectProperty[i].Equals(other.ObjectProperty[i]))
                    {
                        Debug.Log($"Change - ObjectProperty: {ObjectProperty[i]} -> {other.ObjectProperty[i]}");
                        return;
                    }
                }

                // Diff ObjectStatic
                if (ObjectStatic.Length != other.ObjectStatic.Length)
                {
                    Debug.Log($"Change - ObjectStatic.Length: {ObjectStatic.Length} -> {other.ObjectStatic.Length}");
                    return;
                }
                for (int i = 0; i != ObjectStatic.Length; i++)
                {
                    if (!ObjectStatic[i].Equals(other.ObjectStatic[i]))
                    {
                        Debug.Log($"Change - ObjectStatic: {ObjectStatic[i]} -> {other.ObjectStatic[i]}");
                        return;
                    }
                }

                // Diff ObjectProperty
                if (ObjectProperty.Length != other.ObjectProperty.Length)
                {
                    Debug.Log($"Change - ObjectProperty.Length: {ObjectProperty.Length} -> {other.ObjectProperty.Length}");
                    return;
                }
                for (int i = 0; i != ObjectProperty.Length; i++)
                {
                    if (!ObjectProperty[i].Equals(other.ObjectProperty[i]))
                    {
                        Debug.Log($"Change - ObjectProperty: {ObjectProperty[i]} -> {other.ObjectProperty[i]}");
                        return;
                    }
                }

                // Diff ObjectStatic
                if (ObjectStatic.Length != other.ObjectStatic.Length)
                {
                    Debug.Log($"Change - ObjectStatic.Length: {ObjectStatic.Length} -> {other.ObjectStatic.Length}");
                    return;
                }
                for (int i = 0; i != ObjectStatic.Length; i++)
                {
                    if (!ObjectStatic[i].Equals(other.ObjectStatic[i]))
                    {
                        Debug.Log($"Change - ObjectStatic: {ObjectStatic[i]} -> {other.ObjectStatic[i]}");
                        return;
                    }
                }

                // Diff Object reference
                if (ObjectReference.Length != other.ObjectReference.Length)
                {
                    Debug.Log($"Change - ObjectReference.Length: {ObjectReference.Length} -> {other.ObjectReference.Length}");
                    return;
                }
                for (int i = 0; i != ObjectReference.Length; i++)
                {
                    if (ObjectReference[i] != other.ObjectReference[i])
                    {
                        Debug.Log($"Change - ObjectReference: {ObjectReference[i]} -> {other.ObjectReference[i]}");
                        return;
                    }
                }

                // Diff Persistent Asset
                if (PersistentAsset.Length != other.PersistentAsset.Length)
                {
                    Debug.Log($"Change - PersistentAsset.Length: {PersistentAsset.Length} -> {other.PersistentAsset.Length}");
                    return;
                }
                for (int i = 0; i != PersistentAsset.Length; i++)
                {
                    if (PersistentAsset[i] != other.PersistentAsset[i])
                    {
                        Debug.Log($"Change - PersistentAsset: {PersistentAsset[i]} -> {other.PersistentAsset[i]}");
                        return;
                    }
                }

                // Diff Active
                if (Active.Length != other.Active.Length)
                {
                    Debug.Log($"Change - Active.Length: {Active.Length} -> {other.Active.Length}");
                    return;
                }

                for (int i = 0; i != Active.Length; i++)
                {
                    if (!Active[i].Equals(other.Active[i]))
                    {
                        Debug.Log($"Change - Active: {Active[i]} -> {other.Active[i]}");
                        return;
                    }
                }

                // Diff LightBaking
                if (LightBaking != other.LightBaking)
                {
                    Debug.Log($"Change - LightBaking: {LightBaking} -> {other.LightBaking}");
                }

                Debug.LogError($"no actual changes but early out false...");
            }


            public bool EqualDependencies(ref RecordedDependencies other)
            {
                var same =
                    GetComponent.ArraysEqual(other.GetComponent) &&
                    GetComponents.ArraysEqual(other.GetComponents) &&
                    GetHierarchySingle.ArraysEqual(other.GetHierarchySingle) &&
                    GetHierarchy.ArraysEqual(other.GetHierarchy) &&
                    ObjectExist.ArraysEqual(other.ObjectExist) &&
                    ObjectProperty.ArraysEqual(other.ObjectProperty) &&
                    ObjectStatic.ArraysEqual(other.ObjectStatic) &&
                    ObjectReference.ArraysEqual(other.ObjectReference) &&
                    PersistentAsset.ArraysEqual(other.PersistentAsset) &&
                    Active.ArraysEqual(other.Active) &&
                    LightBaking.Equals(other.LightBaking);

                return same;
            }

            void AddObjectReference(EntityId dependOnObject)
            {
                ObjectReference.Add(dependOnObject);
            }

            void AddPersistentAsset(EntityId entityId)
            {
                PersistentAsset.Add(entityId);
            }

            void AddGetComponent(GetComponentDependency componentDependency)
            {
                GetComponent.Add(componentDependency);
            }

            void AddGetComponents(GetComponentsDependency componentsDependency)
            {
                GetComponents.Add(componentsDependency);
            }

            void AddGetHierarchySingle(GetHierarchySingleDependency hierarchySingleDependency)
            {
                GetHierarchySingle.Add(hierarchySingleDependency);
            }

            void AddGetHierarchy(GetHierarchyDependency hierarchyDependency)
            {
                GetHierarchy.Add(hierarchyDependency);
            }

            void AddObjectExist(ObjectExistDependency objectExistDependency)
            {
                ObjectExist.Add(objectExistDependency);
            }

            void AddObjectProperty(ObjectPropertyDependency propertyDependency)
            {
                ObjectProperty.Add(propertyDependency);
            }

            void AddStatic(ObjectStaticDependency staticDependency)
            {
                ObjectStatic.Add(staticDependency);
            }

            public void DependResolveReference(EntityId authoringComponent, UnityEngine.Object referencedObject)
            {
                // Tricky unity details ahead:
                // A UnityEngine.Object might be
                //  - actual null (ReferenceEquals(referencedObject, null)  -> instance ID zero)
                //  - currently unavailable (referencedObject == null)
                //      - If it is unavailable, it might still have an instanceID. The object might for example be brought back through undo or putting the asset in the same path / guid in the project
                //        In that case it will be re-established with the same instanceID and hence we need to have a dependency on when an object
                //        that previously didn't exist now starts existing at the instanceID that previously mapped to an invalid object.
                //  - valid (referencedObject != null) (instanceID non-zero)
                var referencedEntityId = ReferenceEquals(referencedObject, null) ? EntityId.None : referencedObject.GetEntityId();
                if (referencedEntityId != EntityId.None)
                {
                    AddObjectReference(referencedEntityId);

                    var obj = Resources.EntityIdToObject(referencedEntityId);
                    var objTypeId = TypeManager.GetOrCreateTypeIndex(referencedObject.GetType());
                    AddObjectExist(new ObjectExistDependency { EntityId = referencedEntityId, exists = (obj != null), Type = objTypeId });

#if UNITY_EDITOR
                    //@todo: How do we handle creation / destruction of assets / components?
                    if (EditorUtility.IsPersistent(referencedObject))
                        AddPersistentAsset(referencedObject.GetEntityId());
#endif
                }
            }

            unsafe void AddActive(ActiveDependency activeDependency)
            {
                Active.Add(activeDependency);
            }

            public void DependOnActive(EntityId gameObject, EntityId authoringComponent, bool isActive)
            {
                AddActive(new ActiveDependency {GameObjectId = gameObject, Dependent = authoringComponent, IsActive = isActive});
            }

            public void DependOnStatic(EntityId gameObject, EntityId authoring, bool isStatic)
            {
                AddStatic(new ObjectStaticDependency()
                {
                    EntityId = gameObject,
                    AuthoringID = authoring,
                    Value = isStatic
                });

                var obj = Resources.EntityIdToObject(gameObject);
                var objTypeId = TypeManager.GetTypeIndex<GameObject>();
                AddObjectExist(new ObjectExistDependency { EntityId = gameObject, exists = (obj != null), Type = objTypeId });
            }

            public void DependOnObjectName(EntityId gameObject, EntityId authoring, string name)
            {
                AddObjectProperty(new ObjectPropertyDependency()
                {
                    EntityId = gameObject,
                    AuthoringID = authoring,
                    PropertyType = GameObjectPropertyType.Name,
                    Value = name.GetHashCode()
                });

                var obj = Resources.EntityIdToObject(gameObject);
                var objTypeId = TypeManager.GetTypeIndex<GameObject>();
                AddObjectExist(new ObjectExistDependency { EntityId = gameObject, exists = (obj != null), Type = objTypeId });
            }

            public void DependOnObjectLayer(EntityId gameObject, EntityId authoring, int layer)
            {
                AddObjectProperty(new ObjectPropertyDependency()
                {
                    EntityId = gameObject,
                    AuthoringID = authoring,
                    PropertyType = GameObjectPropertyType.Layer,
                    Value = layer
                });

                var obj = Resources.EntityIdToObject(gameObject);
                var objTypeId = TypeManager.GetTypeIndex<GameObject>();
                AddObjectExist(new ObjectExistDependency { EntityId = gameObject, exists = (obj != null), Type = objTypeId });
            }

            public void DependOnObjectTag(EntityId gameObject, EntityId authoring, string tag)
            {
                AddObjectProperty(new ObjectPropertyDependency()
                {
                    EntityId = gameObject,
                    AuthoringID = authoring,
                    PropertyType = GameObjectPropertyType.Tag,
                    Value = tag.GetHashCode()
                });

                var obj = Resources.EntityIdToObject(gameObject);
                var objTypeId = TypeManager.GetTypeIndex<GameObject>();
                AddObjectExist(new ObjectExistDependency { EntityId = gameObject, exists = (obj != null), Type = objTypeId });
            }

            public void DependOnGetComponent(EntityId gameObject, TypeIndex type, EntityId returnedComponent, GetComponentDependencyType dependencyType)
            {
                if (returnedComponent != EntityId.None)
                    AddObjectReference(returnedComponent);

                AddGetComponent(new GetComponentDependency {GameObject = gameObject, Type = type, ResultComponent = returnedComponent, DependencyType = dependencyType});
            }

            public void DependOnGetComponents(EntityId gameObject, TypeIndex type, IEnumerable<Component> returnedComponents, GetComponentDependencyType dependencyType)
            {
                var hashGenerator = new xxHash3.StreamingState(false);

                foreach (var component in returnedComponents)
                {
                    EntityId entityId = EntityId.None;
                    if (component != null)
                    {
                        entityId = component.GetEntityId();
                        if (entityId != EntityId.None)
                            AddObjectReference(entityId);
                    }
                    hashGenerator.Update(entityId);
                }

                var hash = new Hash128(hashGenerator.DigestHash128());
                AddGetComponents(new GetComponentsDependency {GameObject = gameObject, Type = type, DependencyType = dependencyType, ComponentHash = hash});
            }

            public void DependOnGetHierarchySingle(EntityId gameObject, EntityId result, int queryIndex, GetHierarchySingleDependencyType dependencyType)
            {
                if (result != EntityId.None)
                {
                    var objTypeId = TypeManager.GetTypeIndex<GameObject>();
                    AddObjectExist(new ObjectExistDependency { EntityId = result, exists = true, Type = objTypeId });
                }

                AddGetHierarchySingle(new GetHierarchySingleDependency {GameObject = gameObject, QueryIndex = queryIndex, Result = result, DependencyType = dependencyType});
            }

            public void DependOnGetHierarchySingle(EntityId gameObject, int childCount, int queryIndex, GetHierarchySingleDependencyType dependencyType)
            {
                AddGetHierarchySingle(new GetHierarchySingleDependency {GameObject = gameObject, QueryIndex = queryIndex, Result = EntityId.None, ChildCountResult = childCount, DependencyType = dependencyType});
            }

            public void DependOnGetHierarchy(EntityId gameObject, IEnumerable<GameObject> returnGameObjects, GetHierarchyDependencyType dependencyType)
            {
                var hashGenerator = new xxHash3.StreamingState(false);

                var objTypeId = TypeManager.GetTypeIndex<GameObject>();
                foreach (var returnGameObject in returnGameObjects)
                {
                    EntityId entityId = returnGameObject.GetEntityId();
                    if (entityId != EntityId.None)
                    {
                        AddObjectExist(new ObjectExistDependency { EntityId = entityId, exists = true, Type = objTypeId });
                    }

                    hashGenerator.Update(entityId);
                }

                var hash = new Hash128(hashGenerator.DigestHash128());
                AddGetHierarchy(new GetHierarchyDependency {GameObject = gameObject, Hash = hash, DependencyType = dependencyType});
            }

            public void DependOnParentTransformHierarchy(Transform transform)
            {
                if (transform != null)
                {
                    var hashGenerator = new xxHash3.StreamingState(false);
                    GameObject go = transform.gameObject;
                    EntityId goEntityId = go.GetEntityId();

                    // We take the dependency on the parent hierarchy.
                    transform = transform.parent;
                    while (transform != null)
                    {
                        hashGenerator.Update(transform.gameObject.GetEntityId());

                        AddObjectReference(transform.GetEntityId());
                        transform = transform.parent;
                    }

                    var hash = new Hash128(hashGenerator.DigestHash128());
                    AddGetHierarchy(new GetHierarchyDependency {GameObject = goEntityId, Hash = hash, DependencyType = GetHierarchyDependencyType.Parent});
                }
            }

            public void DependOnLightBaking()
            {
                LightBaking = 1;
            }
        }

        internal enum GetComponentDependencyType
        {
            GetComponent,
            GetComponentInParent,
            GetComponentInChildren
        }

        internal struct GetComponentDependency : IEquatable<GetComponentDependency>
        {
            public EntityId                             GameObject;
            public TypeIndex                            Type;
            public GetComponentDependencyType           DependencyType;
            public EntityId                             ResultComponent;

            public bool IsValid(ref GameObjectComponents components, ref SceneHierarchy hierarchy)
            {
                switch (DependencyType)
                {
                    case GetComponentDependencyType.GetComponentInParent:
                        return GameObjectComponents.GetComponentInParent(ref components, ref hierarchy, GameObject, Type) == ResultComponent;
                    case GetComponentDependencyType.GetComponent:
                        return components.GetComponent(GameObject, Type) == ResultComponent;
                    case GetComponentDependencyType.GetComponentInChildren:
                        return GameObjectComponents.GetComponentInChildren(ref components, ref hierarchy, GameObject, Type) == ResultComponent;
                }
                return false;
            }

            public bool Equals(GetComponentDependency other)
            {
                return GameObject == other.GameObject && Type.Equals(other.Type) && DependencyType == other.DependencyType && ResultComponent == other.ResultComponent;
            }
        }

        internal struct GetComponentsDependency : IEquatable<GetComponentsDependency>
        {
            public EntityId                             GameObject;
            public TypeIndex                            Type;
            public GetComponentDependencyType           DependencyType;
            public Hash128                              ComponentHash;


            public bool IsValid(ref GameObjectComponents components, ref SceneHierarchy hierarchy)
            {
                Hash128 hash;
                switch (DependencyType)
                {
                    case GetComponentDependencyType.GetComponentInParent:
                        hash = GameObjectComponents.GetComponentsInParentHash(ref components, ref hierarchy, GameObject, Type);
                        break;
                    case GetComponentDependencyType.GetComponent:
                        hash = components.GetComponentsHash(GameObject, Type);
                        break;
                    case GetComponentDependencyType.GetComponentInChildren:
                        hash = GameObjectComponents.GetComponentsInChildrenHash(ref components, ref hierarchy, GameObject, Type);
                        break;
                    default:
                        hash = default;
                        break;
                }
                return (hash == ComponentHash);
            }

            public bool Equals(GetComponentsDependency other)
            {
                return GameObject == other.GameObject && Type.Equals(other.Type) && DependencyType == other.DependencyType
                       && ComponentHash == other.ComponentHash;
            }
        }

        internal enum GetHierarchySingleDependencyType
        {
            Parent,
            Child,
            ChildCount
        }

        internal struct GetHierarchySingleDependency : IEquatable<GetHierarchySingleDependency>
        {
            public EntityId                             GameObject;
            public int                                  QueryIndex;
            public GetHierarchySingleDependencyType     DependencyType;
            public EntityId                             Result;
            // This dependency is also used to track the child count instead. Previously this hid this in the EntityId value
            // But now that instanceIDs are not just ints, we can't really do that. So an extra value is added specifically for that case
            // It is only a valid value when the type of this dependency is GetHierarchySingleDependencyType.ChildCount
            // Any other time it should be -1, and that doesn't mean it is indicating anything about the child count
            // This should be refactored to be a different dependency type entirely in future
            public int                                  ChildCountResult;

            public EntityId GetParentInstanceId(ref SceneHierarchy hierarchy, EntityId entityId)
            {
                if (hierarchy.TryGetIndexForEntityId(entityId, out int index))
                {
                    int parentIndex = hierarchy.GetParentForIndex(index);
                    if (parentIndex != -1)
                    {
                        return hierarchy.GetEntityIdForIndex(parentIndex);
                    }
                }
                return EntityId.None;
            }

            public EntityId GetChildEntityId(ref SceneHierarchy hierarchy, EntityId entityId, int queryChild)
            {
                if (hierarchy.TryGetIndexForEntityId(entityId, out int index))
                {
                    var childIterator = hierarchy.GetChildIndicesForIndex(index);
                    int currentChild = 0;
                    while (childIterator.MoveNext())
                    {
                        if (queryChild == currentChild)
                        {
                            // We return the index of the child that we wanted to query
                            return hierarchy.GetEntityIdForIndex(childIterator.Current);
                        }
                        ++currentChild;
                    }
                }
                return EntityId.None;
            }

            public int GetChildCount(ref SceneHierarchy hierarchy, EntityId entityId)
            {
                int childCount = 0;
                if (hierarchy.TryGetIndexForEntityId(entityId, out int index))
                {
                    var childIterator = hierarchy.GetChildIndicesForIndex(index);
                    while (childIterator.MoveNext())
                    {
                        ++childCount;
                    }
                }
                return childCount;
            }

            public bool IsValid(ref SceneHierarchy hierarchy)
            {
                EntityId returnValue = EntityId.None;
                switch (DependencyType)
                {
                    case GetHierarchySingleDependencyType.Parent:
                        returnValue = GetParentInstanceId(ref hierarchy, GameObject);
                        break;
                    case GetHierarchySingleDependencyType.Child:
                        returnValue = GetChildEntityId(ref hierarchy, GameObject, QueryIndex);
                        break;
                    case GetHierarchySingleDependencyType.ChildCount:
                        // This is to depend on the childCount, 'result' is in this case in no way an EntityId.
                        // I just used this to separate the work of creating an extra type of dependency or merging it with another.
                        var childCount = GetChildCount(ref hierarchy, GameObject);
                        return (childCount == ChildCountResult);
                }
                return (returnValue == Result);
            }

            public bool Equals(GetHierarchySingleDependency other)
            {
                return GameObject == other.GameObject && QueryIndex == other.QueryIndex && DependencyType == other.DependencyType
                       && Result == other.Result;
            }
        }

        public enum GetHierarchyDependencyType
        {
            Parent,
            ImmediateChildren,
            AllChildren,
        }

        internal struct GetHierarchyDependency : IEquatable<GetHierarchyDependency>
        {
            public EntityId                             GameObject;
            public GetHierarchyDependencyType           DependencyType;
            public Hash128                              Hash;

            public Hash128 GetParentsHash(ref SceneHierarchy hierarchy, EntityId entityId)
            {
                var hashGenerator = new xxHash3.StreamingState(false);

                if (hierarchy.TryGetIndexForEntityId(entityId, out int currentIndex))
                {
                    while (currentIndex != -1)
                    {
                        int parentIndex = hierarchy.GetParentForIndex(currentIndex);
                        if (parentIndex != -1)
                        {
                            EntityId parentEntityId = hierarchy.GetEntityIdForIndex(parentIndex);
                            hashGenerator.Update(parentEntityId);
                        }
                        currentIndex = parentIndex;
                    }
                }
                return new Hash128(hashGenerator.DigestHash128());
            }

            public void GetChildrenHashInternal(ref SceneHierarchy hierarchy, int currentIndex, bool recursive, ref xxHash3.StreamingState hashGenerator)
            {
                var childIterator = hierarchy.GetChildIndicesForIndex(currentIndex);
                while (childIterator.MoveNext())
                {
                    int childIndex = childIterator.Current;
                    EntityId childEntityId = hierarchy.GetEntityIdForIndex(childIndex);
                    hashGenerator.Update(childEntityId);

                    if (recursive)
                    {
                        GetChildrenHashInternal(ref hierarchy, childIndex, recursive, ref hashGenerator);
                    }
                }
            }

            public Hash128 GetChildrenHash(ref SceneHierarchy hierarchy, EntityId entityId, bool recursive)
            {
                var hashGenerator = new xxHash3.StreamingState(false);

                if (hierarchy.TryGetIndexForEntityId(entityId, out int rootIndex))
                {
                    GetChildrenHashInternal(ref hierarchy, rootIndex, recursive, ref hashGenerator);
                }
                return new Hash128(hashGenerator.DigestHash128());
            }

            public bool IsValid(ref SceneHierarchy hierarchy)
            {
                Hash128 returnValue = default;
                switch (DependencyType)
                {
                    case GetHierarchyDependencyType.Parent:
                        returnValue = GetParentsHash(ref hierarchy, GameObject);
                        break;
                    case GetHierarchyDependencyType.ImmediateChildren:
                        returnValue = GetChildrenHash(ref hierarchy, GameObject, false);
                        break;
                    case GetHierarchyDependencyType.AllChildren:
                        returnValue = GetChildrenHash(ref hierarchy, GameObject, true);
                        break;
                }

                return (returnValue == Hash);
            }

            public bool Equals(GetHierarchyDependency other)
            {
                return GameObject == other.GameObject && DependencyType == other.DependencyType
                       && Hash == other.Hash;
            }
        }

        internal struct ObjectExistDependency : IEquatable<ObjectExistDependency>
        {
            public EntityId  EntityId;
            public TypeIndex Type;
            public bool      exists;

            public bool IsValid(ref GameObjectComponents components, ref SceneHierarchy hierarchy)
            {
                UnityEngine.Object obj = Resources.EntityIdToObject(EntityId);
                bool validObj = obj != null;
                return (exists == validObj);
            }

            public bool Equals(ObjectExistDependency other)
            {
                return EntityId == other.EntityId && exists == other.exists && Type.Equals(other.Type);
            }
        }

        internal enum GameObjectPropertyType
        {
            Name,
            Layer,
            Tag
        }

        internal struct ObjectPropertyDependency : IEquatable<ObjectPropertyDependency>
        {
            public EntityId                             EntityId;
            public EntityId                             AuthoringID;
            public GameObjectPropertyType               PropertyType;
            public int                                  Value;


            public bool IsValid(IncrementalBakingData.GameObjectProperties properties)
            {
                int refValue;
                switch (PropertyType)
                {
                    case GameObjectPropertyType.Name:
                        refValue = properties.NameHash;
                        break;
                    case GameObjectPropertyType.Layer:
                        refValue = properties.Layer;
                        break;
                    case GameObjectPropertyType.Tag:
                        refValue = properties.TagHash;
                        break;
                    default:
                        refValue = default;
                        break;
                }
                return (refValue == Value);
            }

            public bool Equals(ObjectPropertyDependency other)
            {
                return EntityId == other.EntityId && AuthoringID == other.AuthoringID && PropertyType == other.PropertyType && Value == other.Value;
            }
        }

        internal struct ObjectStaticDependency : IEquatable<ObjectStaticDependency>
        {
            public EntityId                             EntityId;
            public EntityId                             AuthoringID;
            public bool                                 Value;

            public bool IsValid(ref GameObjectComponents components, ref SceneHierarchy sceneHierarchy, TypeIndex staticOptimizeTypeIndex)
            {
                if(sceneHierarchy.TryGetIndexForEntityId(EntityId, out var gameObjectIndex))
                {
                    bool isStatic = sceneHierarchy.IsStatic(gameObjectIndex);
                    if (!isStatic)
                    {
                        // Check for StaticOptimizeEntity
                        var containsStaticOptimize = GameObjectComponents.GetComponentInParent(ref components, ref sceneHierarchy, EntityId, staticOptimizeTypeIndex);
                        isStatic = (containsStaticOptimize != EntityId.None);
                    }
                    if (Value == isStatic)
                        return true;
                }
                return false;
            }

            public bool Equals(ObjectStaticDependency other)
            {
                return EntityId == other.EntityId && AuthoringID == other.AuthoringID && Value == other.Value;
            }
        }

        internal struct ActiveDependency : IEquatable<ActiveDependency>
        {
            public EntityId       GameObjectId;
            public EntityId       Dependent;
            public bool           IsActive;

            public bool IsValid(ref SceneHierarchy sceneHierarchy)
            {
                if(sceneHierarchy.TryGetIndexForEntityId(GameObjectId, out var gameObjectIndex))
                {
                    if (IsActive == sceneHierarchy.IsActive(gameObjectIndex))
                        return true;
                }

                return false;
            }

            public bool Equals(ActiveDependency other)
            {
                return GameObjectId == other.GameObjectId && Dependent == other.Dependent && IsActive == other.IsActive;
            }
        }

        void AddDependencies(EntityId authoringComponent, ref RecordedDependencies dependencies)
        {
            foreach (var dep in dependencies.ObjectReference)
            {
                _IsReversePropertyChangeDependencyUpToDate = 0;
                _PropertyChangeDependency.Add(authoringComponent, dep);
            }

            foreach (var dep in dependencies.GetComponent)
                _StructuralGetComponentDependency.Add(authoringComponent, dep);

            foreach (var dep in dependencies.GetComponents)
                _StructuralGetComponentsDependency.Add(authoringComponent, dep);

            foreach (var dep in dependencies.GetHierarchySingle)
                _StructuralGetHierarchySingleDependency.Add(authoringComponent, dep);

            foreach (var dep in dependencies.GetHierarchy)
                _StructuralGetHierarchyDependency.Add(authoringComponent, dep);

            foreach (var dep in dependencies.ObjectExist)
                _StructuralObjectExistDependency.Add(authoringComponent, dep);

            foreach (var dep in dependencies.ObjectProperty)
                _ObjectPropertyDependency.Add(authoringComponent, dep);

            foreach (var dep in dependencies.ObjectStatic)
                _ObjectStaticDependency.Add(authoringComponent, dep);

            foreach(var dep in dependencies.Active)
                _ActiveDependencies.Add(dep.Dependent, dep);

            if (dependencies.LightBaking != 0)
                _LightBakingDependency.Add(authoringComponent);

#if UNITY_EDITOR
            foreach (var dep in dependencies.PersistentAsset)
            {
                var assetGUID = GlobalObjectId.GetGlobalObjectIdSlow(dep).assetGUID;
                if (assetGUID != default)
                {
                    //@TODO: DOTS-5440
                    if (!_AssetStateKeys.Contains(assetGUID))
                    {
                        //@TODO: DOTS-5441
                        var newHash = AssetDatabase.GetAssetDependencyHash(assetGUID);
                        _AssetState.Add(new AssetState(assetGUID, newHash));
                        _AssetStateKeys.Add(assetGUID);
                    }

                    _ComponentIdToAssetGUID.Add(authoringComponent, assetGUID);
                }
            }
#endif
        }

        public BakeDependencies(Allocator allocator)
        {
            _PropertyChangeDependency = new UnsafeParallelMultiHashMap<EntityId, EntityId>(1024, allocator);
            _ReversePropertyChangeDependency = new UnsafeParallelMultiHashMap<EntityId, EntityId>(0, allocator);
            _IsReversePropertyChangeDependencyUpToDate = 0;

            _StructuralGetComponentDependency = new UnsafeParallelMultiHashMap<EntityId, GetComponentDependency>(1024, allocator);
            _StructuralGetComponentsDependency = new UnsafeParallelMultiHashMap<EntityId, GetComponentsDependency>(1024, allocator);
            _StructuralGetHierarchySingleDependency = new UnsafeParallelMultiHashMap<EntityId, GetHierarchySingleDependency>(1024, allocator);
            _StructuralGetHierarchyDependency = new UnsafeParallelMultiHashMap<EntityId, GetHierarchyDependency>(1024, allocator);
            _StructuralObjectExistDependency = new UnsafeParallelMultiHashMap<EntityId, ObjectExistDependency>(1024, allocator);
            _ObjectPropertyDependency = new UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency>(1024, allocator);
            _ReverseObjectPropertyDependency = new UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency>(1024, allocator);
            _ObjectStaticDependency = new UnsafeParallelMultiHashMap<EntityId, ObjectStaticDependency>(1024, allocator);
            _ActiveDependencies = new UnsafeParallelMultiHashMap<EntityId, ActiveDependency>(1024, allocator);
            _LightBakingDependency = new UnsafeHashSet<EntityId>(1024, allocator);

#if UNITY_EDITOR
            _AssetStateKeys = new UnsafeParallelHashSet<GUID>(1024, allocator);
            _AssetState = new UnsafeList<AssetState>(1024, allocator);
            _ComponentIdToAssetGUID = new UnsafeParallelMultiHashMap<EntityId, GUID>(1024, allocator);
#endif
        }

        public void Dispose()
        {
            if (_PropertyChangeDependency.IsCreated)
                _PropertyChangeDependency.Dispose();
            if (_ReversePropertyChangeDependency.IsCreated)
                _ReversePropertyChangeDependency.Dispose();
            if (_StructuralGetComponentDependency.IsCreated)
                _StructuralGetComponentDependency.Dispose();
            if (_StructuralGetComponentsDependency.IsCreated)
                _StructuralGetComponentsDependency.Dispose();
            if (_StructuralGetHierarchySingleDependency.IsCreated)
                _StructuralGetHierarchySingleDependency.Dispose();
            if (_StructuralGetHierarchyDependency.IsCreated)
                _StructuralGetHierarchyDependency.Dispose();
            if (_StructuralObjectExistDependency.IsCreated)
                _StructuralObjectExistDependency.Dispose();
            if (_ObjectPropertyDependency.IsCreated)
                _ObjectPropertyDependency.Dispose();
            if (_ReverseObjectPropertyDependency.IsCreated)
                _ReverseObjectPropertyDependency.Dispose();
            if (_ObjectStaticDependency.IsCreated)
                _ObjectStaticDependency.Dispose();
            if (_ActiveDependencies.IsCreated)
                _ActiveDependencies.Dispose();
            if (_LightBakingDependency.IsCreated)
                _LightBakingDependency.Dispose();

#if UNITY_EDITOR
            if (_AssetStateKeys.IsCreated)
                _AssetStateKeys.Dispose();
            if (_AssetState.IsCreated)
                _AssetState.Dispose();
            if (_ComponentIdToAssetGUID.IsCreated)
                _ComponentIdToAssetGUID.Dispose();
#endif
        }

        static readonly ProfilerMarker s_ResetDependenciesPropertyChange               = new ProfilerMarker("Baking.ResetDependencies.PropertyChange");
        static readonly ProfilerMarker s_ResetDependenciesHasFlippedChange             = new ProfilerMarker("Baking.ResetDependencies.HasFlippedWindingChange");
        static readonly ProfilerMarker s_ResetDependenciesActiveChange                 = new ProfilerMarker("Baking.ResetDependencies.ActiveChange");
        static readonly ProfilerMarker s_ResetDependenciesStructuralGetComponent       = new ProfilerMarker("Baking.ResetDependencies.StructuralGetComponent");
        static readonly ProfilerMarker s_ResetDependenciesStructuralGetComponents      = new ProfilerMarker("Baking.ResetDependencies.StructuralGetComponents");
        static readonly ProfilerMarker s_ResetDependenciesStructuralGetHierarchySingle = new ProfilerMarker("Baking.ResetDependencies.StructuralGetHierarchySingle");
        static readonly ProfilerMarker s_ResetDependenciesStructuralGetHierarchy       = new ProfilerMarker("Baking.ResetDependencies.StructuralGetHierarchy");
        static readonly ProfilerMarker s_ResetDependenciesStructuralObjectExist        = new ProfilerMarker("Baking.ResetDependencies.StructuralObjectExist");
        static readonly ProfilerMarker s_ResetDependenciesAuthoringToAssetGUID         = new ProfilerMarker("Baking.ResetDependencies.AuthoringToAssetGUID");
        static readonly ProfilerMarker s_ResetDependenciesObjectProperty               = new ProfilerMarker("Baking.ResetDependencies.ObjectPropertyChange");
        static readonly ProfilerMarker s_ResetDependenciesObjectStatic                 = new ProfilerMarker("Baking.ResetDependencies.ObjectStaticChange");

        void _ResetBakerDependencies(EntityId authoringComponent, ref RecordedDependencies dependencies)
        {
            using(s_ResetDependenciesActiveChange.Auto())
                _ActiveDependencies.Remove(authoringComponent);

            using (s_ResetDependenciesPropertyChange.Auto())
            {
                if (_PropertyChangeDependency.Remove(authoringComponent) != 0)
                    _IsReversePropertyChangeDependencyUpToDate = 0;
            }

            using(s_ResetDependenciesStructuralGetComponent.Auto())
                _StructuralGetComponentDependency.Remove(authoringComponent);

            using (s_ResetDependenciesStructuralGetComponents.Auto())
                _StructuralGetComponentsDependency.Remove(authoringComponent);

            using(s_ResetDependenciesStructuralGetHierarchySingle.Auto())
                _StructuralGetHierarchySingleDependency.Remove(authoringComponent);

            using(s_ResetDependenciesStructuralGetHierarchy.Auto())
                _StructuralGetHierarchyDependency.Remove(authoringComponent);

            using (s_ResetDependenciesStructuralObjectExist.Auto())
                _StructuralObjectExistDependency.Remove(authoringComponent);

            using (s_ResetDependenciesObjectProperty.Auto())
                _ObjectPropertyDependency.Remove(authoringComponent);

            using (s_ResetDependenciesObjectStatic.Auto())
                _ObjectStaticDependency.Remove(authoringComponent);

            _LightBakingDependency.Remove(authoringComponent);

#if UNITY_EDITOR
            using (s_ResetDependenciesAuthoringToAssetGUID.Auto())
                _ComponentIdToAssetGUID.Remove(authoringComponent);
#endif
        }

#if UNITY_EDITOR
        internal UnsafeList<AssetState> GetAllAssetDependencies()
        {
            return _AssetState;
        }
#endif

        [BurstCompile]
        public static void ResetBakerDependencies(ref EntityId authoringComponent, ref BakeDependencies bakeDependencies, ref RecordedDependencies dependencies)
        {
            bakeDependencies._ResetBakerDependencies(authoringComponent, ref dependencies);
        }

        [BurstCompile]
        public static void AddDependencies(ref BakeDependencies bakeDependencies, ref EntityId authoringComponent, ref RecordedDependencies state)
        {
            bakeDependencies.AddDependencies(authoringComponent, ref state);
        }

        [BurstCompile]
        public static bool UpdateDependencies(ref BakeDependencies bakeDependencies, ref EntityId authoringComponent, ref RecordedDependencies state, ref RecordedDependencies newDependencies)
        {
            if (state.EqualDependencies(ref newDependencies))
                return false;

            // state.ReportDiff(ref newDependencies);

            bakeDependencies._ResetBakerDependencies(authoringComponent, ref state);
            state.CopyFrom(ref newDependencies);
            bakeDependencies.AddDependencies(authoringComponent, ref state);

            return true;
        }

        public void CalculateDependencies(ref GameObjectComponents components, ref IncrementalBakingData incrementalConversionDataCache, ChangedSceneTransforms changedSceneTransforms, ref UnsafeParallelHashSet<EntityId> outputChangedComponents, JobHandle transformJobHandle, bool assetsChanged)
        {
            using var marker = CalculateDependenciesMarker.Auto();

            var changedComponentsPerThread = new UnsafeDependencyStream<EntityId>(Allocator.TempJob);
            changedComponentsPerThread.BeginWriting();

            //NOTE: All of this code is written so that it should be straightforward to jobified and bursted.
            //      So far we haven't seen it be a bottleneck yet.

            var nonStructuralDependenciesJobHandle = CalculateNonStructuralDependencies(ref components, ref incrementalConversionDataCache, changedSceneTransforms, ref changedComponentsPerThread);

            bool hasStructuralChange = incrementalConversionDataCache.HasStructuralChanges();
            JobHandle structuralDependenciesJobHandle = default;
            if (hasStructuralChange)
            {
                structuralDependenciesJobHandle = CalculateStructuralDependencies(ref components, ref changedSceneTransforms.Hierarchy, ref changedComponentsPerThread);
            }

            var calculateDependenciesJobHandle = JobHandle.CombineDependencies(nonStructuralDependenciesJobHandle, structuralDependenciesJobHandle, transformJobHandle);

#if UNITY_EDITOR
            var calculateAssetDependenciesJobHandle = CalculateAssetDependencies(ref changedComponentsPerThread);
            calculateDependenciesJobHandle = JobHandle.CombineDependencies(calculateDependenciesJobHandle, calculateAssetDependenciesJobHandle);
#endif

            // ObjectExist
            // We need to check ObjectExist if the assets change for the case when a referenced asset in disk is being removed and restored
            // We also need to check ObjectExist for cases where a reference to a runtime asset is deleted and then restored
            // This is moved out of CalculateStructuralDependencies and CalculateAssetDependencies to avoid a potential case of
            // having CalculateObjectExistDependencies being called twice, if both type of changes are triggerred together
            // Internally we use Resources.EntityIdToObject to check if the object with that referenced EntityId exists
            if (hasStructuralChange || assetsChanged)
            {
                var objectExistDependencies = CalculateObjectExistDependencies(ref components, ref changedSceneTransforms.Hierarchy, ref changedComponentsPerThread);
                calculateDependenciesJobHandle = JobHandle.CombineDependencies(calculateDependenciesJobHandle, objectExistDependencies);
            }

            var endWritingDependency = changedComponentsPerThread.EndWriting(calculateDependenciesJobHandle);
            var composeHashSetJobHandle = changedComponentsPerThread.CopyTo(outputChangedComponents, endWritingDependency);
            composeHashSetJobHandle.Complete();

            // Release
            changedComponentsPerThread.Dispose();
        }

        // This section is a job/burst version of CalculateObjectExistDependencies, but at the moment it is slower than the non job version. This should be reviewed when Resources.EntityIdToObject is replaced (ticket DOTS-5351).

        JobHandle CalculateObjectExistDependencies(ref GameObjectComponents components, ref SceneHierarchy hierarchy, ref UnsafeDependencyStream<EntityId> changedComponentsPerThread)
        {
            using var marker = ObjectExistDependenciesMarker.Auto();

            var deduplicatedObjIds = new NativeParallelHashMap<EntityId, int>(1024, Allocator.TempJob);
            var objectIds = new NativeList<EntityId>(1024, Allocator.TempJob);

            var prepareJob = new PrepareObjectExistJob()
            {
                objectExistDependencies = _StructuralObjectExistDependency,
                deduplicatedObjIds = deduplicatedObjIds,
                objectIds = objectIds
            };
            var prepareJobHandle = prepareJob.Schedule();
            prepareJobHandle.Complete();

            // Resolve the objectIds (Get Object)
            // Check if they are null (If they are null)
            NativeArray<bool> objectExists = new NativeArray<bool>(objectIds.Length, Allocator.TempJob);

            InstanceIDsToValidArrayMarker.Begin();
            Resources.EntityIdsToValidArray(objectIds.AsArray(), objectExists);
            InstanceIDsToValidArrayMarker.End();

            var diffJob = new CalculateObjectExistDiffsJob()
            {
                objectExistDependencies = _StructuralObjectExistDependency,
                objectExists = objectExists,
                deduplicatedObjIds = deduplicatedObjIds,
                changedComponentsPerThread = changedComponentsPerThread
            };
            var diffJobHandle = diffJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_StructuralObjectExistDependency), 64);
            objectExists.Dispose(diffJobHandle);
            deduplicatedObjIds.Dispose(diffJobHandle);
            objectIds.Dispose(diffJobHandle);

            return diffJobHandle;
        }

        [BurstCompile]
        struct PrepareObjectExistJob : IJob
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, ObjectExistDependency> objectExistDependencies;
            public NativeParallelHashMap<EntityId, int> deduplicatedObjIds;
            public NativeList<EntityId> objectIds;
            public void Execute()
            {
                int nextIndex = 0;
                foreach (var i in objectExistDependencies)
                {
                    if (!deduplicatedObjIds.TryGetValue(i.Value.EntityId, out var index))
                    {
                        objectIds.Add(i.Value.EntityId);
                        index = nextIndex++;
                        deduplicatedObjIds[i.Value.EntityId] = index;
                    }
                }
            }
        }

        [BurstCompile]
        struct CalculateObjectExistDiffsJob : IKeyValueJobCallback<EntityId, BakeDependencies.ObjectExistDependency>, IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<bool> objectExists;
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, ObjectExistDependency> objectExistDependencies;
            [ReadOnly]
            public NativeParallelHashMap<EntityId, int> deduplicatedObjIds;
            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                DependenciesHashMapHelper.ExecuteOnEntries(this, objectExistDependencies, m_ThreadIndex, i);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, ObjectExistDependency> hashMap, in EntityId key, in ObjectExistDependency value)
            {
                // Add them if the exist state has changed (State has changed)
                int existsID = deduplicatedObjIds[value.EntityId];
                if (value.exists != objectExists[existsID])
                {
                    changedComponentsPerThread.Add(key, m_ThreadIndex);
                    IncrementalBakingLog.RecordComponentBake(key, ComponentBakeReason.ObjectExistStructuralChange, value.EntityId, value.Type);
                }
            }
        }

#if UNITY_EDITOR
        JobHandle CalculateAssetDependencies(ref UnsafeDependencyStream<EntityId> changedComponentsPerThread)
        {
            using var marker = AssetDependenciesMarker.Auto();

            // Debug.Log("CalculateAssetDependencies");
            var guidToAuthoring = new UnsafeParallelMultiHashMap<GUID, EntityId>(1024, Allocator.TempJob);
            var prepareAssetDataJob = new PrepareAssetDataJob()
            {
                authoringToAssetGUID = _ComponentIdToAssetGUID,
                guidToAuthoring = guidToAuthoring
            };
            var prepareAssetDataJobHandle = prepareAssetDataJob.Schedule();

            // Calculate the hashes in the main thread because of AssetDatabase.GetAssetDependencyHash
            NativeArray<Hash128> newHashValues = new NativeArray<Hash128>(_AssetState.Length, Allocator.TempJob);
            for (int index = 0; index < _AssetState.Length; ++index)
            {
                ref var asset = ref _AssetState.ElementAt(index);
                newHashValues[index] = AssetDatabase.GetAssetDependencyHash(asset.GUID);
            }

            var assetDependencyJob = new CalculateAssetDependenciesJob()
            {
                assetState = _AssetState,
                newHashValues = newHashValues,
                guidToAuthoring = guidToAuthoring,
                changedComponentsPerThread = changedComponentsPerThread
            };
            var assetDependencyJobHandle = assetDependencyJob.Schedule(_AssetState.Length, 64, prepareAssetDataJobHandle);

            guidToAuthoring.Dispose(assetDependencyJobHandle);
            newHashValues.Dispose(assetDependencyJobHandle);

            return assetDependencyJobHandle;
        }

        [BurstCompile]
        internal struct PrepareAssetDataJob : IJob
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, GUID> authoringToAssetGUID;

            public UnsafeParallelMultiHashMap<GUID, EntityId> guidToAuthoring;

            public void Execute()
            {
                foreach(var dep in authoringToAssetGUID)
                    guidToAuthoring.Add(dep.Value, dep.Key);
            }
        }

        [BurstCompile]
        internal struct CalculateAssetDependenciesJob : IJobParallelFor
        {
            [ReadOnly]
            public UnsafeList<AssetState> assetState;
            [ReadOnly]
            public UnsafeParallelMultiHashMap<GUID, EntityId> guidToAuthoring;
            [ReadOnly]
            public NativeArray<Hash128> newHashValues;

            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int index)
            {
                ref var asset = ref assetState.ElementAt(index);
                Hash128 currentHash = newHashValues[index];
                if (asset.Hash != currentHash)
                {
                    foreach (var componentId in guidToAuthoring.GetValuesForKey(asset.GUID))
                    {
                        changedComponentsPerThread.Add(componentId, m_ThreadIndex);

                        IncrementalBakingLog.RecordComponentBake(componentId, ComponentBakeReason.ReferenceChangedOnDisk, asset.GUID, default);
                    }
                    asset.Hash = currentHash;
                }

                IncrementalBakingLog.RecordAssetChangedOnDisk(asset.GUID);
            }
        }
#endif
        JobHandle CalculateStructuralDependencies(ref GameObjectComponents components, ref SceneHierarchy hierarchy, ref UnsafeDependencyStream<EntityId> changedComponentsPerThread)
        {
            using var marker = StructuralDependenciesMarker.Auto();

            var structuralComponentJob = new CalculateStructuralGetComponentDependencyJob()
            {
                structuralGetComponentDependency = _StructuralGetComponentDependency,
                hierarchy = hierarchy,
                components = components,
                changedComponentsPerThread = changedComponentsPerThread
            };
            JobHandle structuralComponentJobHandle = structuralComponentJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_StructuralGetComponentDependency), 64, default);

            var structuralComponentsJob = new CalculateStructuralGetComponentsDependencyJob()
            {
                structuralGetComponentsDependency = _StructuralGetComponentsDependency,
                hierarchy = hierarchy,
                components = components,
                changedComponentsPerThread = changedComponentsPerThread
            };
            JobHandle structuralComponentsJobHandle = structuralComponentsJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_StructuralGetComponentsDependency), 16, default);

            var structuralHierarchySingleJob = new CalculateStructuralGetHierarchySingleDependencyJob()
            {
                structuralGetHierarchySingleDependency = _StructuralGetHierarchySingleDependency,
                hierarchy = hierarchy,
                changedComponentsPerThread = changedComponentsPerThread
            };
            JobHandle structuralHierarchySingleJobHandle = structuralHierarchySingleJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_StructuralGetHierarchySingleDependency), 64, default);

            var structuralHierarchyJob = new CalculateStructuralGetHierarchyDependencyJob()
            {
                structuralGetHierarchyDependency = _StructuralGetHierarchyDependency,
                hierarchy = hierarchy,
                changedComponentsPerThread = changedComponentsPerThread
            };
            JobHandle structuralHierarchyJobHandle = structuralHierarchyJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_StructuralGetHierarchyDependency), 64, default);

            var activeDependencyJob = new CalculateActiveDependenciesJob
            {
                hierarchy = hierarchy,
                unityTypeIndex = TypeManager.GetTypeIndex<GameObject>(),
                changedComponentsPerThread = changedComponentsPerThread,
                HashMap = _ActiveDependencies
            };
            JobHandle activeDependencyJobHandle = activeDependencyJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_ActiveDependencies), 64, default);

            var staticDependencyJob = new CalculateIsStaticDependenciesJob
            {
                hierarchy = hierarchy,
                components = components,
                staticOptimizeEntityTypeIndex = TypeManager.GetTypeIndex<StaticOptimizeEntity>(),
                changedComponentsPerThread = changedComponentsPerThread,
                HashMap = _ObjectStaticDependency
            };
            JobHandle staticDependecyJobHandle = staticDependencyJob.Schedule(DependenciesHashMapHelper.GetBucketSize(_ObjectStaticDependency), 64, default);

            // Combine dependencies
            NativeArray<JobHandle> dependencyArray = new NativeArray<JobHandle>(6, Allocator.Temp);
            int dependencyArrayIndex = 0;
            dependencyArray[dependencyArrayIndex++] = structuralComponentJobHandle;
            dependencyArray[dependencyArrayIndex++] = structuralComponentsJobHandle;
            dependencyArray[dependencyArrayIndex++] = activeDependencyJobHandle;
            dependencyArray[dependencyArrayIndex++] = staticDependecyJobHandle;
            dependencyArray[dependencyArrayIndex++] = structuralHierarchySingleJobHandle;
            dependencyArray[dependencyArrayIndex++] = structuralHierarchyJobHandle;
            var combineDependencies = JobHandle.CombineDependencies(dependencyArray);
            dependencyArray.Dispose();
            return combineDependencies;
        }

        [BurstCompile]
        internal struct CalculateStructuralGetComponentDependencyJob : IKeyValueJobCallback<EntityId, BakeDependencies.GetComponentDependency>, IJobParallelFor
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, BakeDependencies.GetComponentDependency> structuralGetComponentDependency;
            [ReadOnly]
            public SceneHierarchy hierarchy;
            [ReadOnly]
            public GameObjectComponents components;

            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                // ExecuteOnFirstKey could be used as well, but ExecuteOnEntries seems slightly more performant
                DependenciesHashMapHelper.ExecuteOnEntries(this, structuralGetComponentDependency, m_ThreadIndex, i);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, GetComponentDependency> hashMap, in EntityId key, in GetComponentDependency value)
            {
                if (!value.IsValid(ref components, ref hierarchy))
                {
                    changedComponentsPerThread.Add(key, m_ThreadIndex);
                    IncrementalBakingLog.RecordComponentBake(key, ComponentBakeReason.GetComponentStructuralChange, value.ResultComponent, value.Type);
                }
            }
        }

        [BurstCompile]
        internal struct CalculateStructuralGetComponentsDependencyJob : IKeyValueJobCallback<EntityId, BakeDependencies.GetComponentsDependency>, IJobParallelFor
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, BakeDependencies.GetComponentsDependency> structuralGetComponentsDependency;
            [ReadOnly]
            public SceneHierarchy hierarchy;
            [ReadOnly]
            public GameObjectComponents components;

            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                // ExecuteOnFirstKey could be used as well, but ExecuteOnEntries seems slightly more performant
                DependenciesHashMapHelper.ExecuteOnEntries(this, structuralGetComponentsDependency, m_ThreadIndex, i);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, GetComponentsDependency> hashMap, in EntityId key, in GetComponentsDependency value)
            {
                if (!value.IsValid(ref components, ref hierarchy))
                {
                    changedComponentsPerThread.Add(key, m_ThreadIndex);
                    IncrementalBakingLog.RecordComponentBake(key, ComponentBakeReason.GetComponentsStructuralChange, EntityId.None, value.Type);
                }
            }
        }

        [BurstCompile]
        internal struct CalculateStructuralGetHierarchySingleDependencyJob : IKeyValueJobCallback<EntityId, BakeDependencies.GetHierarchySingleDependency>, IJobParallelFor
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, BakeDependencies.GetHierarchySingleDependency> structuralGetHierarchySingleDependency;
            [ReadOnly]
            public SceneHierarchy hierarchy;

            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                // ExecuteOnFirstKey could be used as well, but ExecuteOnEntries seems slightly more performant
                DependenciesHashMapHelper.ExecuteOnEntries(this, structuralGetHierarchySingleDependency, m_ThreadIndex, i);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, GetHierarchySingleDependency> hashMap, in EntityId key, in GetHierarchySingleDependency value)
            {
                if (!value.IsValid(ref hierarchy))
                {
                    changedComponentsPerThread.Add(key, m_ThreadIndex);
                    IncrementalBakingLog.RecordComponentBake(key, ComponentBakeReason.GetHierarchySingleStructuralChange, value.Result, default);
                }
            }
        }

        [BurstCompile]
        internal struct CalculateStructuralGetHierarchyDependencyJob : IKeyValueJobCallback<EntityId, BakeDependencies.GetHierarchyDependency>, IJobParallelFor
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, BakeDependencies.GetHierarchyDependency> structuralGetHierarchyDependency;
            [ReadOnly]
            public SceneHierarchy hierarchy;

            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                // ExecuteOnFirstKey could be used as well, but ExecuteOnEntries seems slightly more performant
                DependenciesHashMapHelper.ExecuteOnEntries(this, structuralGetHierarchyDependency, m_ThreadIndex, i);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, GetHierarchyDependency> hashMap, in EntityId key, in GetHierarchyDependency value)
            {
                if (!value.IsValid(ref hierarchy))
                {
                    changedComponentsPerThread.Add(key, m_ThreadIndex);
                    IncrementalBakingLog.RecordComponentBake(key, ComponentBakeReason.GetHierarchyStructuralChange, EntityId.None, default);
                }
            }
        }

        [BurstCompile]
        internal struct CalculateActiveDependenciesJob : IKeyValueJobCallback<EntityId, BakeDependencies.ActiveDependency>, IJobParallelFor
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, BakeDependencies.ActiveDependency> HashMap;
            [ReadOnly]
            public SceneHierarchy hierarchy;
            public TypeIndex unityTypeIndex;
            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int index)
            {
                DependenciesHashMapHelper.ExecuteOnEntries(this, HashMap, m_ThreadIndex, index);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, ActiveDependency> hashMap, in EntityId key, in ActiveDependency value)
            {
                if (!value.IsValid(ref hierarchy))
                {
                    changedComponentsPerThread.Add(value.Dependent, threadIndex);
                    IncrementalBakingLog.RecordComponentBake(value.Dependent, ComponentBakeReason.ActiveChanged, value.GameObjectId, unityTypeIndex);
                }
            }
        }

        [BurstCompile]
        internal struct CalculateIsStaticDependenciesJob : IKeyValueJobCallback<EntityId, BakeDependencies.ObjectStaticDependency>, IJobParallelFor
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, BakeDependencies.ObjectStaticDependency> HashMap;
            [ReadOnly]
            public SceneHierarchy hierarchy;
            [ReadOnly]
            public GameObjectComponents components;
            public TypeIndex staticOptimizeEntityTypeIndex;
            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int index)
            {
                DependenciesHashMapHelper.ExecuteOnEntries(this, HashMap, m_ThreadIndex, index);
            }

            public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<EntityId, ObjectStaticDependency> hashMap, in EntityId key, in ObjectStaticDependency value)
            {
                if (!value.IsValid(ref components, ref hierarchy, staticOptimizeEntityTypeIndex))
                {
                    changedComponentsPerThread.Add(value.AuthoringID, threadIndex);
                    IncrementalBakingLog.RecordComponentBake(value.AuthoringID, ComponentBakeReason.GameObjectStaticChange, value.EntityId, default);
                }
            }
        }

        JobHandle CalculateNonStructuralDependencies(ref GameObjectComponents components, ref IncrementalBakingData incrementalConversionDataCache, ChangedSceneTransforms changedSceneTransforms, ref UnsafeDependencyStream<EntityId> changedComponentsPerThread)
        {
            using var marker = NonStructuralDependenciesMarker.Auto();

            if (incrementalConversionDataCache.LightBakingChanged)
            {
                foreach (var component in _LightBakingDependency)
                {
                    changedComponentsPerThread.Add(component, 0);
                }
            }

            // Make sure reverse property change dependency table is up to date
            JobHandle calculateGlobalReversePropertyJobHandle = default;
            if (incrementalConversionDataCache.ChangedComponents.Length > 0 || incrementalConversionDataCache.ChangedAssets.Length > 0 || incrementalConversionDataCache.ChangedGameObjectProperties.Length > 0)
            {
                if (_IsReversePropertyChangeDependencyUpToDate == 0)
                {
                    var calculateReversePropertyJob = new CalculateReversePropertyChangeDependencyJob()
                    {
                        propertyChangeDependency = _PropertyChangeDependency,
                        reversePropertyChangeDependency = _ReversePropertyChangeDependency
                    };
                    calculateGlobalReversePropertyJobHandle = calculateReversePropertyJob.Schedule();

                    _IsReversePropertyChangeDependencyUpToDate = 1;
                }
            }

            JobHandle calculateGameObjectPropertyReverseJobHandle = default;
            if (incrementalConversionDataCache.ChangedGameObjectProperties.Length > 0)
            {
                // ObjectPropertyDependency Reverse
                var calculatePropertyReversePropertyJob = new CalculateReverseGameObjectPropertyChangeDependencyJob()
                {
                    propertyGameObjectChangeDependency = _ObjectPropertyDependency,
                    reverseGameObjectPropertyChangeDependency = _ReverseObjectPropertyDependency
                };
                var PropertyJobHandle = calculatePropertyReversePropertyJob.Schedule();

                calculateGameObjectPropertyReverseJobHandle = PropertyJobHandle;
            }

            JobHandle nonStructuralChangedComponentJobHandle = default;
            if (incrementalConversionDataCache.ChangedComponents.Length > 0)
            {
                var nonStructuralChangedComponentJob = new NonStructuralChangedComponentJob()
                {
                    changedComponents = incrementalConversionDataCache.ChangedComponents,
                    reversePropertyChangeDependency = _ReversePropertyChangeDependency,
                    changedComponentsPerThread = changedComponentsPerThread
                };
                nonStructuralChangedComponentJobHandle = nonStructuralChangedComponentJob.Schedule(incrementalConversionDataCache.ChangedComponents.Length, 64, calculateGlobalReversePropertyJobHandle);
            }

            JobHandle nonStructuralChangedGameObjectPropertiesJobHandle = default;
            if (incrementalConversionDataCache.ChangedGameObjectProperties.Length > 0)
            {
                var nonStructuralChangedGameObjectPropertiesJob = new NonStructuralChangedGameObjectPropertiesJob()
                {
                    changedGameObjects = incrementalConversionDataCache.ChangedGameObjectProperties,
                    reversePropertyChangeDependency = _ReversePropertyChangeDependency,
                    reverseGameObjectPropertyChangeDependency = _ReverseObjectPropertyDependency,
                    changedComponentsPerThread = changedComponentsPerThread
                };
                nonStructuralChangedGameObjectPropertiesJobHandle = nonStructuralChangedGameObjectPropertiesJob.Schedule(incrementalConversionDataCache.ChangedGameObjectProperties.Length, 64, JobHandle.CombineDependencies(calculateGameObjectPropertyReverseJobHandle, calculateGlobalReversePropertyJobHandle));
            }

            JobHandle nonStructuralChangedAssetsJobHandle = default;
#if UNITY_EDITOR
            if (incrementalConversionDataCache.ChangedAssets.Length > 0)
            {
                var nonStructuralChangedAssetsJob = new NonStructuralChangedAssetsJob()
                {
                    changedAssets = incrementalConversionDataCache.ChangedAssets,
                    reversePropertyChangeDependency = _ReversePropertyChangeDependency,
                    changedComponentsPerThread = changedComponentsPerThread
                };
                nonStructuralChangedAssetsJobHandle = nonStructuralChangedAssetsJob.Schedule(incrementalConversionDataCache.ChangedAssets.Length, 64, calculateGlobalReversePropertyJobHandle);
            }
#endif
            var changedComponentGoJobHandle = JobHandle.CombineDependencies(nonStructuralChangedComponentJobHandle, nonStructuralChangedGameObjectPropertiesJobHandle);
            return JobHandle.CombineDependencies(changedComponentGoJobHandle, nonStructuralChangedAssetsJobHandle);
        }

        [BurstCompile]
        internal struct CalculateReversePropertyChangeDependencyJob : IJob
        {
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, EntityId> propertyChangeDependency;
            public UnsafeParallelMultiHashMap<EntityId, EntityId> reversePropertyChangeDependency;

            public void Execute()
            {
                reversePropertyChangeDependency.Clear();
                foreach (var kvp in propertyChangeDependency)
                    reversePropertyChangeDependency.Add(kvp.Value, kvp.Key);
            }
        }

        [BurstCompile]
        internal struct CalculateReverseGameObjectPropertyChangeDependencyJob : IJob
        {
            [ReadOnly]
            public  UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency> propertyGameObjectChangeDependency;
            public UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency> reverseGameObjectPropertyChangeDependency;

            public void Execute()
            {
                reverseGameObjectPropertyChangeDependency.Clear();
                foreach (var kvp in propertyGameObjectChangeDependency)
                    reverseGameObjectPropertyChangeDependency.Add(kvp.Value.EntityId, kvp.Value);
            }
        }

        [BurstCompile]
        internal struct NonStructuralChangedComponentJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeList<IncrementalBakingData.ChangedComponentsInfo> changedComponents;
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, EntityId> reversePropertyChangeDependency;
            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                var component = changedComponents[i];
                changedComponentsPerThread.Add(component.instanceID, m_ThreadIndex);

                IncrementalBakingLog.RecordComponentBake(component.instanceID, ComponentBakeReason.ComponentChanged, component.instanceID, component.unityTypeIndex);
                IncrementalBakingLog.RecordComponentChanged(component.instanceID);

                foreach (var dep in reversePropertyChangeDependency.GetValuesForKey(component.instanceID))
                {
                    changedComponentsPerThread.Add(dep, m_ThreadIndex);

                    IncrementalBakingLog.RecordComponentBake(dep, ComponentBakeReason.GetComponentChanged, component.instanceID, component.unityTypeIndex);
                }
            }
        }

        [BurstCompile]
        internal struct NonStructuralChangedGameObjectPropertiesJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeList<IncrementalBakingData.GameObjectProperties> changedGameObjects;
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, EntityId> reversePropertyChangeDependency;
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, ObjectPropertyDependency> reverseGameObjectPropertyChangeDependency;
            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                var instanceID = changedGameObjects[i].InstanceID;
                foreach (var dep in reversePropertyChangeDependency.GetValuesForKey(instanceID))
                {
                    changedComponentsPerThread.Add(dep, m_ThreadIndex);
                    IncrementalBakingLog.RecordComponentBake(dep, ComponentBakeReason.ReferenceChanged, instanceID, default);
                }

                // We check the GameObject properties (Name, Layer, Tag)
                foreach (var dep in reverseGameObjectPropertyChangeDependency.GetValuesForKey(instanceID))
                {
                    if (!dep.IsValid(changedGameObjects[i]))
                    {
                        changedComponentsPerThread.Add(dep.AuthoringID, m_ThreadIndex);
                        IncrementalBakingLog.RecordComponentBake(dep.AuthoringID, ComponentBakeReason.GameObjectPropertyChange, instanceID, default);
                    }
                }
            }
        }

        [BurstCompile]
        internal struct NonStructuralChangedAssetsJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeList<EntityId> changedAssets;
            [ReadOnly]
            public UnsafeParallelMultiHashMap<EntityId, EntityId> reversePropertyChangeDependency;
            public UnsafeDependencyStream<EntityId> changedComponentsPerThread;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int i)
            {
                var asset = changedAssets[i];
                foreach (var dep in reversePropertyChangeDependency.GetValuesForKey(asset))
                {
                    changedComponentsPerThread.Add(dep, m_ThreadIndex);

                    IncrementalBakingLog.RecordComponentBake(dep, ComponentBakeReason.ReferenceChanged, asset, default);
                }

                IncrementalBakingLog.RecordAssetChanged(asset);
            }
        }

        private static string BurstFunctionName(string functionName)
        {
            return $"{nameof(BakeDependencies)}:{functionName} (Burst)";
        }
    }
}
