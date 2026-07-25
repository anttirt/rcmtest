using System;
using System.Collections.Generic;
using Unity.Collections;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

namespace Unity.Entities.Editor
{
    partial class MemoryProfilerModule
    {
        internal readonly struct MemoryProfilerTreeViewItemData
        {
            public readonly string WorldName;
            public readonly ulong WorldSequenceNumber;
            public readonly ulong StableHash;
            public readonly ulong AllocatedBytes;
            public readonly ulong UnusedBytes;
            public readonly int EntityCount;
            public readonly int UnusedEntityCount;
            public readonly int ChunkCount;
            public readonly int ChunkCapacity;
            public readonly int SegmentCount;
            public readonly int InstanceSize;
            public readonly TypeIndex[] ComponentTypes;

            public MemoryProfilerTreeViewItemData(string worldName, ulong worldSequenceNumber, ArchetypeData archetypeData, ArchetypeMemoryData archetypeMemoryData, NativeArray<ArchetypeComponentData> archetypeComponentsData)
            {
                WorldName = worldName;
                WorldSequenceNumber = worldSequenceNumber;
                StableHash = archetypeData.StableHash;
                AllocatedBytes = archetypeMemoryData.CalculateAllocatedBytes();
                UnusedBytes = archetypeMemoryData.CalculateUnusedBytes(archetypeData);
                EntityCount = archetypeMemoryData.EntityCount;
                UnusedEntityCount = archetypeMemoryData.CalculateUnusedEntityCount(archetypeData);
                ChunkCount = archetypeMemoryData.ChunkCount;
                ChunkCapacity = archetypeData.ChunkCapacity;
                SegmentCount = archetypeMemoryData.SegmentCount;
                InstanceSize = archetypeData.InstanceSize;

                // Count matching components first
                var componentCount = 0;
                for (var i = 0; i < archetypeComponentsData.Length; ++i)
                {
                    if (archetypeComponentsData[i].ArchetypeStableHash == archetypeData.StableHash)
                        componentCount++;
                }

                ComponentTypes = new TypeIndex[componentCount];

                // Components are already ordered by IndexInArchetype during creation
                // Just extract matching ones
                var writeIndex = 0;
                for (var i = 0; i < archetypeComponentsData.Length && writeIndex < componentCount; ++i)
                {
                    if (archetypeComponentsData[i].ArchetypeStableHash == archetypeData.StableHash)
                    {
                        ComponentTypes[writeIndex++] = archetypeComponentsData[i].GetTypeIndex();
                    }
                }
            }
        }
        
        class MemoryProfilerTreeViewItem
        {
            public string displayName { get; set; }
            public string worldName { get; set; }
            public ulong worldSequenceNumber { get; set; }
            public bool isWorldItem { get; set; }
            public ulong totalAllocatedBytes { get; set; }
            public ulong totalUnusedBytes { get; set; }
            public MemoryProfilerTreeViewItemData data { get; set; }
        }
    }
}
