#pragma warning disable 0618
#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public partial struct SomeSystem : global::Unity.Entities.ISystemCompilerGenerated
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0_ref_Unity.Entities.SystemState&")]
    void __OnUpdate_6D4E9467(ref SystemState state){
        #line 10 "/0/Test0.cs"
        var e = state.EntityManager.CreateEntity();
        #line 11 "/0/Test0.cs"
        state.EntityManager.AddComponentData(e, new EcsTestManagedComponent{value = "cake"});
        #line 13 "/0/Test0.cs"
        
#pragma warning disable CS0618 // managed API obsolete; internal/test caller still needs it.
        var comp = __query_1641826531_0.GetSingleton<EcsTestManagedComponent>().value;
        #line hidden
        
#pragma warning restore CS0618
    }

    #pragma warning disable 0618
    
    TypeHandle __TypeHandle;
    global::Unity.Entities.EntityQuery __query_1641826531_0;
    struct TypeHandle
    {
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        __query_1641826531_0 = 
            entityQueryBuilder
                .WithAll<global::Unity.Entities.Tests.EcsTestManagedComponent>()
                .WithOptions(global::Unity.Entities.EntityQueryOptions.IncludeSystems)
                .Build(ref state);
        entityQueryBuilder.Reset();
        entityQueryBuilder.Dispose();
    }
    
    public void OnCreateForCompiler(ref SystemState state)
    {
        __AssignQueries(ref state);
        __TypeHandle.__AssignHandles(ref state);
    }
    #pragma warning restore 0618
}
