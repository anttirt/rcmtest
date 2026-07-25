---
uid: ecs-workflow-example-ecb
---

# Use entity command buffers for structural changes

When iterating through entities in an ECS system, your use case might require performing certain operations with entity components or entities themselves, such as removing or adding a component, or destroying an entity from the current loop iteration. Such changes are called [structural changes](concepts-structural-changes.md).

Structural changes modify an entity's archetype, which changes the chunk layout the iteration is processing. This section shows why you can't perform such changes immediately during an iteration, and how to use an entity command buffer (ECB) to defer them to a point after the iteration completes.

The section covers the following topics:

* [Example use case involving structural changes during iteration](#example-use-case).
* [Why EntityManager.RemoveComponent doesn't work in this case](#invalid-example).
* [The solution: Use an entity command buffer](#use-ecb).
* [Alternative approach: creating ECB instance manually](#create-ecb-manually).

> [!NOTE]
> This workflow builds on concepts introduced in the [Authoring and baking workflow](ecs-workflow-example-authoring-baking.md). It assumes you have a working project with the **Cube** prefab, the **RotationSpeed** component, and the `RotationSystem` that rotates entities. Completing the steps in [Entity prefab instantiation workflow](ecs-workflow-example-prefab-instantiation.md#create-spawner) is optional, but makes it easier to observe the effects of these examples.

## Prerequisites

1. A Unity 6.X project with the following packages installed:
    * [Entities](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html)
    * [Entities Graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest/index.html)

2. To follow along in the Editor, complete the steps described in [Authoring and baking workflow](ecs-workflow-example-authoring-baking.md).

3. Optional: Complete the steps described in [Entity prefab instantiation workflow](ecs-workflow-example-prefab-instantiation.md#create-spawner).

## <a id="example-use-case"></a>Example of structural changes during iteration

Consider the [rotation system](ecs-workflow-example-authoring-baking.md#rotation-system) from [Authoring and baking workflow](ecs-workflow-example-authoring-baking.md):

[!code-cs[Rotation system](../DocCodeSamples.Tests/getting-started/RotationSystem.cs#example-no-using)]

Assume you want entities to stop rotating after they've rotated through a certain angle. To implement this, you need to remove the `RotationSpeed` component from entities that have completed their rotation.

### <a id="rotation-lifetime"></a>Create the RotationLifetime component

First, create a new component that tracks how much rotation remains before an entity should stop rotating.

1. Create a new C# script called `RotationLifetimeAuthoring.cs` and replace the contents with the following code:

    [!code-cs[RotationLifetimeAuthoring](../DocCodeSamples.Tests/getting-started/RotationLifetimeAuthoring.cs#example)]

2. Add the **Rotation Lifetime Authoring** component to the **Cube** prefab. This gives each cube a configurable amount of rotation before it stops.

## <a id="invalid-example"></a>Why EntityManager.RemoveComponent doesn't work in this case

The [`EntityManager`](xref:Unity.Entities.EntityManager) struct provides a [`RemoveComponent`](xref:Unity.Entities.EntityManager.RemoveComponent*) method. This method is suitable for immediate, synchronous changes when the code isn't iterating over entities that would be affected by the structural change.

The following code example shows a common mistake of attempting to remove the `RotationSpeed` component inside a `foreach` loop using the [`EntityManager.RemoveComponent`](xref:Unity.Entities.EntityManager.RemoveComponent*) method:

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

// NOTICE: This code demonstrates an INVALID approach that causes an error.
[BurstCompile]
public partial struct RotationSystemInvalidExample : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, speed, lifetime, entity) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>,
                    RefRW<RotationLifetime>>().WithEntityAccess())
        {
            float rotationThisFrame = speed.ValueRO.RadiansPerSecond * deltaTime;
            transform.ValueRW = transform.ValueRO.RotateY(rotationThisFrame);
            lifetime.ValueRW.RadiansRemaining -= rotationThisFrame;

            if (lifetime.ValueRO.RadiansRemaining <= 0)
            {
                // INVALID: This operation causes an InvalidOperationException.
                // You cannot make structural changes while iterating over entities.
                state.EntityManager.RemoveComponent<RotationSpeed>(entity);
            }
        }
    }
}
```

The previous code causes an exception at runtime:

```json
InvalidOperationException: Structural changes are not allowed during iteration.
You must use an EntityCommandBuffer to defer structural changes until after the iteration is complete.
```

When you iterate over entities with `SystemAPI.Query`, ECS reads from memory chunks that are organized by archetype (the specific combination of components an entity has). Removing a component changes the entity's archetype, which means:

* The entity must move to a different chunk.
* The chunk layout the code is currently iterating over becomes invalid.
* ECS can't safely continue the iteration.

The following operations are considered structural changes:

* Creating or destroying an [entity](concepts-entities.md).
* Adding or removing [components](concepts-components.md).
* Setting a [shared component](components-shared-introducing.md) value.

## <a id="use-ecb"></a>The solution: Use an entity command buffer

Instead of executing an `EntityManager.RemoveComponent` command immediately, you can queue the command into an entity command buffer. These queued commands are executed later in the frame, after the iteration is complete.

For better performance and easier management, use one of Unity's [built-in ECB systems](systems-entity-command-buffer-automatic-playback.md#default-ecb-systems), such as [`EndSimulationEntityCommandBufferSystem`](xref:Unity.Entities.EndSimulationEntityCommandBufferSystem). This allows multiple systems to queue commands that Unity executes together at a specific point in the frame.

To implement this approach:

1. In a system, get the singleton for the `EndSimulationEntityCommandBufferSystem` using [`SystemAPI.GetSingleton`](xref:Unity.Entities.SystemAPI.GetSingleton*).

    ```csharp
    var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
    ```

2. Create the command buffer from the singleton using the [`CreateCommandBuffer`](xref:Unity.Entities.BeginFixedStepSimulationEntityCommandBufferSystem.Singleton.CreateCommandBuffer(Unity.Entities.WorldUnmanaged)) method.

    ```csharp
    var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
    ```

3. Inside a loop, use `ecb.RemoveComponent<RotationSpeed>(entity)` instead of `EntityManager.RemoveComponent<RotationSpeed>(entity)` to remove a component. This queues the commands that make structural changes in the command buffer.
    
    [!code-cs[RotationSystemECB](../DocCodeSamples.Tests/getting-started/RotationSystemECB.cs#foreach-loop)]    

    > [!NOTE]
    >`SystemAPI.Query` is called with the [`WithEntityAccess`](xref:Unity.Entities.QueryEnumerable`1.WithEntityAccess*) method in this loop. This is to get a reference to the entity itself, from which the code removes a component later.

4. The `EndSimulationEntityCommandBufferSystem` automatically executes the commands at the end of the simulation group. You don't need to trigger the execution manually. For more information about built-in ECB systems, refer to the section [Default EntityCommandBufferSystem systems](systems-entity-command-buffer-automatic-playback.md#default-ecb-systems).

Create a new C# script called `RotationSystemECB.cs` and replace the contents with the following code:

[!code-cs[RotationSystemECB](../DocCodeSamples.Tests/getting-started/RotationSystemECB.cs#example)]

In the previous system, `ecb.RemoveComponent()` queues the command, the iteration continues safely, and the `EndSimulationEntityCommandBufferSystem` system executes the commands after the iteration ends. This lets you avoid exceptions from modifying entities during iteration, and reduces sync points by batching structural changes.

If you have the original `RotationSystem` from the [Authoring and baking workflow](ecs-workflow-example-authoring-baking.md) in your project, either remove it or disable it using the `[DisableAutoCreation]` attribute. Otherwise, both systems will process the same entities.

> [!NOTE]
> Entity command buffers don't batch commands for faster execution. When the ECB executes, it executes the same code path as `EntityManager` does. The main benefit of ECBs is that they let you defer structural changes to a safe point in the frame, and enable multiple systems or parallel jobs to queue commands independently.

## <a id="create-ecb-manually"></a>Alternative approach: create an ECB instance manually

The code in the previous section uses the built-in [`EndSimulationEntityCommandBufferSystem`](xref:Unity.Entities.EndSimulationEntityCommandBufferSystem) system to create an entity command buffer.

In certain scenarios, you might want to create and manage an entity command buffer manually, without relying on a built-in system. For example, if you want to execute commands immediately after a loop completes.

To implement manual ECB creation and execution:

1. Create a new `EntityCommandBuffer` using `Allocator.Temp` before your loop begins.

    ```csharp
    var ecb = new EntityCommandBuffer(Allocator.Temp);
    ```

    For more information on different allocators, refer to [Memory allocators overview](allocators-overview.md).

2. Inside a loop, use `ecb.RemoveComponent<RotationSpeed>(entity)` to remove a component.

3. After the loop finishes, call `ecb.Playback(state.EntityManager)` to execute all queued commands.

4. Dispose of the buffer using `ecb.Dispose()`. When using `Allocator.Temp`, the buffer is deallocated automatically at the end of the frame, but it's good practice to dispose of it explicitly.

The complete code implementing the alternative solution:

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public partial struct RotationSystemManualECB : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Create a temporary command buffer.
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, speed, lifetime, entity) in
                    SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>,
                    RefRW<RotationLifetime>>().WithEntityAccess())
        {
            float rotationThisFrame = speed.ValueRO.RadiansPerSecond * deltaTime;
            transform.ValueRW = transform.ValueRO.RotateY(rotationThisFrame);
            lifetime.ValueRW.RadiansRemaining -= rotationThisFrame;

            if (lifetime.ValueRO.RadiansRemaining <= 0)
            {
                // Queue the command.
                ecb.RemoveComponent<RotationSpeed>(entity);
            }
        }

        // Execute all queued commands immediately after the loop.
        ecb.Playback(state.EntityManager);

        // Dispose of the buffer.
        ecb.Dispose();
    }
}
```

## Additional resources

* [Entity command buffer overview](systems-entity-command-buffers.md)
* [Use an entity command buffer](systems-entity-command-buffer-use.md)
* [Entity command buffer playback](systems-entity-command-buffer-playback.md)
* [Automatic playback and disposal of entity command buffers](systems-entity-command-buffer-automatic-playback.md)
* [Manage structural changes introduction](systems-manage-structural-changes-intro.md)
* [Authoring and baking workflow](ecs-workflow-example-authoring-baking.md)
* [Prefab instantiation workflow](ecs-workflow-example-prefab-instantiation.md)
