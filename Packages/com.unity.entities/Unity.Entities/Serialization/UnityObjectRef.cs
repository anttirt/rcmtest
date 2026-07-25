using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Unity.Entities
{
    internal struct UnityObjectRefMap : IDisposable
    {
        /// <summary>
        /// An uncreated map that skips building the referenced objects list during serialization.
        /// Object references are still serialized (as -1), but no output list is produced.
        /// </summary>
        public static UnityObjectRefMap None => default;

        public NativeHashMap<EntityId, int> EntityIdMap;
        public NativeList<EntityId> EntityIds;

        public bool IsCreated => EntityIds.IsCreated && EntityIdMap.IsCreated;

        public UnityObjectRefMap(Allocator allocator)
        {
            EntityIdMap = new NativeHashMap<EntityId, int>(0, allocator);
            EntityIds = new NativeList<EntityId>(0, allocator);
        }

        public void Dispose()
        {
            EntityIdMap.Dispose();
            EntityIds.Dispose();
        }

        public UnityEngine.Object[] ToObjectArray()
        {
            var objects = new List<UnityEngine.Object>();

            if (IsCreated && EntityIds.Length > 0)
                Resources.EntityIdsToObjectList(EntityIds.AsArray(), objects);

            return objects.ToArray();
        }

        public int Add(EntityId entityId)
        {
            var index = -1;
            if (entityId != EntityId.None && IsCreated)
            {
                if (!EntityIdMap.TryGetValue(entityId, out index))
                {
                    index = EntityIds.Length;
                    EntityIdMap.Add(entityId, index);
                    EntityIds.Add(entityId);
                }
            }

            return index;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    internal static class UnityObjectRefUtility
    {
#if UNITY_EDITOR
        static UnityObjectRefUtility()
        {
            EditorInitializeOnLoadMethod();
        }
#endif

        unsafe class ManagedUnityObjectRefCollector:
            Properties.IPropertyBagVisitor,
            Properties.IPropertyVisitor,
            ITypedVisit<UntypedUnityObjectRef>,
            IDisposable
        {
            UnsafeHashSet<EntityId>* m_EntityIdRefs;

            public ManagedUnityObjectRefCollector(UnsafeHashSet<EntityId>* entityIdRefs)
            {
                m_EntityIdRefs = entityIdRefs;
            }

            IPropertyBag GetPropertyBag(object obj)
            {
                var properties = PropertyBag.GetPropertyBag(obj.GetType());
                return properties;
            }

            public void CollectReferences(ref object obj)
            {
                GetPropertyBag(obj).Accept(this, ref obj);
            }

            void Properties.IPropertyBagVisitor.Visit<TContainer>(Properties.IPropertyBag<TContainer> properties, ref TContainer container)
            {
                foreach (var property in properties.GetProperties(ref container))
                    ((Properties.IPropertyAccept<TContainer>)property).Accept(this, ref container);
            }

            void Properties.IPropertyVisitor.Visit<TContainer, TValue>(Properties.Property<TContainer, TValue> property,
                ref TContainer container)
            {
                if (Properties.TypeTraits<TValue>.CanBeNull || !Properties.TypeTraits<TValue>.IsValueType)
                    return;

                var value = property.GetValue(ref container);

                if (this is ITypedVisit<TValue> typed)
                {
                    typed.Visit(property, ref container, ref value);
                    return;
                }

                Properties.PropertyContainer.Accept(this, ref value);
            }

            void ITypedVisit<UntypedUnityObjectRef>.Visit<TContainer>(Properties.Property<TContainer, UntypedUnityObjectRef> property, ref TContainer container, ref UntypedUnityObjectRef value)
            {
                m_EntityIdRefs->Add(value.entityId);
            }

            public void Dispose() { }
        }

        private static unsafe void AddInstanceIDRefsFromComponent(byte* componentData, TypeManager.EntityOffsetInfo* unityObjectRefOffsets, int unityObjectRefCount, UnsafeHashSet<EntityId>* entityIdRefs)
        {
            for (int i = 0; i < unityObjectRefCount; ++i)
            {
                var unityObjectRefOffset = unityObjectRefOffsets[i].Offset;
                var unityObjectRefPtr = (UntypedUnityObjectRef*)(componentData + unityObjectRefOffset);
                entityIdRefs->Add(unityObjectRefPtr->entityId);
            }
        }

        static unsafe void AddInstanceIDRefsFromChunk(Archetype* archetype, int entityCount, byte* chunkBuffer, UnsafeHashSet<EntityId>* entityIdRefs)
        {
            var typeCount = archetype->TypesCount;

            // This loop only cares about non-zero-sized unmanaged components and buffer components.
            for(int ti=1,tiEnd=archetype->BufferComponentsEnd; ti<tiEnd; ++ti)
            {
                var type = archetype->Types[ti];
                if (type.TypeIndex == ManagedComponentStore.CompanionLinkTypeIndex || type.TypeIndex == ManagedComponentStore.CompanionLinkTransformTypeIndex)
                    continue;

                ref readonly var ct = ref TypeManager.GetTypeInfo(type.TypeIndex);
                var unityObjectRefCount = ct.UnityObjectRefOffsetCount;

                if (unityObjectRefCount == 0)
                    continue;

                var unityObjectRefOffsets = TypeManager.GetUnityObjectRefOffsets(ct);
                int subArrayOffset = archetype->Offsets[ti];
                byte* componentArrayStart = chunkBuffer + subArrayOffset;

                if (type.IsBuffer)
                {
                    BufferHeader* header = (BufferHeader*)componentArrayStart;
                    int strideSize = archetype->SizeOfs[ti];
                    var elementSize = ct.ElementSize;

                    for (int bi = 0; bi < entityCount; ++bi)
                    {
                        var bufferStart = BufferHeader.GetElementPointer(header);
                        var bufferEnd = bufferStart + header->Length * elementSize;
                        for (var componentData = bufferStart; componentData < bufferEnd; componentData += elementSize)
                        {
                            AddInstanceIDRefsFromComponent(componentData, unityObjectRefOffsets,unityObjectRefCount, entityIdRefs);
                        }

                        header = (BufferHeader*)((byte*)header + strideSize);
                    }
                }
                else
                {
                    int size = archetype->SizeOfs[ti];
                    byte* end = componentArrayStart + size * entityCount;
                    for (var componentData = componentArrayStart; componentData < end; componentData += size)
                    {
                        AddInstanceIDRefsFromComponent(componentData, unityObjectRefOffsets, unityObjectRefCount, entityIdRefs);
                    }
                }
            }
        }

        static unsafe void AddInstanceIDRefsFromAllChunks(Archetype* archetype, UnsafeHashSet<EntityId>* entityIdRefs)
        {
            for (var chunkIndex = 0; chunkIndex < archetype->Chunks.Count; chunkIndex++)
            {
                var chunk = archetype->Chunks[chunkIndex];
                var chunkPtr = chunk.GetPtr();
                var chunkBuffer = chunkPtr->Buffer;

                AddInstanceIDRefsFromChunk(archetype, chunk.Count, chunkBuffer, entityIdRefs);
            }
        }

        static unsafe void AddInstanceIDRefsFromUnmanagedSharedComponents(EntityDataAccess* access, Archetype* archetype, UnsafeHashSet<EntityId>* entityIdRefs)
        {
            int numSharedComponents = archetype->NumSharedComponents;
            for (int iType = 0; iType < numSharedComponents; iType++)
            {
                var sharedComponents = archetype->Chunks.GetSharedComponentValueArrayForType(iType);

                for (int chunkIndex = 0; chunkIndex < archetype->Chunks.Count; chunkIndex++)
                {
                    var sharedComponentIndex = sharedComponents[chunkIndex];

                    if (EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
                    {
                        var typeIndex = EntityComponentStore.GetComponentTypeFromSharedComponentIndex(sharedComponentIndex);
                        ref readonly var typeInfo = ref TypeManager.GetTypeInfo(typeIndex);

                        var unityObjectRefCount = typeInfo.UnityObjectRefOffsetCount;
                        if (unityObjectRefCount == 0)
                            continue;

                        var unityObjectRefOffsets = TypeManager.GetUnityObjectRefOffsets(typeInfo);
                        var dataPtr = (byte*)access->EntityComponentStore->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
                        AddInstanceIDRefsFromComponent(dataPtr, unityObjectRefOffsets, unityObjectRefCount, entityIdRefs);
                    }
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        static unsafe void AddInstanceIDRefsFromManagedComponents(EntityDataAccess* access, UnsafeHashSet<EntityId>* entityIdRefs)
        {
            using var managedObjectRefWalker = new ManagedUnityObjectRefCollector(entityIdRefs);

            // Managed components
            s_AddFromManagedComponents.Begin();
            for (int i = 0; i < access->ManagedComponentStore.m_ManagedComponentData.Length; i++)
            {
                var managedComponent = access->ManagedComponentStore.m_ManagedComponentData[i];
                if (managedComponent == null)
                    continue;

                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(managedComponent.GetType()));

                if (!typeInfo.HasUnityObjectRefs || typeInfo.TypeIndex == ManagedComponentStore.CompanionReferenceTypeIndex )
                    continue;

                managedObjectRefWalker.CollectReferences(ref managedComponent);
            }
            s_AddFromManagedComponents.End();

            // Managed shared components
            s_AddFromManagedSharedComponents.Begin();
            int sharedComponentCount = access->ManagedComponentStore.GetSharedComponentCount();
            for (int i = 0; i < sharedComponentCount; i++)
            {
                var managedSharedComponent = access->ManagedComponentStore.m_SharedComponentData[i];
                if (managedSharedComponent == null)
                    continue;

                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(managedSharedComponent.GetType()));

                if (!typeInfo.HasUnityObjectRefs)
                    continue;

                managedObjectRefWalker.CollectReferences(ref managedSharedComponent);
            }
            s_AddFromManagedSharedComponents.End();
        }
#endif

        private static readonly ProfilerMarker s_AddFromChunks = new ProfilerMarker("AddFromChunks");
        private static readonly ProfilerMarker s_AddFromUnmanagedSharedComponents = new ProfilerMarker("AddFromUnmanagaedSharedComponents");
        private static readonly ProfilerMarker s_AddFromManagedComponents = new ProfilerMarker("AddFromManagedComponents");
        private static readonly ProfilerMarker s_AddFromManagedSharedComponents = new ProfilerMarker("AddFromManagedSharedComponents");

        internal delegate void AdditionalRootsHandlerDelegate(IntPtr state);

        private static List<AdditionalRootsHandlerDelegate> s_AdditionalRootsHandlerDelegates = new List<AdditionalRootsHandlerDelegate>();

        static unsafe void RootsHandlerDelegate(IntPtr state)
        {
            foreach (var additionalRootsHandlerDelegate in s_AdditionalRootsHandlerDelegates)
            {
                additionalRootsHandlerDelegate(state);
            }

            using var entityIdRefs = new UnsafeHashSet<EntityId>(256, Allocator.Temp);
            foreach (var world in World.s_AllWorlds)
            {
                var access = world.EntityManager.GetCheckedEntityDataAccessExclusive();
                access->CompleteAllTrackedJobs();
                for (var i = 0; i < access->EntityComponentStore->m_Archetypes.Length; ++i)
                {
                    var archetype = access->EntityComponentStore->m_Archetypes.Ptr[i];

                    if (!archetype->HasUnityObjectRefs)
                        continue;

                    s_AddFromChunks.Begin();
                    AddInstanceIDRefsFromAllChunks(archetype, &entityIdRefs);
                    s_AddFromChunks.End();
                    s_AddFromUnmanagedSharedComponents.Begin();
                    AddInstanceIDRefsFromUnmanagedSharedComponents(access, archetype, &entityIdRefs);
                    s_AddFromUnmanagedSharedComponents.End();
                    #if !UNITY_DISABLE_MANAGED_COMPONENTS
                    AddInstanceIDRefsFromManagedComponents(access, &entityIdRefs);
                    #endif
                }
            }

            if (entityIdRefs.Count == 0)
                return;

            using var instanceIDs = entityIdRefs.ToNativeArray(Allocator.Temp);
            ResourcesAPIInternal.EntitiesAssetGC.MarkInstanceIDsAsRoot((IntPtr)instanceIDs.GetUnsafePtr(), instanceIDs.Length, state);
        }

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod]
#endif
        static void EditorInitializeOnLoadMethod()
        {
            ResourcesAPIInternal.EntitiesAssetGC.RegisterAdditionalRootsHandler(RootsHandlerDelegate);
        }

        public static void MarkInstanceIDsAsRootForEntitiesAssetGC(IntPtr instanceIDs, int count, IntPtr state)
        {
            ResourcesAPIInternal.EntitiesAssetGC.MarkInstanceIDsAsRoot(instanceIDs, count, state);
        }

        public static void RegisterAdditionalRootsHandlerForEntitiesAssetGC(AdditionalRootsHandlerDelegate additionalRootsHandlerDelegate)
        {
            if (additionalRootsHandlerDelegate != null)
                s_AdditionalRootsHandlerDelegates.Add(additionalRootsHandlerDelegate);
        }

        internal static unsafe void MarkInstanceIDsAsRoot(NativeArray<EntityId> unityObjects, IntPtr state)
        {
            ResourcesAPIInternal.EntitiesAssetGC.MarkInstanceIDsAsRoot((IntPtr)unityObjects.GetUnsafePtr(), unityObjects.Length, state);
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct UntypedUnityObjectRef : IEquatable<UntypedUnityObjectRef>
    {
        [SerializeField]
        internal EntityId entityId;

        public bool Equals(UntypedUnityObjectRef other)
        {
            return entityId == other.entityId;
        }

        public override bool Equals(object obj)
        {
            return obj is UntypedUnityObjectRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return entityId.GetHashCode();
        }
    }

    /// <summary>
    /// A utility structure that stores a reference of an <see cref="UnityEngine.Object"/> for Entities. Allows references to be stored on unmanaged component.
    /// </summary>
    /// <typeparam name="T">Type of the Object that is going to be referenced by UnityObjectRef.</typeparam>
    /// <remarks>
    /// Stores the Object's instance ID. Also serializes asset references in subscenes the same way managed components
    /// do with direct references to <see cref="UnityEngine.Object"/>. This is the recommended way to store references to Unity
    /// assets in Entities because it remains unmanaged.
    ///
    /// Serialization is supported on <see cref="IComponentData"/> <see cref="ISharedComponentData"/> and <see cref="IBufferElementData"/>.
    ///
    /// Just as when referencing an asset in a Monobehaviour, the asset will not be collected by any asset garbage collection (such as calling <see cref="Resources.UnloadUnusedAssets()"/>).
    ///
    /// For more information, refer to [Reference Unity objects in your code](xref:reference-unity-objects).
    /// </remarks>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UnityObjectRef<T> : IEquatable<UnityObjectRef<T>>
        where T : Object
    {
        [SerializeField]
        internal UntypedUnityObjectRef Id;
        /// <summary>
        /// Implicitly converts an <see cref="UnityEngine.Object"/> to an <see cref="UnityObjectRef{T}"/>.
        /// </summary>
        /// <param name="instance">Instance of the Object to store as a reference.</param>
        /// <returns>A UnityObjectRef referencing instance</returns>
        public static implicit operator UnityObjectRef<T>(T instance)
        {
            var entityId = instance == null ? EntityId.None : instance.GetEntityId();

            return FromInstanceID(entityId);
        }

        internal static UnityObjectRef<T> FromInstanceID(EntityId entityId)
        {
            var result = new UnityObjectRef<T>{Id = new UntypedUnityObjectRef{ entityId = entityId }};
            return result;
        }

        /// <summary>
        /// Implicitly converts an <see cref="UnityObjectRef{T}"/> to an <see cref="UnityEngine.Object"/>.
        /// </summary>
        /// <param name="unityObjectRef">Reference used to access the Object.</param>
        /// <returns>The instance of type T referenced by unityObjectRef.</returns>
        public static implicit operator T(UnityObjectRef<T> unityObjectRef)
        {
            var entityId = unityObjectRef.Id.entityId;
            if (entityId == EntityId.None)
                return null;

            // Fast path: lock-free resident lookup, no native crossing.
            var obj = EntityIdStore.GetManagedObject<T>(entityId);
            if (!ReferenceEquals(obj, null))
                return obj;

            // Miss: not resident, or no managed wrapper yet (e.g. a baked / deserialized
            // ref on first access). Resolve via native, which loads the asset from its
            // serialized file if still mapped and materializes the wrapper.
            return (T)Resources.EntityIdToObject(entityId);
        }

        /// <summary>
        /// Object being referenced by this <see cref="UnityObjectRef{T}"/>.
        /// </summary>
        public T Value
        {
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            get => this;
            [ExcludeFromBurstCompatTesting("Sets managed object")]
            set => this = value;
        }

        /// <summary>
        /// Checks if this reference and another reference are equal.
        /// </summary>
        /// <param name="other">The UnityObjectRef to compare for equality.</param>
        /// <returns>True if the two lists are equal.</returns>
        public bool Equals(UnityObjectRef<T> other)
        {
            return Id.entityId == other.Id.entityId;
        }

        /// <summary>
        /// Checks if this object references the same UnityEngine.Object as another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True, if the <paramref name="obj"/> parameter is a UnityEngine.Object instance that points to the same
        /// instance as this.</returns>
        public override bool Equals(object obj)
        {
            return obj is UnityObjectRef<T> other && Equals(other);
        }


        /// <summary>
        /// Overload of the 'bool' operator to check for the validity of the instance ID.
        /// </summary>
        /// <param name="obj">The object to check for validity.</param>
        /// <returns>True, if the instance ID is valid.</returns>
        public static implicit operator bool(UnityObjectRef<T> obj)
        {
            return obj.IsValid();
        }

        /// <summary>
        /// Computes a hash code for this object.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return Id.entityId.GetHashCode();
        }

        /// <summary>
        /// Returns 'true' if the UnityObjectRef is still valid, as in the Object still exists.
        /// </summary>
        /// <returns>Valid state.</returns>
        public bool IsValid()
        {
            return Resources.EntityIdIsValid(Id.entityId);
        }

        /// <summary>
        /// Returns true if two <see cref="UnityObjectRef{T}"/> are equal.
        /// </summary>
        /// <param name="left">The first reference to compare for equality.</param>
        /// <param name="right">The second reference to compare for equality.</param>
        /// <returns>True if the two references are equal.</returns>
        public static bool operator ==(UnityObjectRef<T> left, UnityObjectRef<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns true if two <see cref="UnityObjectRef{T}"/> are not equal.
        /// </summary>
        /// <param name="left">The first reference to compare for equality.</param>
        /// <param name="right">The second reference to compare for equality.</param>
        /// <returns>True if the two references are not equal.</returns>
        public static bool operator !=(UnityObjectRef<T> left, UnityObjectRef<T> right)
        {
            return !left.Equals(right);
        }
    }
}
