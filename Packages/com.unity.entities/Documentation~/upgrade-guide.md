# Upgrading to Entities 1.4

Entities 1.4 has some changes that might introduce warnings to your project. To fix those warnings, do the following:

* [Change Entities.ForEach code](#change-entitiesforeach-code)
* [Change Aspects code](#change-aspects-code)
* [Change EntityCommandBuffer PlaybackPolicy code](#change-entitycommandbuffer-playbackpolicy-code)
* [Migrate PostLoadCommandBuffer to RequestSceneLoaded.ImportEntity](#migrate-postloadcommandbuffer-to-requestsceneloadedimportentity)

If your project uses InstanceID-based APIs, refer to the [EntityId API migration guide](xref:um-instanceid-to-entityid-migration).

## Change Entities.ForEach code

To consolidate the Entities API and improve iteration time, `Entities.ForEach` is deprecated in Entities 1.4, and you should use either [`IJobEntity`](#ijobentity) or [`SystemAPI.Query`](#systemapiquery).

### IJobEntity

Because `IJobEntity` `Execute` methods support `ref` and `in` parameters to denote read-only and read-write status, you can often copy the lambda of an `Entities.ForEach`into the `Execute` method for the `IJobEntity` job struct. Additionally, `IJobEntity` supports all the scheduling options that `Entities.ForEach` supports. 

> [!NOTE]
> `IJobEntity` isn't Burst-compiled by default and it can't capture variables because there is no lambda body. Use the `[BurstCompile]` attribute to enable Burst compilation and write captured variables into fields on the job struct.

Code example using `Entities.ForEach`

```c#
public partial class RotationSpeedSystemForEachISystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        Entities
            .ForEach((ref LocalTransform transform, in RotationSpeed rotationSpeed) =>
            {
                transform.Rotation = math.mul(
                    math.normalize(transform.Rotation),
                    quaternion.AxisAngle(math.up(), rotationSpeed.RadiansPerSecond * deltaTime));
            })
            .ScheduleParallel();
    }
}

```

Code example using `IJobEntity`

```c#
[BurstCompile]
public partial struct ASampleJob : IJobEntity
{
    public float DeltaTime;
    void Execute(ref LocalTransform transform, in RotationSpeed rotationSpeed)
    {
        transform.Rotation = math.mul(
            math.normalize(transform.Rotation),
            quaternion.AxisAngle(math.up(), rotationSpeed.RadiansPerSecond * DeltaTime));
    }
}

public partial class ASample : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        new ASampleJob{ DeltaTime = deltaTime }.ScheduleParallel();
    }
}
```

For more information about `IJobEntity`, refer to [Iterate over component data with IJobEntity](iterating-data-ijobentity.md)

### SystemAPI.Query

For entity iteration that doesn't have to happen in a job (but can still be Burst compiled), `SystemAPI.Query` can provide a simpler option because`SystemAPI.Query` utilizes `RefRO` and `RefRW` types to wrap type parameters that are accessed as read-only and read-write status respectively. There are additional builder methods on `Query` to indicate `WithAll`, `WithNone`, `WithAny` and other options.

The following changes the previous `Entities.ForEach` example to use `SystemAPI.Query`

```c#
public partial class ASample : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        foreach (var (transform, rotationSpeed) in 
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
        {
            transform.ValueRW.Rotation = math.mul(
                math.normalize(transform.ValueRO.Rotation),
                quaternion.AxisAngle(math.up(), rotationSpeed.ValueRO.RadiansPerSecond * deltaTime));
        }
    }
}
```

For more information about `SystemAPI.Query`, refer to [Iterate over component data with SystemAPI.Query](systems-systemapi-query.md).

## Change Aspects code

Aspects is deprecated from Entities 1.4, and there's no direct replacement for them. Instead you must replace the abstraction with explicit code that queries for the correct set of components and performs the expected operation on them. The following code provides a simple example of converting an aspect and its usage into an explicit `EntityQuery` and a helper method designed to perform the operation.

Code example using Aspects:

```c#
public partial struct RotationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var elapsedTime = SystemAPI.Time.ElapsedTime;

        foreach (var movement in SystemAPI.Query<VerticalMovementAspect>())
        {
            movement.Move(elapsedTime);
        }
    }
}

readonly partial struct VerticalMovementAspect : IAspect
{
    readonly RefRW<LocalTransform> m_Transform;
    readonly RefRO<RotationSpeed> m_Speed;

    public void Move(double elapsedTime)
    {
        m_Transform.ValueRW.Position.y = (float)math.sin(elapsedTime * m_Speed.ValueRO.RadiansPerSecond);
    }
}


```

Code example using `EntityQuery`:

```c#
public partial struct RotationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var elapsedTime = SystemAPI.Time.ElapsedTime;

        foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
        {
            VerticalMovementHelper.Move(elapsedTime, transform, speed);
        }
    }
}

static class VerticalMovementHelper
{
    public static void Move(double elapsedTime, RefRW<LocalTransform> transform, RefRO<RotationSpeed> speed)
    {
        transform.ValueRW.Position.y = (float)math.sin(elapsedTime * speed.ValueRO.RadiansPerSecond);
    }
}
```

## Change EntityCommandBuffer PlaybackPolicy code

The `PlaybackPolicy` enum is deprecated in its entirety and will be removed in a future version. This includes both `PlaybackPolicy.SinglePlayback` and `PlaybackPolicy.MultiPlayback`, as well as any `EntityCommandBuffer` constructor overload that takes a `PlaybackPolicy` parameter.

Going forward, `SinglePlayback` is the only supported behavior and is the default: an `EntityCommandBuffer` can be played back only once. Create an `EntityCommandBuffer` without specifying a `PlaybackPolicy`. If you need to apply the same set of commands more than once, record them again into a new `EntityCommandBuffer` for each playback.

## Migrate PostLoadCommandBuffer to RequestSceneLoaded.ImportEntity

The `PostLoadCommandBuffer` managed `IComponentData` is deprecated and will be removed in a future version. Replace it with [`RequestSceneLoaded.ImportEntity`](xref:Unity.Entities.RequestSceneLoaded), which delivers a regular main-world entity (and all of its components) into the section's streaming world before `ProcessAfterLoadGroup` runs.

To migrate:

1. Build the per-instance data on a regular entity in the main world, directly adding the components you previously recorded into the `EntityCommandBuffer`.
1. Pass that entity as `ImportEntity` in the `SceneSystem.LoadParameters` you give to `SceneSystem.LoadSceneAsync` (or write `RequestSceneLoaded { ImportEntity = dataEntity }` on the scene or section meta entity).
1. Your existing `ProcessAfterLoad` system queries the imported components exactly as before; the carrier entity appears in the streaming world. Destroy it inside that system if you don't want it to survive into the main world after the load.

You own the source entity in the main world: keep it alive until the load completes, and destroy it yourself when it's no longer needed.

## Convert managed components to unmanaged components

Managed components (`IComponentData`) and managed shared components (`ISharedComponentData`) are deprecated and will be removed in a future version. A component or shared component is managed if it's a `class`, or a `struct` that contains managed (reference-type) fields such as `string` or other classes. Convert these to unmanaged components: use a `struct` that contains only unmanaged fields, and reference `UnityEngine.Object` instances with `UnityObjectRef<T>`. For more information, refer to [Reference Unity objects in your code](reference-unity-objects.md).
