namespace Doc.CodeSamples.Tests.GettingStarted
{
    #region example
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Transforms;

    // Multi-threaded version of RotationSystem. Schedules a Burst-compiled job that
    // rotates entities and performs a demo CPU workload in parallel.
    [BurstCompile]
    public partial struct RotationSystemMultithreaded : ISystem
    {
        #region OnCreate
        public void OnCreate(ref SystemState state)
        {
            // Run only when there is at least one entity with a RotationSpeed component.
            state.RequireForUpdate<RotationSpeed>();
        }
        #endregion

        #region OnUpdate
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new RotationJob
            {
                deltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }
        #endregion
    }

    #region RotationJob
    [BurstCompile]
    public partial struct RotationJob : IJobEntity
    {
        public float deltaTime;

        // IJobEntity generates a query for entities that have LocalTransform and
        // RotationSpeed.
        private void Execute(ref LocalTransform transform, in RotationSpeed speed)
        {
            transform = transform.RotateY(speed.RadiansPerSecond * deltaTime);
        }
    }
    #endregion
    #endregion
}
