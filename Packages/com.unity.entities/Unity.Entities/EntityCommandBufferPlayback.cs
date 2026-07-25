using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Profiling;


namespace Unity.Entities
{
    public unsafe partial struct EntityCommandBuffer : IDisposable
    {
        // TODO(michalb): bugfix for https://jira.unity3d.com/browse/BUR-1767, remove when burst is upgraded to 1.7.2.
        static readonly ProfilerMarker k_ProfileEcbPlayback = new ProfilerMarker("EntityCommandBuffer.Playback");
        static readonly ProfilerMarker k_ProfileEcbDispose = new ProfilerMarker("EntityCommandBuffer.Dispose");

        /// <summary>
        /// Controls whether this command buffer should play back.
        /// </summary>
        ///
        /// This property is normally true, but can be useful to prevent
        /// the buffer from playing back when the user code is not in control
        /// of the site of playback.
        ///
        /// For example, is a buffer has been acquired from an EntityCommandBufferSystem and partially
        /// filled in with data, but it is discovered that the work should be aborted,
        /// this property can be set to false to prevent the buffer from playing back.
        public bool ShouldPlayback
        {
            get { return m_Data != null ? m_Data->m_ShouldPlayback : false; }
            set { if (m_Data != null) m_Data->m_ShouldPlayback = value; }
        }

        /// <summary>
        /// Play back all recorded operations against an entity manager.
        /// </summary>
        /// <param name="mgr">The entity manager that will receive the operations</param>
        public void Playback(EntityManager mgr)
        {
            PlaybackInternal(mgr.GetCheckedEntityDataAccess());
        }

        /// <summary>
        /// Play back all recorded operations with an exclusive entity transaction.
        /// <seealso cref="EntityManager.BeginExclusiveEntityTransaction"/>.
        /// </summary>
        /// <param name="mgr">The exclusive entity transaction that will process the operations</param>
        public void Playback(ExclusiveEntityTransaction mgr)
        {
            PlaybackInternal(mgr.EntityManager.GetCheckedEntityDataAccess());
        }

        void PlaybackInternal(EntityDataAccess* mgr)
        {
            EnforceSingleThreadOwnership();

            if (!ShouldPlayback || m_Data == null)
                return;
#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
            if (m_Data != null && m_Data->m_DidPlayback && m_Data->m_PlaybackPolicy == PlaybackPolicy.SinglePlayback)
            {
                throw new InvalidOperationException(
                    "Attempt to call Playback() on an EntityCommandBuffer that has already been played back.\nEntityCommandBuffers created with the SinglePlayback policy can only be played back once.");
            }
#pragma warning restore 618

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_BufferSafety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_ArrayInvalidationSafety);
#endif

            k_ProfileEcbPlayback.Begin();

            if (ENABLE_PRE_PLAYBACK_VALIDATION)
            {
                var walker = new EcbWalker<PrePlaybackValidationProcessor>(default, ECBProcessorType.PrePlaybackValidationProcessor);
                walker.processor.Init(mgr, m_Data, in OriginSystemHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                walker.processor.playbackProcessor.ecbSafetyHandle = m_Safety0;
#endif
                walker.WalkChains(this);
                walker.processor.Cleanup();
                PassedPrePlaybackValidation = 1;
            }
            else if (PLAYBACK_WITH_TRACE)
            {
                var walker = new EcbWalker<PlaybackWithTraceProcessor>(default, ECBProcessorType.PlaybackWithTraceProcessor);
                walker.processor.Init(mgr, m_Data, in OriginSystemHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                walker.processor.playbackProcessor.ecbSafetyHandle = m_Safety0;
#endif
                walker.WalkChains(this);
                walker.processor.Cleanup();
            }
            else
            {
                var walker = new EcbWalker<PlaybackProcessor>(default, ECBProcessorType.PlaybackProcessor);
                walker.processor.Init(mgr, m_Data, in OriginSystemHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                walker.processor.ecbSafetyHandle = m_Safety0;
#endif
                walker.WalkChains(this);
                walker.processor.Cleanup();
            }

            m_Data->m_DidPlayback = true;
            k_ProfileEcbPlayback.End();
        }

        // This enum is used by the ECBInterop to allow us to have generic chain walking code
        // Each IEcbProcessor should have a type here
        // ECBInterop._ProcessChainChunk(...) needs to be updated with the new option as well
        internal enum ECBProcessorType
        {
            PlaybackProcessor,
            DebugViewProcessor,
            PlaybackWithTraceProcessor,
            PrePlaybackValidationProcessor,
        }

        internal interface IEcbProcessor
        {
            public void DestroyEntity(BasicCommand* header);
            public void RemoveComponent(BasicCommand* header);
            public void RemoveMultipleComponents(BasicCommand* header);
            public void CreateEntity(ECBPlaybackState* playbackState, BasicCommand* header);
            public void InstantiateEntity(BasicCommand* header);
            public void AddComponent(BasicCommand* header);
            public void AddMultipleComponents(BasicCommand* header);
            public void SetComponent(BasicCommand* header);
            public void SetEnabled(BasicCommand* header);
            public void SetComponentEnabled(BasicCommand* header);
            public void SetName(BasicCommand* header);
            public void AddBuffer(BasicCommand* header);
            public void SetBuffer(BasicCommand* header);
            public void AppendToBuffer(BasicCommand* header);
            public void AddComponentForEntityQuery(BasicCommand* header);
            public void AddComponentForMultipleEntities(BasicCommand* header);
            public void RemoveComponentForEntityQuery(BasicCommand* header);
            public void RemoveComponentForMultipleEntities(BasicCommand* header);
            public void AddMultipleComponentsForMultipleEntities(BasicCommand* header);
            public void AddMultipleComponentsForEntityQuery(BasicCommand* header);
            public void RemoveMultipleComponentsForMultipleEntities(BasicCommand* header);
            public void RemoveMultipleComponentsForEntityQuery(BasicCommand* header);
            public void DestroyMultipleEntities(BasicCommand* header);
            public void DestroyForEntityQuery(BasicCommand* header);
            public void AddComponentLinkedEntityGroup(BasicCommand* header);
            public void SetComponentLinkedEntityGroup(BasicCommand* header);
            public void ReplaceComponentLinkedEntityGroup(BasicCommand* header);
            public void AddManagedComponentData(BasicCommand* header);
            public void MoveManagedComponentData(BasicCommand* header);
            public void AddComponentObjectForMultipleEntities(BasicCommand* header);
            public void SetComponentObjectForMultipleEntities(BasicCommand* header);
            public void AddSharedComponentData(BasicCommand* header);
            public void AddSharedComponentWithValueForMultipleEntities(BasicCommand* header);
            public void AddSharedComponentWithValueForEntityQuery(BasicCommand* header);
            public void SetSharedComponentValueForMultipleEntities(BasicCommand* header);
            public void SetSharedComponentValueForEntityQuery(BasicCommand* header);
            public void SetManagedComponentData(BasicCommand* header);
            public void SetSharedComponentData(BasicCommand* header);
            public void AddUnmanagedSharedComponentData(BasicCommand* header);
            public void SetUnmanagedSharedComponentData(BasicCommand* header);
            public void AddUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header);
            public void AddUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header);
            public void SetUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header);
            public void SetUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header);

            public ECBProcessorType ProcessorType { get; }
        }

        internal static void ProcessManagedCommand<T>(T* processor, BasicCommand* header) where T : unmanaged, IEcbProcessor
        {
            switch ((ECBCommand)header->CommandType)
            {
                case ECBCommand.AddManagedComponentData:
                    processor->AddManagedComponentData(header);
                    break;

                case ECBCommand.MoveManagedComponentData:
                    processor->MoveManagedComponentData(header);
                    break;

                case ECBCommand.AddSharedComponentData:
                    processor->AddSharedComponentData(header);
                    break;

                case ECBCommand.AddComponentObjectForMultipleEntities:
                    processor->AddComponentObjectForMultipleEntities(header);
                    break;

                case ECBCommand.SetComponentObjectForMultipleEntities:
                    processor->SetComponentObjectForMultipleEntities(header);
                    break;

                case ECBCommand.AddSharedComponentWithValueForMultipleEntities:
                    processor->AddSharedComponentWithValueForMultipleEntities(header);
                    break;

                case ECBCommand.SetSharedComponentValueForMultipleEntities:
                    processor->SetSharedComponentValueForMultipleEntities(header);
                    break;

                case ECBCommand.AddSharedComponentWithValueForEntityQuery:
                    processor->AddSharedComponentWithValueForEntityQuery(header);
                    break;

                case ECBCommand.SetSharedComponentValueForEntityQuery:
                    processor->SetSharedComponentValueForEntityQuery(header);
                    break;

                case ECBCommand.SetManagedComponentData:
                    processor->SetManagedComponentData(header);
                    break;

                case ECBCommand.SetSharedComponentData:
                    processor->SetSharedComponentData(header);
                    break;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                default:
                {
                    throw new InvalidOperationException($"Invalid command type {(ECBCommand)header->CommandType} not recognized.");
                }
#endif
            }
        }

        internal struct EcbWalker<T> where T: unmanaged, IEcbProcessor {
            public T processor;
            public ECBProcessorType processorType;
            public EcbWalker(T ecbProcessor, ECBProcessorType type)
            {
                processor = ecbProcessor;
                processorType = type;
            }
            public void WalkChains(EntityCommandBuffer ecb)
            {
                EntityCommandBufferData* data = ecb.m_Data;
                // Walk all chains (Main + Threaded) and build a NativeArray of PlaybackState objects.
                // Only chains with non-null Head pointers will be included.
                if (data->m_RecordedChainCount <= 0)
                    return;

                ECBPlaybackState* playbackState = stackalloc ECBPlaybackState[1];
                playbackState->CurrentChunk = ChunkIndex.Null;
                playbackState->CurrentChunkArchetype = null;

                fixed (void* pThis = &this)
                {
                    var chainStates = stackalloc ECBChainPlaybackState[data->m_RecordedChainCount];

                    playbackState->ChainPlaybackState = chainStates;

                    int initialChainCount = 0;
                    for (var chain = &data->m_MainThreadChain; chain != null; chain = chain->m_NextChain)
                    {
                        if (chain->m_Head != null)
                        {
#pragma warning disable 728
                            chainStates[initialChainCount++] = new ECBChainPlaybackState
                            {
                                Chunk = chain->m_Head,
                                Offset = 0,
                                NextSortKey = chain->m_Head->BaseSortKey,
                                CanBurstPlayback = chain->m_CanBurstPlayback
                            };
#pragma warning restore 728
                        }
                    }

                    if (data->m_ThreadedChains != null)
                    {
                        int maxThreadCount = JobsUtility.ThreadIndexCount;
                        for (int i = 0; i < maxThreadCount; ++i)
                        {
                            for (var chain = &data->m_ThreadedChains[i]; chain != null; chain = chain->m_NextChain)
                            {
                                if (chain->m_Head != null)
                                {
#pragma warning disable 728
                                    chainStates[initialChainCount++] = new ECBChainPlaybackState
                                    {
                                        Chunk = chain->m_Head,
                                        Offset = 0,
                                        NextSortKey = chain->m_Head->BaseSortKey,
                                        CanBurstPlayback = chain->m_CanBurstPlayback
                                    };
#pragma warning restore 728
                                }
                            }
                        }
                    }
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                    if (data->m_RecordedChainCount != initialChainCount)
                        Assert.IsTrue(false,
                            $"RecordedChainCount ({data->m_RecordedChainCount}) != initialChainCount ({initialChainCount}");
#endif

                    using (ECBChainPriorityQueue chainQueue = new ECBChainPriorityQueue(chainStates,
                        data->m_RecordedChainCount, Allocator.Temp))
                    {
                        ECBChainHeapElement currentElem = chainQueue.Pop();

                        while (currentElem.ChainIndex != -1)
                        {
                            ECBChainHeapElement nextElem = chainQueue.Peek();

                            var chunk = chainStates[currentElem.ChainIndex].Chunk;
                            var off = chainStates[currentElem.ChainIndex].Offset;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                            if (chunk == null)
                                Assert.IsTrue(false, $"chainStates[{currentElem.ChainIndex}].Chunk is null.");
                            if (off < 0 || off >= chunk->Used)
                                Assert.IsTrue(false, $"chainStates[{currentElem.ChainIndex}].Offset is invalid: {off}. Should be between 0 and {chunk->Used}");
#endif

                            if (chainStates[currentElem.ChainIndex].CanBurstPlayback)
                            {
                                // Bursting PlaybackChain
                                ECBInterop.ProcessChainChunk(pThis, (int)processorType, playbackState,
                                    currentElem.ChainIndex, nextElem.ChainIndex);
                            }
                            else
                            {
                                // Non-Bursted PlaybackChain
                                ECBInterop._ProcessChainChunk(pThis, (int)processorType, playbackState,
                                    currentElem.ChainIndex, nextElem.ChainIndex);
                            }

                            if (chainStates[currentElem.ChainIndex].Chunk == null)
                            {
                                chainQueue.Pop(); // ignore return value; we already have it as nextElem
                            }
                            else
                            {
                                currentElem.SortKey = chainStates[currentElem.ChainIndex].NextSortKey;
                                chainQueue.ReplaceTop(currentElem);
                            }

                            currentElem = nextElem;
                        }
                    }
                }
            }

            internal void ProcessChain(ECBPlaybackState* playbackState, int currentChain,
                int nextChain)
            {
                ECBChainPlaybackState* chainStates = playbackState->ChainPlaybackState;

                int nextChainSortKey = (nextChain != -1) ? chainStates[nextChain].NextSortKey : -1;
                var chunk = chainStates[currentChain].Chunk;
                var off = chainStates[currentChain].Offset;

                while (chunk != null)
                {
                    var buf = (byte*) chunk + sizeof(ECBChunk);
                    while (off < chunk->Used)
                    {
                        var header = (BasicCommand*) (buf + off);
                        if (nextChain != -1 && header->SortKey > nextChainSortKey)
                        {
                            // early out because a different chain needs to playback
                            var state = chainStates[currentChain];
                            state.Chunk = chunk;
                            state.Offset = off;
                            state.NextSortKey = header->SortKey;
                            chainStates[currentChain] = state;
                            return;
                        }

                        var processed = ProcessUnmanagedCommand(playbackState, header);
                        if (!processed)
                        {
                            ECBInterop.ProcessManagedCommand(UnsafeUtility.AddressOf(ref processor), (int)processor.ProcessorType, header);
                        }

                        off += header->TotalSize;
                    }

                    // Reached the end of a chunk; advance to the next one
                    chunk = chunk->Next;
                    off = 0;
                }

                // Reached the end of the chain; update its playback state to make sure it's ignored
                // for the remainder of playback.
                {
                    var state = chainStates[currentChain];
                    state.Chunk = null;
                    state.Offset = 0;
                    state.NextSortKey = Int32.MinValue;
                    chainStates[currentChain] = state;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ProcessUnmanagedCommand(ECBPlaybackState* playbackState, BasicCommand* header)
            {
                switch ((ECBCommand)header->CommandType)
                {
                    case ECBCommand.InstantiateEntity:
                        processor.InstantiateEntity(header);
                        return true;
                    case ECBCommand.CreateEntity:
                        processor.CreateEntity(playbackState, header);
                        return true;
                    case ECBCommand.DestroyEntity:
                        processor.DestroyEntity(header);
                        return true;

                    case ECBCommand.AddComponent:
                        processor.AddComponent(header);
                        return true;
                    case ECBCommand.AddMultipleComponents:
                        processor.AddMultipleComponents(header);
                        return true;

                    case ECBCommand.RemoveComponent:
                        processor.RemoveComponent(header);
                        return true;

                    case ECBCommand.RemoveMultipleComponents:
                        processor.RemoveMultipleComponents(header);
                        return true;

                    case ECBCommand.SetComponent:
                        processor.SetComponent(header);
                        return true;

                    case ECBCommand.SetEntityEnabled:
                        processor.SetEnabled(header);
                        return true;

                    case ECBCommand.SetComponentEnabled:
                        processor.SetComponentEnabled(header);
                        return true;


                    case ECBCommand.SetName:
                        processor.SetName(header);
                        return true;


                    case ECBCommand.AddBuffer:
                        processor.AddBuffer(header);
                        return true;

                    case ECBCommand.SetBuffer:
                        processor.SetBuffer(header);
                        return true;

                    case ECBCommand.AppendToBuffer:
                        processor.AppendToBuffer(header);
                        return true;

                    case ECBCommand.AddComponentForEntityQuery:
                        processor.AddComponentForEntityQuery(header);
                        return true;

                    case ECBCommand.AddComponentForMultipleEntities:
                        processor.AddComponentForMultipleEntities(header);
                        return true;

                    case ECBCommand.RemoveComponentForEntityQuery:
                        processor.RemoveComponentForEntityQuery(header);
                        return true;

                    case ECBCommand.RemoveComponentForMultipleEntities:
                        processor.RemoveComponentForMultipleEntities(header);
                        return true;

                    case ECBCommand.AddMultipleComponentsForMultipleEntities:
                        processor.AddMultipleComponentsForMultipleEntities(header);
                        return true;

                    case ECBCommand.AddMultipleComponentsForEntityQuery:
                        processor.AddMultipleComponentsForEntityQuery(header);
                        return true;

                    case ECBCommand.RemoveMultipleComponentsForMultipleEntities:
                        processor.RemoveMultipleComponentsForMultipleEntities(header);
                        return true;

                    case ECBCommand.RemoveMultipleComponentsForEntityQuery:
                        processor.RemoveMultipleComponentsForEntityQuery(header);
                        return true;

                    case ECBCommand.DestroyMultipleEntities:
                        processor.DestroyMultipleEntities(header);
                        return true;
                    case ECBCommand.DestroyForEntityQuery:
                        processor.DestroyForEntityQuery(header);
                        return true;

                    case ECBCommand.AddUnmanagedSharedComponentData:
                        processor.AddUnmanagedSharedComponentData(header);
                        return true;
                    case ECBCommand.SetUnmanagedSharedComponentData:
                        processor.SetUnmanagedSharedComponentData(header);
                        return true;
                    case ECBCommand.AddUnmanagedSharedComponentValueForMultipleEntities:
                        processor.AddUnmanagedSharedComponentValueForMultipleEntities(header);
                        return true;
                    case ECBCommand.SetUnmanagedSharedComponentValueForMultipleEntities:
                        processor.SetUnmanagedSharedComponentValueForMultipleEntities(header);
                        return true;
                    case ECBCommand.AddUnmanagedSharedComponentValueForEntityQuery:
                        processor.AddUnmanagedSharedComponentValueForEntityQuery(header);
                        return true;
                    case ECBCommand.SetUnmanagedSharedComponentValueForEntityQuery:
                        processor.SetUnmanagedSharedComponentValueForEntityQuery(header);
                        return true;

                    case ECBCommand.AddComponentLinkedEntityGroup:
                        processor.AddComponentLinkedEntityGroup(header);
                        return true;

                    case ECBCommand.SetComponentLinkedEntityGroup:
                        processor.SetComponentLinkedEntityGroup(header);
                        return true;

                    case ECBCommand.ReplaceComponentLinkedEntityGroup:
                        processor.ReplaceComponentLinkedEntityGroup(header);
                        return true;
                }

                return false;
            }
        }

        internal struct PlaybackProcessor : IEcbProcessor
        {
            public EntityDataAccess* mgr;
            public EntityComponentStore.ArchetypeChanges archetypeChanges;
            public ECBSharedPlaybackState playbackState;
#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
            public PlaybackPolicy playbackPolicy;
#pragma warning restore 618
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public AtomicSafetyHandle ecbSafetyHandle; // used for temporary NativeArray views created & destroyed during playback
#endif
            public byte isFirstPlayback;
            public int entityCount;
            public SystemHandle originSystem;
            private byte trackStructuralChanges;

            public void Init(EntityDataAccess* entityDataAccess, EntityCommandBufferData* data, in SystemHandle originSystemHandle)
            {
                mgr = entityDataAccess;
#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
                playbackPolicy = data->m_PlaybackPolicy;
#pragma warning restore 618
                isFirstPlayback = (byte)(data->m_DidPlayback ? 0 : 1);
                originSystem = originSystemHandle;

                // Don't begin/end structural changes unless at least one command was recorded.
                // This prevents empty command buffers from needlessly causing structural changes, which some
                // existing code relies on.
                if (data->m_RecordedChainCount > 0)
                {
                    archetypeChanges = mgr->BeginStructuralChanges();
                    trackStructuralChanges = 1;
                }

                entityCount = SumEntityCount(data);

                var remap = default(NativeHashMap<Entity, Entity>);
                if (isFirstPlayback == 0)
                {
                    remap = new NativeHashMap<Entity, Entity>(entityCount, Allocator.Temp);
                    var tempOld = (Entity*)Memory.Unmanaged.Allocate(entityCount * sizeof(Entity), 4, Allocator.Temp);
                    var tempNew = (Entity*)Memory.Unmanaged.Allocate(entityCount * sizeof(Entity), 4, Allocator.Temp);
                    CollectEntitiesForReallocation(data, tempOld);
                    EntityComponentStore.s_entityStore.Data.AllocateEntities(tempNew, entityCount);

                    for (int i = 0; i < entityCount; i++)
                        remap.Add(tempOld[i], tempNew[i]);

                    Memory.Unmanaged.Free(tempOld, Allocator.Temp);
                    Memory.Unmanaged.Free(tempNew, Allocator.Temp);
                }

                playbackState = new ECBSharedPlaybackState
                {
                    CommandBufferID = data->m_CommandBufferID,
                    Remap = remap,
                    EntityCount = entityCount,
                };
            }

            public void Cleanup()
            {
                if (trackStructuralChanges != 0)
                {
                    mgr->EndStructuralChanges(ref archetypeChanges);
                }
                if (playbackState.Remap.IsCreated)
                    playbackState.Remap.Dispose();
            }

            private static int SumEntityCount(EntityCommandBufferData* data)
            {
                if (data->m_ThreadedChains == null && data->m_MainThreadChain.m_NextChain == null)
                    return data->m_MainThreadChain.m_EntityCount;

                int total = 0;
                for (var chain = &data->m_MainThreadChain; chain != null; chain = chain->m_NextChain)
                    total += chain->m_EntityCount;
                if (data->m_ThreadedChains != null)
                {
#if UNITY_2022_2_14F1_OR_NEWER
                    int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                    int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                    for (int i = 0; i < maxThreadCount; ++i)
                        for (var chain = &data->m_ThreadedChains[i]; chain != null; chain = chain->m_NextChain)
                            total += chain->m_EntityCount;
                }
                return total;
            }

            private static void CollectEntitiesForReallocation(EntityCommandBufferData* data, Entity* entitiesToReallocate)
            {
                int k = 0;
                for (var chain = &data->m_MainThreadChain; chain != null; chain = chain->m_NextChain)
                    CollectEntitiesForReallocationFromChain(chain, entitiesToReallocate, ref k);
                if (data->m_ThreadedChains != null)
                {
#if UNITY_2022_2_14F1_OR_NEWER
                    int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                    int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                    for (int i = 0; i < maxThreadCount; ++i)
                        for (var chain = &data->m_ThreadedChains[i]; chain != null; chain = chain->m_NextChain)
                            CollectEntitiesForReallocationFromChain(chain, entitiesToReallocate, ref k);
                }
            }

            private static void CollectEntitiesForReallocationFromChain(EntityCommandBufferChain* chain, Entity* entitiesToReallocate, ref int k)
            {
                for (var chunk = chain->m_Head; chunk != null; chunk = chunk->Next)
                {
                    var buf = (byte*)chunk + sizeof(ECBChunk);
                    int offset = 0;
                    while (offset < chunk->Used)
                    {
                        var header = (BasicCommand*)(buf + offset);
                        var commandType = (ECBCommand)header->CommandType;
                        if (commandType == ECBCommand.CreateEntity)
                        {
                            var cmd = (CreateCommand*)header;
                            UnsafeUtility.MemCpy(entitiesToReallocate + k, cmd->Entities, cmd->EntityCount * sizeof(Entity));
                            k += cmd->EntityCount;
                        }
                        else if (commandType == ECBCommand.InstantiateEntity)
                        {
                            var cmd = (EntityCommand*)header;
                            UnsafeUtility.MemCpy(entitiesToReallocate + k, cmd->Entities, cmd->EntityCount * sizeof(Entity));
                            k += cmd->EntityCount;
                        }
                        offset += header->TotalSize;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Entity RemapEntity(Entity e, in ECBSharedPlaybackState playbackState)
            {
                if (!playbackState.Remap.IsCreated)
                    return e;

                if (playbackState.Remap.TryGetValue(e, out var remappedEntity))
                {
                    return remappedEntity;
                }


                return e;
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            private void AssertNoFixupInMultiPlayback(TypeIndex typeIndex, byte* value, int elementCount)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (isFirstPlayback != 0 || !playbackState.Remap.IsCreated)
                    return;
                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(typeIndex);
                int offsetCount = typeInfo.EntityOffsetCount;
                if (offsetCount == 0)
                    return;
                var offsets = TypeManager.GetEntityOffsets(typeInfo);
                int elementStride = typeInfo.ElementSize;
                for (int el = 0; el < elementCount; el++)
                {
                    byte* elementPtr = value + el * elementStride;
                    for (int i = 0; i < offsetCount; i++)
                    {
                        var e = *(Entity*)(elementPtr + offsets[i].Offset);
                        if (playbackState.Remap.ContainsKey(e))
                            throw new InvalidOperationException(
                                "EntityCommandBuffer commands which set components with entity references cannot be played more than once.");
                    }
                }
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DestroyEntity(BasicCommand* header)
            {
                var cmd = (EntityCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.DestroyEntity");
#endif
                var entity = RemapEntity(cmd->Entity, in playbackState);
                mgr->DestroyEntityInternalDuringStructuralChange(&entity, 1, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveComponent(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.RemoveComponent");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                mgr->RemoveComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveMultipleComponents(BasicCommand* header)
            {
                var cmd = (EntityMultipleComponentsCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.RemoveMultipleComponents");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                var componentTypes = cmd->TypeSet;

                mgr->RemoveComponentDuringStructuralChange(entity, componentTypes, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AssignCachedChunkToExistingEntities(ECBPlaybackState* ecbPlaybackState, EntityArchetype entityArchetype, Entity* entities, int count)
            {
                Archetype* archetype = entityArchetype.Archetype;
                int chunkCapacity = archetype->ChunkCapacity;

                while (count != 0)
                {
                    //Check if the chunk is
                    ChunkIndex currentChunk = ecbPlaybackState->CurrentChunk;
                    if (currentChunk == ChunkIndex.Null
                        || ecbPlaybackState->CurrentChunkArchetype != archetype
                        || mgr->EntityComponentStore->GetArchetype(currentChunk) != archetype
                        || currentChunk.Count == archetype->ChunkCapacity)
                    {
                        currentChunk = mgr->GetChunkWithEmptySlots(entityArchetype);
                        ecbPlaybackState->CurrentChunk = currentChunk;
                        ecbPlaybackState->CurrentChunkArchetype = archetype;
                    }

                    var allocateCount = count < chunkCapacity - currentChunk.Count ? count : chunkCapacity - currentChunk.Count;
                    ChunkDataUtility.AssignChunkToExistingEntities(archetype, currentChunk, entities, allocateCount);
                    count -= allocateCount;
                    entities += allocateCount;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CreateEntity(ECBPlaybackState* ecbPlaybackState, BasicCommand* header)
            {
                var cmd = (CreateCommand*)header;
                EntityArchetype entityArchetype = cmd->Archetype;

                if (!entityArchetype.Valid)
                    entityArchetype = mgr->GetEntityAndSimulateArchetype();

#if ENABLE_PROFILER
                if (StructuralChangesProfiler.Enabled)
                    mgr->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.CreateEntity, in mgr->m_WorldUnmanaged);
#endif
                Entity* createdEntities;
                Entity* remappedEntities = null;
                if (isFirstPlayback == 1)
                {
                    createdEntities = cmd->Entities;
                }
                else
                {
                    remappedEntities = (Entity*)Memory.Unmanaged.Allocate(cmd->EntityCount * sizeof(Entity), 4, Allocator.Temp);
                    for (int i = 0; i < cmd->EntityCount; i++)
                        remappedEntities[i] = RemapEntity(cmd->Entities[i], in playbackState);
                    createdEntities = remappedEntities;
                }

                AssignCachedChunkToExistingEntities(ecbPlaybackState, entityArchetype, createdEntities, cmd->EntityCount);

#if ENABLE_PROFILER
                if (StructuralChangesProfiler.Enabled)
                    mgr->StructuralChangesRecorder.End();
#endif

#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
                if (Burst.CompilerServices.Hint.Unlikely(mgr->EntityComponentStore->m_RecordToJournal != 0))
                    mgr->JournalAddRecord_CreateEntity(in originSystem, createdEntities, cmd->EntityCount, (TypeIndex*)entityArchetype.Types, entityArchetype.TypesCount);
#endif

                if (remappedEntities != null)
                    Memory.Unmanaged.Free(remappedEntities, Allocator.Temp);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InstantiateEntity(BasicCommand* header)
            {
                var cmd = (EntityCommand*)header;
                var srcEntity = cmd->Entity;
                var count = cmd->EntityCount;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.InstantiateEntity");
#endif
                Entity* entities;
                Entity* remappedEntities = null;
                if (isFirstPlayback == 1)
                {
                    entities = cmd->Entities;
                }
                else
                {
                    remappedEntities = (Entity*)Memory.Unmanaged.Allocate(count * sizeof(Entity), 4, Allocator.Temp);
                    for (int i = 0; i < count; i++)
                        remappedEntities[i] = RemapEntity(cmd->Entities[i], in playbackState);
                    entities = remappedEntities;
                }

                mgr->InstantiateExistingInternalDuringStructuralChange(srcEntity, entities,
                    count, in originSystem);

                if (remappedEntities != null)
                    Memory.Unmanaged.Free(remappedEntities, Allocator.Temp);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetComponentFromEmbeddedValue(EntityComponentCommand* cmd, Entity entity)
            {
                byte* srcValue = (byte*)(cmd + 1);
                AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex, srcValue, 1);
                mgr->SetComponentDataRaw(entity, cmd->ComponentTypeIndex, srcValue, cmd->ComponentSize, originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponent(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddComponent");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                mgr->AddComponentDuringStructuralChange(entity, componentType, in originSystem);
                if (cmd->ComponentSize != 0)
                {
                    SetComponentFromEmbeddedValue(cmd, entity);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMultipleComponents(BasicCommand* header)
            {
                var cmd = (EntityMultipleComponentsCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddMultipleComponents");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                var componentTypes = cmd->TypeSet;
                mgr->AddMultipleComponentsDuringStructuralChange(entity, componentTypes);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetComponent(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetComponent");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                SetComponentFromEmbeddedValue(cmd, entity);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetEnabled(BasicCommand* header)
            {
                var cmd = (EntityEnabledCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetEnabled");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                mgr->SetEnabled(entity, cmd->IsEnabled != 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetComponentEnabled(BasicCommand* header)
            {
                var cmd = (EntityComponentEnabledCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetComponentEnabled");
#endif
                var entity = RemapEntity(cmd->Header.Header.Entity, in playbackState);
                mgr->SetComponentEnabled(entity, cmd->ComponentTypeIndex, cmd->Header.IsEnabled != 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetName(BasicCommand* header)
            {
                var cmd = (EntityNameCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetName");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                mgr->SetName(entity, in cmd->Name);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddBuffer(BasicCommand* header)
            {
                var cmd = (EntityBufferCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddBuffer");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                mgr->AddComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), in originSystem);
#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
                if (playbackPolicy == PlaybackPolicy.SinglePlayback)
                {
#pragma warning restore 618
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex,
                        &cmd->BufferNode.TempBuffer,
                        cmd->ComponentSize, in originSystem);

                    // Clear the buffer header to mark that ownership has been transferred to the entity.
                    // This avoids double disposal if the playback is interrupted by an exception.
                    BufferHeader.Initialize(&cmd->BufferNode.TempBuffer, 0);
                }
                else
                {
                    AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex,
                        BufferHeader.GetElementPointer(&cmd->BufferNode.TempBuffer),
                        cmd->BufferNode.TempBuffer.Length);
                    // copy the buffer to ensure that no two entities point to the same buffer from the ECB
                    // either in the same world or in different worlds
                    var buffer = CloneBuffer(&cmd->BufferNode.TempBuffer, cmd->ComponentTypeIndex);
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex, &buffer,
                        cmd->ComponentSize, in originSystem);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetBuffer(BasicCommand* header)
            {
                var cmd = (EntityBufferCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetBuffer");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
#pragma warning disable 618 //Remove when PlaybackPolicy is obsolete
                if (playbackPolicy == PlaybackPolicy.SinglePlayback)
#pragma warning restore 618
                {
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex, &cmd->BufferNode.TempBuffer,
                        cmd->ComponentSize, in originSystem);

                    // Clear the buffer header to mark that ownership has been transferred to the entity.
                    // This avoids double disposal if the playback is interrupted by an exception.
                    BufferHeader.Initialize(&cmd->BufferNode.TempBuffer, 0);
                }
                else
                {
                    AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex,
                        BufferHeader.GetElementPointer(&cmd->BufferNode.TempBuffer),
                        cmd->BufferNode.TempBuffer.Length);
                    // copy the buffer to ensure that no two entities point to the same buffer from the ECB
                    // either in the same world or in different worlds
                    var buffer = CloneBuffer(&cmd->BufferNode.TempBuffer, cmd->ComponentTypeIndex);
                    mgr->SetBufferRaw(entity, cmd->ComponentTypeIndex, &buffer, cmd->ComponentSize, in originSystem);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AppendToBuffer(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AppendToBuffer");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                CheckBufferExistsOnEntity(mgr->EntityComponentStore, entity, cmd);
                BufferHeader* bufferHeader =
                    (BufferHeader*)mgr->GetComponentDataRW_AsBytePointer(entity, cmd->ComponentTypeIndex);

                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(cmd->ComponentTypeIndex);
                var alignment = typeInfo.AlignmentInBytes;
                var elementSize = typeInfo.ElementSize;

                BufferHeader.EnsureCapacity(bufferHeader, bufferHeader->Length + 1, elementSize, alignment, BufferHeader.TrashMode.RetainOldData, false, 0);

                AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex, (byte*)(cmd + 1), 1);
                var offset = bufferHeader->Length * elementSize;
                byte* dst = BufferHeader.GetElementPointer(bufferHeader) + offset;
                UnsafeUtility.MemCpy(dst, cmd + 1, (long)elementSize);
                bufferHeader->Length += 1;
            }

            // Creates a temporary NativeArray<Entity> view data from an Entity* pointer+count. This array does not need to be Disposed();
            // it does not own any of its data. The array will share a safety handle with the ECB, and its lifetime must not exceed that
            // of the ECB itself.
            private void CreateTemporaryNativeArrayView(Entity* entities, int entityCount,
                out NativeArray<Entity> outArray)
            {
                outArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(entities, entityCount,
                    Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // The temporary array still needs an atomic handle, but the array itself will not have Dispose() called
                // on it (it doesn't own its data). And even if it did, NativeArray.Dispose() skips disposing the safety
                // handle if the array uses Allocator.None. The solution is to pass the primary ECB safety handle into
                // the playback processor, and use it for temporary arrays created during playback.
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref outArray, ecbSafetyHandle);
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponentForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->AddComponentToQueryDuringStructuralChange(cmd->Header.QueryImpl, componentType, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponentForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);
                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                mgr->AddComponentDuringStructuralChange(entities, componentType, in originSystem);

                if (cmd->ComponentSize > 0)
                {
                    byte* srcValue = (byte*)(cmd + 1);
                    AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex, srcValue, 1);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        mgr->SetComponentDataRaw(entities[i], cmd->ComponentTypeIndex, srcValue, cmd->ComponentSize, in originSystem);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveComponentForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->RemoveComponentFromQueryDuringStructuralChange(cmd->Header.QueryImpl, componentType, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveComponentForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                mgr->RemoveComponentDuringStructuralChange(entities, componentType, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMultipleComponentsForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesAndComponentsCommand*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                mgr->AddMultipleComponentsDuringStructuralChange(entities, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMultipleComponentsForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentTypeSetCommand*)header;
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->AddComponentsToQueryDuringStructuralChange(cmd->Header.QueryImpl, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveMultipleComponentsForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesAndComponentsCommand*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                mgr->RemoveMultipleComponentsDuringStructuralChange(entities, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveMultipleComponentsForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentTypeSetCommand*)header;
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary).
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->RemoveMultipleComponentsFromQueryDuringStructuralChange(cmd->Header.QueryImpl, cmd->TypeSet, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DestroyMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesCommand*)header;

                CreateTemporaryNativeArrayView(cmd->Entities.Ptr, cmd->EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                mgr->DestroyEntityDuringStructuralChange(entities, in originSystem);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DestroyForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryCommand*)header;
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->DestroyEntitiesInQueryDuringStructuralChange(cmd->QueryImpl, in originSystem);
            }

            public void AddComponentLinkedEntityGroup(BasicCommand* header)
            {
                var cmd = (EntityQueryMaskCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddComponentLinkedEntityGroup");
#endif
                var entity = RemapEntity(cmd->Header.Header.Entity, in playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->AddComponentForLinkedEntityGroup(entity, cmd->Mask, cmd->Header.ComponentTypeIndex, srcValue,
                    cmd->Header.ComponentSize);
            }

            public void SetComponentLinkedEntityGroup(BasicCommand* header)
            {
                var cmd = (EntityQueryMaskCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetComponentLinkedEntityGroup");
#endif
                var entity = RemapEntity(cmd->Header.Header.Entity, in playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                mgr->SetComponentForLinkedEntityGroup(entity, cmd->Mask, cmd->Header.ComponentTypeIndex, srcValue,
                    cmd->Header.ComponentSize);
            }

            public void ReplaceComponentLinkedEntityGroup(BasicCommand* header)
            {
                var cmd = (EntityComponentCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.ReplaceComponentLinkedEntityGroup");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                mgr->ReplaceComponentForLinkedEntityGroup(entity, cmd->ComponentTypeIndex, srcValue,
                    cmd->ComponentSize);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddManagedComponentData(BasicCommand* header)
            {
                var cmd = (EntityManagedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddManagedComponentData");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                var addedManaged = mgr->AddComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), in originSystem);
                if (addedManaged)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }

                var box = cmd->GetBoxedObject();
                if (box != null && TypeManager.HasEntityReferences(cmd->ComponentTypeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                mgr->SetComponentObject(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), box, in originSystem);
                #pragma warning restore 0618
            }


            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MoveManagedComponentData(BasicCommand* header)
            {
                var cmd = (EntityMoveManagedComponentCommand*)header;
                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                var srcEntity = RemapEntity(cmd->SrcEntity, in playbackState);
                var dstEntity = RemapEntity(cmd->Header.Entity, in playbackState);
                mgr->MoveComponentObjectDuringStructuralChange(srcEntity, dstEntity, componentType, originSystem);
                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddUnmanagedSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntityUnmanagedSharedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddUnmanagedSharedComponentData");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                var tmp = new NativeArray<Entity>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                tmp[0] = entity;
                mgr->AddSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    tmp,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    cmd->IsDefault == 0 ? (void*)srcValue : null);
                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntitySharedComponentCommand*) header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.AddSharedComponentData");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                var addedShared = mgr->AddSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(
                    entity,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    cmd->GetBoxedObject(),
                    in originSystem);
                if (addedShared)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddComponentObjectForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }
                mgr->AddComponentDuringStructuralChange(entities, componentType, in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);

                var box = cmd->GetBoxedObject();
                var typeIndex = cmd->ComponentTypeIndex;
                if (box != null && TypeManager.HasEntityReferences(typeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                for (int len = entities.Length, i = 0; i < len; i++)
                {
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    mgr->SetComponentObject(entities[i], componentType, box, in originSystem);
                    #pragma warning restore 0618
                }
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetComponentObjectForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                var componentType = ComponentType.FromTypeIndex(cmd->ComponentTypeIndex);
                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                if (!mgr->EntityComponentStore->ManagedChangesTracker.Empty)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }

                var box = cmd->GetBoxedObject();
                var typeIndex = cmd->ComponentTypeIndex;
                if (box != null && TypeManager.HasEntityReferences(typeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                for (int len = entities.Length, i = 0; i < len; i++)
                {
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    mgr->SetComponentObject(entities[i], componentType, box, in originSystem);
                    #pragma warning restore 0618
                }
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSharedComponentWithValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                var boxedObject = cmd->GetBoxedObject();
                var hashcode = cmd->HashCode;
                var typeIndex = cmd->ComponentTypeIndex;

                // TODO: we aren't yet doing fix-up for Entity fields (see DOTS-3465)
                mgr->AddSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entities, typeIndex,
                    hashcode, boxedObject, in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddSharedComponentWithValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithObject*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Managed(typeIndex,
                    cmd->HashCode, cmd->GetBoxedObject());
                // TODO: we aren't yet doing fix-up for Entity fields (see DOTS-3465)
                mgr->AddSharedComponentDataToQueryDuringStructuralChange(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex), in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetSharedComponentValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesComponentCommandWithObject*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                var boxedObject = cmd->GetBoxedObject();
                var hashcode = cmd->HashCode;
                var typeIndex = cmd->ComponentTypeIndex;

                for (int len = entities.Length, i = 0; i < len; i++)
                {
                    var e = entities[i];
                    mgr->SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(e, typeIndex,
                        hashcode, boxedObject, in originSystem);
                }

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetSharedComponentValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithObject*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Managed(typeIndex,
                    cmd->HashCode, cmd->GetBoxedObject());
                // TODO: we aren't yet doing fix-up for Entity fields (see DOTS-3465)
                mgr->SetSharedComponentDataOnQueryDuringStructuralChange(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex), in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [BurstDiscard]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetManagedComponentData(BasicCommand* header)
            {
                var cmd = (EntityManagedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetManagedComponentData");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                if (!mgr->EntityComponentStore->ManagedChangesTracker.Empty)
                {
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }

                var box = cmd->GetBoxedObject();
                if (box != null && TypeManager.HasEntityReferences(cmd->ComponentTypeIndex))
                    FixupManagedComponent.FixUpComponent(box, playbackState);

                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                mgr->SetComponentObject(entity, ComponentType.FromTypeIndex(cmd->ComponentTypeIndex), cmd->GetBoxedObject(), in originSystem);
                #pragma warning restore 0618
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetUnmanagedSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntityUnmanagedSharedComponentCommand*) header;
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                byte *srcValue = (byte*) (cmd + 1);
                AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex, srcValue, 1);
                var tmp = new NativeArray<Entity>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                tmp[0] = entity;
                mgr->SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    tmp,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    (cmd->IsDefault == 0) ? srcValue : null);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesCommand_WithUnmanagedSharedComponent*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                var hashcode = cmd->HashCode;
                var typeIndex = cmd->ComponentTypeIndex;
                byte *srcValue = (byte*) (cmd + 1);
                AssertNoFixupInMultiPlayback(typeIndex, srcValue, 1);

                mgr->AddSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    entities,
                    typeIndex,
                    hashcode,
                    (cmd->IsDefault == 0) ? srcValue : null);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithUnmanagedSharedComponent*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                byte *srcValue = (byte*) (cmd + 1);
                AssertNoFixupInMultiPlayback(typeIndex, srcValue, 1);

                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Unmanaged(typeIndex, cmd->HashCode,
                    cmd->IsDefault == 1 ? null : srcValue,
                    null);
                mgr->AddSharedComponentDataToQueryDuringStructuralChange_Unmanaged(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex),
                    cmd->IsDefault == 1 ? null : srcValue,
                    in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetUnmanagedSharedComponentValueForMultipleEntities(BasicCommand* header)
            {
                var cmd = (MultipleEntitiesCommand_WithUnmanagedSharedComponent*)header;

                CreateTemporaryNativeArrayView(cmd->Header.Entities.Ptr, cmd->Header.EntitiesCount, out var entities);

                for (int len = entities.Length, i = 0; i < len; ++i)
                {
                    entities[i] = RemapEntity(entities[i], in playbackState);
                }

                byte *srcValue = (byte*) (cmd + 1);
                AssertNoFixupInMultiPlayback(cmd->ComponentTypeIndex, srcValue, 1);

                mgr->SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
                    entities,
                    cmd->ComponentTypeIndex,
                    cmd->HashCode,
                    cmd->IsDefault == 1 ? null : srcValue);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetUnmanagedSharedComponentValueForEntityQuery(BasicCommand* header)
            {
                var cmd = (EntityQueryComponentCommandWithUnmanagedSharedComponent*)header;

                if (Hint.Likely(trackStructuralChanges != 0))
                {
                    // We need to make sure any outstanding structural changes are applied before evaluating the query, to
                    // ensure that its matching chunk cache get invalidated (if necessary). And then immediately begin
                    // a new batch, to track the changes made by this operation.
                    CommitStructuralChanges(mgr, ref archetypeChanges);
                }
                var typeIndex = cmd->Header.ComponentTypeIndex;
                byte *srcValue = (byte*) (cmd + 1);
                AssertNoFixupInMultiPlayback(typeIndex, srcValue, 1);

                int newSharedComponentDataIndex = mgr->InsertSharedComponent_Unmanaged(typeIndex, cmd->HashCode,
                    cmd->IsDefault == 1 ? null : srcValue,
                    null);
                mgr->SetSharedComponentDataOnQueryDuringStructuralChange_Unmanaged(cmd->Header.Header.QueryImpl,
                    newSharedComponentDataIndex, ComponentType.FromTypeIndex(typeIndex),
                    cmd->IsDefault == 1 ? null : srcValue,
                    in originSystem);

                CommitStructuralChanges(mgr, ref archetypeChanges);
            }

            public ECBProcessorType ProcessorType => ECBProcessorType.PlaybackProcessor;

            public void SetSharedComponentData(BasicCommand* header)
            {
                var cmd = (EntitySharedComponentCommand*)header;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(cmd->Header.Entity == Entity.Null))
                    throw new InvalidOperationException("Invalid Entity.Null passed. ECBCommand.SetSharedComponentData");
#endif
                var entity = RemapEntity(cmd->Header.Entity, in playbackState);
                mgr->SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entity, cmd->ComponentTypeIndex, cmd->HashCode,
                    cmd->GetBoxedObject(), in originSystem);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckEntityNotNull(Entity entity)
        {
            if (entity == Entity.Null)
                throw new InvalidOperationException("Invalid Entity.Null passed.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckEntityVersionValid(Entity entity)
        {
            if (entity.Version <= 0)
                throw new InvalidOperationException("Invalid Entity version");
        }
        private static void CommitStructuralChanges(EntityDataAccess* mgr,
            ref EntityComponentStore.ArchetypeChanges archetypeChanges)
        {
            mgr->EndStructuralChanges(ref archetypeChanges);
            archetypeChanges = mgr->BeginStructuralChanges();
        }

        class FixupManagedComponent : Unity.Properties.PropertyVisitor, Unity.Properties.IVisitPropertyAdapter<Entity>
        {
            [ThreadStatic]
            public static FixupManagedComponent _CachedVisitor;

            ECBSharedPlaybackState PlaybackState;
            readonly UniqueReferenceExcludeAdapter m_UniqueRefExclude = new UniqueReferenceExcludeAdapter();

            public FixupManagedComponent()
            {
                AddAdapter(m_UniqueRefExclude);
                AddAdapter(this);
            }

            public static void FixUpComponent(object obj, in ECBSharedPlaybackState state)
            {
                var visitor = FixupManagedComponent._CachedVisitor;
                if (FixupManagedComponent._CachedVisitor == null)
                    FixupManagedComponent._CachedVisitor = visitor = new FixupManagedComponent();

                visitor.m_UniqueRefExclude.PrepareForNewRootVisit();
                visitor.PlaybackState = state;
                try
                {
                    Unity.Properties.PropertyContainer.Accept(visitor, ref obj);
                }
                finally
                {
                    visitor.m_UniqueRefExclude.PrepareForNewRootVisit();
                }
            }

            void Unity.Properties.IVisitPropertyAdapter<Entity>.Visit<TContainer>(in Unity.Properties.VisitContext<TContainer, Entity> context, ref TContainer container, ref Entity value)
            {
                if (PlaybackState.Remap.IsCreated && PlaybackState.Remap.TryGetValue(value, out var mapped))
                    value = mapped;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckBufferExistsOnEntity(EntityComponentStore* mgr, Entity entity, EntityComponentCommand* cmd)
        {
            if (!mgr->HasComponent(entity, cmd->ComponentTypeIndex, out bool entityExists))
                throw new InvalidOperationException($"Buffer does not exist on entity {entity} (entityExists:{entityExists}), cannot append element.");
        }

        static BufferHeader CloneBuffer(BufferHeader* srcBuffer, TypeIndex componentTypeIndex)
        {
            BufferHeader clone = new BufferHeader();
            BufferHeader.Initialize(&clone, 0);

            var alignment = 8; // TODO: Need a way to compute proper alignment for arbitrary non-generic types in TypeManager
            ref readonly var elementSize = ref TypeManager.GetTypeInfo(componentTypeIndex).ElementSize;
            BufferHeader.Assign(&clone, BufferHeader.GetElementPointer(srcBuffer), srcBuffer->Length, elementSize, alignment, false, 0);
            return clone;
        }
    }
}
