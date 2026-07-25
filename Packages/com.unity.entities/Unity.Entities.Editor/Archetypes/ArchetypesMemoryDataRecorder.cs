using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

namespace Unity.Entities.Editor
{
    internal unsafe class ArchetypesMemoryDataRecorder : IDisposable
    {
        class Recorder : IDisposable
        {
            [BurstCompile]
            struct GetArchetypesDataJob : IJobParallelFor
            {
                [ReadOnly] public ulong WorldSequenceNumber;
                [ReadOnly] public NativeArray<EntityArchetype> Archetypes;
                [WriteOnly] public NativeArray<ArchetypeData> ArchetypesData;
                [WriteOnly] public NativeArray<ArchetypeMemoryData> ArchetypesMemoryData;
                [WriteOnly] public NativeArray<ulong> ArchetypesStableHash;

                public void Execute(int index)
                {
                    var archetype = Archetypes[index].Archetype;
                    ArchetypesData[index] = new ArchetypeData(archetype);
                    ArchetypesMemoryData[index] = new ArchetypeMemoryData(WorldSequenceNumber, archetype);
                    ArchetypesStableHash[index] = archetype->StableHash;
                }
            }

            NativeList<EntityArchetype> m_Archetypes;
            NativeList<ArchetypeData> m_ArchetypesData;
            NativeList<ArchetypeComponentData> m_ArchetypeComponentsData;
            NativeList<ArchetypeMemoryData> m_ArchetypesMemoryData;
            NativeList<ulong> m_ArchetypesStableHash;

            public NativeArray<ArchetypeData> ArchetypesData => m_ArchetypesData.AsArray();
            public NativeArray<ArchetypeComponentData> ArchetypeComponentsData => m_ArchetypeComponentsData.AsArray();
            public NativeArray<ArchetypeMemoryData> ArchetypesMemoryData => m_ArchetypesMemoryData.AsArray();
            public NativeArray<ulong> ArchetypesStableHash => m_ArchetypesStableHash.AsArray();

            public Recorder()
            {
                m_Archetypes = new NativeList<EntityArchetype>(64, Allocator.Persistent);
                m_ArchetypesData = new NativeList<ArchetypeData>(64, Allocator.Persistent);
                m_ArchetypeComponentsData = new NativeList<ArchetypeComponentData>(64 * 16, Allocator.Persistent);
                m_ArchetypesMemoryData = new NativeList<ArchetypeMemoryData>(64, Allocator.Persistent);
                m_ArchetypesStableHash = new NativeList<ulong>(64, Allocator.Persistent);
            }

            public void Dispose()
            {
                m_ArchetypesStableHash.Dispose();
                m_ArchetypesMemoryData.Dispose();
                m_ArchetypeComponentsData.Dispose();
                m_ArchetypesData.Dispose();
                m_Archetypes.Dispose();
            }

            public void Record(World world)
            {
                m_Archetypes.Clear();
                world.EntityManager.GetAllArchetypes(m_Archetypes);

                m_ArchetypesData.Resize(m_Archetypes.Length, NativeArrayOptions.UninitializedMemory);
                m_ArchetypesMemoryData.Resize(m_Archetypes.Length, NativeArrayOptions.UninitializedMemory);
                m_ArchetypesStableHash.Resize(m_Archetypes.Length, NativeArrayOptions.UninitializedMemory);

                new GetArchetypesDataJob
                {
                    WorldSequenceNumber = world.SequenceNumber,
                    Archetypes = m_Archetypes.AsArray(),
                    ArchetypesData = m_ArchetypesData.AsArray(),
                    ArchetypesMemoryData = m_ArchetypesMemoryData.AsArray(),
                    ArchetypesStableHash = m_ArchetypesStableHash.AsArray()
                }.Run(m_Archetypes.Length);

                // Populate component data for each archetype
                m_ArchetypeComponentsData.Clear();
                for (var i = 0; i < m_Archetypes.Length; ++i)
                {
                    var archetype = m_Archetypes[i].Archetype;
                    var archetypeStableHash = archetype->StableHash;
                    for (var componentIndex = 0; componentIndex < archetype->TypesCount; ++componentIndex)
                    {
                        var typeIndex = archetype->Types[componentIndex].TypeIndex;
                        var stableTypeHash = TypeManager.GetTypeInfo(typeIndex).StableTypeHash;
                        var flags = TypeManager.IsChunkComponent(typeIndex) ? ComponentTypeFlags.ChunkComponent : ComponentTypeFlags.None;
                        m_ArchetypeComponentsData.Add(new ArchetypeComponentData(archetypeStableHash, stableTypeHash, flags, componentIndex));
                    }
                }
            }
        }

        Recorder m_Recorder;
        NativeList<WorldData> m_WorldsData;
        NativeList<ArchetypeData> m_ArchetypesData;
        NativeList<ArchetypeComponentData> m_ArchetypeComponentsData;
        NativeList<ArchetypeMemoryData> m_ArchetypesMemoryData;
        NativeList<ulong> m_ArchetypesStableHash;

        public NativeArray<WorldData> WorldsData => m_WorldsData.AsArray();
        public NativeArray<ArchetypeData> ArchetypesData => m_ArchetypesData.AsArray();
        public NativeArray<ArchetypeComponentData> ArchetypeComponentsData => m_ArchetypeComponentsData.AsArray();
        public NativeArray<ArchetypeMemoryData> ArchetypesMemoryData => m_ArchetypesMemoryData.AsArray();
        public NativeArray<ulong> ArchetypesStableHash => m_ArchetypesStableHash.AsArray();

        public ArchetypesMemoryDataRecorder()
        {
            m_Recorder = new Recorder();
            m_WorldsData = new NativeList<WorldData>(8, Allocator.Persistent);
            m_ArchetypesData = new NativeList<ArchetypeData>(64, Allocator.Persistent);
            m_ArchetypeComponentsData = new NativeList<ArchetypeComponentData>(64 * 16, Allocator.Persistent);
            m_ArchetypesMemoryData = new NativeList<ArchetypeMemoryData>(64, Allocator.Persistent);
            m_ArchetypesStableHash = new NativeList<ulong>(64, Allocator.Persistent);
        }

        public void Dispose()
        {
            m_ArchetypesStableHash.Dispose();
            m_ArchetypesMemoryData.Dispose();
            m_ArchetypeComponentsData.Dispose();
            m_ArchetypesData.Dispose();
            m_WorldsData.Dispose();
            m_Recorder.Dispose();
        }

        public void Record()
        {
            m_WorldsData.Clear();
            m_ArchetypesData.Clear();
            m_ArchetypeComponentsData.Clear();
            m_ArchetypesMemoryData.Clear();
            m_ArchetypesStableHash.Clear();

            for (var i = 0; i < World.All.Count; ++i)
            {
                var world = World.All[i];
                if (!world.IsCreated)
                    continue;

                // Skip if world is in exclusive transaction (required for EntityManager.GetAllArchetypes)
                if (!world.EntityManager.CanBeginExclusiveEntityTransaction())
                    continue;

                m_Recorder.Record(world);
                m_WorldsData.Add(new WorldData(world));
                m_ArchetypesData.AddRange(m_Recorder.ArchetypesData);
                m_ArchetypeComponentsData.AddRange(m_Recorder.ArchetypeComponentsData);
                m_ArchetypesMemoryData.AddRange(m_Recorder.ArchetypesMemoryData);
                m_ArchetypesStableHash.AddRange(m_Recorder.ArchetypesStableHash);
            }
        }
        
        internal static unsafe bool MemCmp<T>(NativeArray<T> lhs, NativeArray<T> rhs)
            where T : unmanaged
        {
            if (lhs.Length != rhs.Length)
                return false;

            return UnsafeUtility.MemCmp(lhs.GetUnsafeReadOnlyPtr(), rhs.GetUnsafeReadOnlyPtr(), sizeof(T) * lhs.Length) == 0;
        }        
    }
}
