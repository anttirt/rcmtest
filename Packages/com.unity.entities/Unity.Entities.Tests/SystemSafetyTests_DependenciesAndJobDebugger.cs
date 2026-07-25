using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    partial class SystemSafetyTests_DependenciesAndJobDebugger : ECSTestsFixture
    {
        struct ReadWriteJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> Blah;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        #region SystemBase

        [UpdateBefore(typeof(CorrectSystem))]
        partial class MisbehavingSystem : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                GetComponentTypeHandle<EcsTestData>(false);
            }

            protected override void OnUpdate()
            {
                //@TODO: EntityManager.GetComponentTypeHandle vs Scheduling dependencies
                var job = new ReadWriteJob {Blah = GetComponentTypeHandle<EcsTestData>(false)};
                job.Schedule(GetEntityQuery(typeof(EcsTestData)), default);
            }
        }

        partial class CorrectSystem : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                GetComponentTypeHandle<EcsTestData>(false);
            }

            protected override void OnUpdate()
            {
                var job = new ReadWriteJob {Blah = GetComponentTypeHandle<EcsTestData>(false)};
                Dependency = job.Schedule(GetEntityQuery(typeof(EcsTestData)), Dependency);
            }
        }

        [UpdateBefore(typeof(CorrectSystem))]
        partial class NestedBrokenSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                World.GetOrCreateSystem<MisbehavingSystem>().Update(World.Unmanaged);
            }
        }

        #endregion

        #region ISystem

        [UpdateBefore(typeof(CorrectISystem))]
        [BurstCompile]
        partial struct MisbehavingISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>(false);
            }

            [BurstCompile]

            public void OnUpdate(ref SystemState state)
            {
                var job = new ReadWriteJob {Blah = state.GetComponentTypeHandle<EcsTestData>(false)};
                job.Schedule(state.GetEntityQuery(ComponentType.ReadWrite<EcsTestData>()), default);
            }
        }

        [BurstCompile]
        partial struct CorrectISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>(false);
            }
            [BurstCompile]

            public void OnUpdate(ref SystemState state)
            {
                var job = new ReadWriteJob {Blah = state.GetComponentTypeHandle<EcsTestData>(false)};
                state.Dependency = job.Schedule(state.GetEntityQuery(ComponentType.ReadWrite<EcsTestData>()), state.Dependency);
            }
        }

        [UpdateBefore(typeof(CorrectISystem))]
        partial struct NestedBrokenISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>(false);
            }
            public void OnUpdate(ref SystemState state)
            {
                state.WorldUnmanaged.GetExistingUnmanagedSystem<MisbehavingISystem>().Update(state.WorldUnmanaged);
            }
        }

        #endregion


        [BurstCompile]
        public partial struct IJobEntityReproISystem : ISystem
        {
            public ComponentLookup<EcsTestData> lookup;

            public void OnCreate(ref SystemState state)
            {
                lookup = state.GetComponentLookup<EcsTestData>();
            }
            partial struct TestJob : IJobEntity
            {
                public ComponentLookup<EcsTestData> lookup;

                public void Execute(Entity entity, ref EcsTestData rotation, in EcsTestData2 rotationSpeed)
                {
                    var value = lookup[entity];
                }
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                lookup.Update(ref state);

                var testJob = new TestJob() { lookup = lookup };
                testJob.ScheduleParallel();
            }
        }
    }
}
