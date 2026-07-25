using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using NUnit.Framework;

// The files in this namespace are used to compile/test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{
    #region lookup-ijobchunk
    [RequireMatchingQueriesForUpdate]
    public partial class MoveTowardsEntitySystem : SystemBase
    {
        private EntityQuery query;

        [BurstCompile]
        private partial struct MoveTowardsJob : IJobEntity
        {

            // Read-only data stored (potentially) in other chunks
            #region lookup-ijobchunk-declare
            [ReadOnly]
            public ComponentLookup<LocalToWorld> EntityPositions;
            #endregion

            // Non-entity data
            public float deltaTime;

            public void Execute(ref LocalTransform transform, in Target target, in LocalToWorld entityPosition)
            {
                // Get the target Entity object
                Entity targetEntity = target.entity;

                // Check that the target still exists
                if (!EntityPositions.HasComponent(targetEntity))
                    return;

                // Update translation to move the chasing entity toward the target
                float3 targetPosition = EntityPositions[targetEntity].Position;
                float3 chaserPosition = transform.Position;

                float3 displacement = targetPosition - chaserPosition;
                transform.Position = chaserPosition + displacement * deltaTime;
            }
        }

        protected override void OnCreate()
        {
            // Select all entities that have Translation and Target Component
            query = this.GetEntityQuery
                (
                    typeof(LocalTransform),
                    ComponentType.ReadOnly<Target>()
                );
        }

        protected override void OnUpdate()
        {
            // Create the job
            var job = new MoveTowardsJob();

            // Set the component data lookup field
            job.EntityPositions = GetComponentLookup<LocalToWorld>(true);

            // Set non-ECS data fields
            job.deltaTime = SystemAPI.Time.DeltaTime;

            // Schedule the job using Dependency property
            Dependency = job.ScheduleParallel(query, Dependency);
        }
    }
    #endregion

    [RequireMatchingQueriesForUpdate]
    public partial class Snippets : SystemBase
    {
        private EntityQuery query;
        protected override void OnCreate()
        {
            // Select all entities that have LocalTransform and Target Component
            query = this.GetEntityQuery(typeof(LocalTransform), ComponentType.ReadOnly<Target>());
        }

        [BurstCompile]
        private partial struct ChaserSystemJob : IJobEntity
        {
            // Non-entity data
            public float deltaTime;

            [ReadOnly]
            public ComponentLookup<LocalToWorld> EntityPositions;

            public void Execute(ref LocalTransform transform, in Target target, in LocalToWorld entityPosition)
            {
                var targetEntity = target.entity;

                // Check that the target still exists
                if (!EntityPositions.HasComponent(targetEntity))
                    return;

                // Update translation to move the chasing entity toward the target
                #region lookup-ijobchunk-read
                float3 targetPosition = EntityPositions[targetEntity].Position;
                float3 chaserPosition = transform.Position;
                float3 displacement = targetPosition - chaserPosition;
                float3 newPosition = chaserPosition + displacement * deltaTime;
                transform.Position = newPosition;
                #endregion

            }
        }

        #region lookup-ijobchunk-set
        protected override void OnUpdate()
        {
            var job = new ChaserSystemJob();

            // Set non-ECS data fields
            job.deltaTime = SystemAPI.Time.DeltaTime;

            // Schedule the job using Dependency property
            Dependency = job.ScheduleParallel(query, this.Dependency);
        }
        #endregion
    }
}
