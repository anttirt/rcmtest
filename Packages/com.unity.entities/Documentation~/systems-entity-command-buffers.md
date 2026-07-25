---
uid: systems-entity-command-buffers
---

# Entity command buffer overview

An entity command buffer (ECB) stores a queue of thread-safe commands which you can add to and later play back. You can use an ECB to schedule [structural changes](concepts-structural-changes.md) from jobs and perform changes on the main thread after the jobs complete. You can also use ECBs on the main thread to delay changes, or play back a set of changes multiple times.

The [methods in `EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) record commands, which mirror methods available in [`EntityManager`](xref:Unity.Entities.EntityManager). For example:

* `CreateEntity(EntityArchetype)`: Registers a command that creates a new entity with the specified archetype.
* `DestroyEntity(Entity)`: Registers a command that destroys the entity.
* `SetComponent<T>(Entity, T)`: Registers a command that sets the value for a component of type `T` on the entity.
* `AddComponent<T>(Entity)`: Registers a command that adds a component of type `T` to the entity.
* `RemoveComponent<T>(EntityQuery)`: Registers a command that removes a component of type `T` from all entities that match the query.

## Entities created by command buffers

The `EntityCommandBuffer` methods `CreateEntity` and `Instantiate` return valid `Entity` references at record time. ECS allocates the entity immediately but doesn't assign it a chunk until the `Playback` method runs. As a result:

* You can use the returned entity in later commands on the same buffer, for example in `ecb.AddComponent<T>(e)`.
* You can store the entity reference or pass it to other code, and it stays valid before and after the `Playback` method runs.
* You can store these entities in the `Entity` fields of component values (including `IBufferElementData`), for example `ecb.SetComponent(e2, new Parent { Value = e })`. Unity stores the references directly, so it doesn't need to remap them during playback.
* You can't access the entity through `EntityManager` or queries until after the `Playback` method runs, because it has no chunk before then.

When you instantiate a prefab with the `Instantiate` method, Unity allocates only the root entity at record time. If the prefab has a `LinkedEntityGroup` buffer (which prefabs with a child hierarchy have automatically), Unity allocates the child entities when the `Playback` method runs, and you can read them from the root's `LinkedEntityGroup` buffer once playback completes.

You can also pass an entity created by one command buffer into the commands of another one. The reference stays valid, but you must call the `Playback` method on the originating buffer before you can access the entity through `EntityManager` or queries. To keep the playback order clear, use a single buffer for each related set of commands.

## Entity command buffer safety

`EntityCommandBuffer` has a job safety handle, similar to a [native container](xref:JobSystemNativeContainer). This safety is only available in the Unity Editor, and not in player builds. The safety checks throw an exception if you try to do any of the following on an incomplete scheduled job that uses an ECB:

* Access the `EntityCommandBuffer` through its `AddComponent`, `Playback`, `Dispose`, or other methods.
* Schedule another job that accesses the same `EntityCommandBuffer`, unless the new job [depends on](xref:JobSystemJobDependencies) the already scheduled job.

>[!NOTE] 
>It’s best practice to use a separate ECB for each distinct job. This is because if you reuse an ECB in consecutive jobs, the jobs might use an overlapping set of sort keys (such as if both use `ChunkIndexInQuery`), and the commands that the jobs record might be interleaved.

## Additional resources

* [Use an entity command buffer](systems-entity-command-buffer-use.md)
* [Entity command buffer playback](systems-entity-command-buffer-playback.md)
