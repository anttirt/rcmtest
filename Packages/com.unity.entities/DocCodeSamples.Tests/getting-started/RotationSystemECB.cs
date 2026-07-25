namespace Doc.CodeSamples.Tests.GettingStarted
{
    #region example
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Transforms;

    // This system rotates entities until they've rotated a specified amount,
    // then removes the RotationSpeed component to stop rotation.
    [DisableAutoCreation]
    public partial struct RotationSystemECB : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get an EntityCommandBuffer from the EndSimulationEntityCommandBufferSystem.
            // Commands recorded to this command buffer play back at the end of the
            // simulation group, after all systems have finished iterating.
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem
                .Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            #region foreach-loop
            foreach (var (transform, speed, lifetime, entity) in
                        SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>,
                        RefRW<RotationLifetime>>().WithEntityAccess())
            {
                float rotationThisFrame = speed.ValueRO.RadiansPerSecond * deltaTime;
                transform.ValueRW = transform.ValueRO.RotateY(rotationThisFrame);
                lifetime.ValueRW.RadiansRemaining -= rotationThisFrame;

                if (lifetime.ValueRO.RadiansRemaining <= 0)
                {
                    // Record the command now, execute it later.
                    // The component is removed after this system, and other systems
                    // in the simulation group, finish their iteration.
                    ecb.RemoveComponent<RotationSpeed>(entity);
                }
            }
            #endregion
        }
    }
    #endregion
}