#if ENABLE_PROFILER
using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Unity.Entities
{
    static partial class MemoryProfiler
    {
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct WorldAllocatorData : IEquatable<WorldAllocatorData>
        {
            public readonly ulong WorldSequenceNumber;
            public readonly int ArchetypeAllocatorUsedBytes;
            public readonly int ArchetypeAllocatorBudgetBytes;
            public readonly int EntityQueryAllocatorUsedBytes;
            public readonly int EntityQueryAllocatorBudgetBytes;

            public WorldAllocatorData(ulong worldSequenceNumber, int archetypeUsed, int archetypeBudget, int queryUsed, int queryBudget)
            {
                WorldSequenceNumber = worldSequenceNumber;
                ArchetypeAllocatorUsedBytes = archetypeUsed;
                ArchetypeAllocatorBudgetBytes = archetypeBudget;
                EntityQueryAllocatorUsedBytes = queryUsed;
                EntityQueryAllocatorBudgetBytes = queryBudget;
            }

            public bool Equals(WorldAllocatorData other)
            {
                return WorldSequenceNumber == other.WorldSequenceNumber;
            }

            [ExcludeFromBurstCompatTesting("Takes managed object")]
            public override bool Equals(object obj)
            {
                return obj is WorldAllocatorData data && Equals(data);
            }

            public override int GetHashCode()
            {
                return WorldSequenceNumber.GetHashCode();
            }
        }
    }
}
#endif
