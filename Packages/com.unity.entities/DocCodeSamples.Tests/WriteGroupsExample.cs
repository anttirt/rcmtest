using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Doc.CodeSamples.Tests
{
    // ===========================
    // Component Definitions
    // ===========================

    #region health-color-components
    public struct HealthComponent : IComponentData
    {
        public int Value;
    }

    public struct ColorComponent : IComponentData
    {
        public float4 Value;
    }
    #endregion

    #region invincible-tag
    [WriteGroup(typeof(ColorComponent))]
    public struct InvincibleTagComponent : IComponentData { }
    #endregion

    #region write-group-abc
    public struct W : IComponentData
    {
        public int Value;
    }

    [WriteGroup(typeof(W))]
    public struct A : IComponentData
    {
        public int Value;
    }

    [WriteGroup(typeof(W))]
    public struct B : IComponentData
    {
        public int Value;
    }
    #endregion

    #region write-group-c
    [WriteGroup(typeof(W))]
    public struct C : IComponentData
    {
        public int Value;
    }
    #endregion

    // =======================================================================
    // ComputeColorFromHealthSystem - Using IJobEntity with FilterWriteGroup
    // =======================================================================

    #region compute-color-job
    [BurstCompile]
    public partial struct ComputeColorFromHealthJob : IJobEntity
    {
        // IJobEntity generates a query for entities that have ColorComponent
        // and HealthComponent. The FilterWriteGroup option is applied via
        // the system's query configuration.
        private void Execute(ref ColorComponent color, in HealthComponent health)
        {
            // Example: map health (0-100) to a color gradient from red to green
            float t = math.saturate(health.Value / 100f);
            color.Value = new float4(1f - t, t, 0f, 1f);
        }
    }
    #endregion

    #region compute-color-system
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ComputeColorFromHealthSystem : ISystem
    {
        private EntityQuery m_Query;

        public void OnCreate(ref SystemState state)
        {
            // Create a query with FilterWriteGroup to support write groups.
            // This allows other systems to exclude entities by adding components
            // to the write group of ColorComponent.
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ColorComponent>()
                .WithAll<HealthComponent>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(ref state);

            state.RequireForUpdate(m_Query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ComputeColorFromHealthJob().ScheduleParallel(m_Query);
        }
    }
    #endregion

    // =======================================================
    // AddingSystem - Using IJobEntity with FilterWriteGroup
    // =======================================================

    #region adding-job
    [BurstCompile]
    public partial struct AddingJob : IJobEntity
    {
        private void Execute(ref W w, in B b)
        {
            w.Value += b.Value;
        }
    }
    #endregion

    #region adding-system
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct AddingSystem : ISystem
    {
        private EntityQuery m_Query;

        public void OnCreate(ref SystemState state)
        {
            // Support write groups by setting EntityQueryOptions.FilterWriteGroup.
            // This excludes entities that have component A, because W is writable
            // and A is part of the write group of W.
            // It doesn't exclude entities with B, because B is explicitly required.
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<W>()
                .WithAll<B>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(ref state);

            state.RequireForUpdate(m_Query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AddingJob().ScheduleParallel(m_Query);
        }
    }
    #endregion

    // ==========================================================
    // Alternative: Using SystemAPI.Query with FilterWriteGroup
    // ==========================================================

    #region adding-system-query
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct AddingSystemWithQuery : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Use SystemAPI.Query with WithOptions for write group filtering.
            foreach (var (w, b) in
                SystemAPI.Query<RefRW<W>, RefRO<B>>()
                    .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                w.ValueRW.Value += b.ValueRO.Value;
            }
        }
    }
    #endregion

    // ========================================================
    // RotationAngleAxis Example - Override Unity.Transforms
    // ========================================================

    #region rotation-angle-axis-component
    [Serializable]
    [WriteGroup(typeof(LocalTransform))]
    public struct RotationAngleAxis : IComponentData
    {
        public float Angle;
        public float3 Axis;
    }
    #endregion

    #region rotation-angle-axis-job
    [BurstCompile]
    public partial struct RotationAngleAxisJob : IJobEntity
    {
        private void Execute(ref LocalTransform transform, in RotationAngleAxis source)
        {
            transform.Rotation = quaternion.AxisAngle(
                math.normalize(source.Axis),
                source.Angle);
        }
    }
    #endregion

    #region rotation-angle-axis-system
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct RotationAngleAxisSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new RotationAngleAxisJob().ScheduleParallel();
        }
    }
    #endregion

    // ==========================================================
    // Extended System Example - Querying multiple combinations
    // ==========================================================

    #region extended-system
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ExtendedWriteGroupSystem : ISystem
    {
        private EntityQuery m_Query;

        public void OnCreate(ref SystemState state)
        {
            // When extending a system that uses write groups, you must explicitly
            // query for each combination of components that make sense.
            // Use WithAny to match entities with A OR B (in addition to C and W).
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<W>()
                .WithAll<C>()
                .WithAny<A, B>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(ref state);

            state.RequireForUpdate(m_Query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Process entities that have C and W, and also have A or B.
            foreach (var (w, c) in
                SystemAPI.Query<RefRW<W>, RefRO<C>>()
                    .WithAny<A, B>()
                    .WithOptions(EntityQueryOptions.FilterWriteGroup))
            {
                w.ValueRW.Value += c.ValueRO.Value;
            }
        }
    }
    #endregion
}