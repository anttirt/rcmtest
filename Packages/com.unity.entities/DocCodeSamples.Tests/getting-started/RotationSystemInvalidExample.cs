namespace Doc.CodeSamples.Tests.GettingStarted
{
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Transforms;

    // NOTICE: This code demonstrates an INVALID approach of removing a component
    // that causes an error.
    public partial struct RotationSystemInvalidExample : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed, lifetime, entity) in
                        SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>, RefRW<RotationLifetime>>()
                                    .WithEntityAccess())
            {
                float rotationThisFrame = speed.ValueRO.RadiansPerSecond * deltaTime;
                transform.ValueRW = transform.ValueRO.RotateY(rotationThisFrame);
                lifetime.ValueRW.RadiansRemaining -= rotationThisFrame;

                if (lifetime.ValueRO.RadiansRemaining <= 0)
                {
                    // NOTICE: This operation causes an InvalidOperationException.
                    // You cannot make structural changes while iterating over entities.
                    // Use an EntityCommandBuffer to defer the removal instead.
                    state.EntityManager.RemoveComponent<RotationSpeed>(entity);
                }
            }
        }
    }
}
