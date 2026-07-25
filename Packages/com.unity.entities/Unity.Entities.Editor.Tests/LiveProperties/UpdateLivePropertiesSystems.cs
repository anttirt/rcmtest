using Unity.Burst;
using Unity.Entities.Tests;
using Unity.Mathematics;

namespace Unity.Entities.Editor.Tests
{
    partial class UpdateSingleLiveProperties : SystemBase
    {
        [BurstCompile]
        partial struct UpdateSingleLivePropertiesJob : IJobEntity
        {
            void Execute(ref ManualConversionComponentTest comp)
            {
                comp.BindInt = 1;
                comp.BindFloat = 1.5f;
                comp.BindBool = false;
                comp.BindQuaternion.value = new float4(3.0f, 4.0f, 5.0f, 6.0f);
                comp.BindVector3 = new float3(3.0f, 4.0f, 5.0f);
            }
        }

        protected override void OnUpdate()
        {
            new UpdateSingleLivePropertiesJob().Schedule();
        }
    }

    partial class UpdateMultipleLiveProperties : SystemBase
    {
        [BurstCompile]
        partial struct UpdateMultipleLivePropertiesJob : IJobEntity
        {
            void Execute(ref BindingRegistryIntComponent comp)
            {
                comp.Int1  = 1;
                comp.Int2  = new int2(1, 2);
                comp.Int3 = new int3(1, 2, 3);
                comp.Int4 = new int4(1, 2, 3, 4);
            }
        }

        protected override void OnUpdate()
        {
            new UpdateMultipleLivePropertiesJob().Schedule();
        }
    }
}
