using Doc.CodeSamples.Tests;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

// The files in this namespace are used to compile/test the code samples in the documentation.
// Snippets used in entity_command_buffer.md
namespace Doc.CodeSamples.Tests
{
    partial struct EcbParallel : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            #region ecb_parallel

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Methods of this writer record commands to
            // the EntityCommandBuffer in a thread-safe way.
            EntityCommandBuffer.ParallelWriter parallelEcb = ecb.AsParallelWriter();

            #endregion
        }
    }

    public struct FooComp : IComponentData
    {
        public int Value;
    }

    public struct BarComp : IComponentData
    {
        public Entity TargetEnt;
    }

    [RequireMatchingQueriesForUpdate]
    partial class SingleThreadedSchedule_ECB : SystemBase
    {
        #region ecb_single_threaded

        // Single-threaded scheduling using IJobEntity (replaces deprecated Entities.ForEach).
        [BurstCompile]
        partial struct AddBarJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;

            void Execute(Entity e, in FooComp foo)
                {
                    if (foo.Value > 0)
                    {
                        // Record a command that will later add BarComp to the entity.
                    Ecb.AddComponent<BarComp>(e);
                    }
            }
        }

        protected override void OnUpdate()
        {
            // Buffer grows as needed.
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Schedule a single job (not parallel) that records commands.
            new AddBarJob { Ecb = ecb }.Schedule();

            // Complete the job so we can play back on the main thread.
            Dependency.Complete();

            // Apply the recorded structural changes.
            ecb.Playback(EntityManager);

            // Dispose the ECB.
            ecb.Dispose();
        }

        #endregion
    }

    #region ecb_multi_threaded

    [RequireMatchingQueriesForUpdate]
    partial struct MultiThreadedSchedule_ECB : ISystem
    {
        partial struct ParallelRecordingJob : IJobEntity
        {
            internal EntityCommandBuffer.ParallelWriter ecbParallel;

            // The ChunkIndexInQuery is unique for each chunk in the query and will be
            // consistent regardless of scheduling. This will result in deterministic
            // playback of the ECB.
            void Execute(Entity e, [ChunkIndexInQuery] int sortKey, in FooComp foo)
            {
                if (foo.Value > 0)
                {
                    // The first arg is the 'sort key' recorded with the command.
                    ecbParallel.AddComponent<BarComp>(sortKey, e);
                }
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            // We need to write to the ECB concurrently across threads.
            new ParallelRecordingJob { ecbParallel = ecb.AsParallelWriter() }.Schedule();

            // Playback is single-threaded as normal. Note that explicitly completing
            // adds a sync point, and is only done here for demonstration purposes.
            // (Having an existing EntityCommandBufferSystem do the playback would not
            // introduce an extra sync point.)
            state.Dependency.Complete();

            // To ensure deterministic playback order,
            // the commands are first sorted by their sort keys.
            ecb.Playback(state.EntityManager);

            ecb.Dispose();
        }
    }

    #endregion

    partial struct SystemContainingAllTheOtherCodeSnippets : ISystem
    {
        partial struct MyParallelRecordingJob : IJobEntity
        {
            internal EntityCommandBuffer.ParallelWriter ecbParallel;

            // The ChunkIndexInQuery is unique for each chunk in the query and will be
            // consistent regardless of scheduling. This will result in deterministic
            // playback of the ECB.
            void Execute(Entity e, [ChunkIndexInQuery] int sortKey, in FooComp foo)
            {
                if (foo.Value > 0)
                {
                    // The first arg is the 'sort key' recorded with the command.
                    ecbParallel.AddComponent<BarComp>(sortKey, e);
                }
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            {
                #region ecb_from_ecbsystem

                // ... in a system.

                // Assume an EntityCommandBufferSystem exists named FooECBSystem.
                // This call to GetSingleton automatically registers the job so that
                // it gets completed by the ECB system.
                var singleton = SystemAPI.GetSingleton<FooECBSystem.Singleton>();

                // Create a command buffer that will be played back
                // and disposed by MyECBSystem.
                EntityCommandBuffer ecb = singleton.CreateCommandBuffer(state.WorldUnmanaged);

                // An IJobEntity with no argument to Schedule implicitly
                // assigns its returned JobHandle to this.Dependency
                new MyParallelRecordingJob() { ecbParallel = ecb.AsParallelWriter() }.Schedule();

                #endregion
            }

            {
                #region ecb_created_entities

                // ... in a system

                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

                // CreateEntity returns a real entity reference immediately.
                // The entity has no chunk yet — that is assigned during Playback.
                Entity newEntity = ecb.CreateEntity();

                // Valid to use newEntity in later commands on the same buffer.
                ecb.AddComponent<FooComp>(newEntity);

                // Before Playback: EntityManager access throws because the
                // entity has no chunk yet.
                // state.EntityManager.AddComponent<BarComp>(newEntity); // throws

                ecb.Playback(state.EntityManager);

                // After Playback: the entity has a chunk and the same
                // reference works directly with EntityManager.
                state.EntityManager.AddComponent<BarComp>(newEntity);

                ecb.Dispose();

                #endregion
            }

            {
                #region ecb_entity_references

                // ... in a system

                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

                // For all entities with a FooComp component...
                foreach (var (f, e) in SystemAPI.Query<FooComp>().WithEntityAccess())
                {
                    // CreateEntity returns a real entity reference immediately.
                    Entity newEntity = ecb.CreateEntity();

                    // (Assume BarComp has an Entity field called TargetEnt.)
                    // The reference can be stored in component data directly.
                    BarComp bar = new BarComp { TargetEnt = newEntity };
                    ecb.AddComponent(e, bar);
                }

                // After playback, each entity with FooComp has a BarComp
                // whose TargetEnt references the corresponding new entity.
                ecb.Playback(state.EntityManager);

                ecb.Dispose();

                #endregion
            }
        }
    }

    public partial class FooSystem : SystemBase
    {
        protected override void OnUpdate() { }
    }

    public partial class FooECBSystem : EntityCommandBufferSystem
    {
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            // Required by IECBSingleton
            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
            }

            // Required by IECBSingleton
            public void SetAllocator(Allocator allocatorIn)
            {
                allocator = allocatorIn;
            }

            // Required by IECBSingleton
            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                allocator = allocatorIn;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }


    #region ecb_define_ecbsystem

    // You should specify where exactly in the frame this ECB system should update.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FooSystem))]
    public partial class MyECBSystem : EntityCommandBufferSystem
    {
        // The singleton component data access pattern should be used to safely access
        // the command buffer system. This data will be stored in the derived ECB System's
        // system entity.

        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem
                    .CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            // Required by IECBSingleton
            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                var ptr = UnsafeUtility.AddressOf(ref buffers);
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*)ptr;
            }

            // Required by IECBSingleton
            public void SetAllocator(Allocator allocatorIn)
            {
                allocator = allocatorIn;
            }

            // Required by IECBSingleton
            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                allocator = allocatorIn;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }

    #endregion

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FooSystem))]
    public partial class NonSingletonECBSystem : EntityCommandBufferSystem {
        // This class is intentionally empty. There is generally no
        // reason to put any code in an EntityCommandBufferSystem.
    }

    // 888       888        d8888 8888888b.  888b    888 8888888 888b    888  .d8888b.
    // 888   o   888       d88888 888   Y88b 8888b   888   888   8888b   888 d88P  Y88b
    // 888  d8b  888      d88P888 888    888 88888b  888   888   88888b  888 888    888
    // 888 d888b 888     d88P 888 888   d88P 888Y88b 888   888   888Y88b 888 888
    // 888d88888b888    d88P  888 8888888P"  888 Y88b888   888   888 Y88b888 888  88888
    // 88888P Y88888   d88P   888 888 T88b   888  Y88888   888   888  Y88888 888    888
    // 8888P   Y8888  d8888888888 888  T88b  888   Y8888   888   888   Y8888 Y88b  d88P
    // 888P     Y888 d88P     888 888   T88b 888    Y888 8888888 888    Y888  "Y8888P88

    // ###########################################################
    // ## DO NOT CHANGE THE CODE BELOW TO USE A SINGLETON,      ##
    // ## IT'S USED IN THE API DOCS FOR AddJobHandleForProducer ##
    // ###########################################################

    #region ecb_addjobhandleforproducer

    public struct ProcessInfo : IComponentData { public float Value; }
    public struct ProcessCompleteTag : IComponentData { }

    [BurstCompile]
    public partial struct ProcessInBackgroundJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ConcurrentCommands;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in ProcessInfo info)
        {
            // ...hypothetical processing goes here...

            // Remove ProcessInfo and add a ProcessCompleteTag...
            ConcurrentCommands.RemoveComponent<ProcessInfo>(chunkIndex, entity);
            ConcurrentCommands.AddComponent<ProcessCompleteTag>(chunkIndex, entity);
        }
    }

    public partial class AsyncProcessJobSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Get a reference to the system that will play back
            var ecbSystem = World.GetOrCreateSystemManaged<NonSingletonECBSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            // Pass the command buffer to the writer job, using its ParallelWriter interface
            var job = new ProcessInBackgroundJob
            {
                ConcurrentCommands = ecb.AsParallelWriter(),
            };
            Dependency = job.ScheduleParallel(Dependency);

            // Register the writer job with the playback system as an input dependency
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    #endregion

}
