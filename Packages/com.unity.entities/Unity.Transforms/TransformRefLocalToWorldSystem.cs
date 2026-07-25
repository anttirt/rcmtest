#if ENABLE_TRANSFORMREF
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using System;
using UnityEngine.Jobs;

namespace Unity.Transforms
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    public unsafe partial struct TransformRefLocalToWorldSystem : ISystem
    {
        EntityQuery _transformHierarchyQuery;
        EntityQuery _freeTransformQuery;
        EntityQuery _dirtyHierarchyQuery;

        TransformTypeHandle _transformTypeHandle;
        ComponentTypeHandle<LocalToWorld> _localToWorldTypeHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Transforms that belong to a hierarchy
            _transformHierarchyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .WithAllRW<LocalToWorld>()
                .WithAny<Parent, Child>()
                .Build(ref state);

            // A change filter on TransformRef won't detect if a parent in the hierarchy changed
            // if the parent happens to be in a different chunk than the child, so only writing
            // LTW for transforms in hierarchies that have changed will be non-trivial.

            // Transforms that have no hierarchy
            _freeTransformQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .WithAllRW<LocalToWorld>()
                .WithNone<Parent, Child>()
                .Build(ref state);

            // Free transforms can be updated only when changed
            _freeTransformQuery.SetChangedVersionFilter(ComponentType.ReadOnly<TransformRef>());

            // TODO(DOTS-10410): This query should only match changed/dirty hierarchies with GameObjects, as there's
            // no need to call QueueTransformDispatch() on pure-Entities hierarchies.
            _dirtyHierarchyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .Build(ref state);
            _dirtyHierarchyQuery.SetChangedVersionFilter(ComponentType.ReadOnly<TransformRef>());

            _transformTypeHandle = state.GetTransformTypeHandle();
            _localToWorldTypeHandle = state.GetComponentTypeHandle<LocalToWorld>();
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformTypeHandle.Update(ref state);
            _localToWorldTypeHandle.Update(ref state);

            // Register dispatches for all GameObject hierarchies with dirty LocalToWorld.
            // This is currently necessary so that the renderer knows to update them.
            // Note that QueueTransformDispatch introduces a sync point on the TransformHierarchy
            // fences.
            int maxDirtyHierarchyCount = _dirtyHierarchyQuery.CalculateEntityCountWithoutFiltering();
            var dirtyHierarchyPtrs = new NativeList<IntPtr>(maxDirtyHierarchyCount, state.WorldUpdateAllocator);
            //2 per entity because we are going to register both read and write. We max out the capacity here to avoid growing the hashmap.
            var hierarchyDependencies = new NativeParallelHashSet<JobHandle>(2*maxDirtyHierarchyCount, state.WorldUpdateAllocator);
            var buildHierarchyListJob = new BuildDirtyHierarchyListJob
            {
                TransformHandle = _transformTypeHandle,
                OutputList = dirtyHierarchyPtrs.AsParallelWriter(),
                HierarchyDependencies = hierarchyDependencies.AsParallelWriter()
            }.ScheduleParallel(_dirtyHierarchyQuery, state.Dependency);

            var hierarchyTransformJob = new LocalToWorldFromTransformRefJob
            {
                TransformTypeHandle = _transformTypeHandle,
                LocalToWorldTypeHandle = _localToWorldTypeHandle
            }.ScheduleParallel(_transformHierarchyQuery, state.Dependency);

            var freeTransformjob = new LocalToWorldFromTransformRefJob
            {
                TransformTypeHandle = _transformTypeHandle,
                LocalToWorldTypeHandle = _localToWorldTypeHandle
                // These jobs are safe to schedule at the same time because their queries are
                // mutually exclusive, but the safety system doesn't recognize that.
            }.ScheduleParallel(_freeTransformQuery, state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(hierarchyTransformJob, freeTransformjob);

            // Consume the list of dirty hierarchy pointers from the previously-scheduled job.
            // Ideally we could either jobify this as well, or at least put this main-thread code later in the frame so the job
            // has more time to run.
            buildHierarchyListJob.Complete();
            JobHandle.CompleteAll(hierarchyDependencies.ToNativeArray(Allocator.Temp));
            UnsafeTransformAccess.BatchQueueTransformDispatch((IntPtr)dirtyHierarchyPtrs.GetUnsafeReadOnlyPtr(), dirtyHierarchyPtrs.Length);
        }

        [BurstCompile]
        struct BuildDirtyHierarchyListJob : IJobChunk
        {
            [ReadOnly] public TransformTypeHandle TransformHandle;
            public NativeList<IntPtr>.ParallelWriter OutputList;
            public NativeParallelHashSet<JobHandle>.ParallelWriter HierarchyDependencies;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                IntPtr* chunkOutputs = stackalloc IntPtr[chunk.Count];
                int chunkOutputCount = 0;
                var chunkTransforms = chunk.GetTransformAccessor(ref TransformHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var transformUnion = chunkTransforms[i].m_TransformUnion;
                    if (transformUnion->_HasHierarchy != 0 && transformUnion->_IsDirty != 0)
                    {
                        chunkOutputs[chunkOutputCount++] = (IntPtr)transformUnion->_UnsafeTransformHierarchyPointer;
                        HierarchyDependencies.Add(transformUnion->GetHierarchyDependency());
                    }
                }
                OutputList.AddRangeNoResize(chunkOutputs, chunkOutputCount);
            }
        }

        [BurstCompile]
        struct LocalToWorldFromTransformRefJob : IJobChunk
        {
            [NativeDisableContainerSafetyRestriction]
            [ReadOnly] public TransformTypeHandle TransformTypeHandle;
            public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var transformAccess = chunk.GetTransformAccessor(ref TransformTypeHandle);
                LocalToWorld* chunkLocalToWorlds = (LocalToWorld*)chunk.GetRequiredComponentDataPtrRW(ref LocalToWorldTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while(enumerator.NextEntityIndex(out var i))
                {
                    chunkLocalToWorlds[i].Value = transformAccess[i].ComputeLocalToWorld();
                }
            }
        }
    }
}
#endif
