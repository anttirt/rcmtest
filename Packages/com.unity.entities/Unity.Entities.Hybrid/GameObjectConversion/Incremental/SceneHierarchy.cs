using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Internal;
using UnityEngine.Jobs;

namespace Unity.Entities.Conversion
{
    /// <summary>
    /// Represents the hierarchy of game objects in a scene and their transforms in a way that can be accessed from a
    /// job.
    ///
    /// ATTENTION: Future public API.
    /// </summary>
    internal struct SceneHierarchyWithTransforms
    {
        /// <summary>
        /// The transforms that are used in the scene. Use the <see cref="Hierarchy"/> to map instance ids of
        /// game objects to indices in this array.
        /// </summary>
        public TransformAccessArray TransformAccessArray;

        /// <summary>
        /// A representation of the hierarchy of the scene.
        /// </summary>
        public SceneHierarchy Hierarchy;
    }

    /// <summary>
    /// Represents the hierarchy of game objects in a scene via their instance ids. Each instance id is encoded into an
    /// index, and that index can then be used to query the hierarchy structure.
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    internal struct SceneHierarchy
    {
        private NativeArray<EntityId> _entityId;
        private NativeArray<int> _parentIndex;
        private NativeParallelHashMap<EntityId, int> _indexByEntityId;
        private NativeParallelHashMap<int, UnsafeList<int>> _childIndicesByIndex;
        private NativeArray<bool> _active;
        private NativeArray<bool> _static;

        internal SceneHierarchy(IncrementalHierarchy hierarchy)
        {
            _entityId = hierarchy.EntityId.AsArray();
            _parentIndex = hierarchy.ParentIndex.AsArray();
            _indexByEntityId = hierarchy.IndexByEntityId;
            _childIndicesByIndex = hierarchy.ChildIndicesByIndex;
            _active = hierarchy.Active.AsArray();
            _static = hierarchy.Static.AsArray();
        }

        /// <summary>
        /// Returns the entity id at the given index.
        /// </summary>
        /// <param name="index">The index to get the entity id of.</param>
        /// <returns>The entity id associated with the given index</returns>
        public EntityId GetEntityIdForIndex(int index) => _entityId[index];

        /// <summary>
        /// Returns the index of the parent of the object at the given index.
        /// </summary>
        /// <param name="index">The index to get the parent index of.</param>
        /// <returns>-1 if there is no parent, the index of the parent otherwise.</returns>
        public int GetParentForIndex(int index) => _parentIndex[index];

        /// <summary>
        /// Returns an enumerator for the indices of the children of an element at the given index.
        /// </summary>
        /// <param name="index">The index to get the child indices of.</param>
        /// <returns>An enumerator for the indices of the children.</returns>
        public Children GetChildIndicesForIndex(int index) => new Children(IncrementalHierarchyFunctions.GetChildren(_childIndicesByIndex, index));

        public bool IsActive(int index)
        {
            if (!_active[index])
                return false;

            int parentIdx = GetParentForIndex(index);

            while (parentIdx != -1)
            {
                if (!_active[parentIdx])
                    return false;

                parentIdx = GetParentForIndex(parentIdx);
            }

            return true;
        }

        public bool IsStatic(int index)
        {
            if (_static[index])
                return true;

            int parentIdx = GetParentForIndex(index);

            while (parentIdx != -1)
            {
                if (_static[parentIdx])
                    return true;

                parentIdx = GetParentForIndex(parentIdx);
            }
            return false;
        }

        /// <summary>
        /// Tries to get the index for the given instance id of a game object.
        /// If the instanceID couldn't be found returns false and sets index to 0.
        /// </summary>
        public bool TryGetIndexForEntityId(EntityId entityId, out int index) =>
            _indexByEntityId.TryGetValue(entityId, out index);

        /// <summary>
        /// Returns the index for the given instanceID. Returns -1 index if the instanceID couldn't be found.
        /// </summary>
        public int GetIndexForEntityId(EntityId instanceId)
        {
            var res = _indexByEntityId.TryGetValue(instanceId, out var index);
            return res ? index : -1;
        }

        [ExcludeFromDocs]
        public struct Children : IEnumerator<int>, IEnumerable<int>
        {
            private UnsafeList<int>.Enumerator _iter;

            internal Children(UnsafeList<int>.Enumerator iter)
            {
                _iter = iter;
            }
            public bool MoveNext() => _iter.MoveNext();
            public void Reset() => _iter.Reset();
            public int Current => _iter.Current;
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            object IEnumerator.Current => Current;
            public void Dispose() => _iter.Dispose();

            public Children GetEnumerator() => this;
            [ExcludeFromBurstCompatTesting("Returning interface value boxes")]
            IEnumerator<int> IEnumerable<int>.GetEnumerator() => this;
            [ExcludeFromBurstCompatTesting("Returning interface value boxes")]
            IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<int>).GetEnumerator();
        }
    }

    [GenerateTestsForBurstCompatibility]
    [BurstCompile]
    internal static class SceneHierarchyExtensions
    {
        /// <summary>
        /// Collects the instance ids of all objects in the hierarchy below a set of root objects.
        /// </summary>
        /// <param name="hierarchy">The hierarchy to operate on.</param>
        /// <param name="rootEntityIds">The instance ids of the root objects.</param>
        /// <param name="visitedInstanceIds">A hashset that is used to output the collected instance ids.</param>
        public static void CollectHierarchyInstanceIds(this SceneHierarchy hierarchy, NativeArray<EntityId> rootEntityIds,
            NativeParallelHashSet<EntityId> visitedInstanceIds)
        {
            CollectHierarchyInstanceIdsImpl(hierarchy, rootEntityIds, visitedInstanceIds);
        }

        static void CollectHierarchyInstanceIdsImpl(SceneHierarchy hierarchy, NativeArray<EntityId> rootEntityIds, NativeParallelHashSet<EntityId> visitedInstanceIds)
        {
            var openIndices = new NativeList<int>(0, Allocator.Temp);
            for (int i = 0; i < rootEntityIds.Length; i++)
            {
                if (hierarchy.TryGetIndexForEntityId(rootEntityIds[i], out int idx))
                    openIndices.Add(idx);
            }

            while (openIndices.Length > 0)
            {
                int idx = openIndices[openIndices.Length - 1];
                openIndices.Length--;
                visitedInstanceIds.Add(hierarchy.GetEntityIdForIndex(idx));
                var iter = hierarchy.GetChildIndicesForIndex(idx);
                while (iter.MoveNext())
                    openIndices.Add(iter.Current);
            }
        }

        /// <summary>
        /// Collects the instance ids of all objects in the hierarchy below a set of root objects.
        /// </summary>
        [BurstCompile]
        private struct CollectHierarchyInstanceIdsJob : IJob
        {
            [ReadOnly]
            internal SceneHierarchy Hierarchy;
            [ReadOnly]
            internal NativeArray<EntityId> Roots;
            [WriteOnly]
            internal NativeParallelHashSet<EntityId> VisitedInstances;
            void IJob.Execute()
            {
                CollectHierarchyInstanceIds(Hierarchy, Roots, VisitedInstances);
            }
        }

        /// <summary>
        /// Collects the instance ids of all objects in the hierarchy below a set of root objects (including the roots).
        /// </summary>
        /// <param name="hierarchy">The hierarchy to operate on.</param>
        /// <param name="rootEntityIds">The instance ids of the root objects.</param>
        /// <param name="visitedInstanceIds">A hashset that is used to output the collected instance ids.</param>
        /// <param name="dependency">The dependency for the job.</param>
        /// <returns>A job handle representing the job.</returns>
        public static JobHandle CollectHierarchyInstanceIdsAsync(this SceneHierarchy hierarchy, NativeArray<EntityId> rootEntityIds, NativeParallelHashSet<EntityId> visitedInstanceIds, JobHandle dependency=default)
        {
            return new CollectHierarchyInstanceIdsJob
            {
                Hierarchy = hierarchy,
                Roots = rootEntityIds,
                VisitedInstances = visitedInstanceIds
            }.Schedule(dependency);
        }

        /// <summary>
        /// Collects the instance ids and indices of all objects in the hierarchy below a set of root objects (including
        /// the roots).
        /// </summary>
        /// <param name="hierarchy">The hierarchy to operate on.</param>
        /// <param name="instanceIds">The instance ids of the root objects, but will also be filled with the instance
        /// ids of all objects that were visited.</param>
        /// <param name="visitedIndices">A hashmap that is used to output the visited indices. A value maps to true if
        /// it was a root, false otherwise.</param>
        /// <param name="dependency">The dependency for the job.</param>
        /// <returns>A job handle representing the job.</returns>
        public static JobHandle CollectHierarchyInstanceIdsAndIndicesAsync(this SceneHierarchy hierarchy, NativeList<EntityId> EntityIds, NativeParallelHashMap<int, bool> visitedIndices, JobHandle dependency=default)
        {
            return new CollectHierarchyInstanceIdsAndIndicesJob
            {
                Hierarchy = hierarchy,
                VisitedEntityIds = EntityIds,
                VisitedIndices = visitedIndices
            }.Schedule(dependency);
        }

        [BurstCompile]
        internal struct CollectHierarchyInstanceIdsAndIndicesJob : IJob
        {
            [ReadOnly] internal SceneHierarchy Hierarchy;
            internal NativeList<EntityId> VisitedEntityIds;
            [WriteOnly] internal NativeParallelHashMap<int, bool> VisitedIndices; // true if part of the input, false if child

            public void Execute()
            {
                var openIndices = new NativeList<int>(0, Allocator.Temp);
                for (int i = 0; i < VisitedEntityIds.Length; i++)
                {
                    if (Hierarchy.TryGetIndexForEntityId(VisitedEntityIds[i], out int idx))
                    {
                        openIndices.Add(idx);
                        VisitedIndices.TryAdd(idx, true);
                    }
                }

                while (openIndices.Length > 0)
                {
                    int idx = openIndices[openIndices.Length - 1];
                    openIndices.Length--;
                    if (VisitedIndices.TryAdd(idx, false))
                        VisitedEntityIds.Add(Hierarchy.GetEntityIdForIndex(idx));
                    var iter = Hierarchy.GetChildIndicesForIndex(idx);
                    while (iter.MoveNext())
                        openIndices.Add(iter.Current);
                }
            }
        }
    }
}
