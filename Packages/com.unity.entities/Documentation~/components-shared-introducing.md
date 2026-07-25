# Shared components introduction

Shared components group entities in chunks based on the values of their shared component, which helps with the de-duplication of data. To do this, Unity stores all entities of an archetype that have the same shared component values together. Each unique shared component value is stored once per [world](concepts-worlds.md), so the data isn't repeated across entities.

You can create both [managed and unmanaged shared components](components-shared-create.md). Managed shared components have the same advantages and restrictions as [regular managed components](components-managed.md).

## When to use shared components

Shared components work best when many entities share the same value, and the total number of unique values is small. Typical examples include grouping entities by LOD level, game faction, or spawn wave in a game. In these cases, shared components avoid duplicating the same data on every entity and let you efficiently process each group, for example by using [`WithSharedComponentFilter`](xref:Unity.Entities.QueryEnumerable`1.WithSharedComponentFilter*) to iterate only the entities that share a particular value.

Shared components are less suitable when:

* **Values change frequently.** Changing a shared component value is a [structural change](concepts-structural-changes.md) that moves the entity to a different chunk. If values change often, the cost of repeated structural changes can outweigh the benefits. For alternative techniques, refer to [Optimize shared components](components-shared-optimize.md).
* **Many entities have unique values.** All entities in a chunk must share the same shared component values. A large number of unique values fragments entities across many sparsely occupied chunks, which negates the benefits of the chunk layout.

## Shared component value changes

When you change a shared component value for an entity, Unity checks whether an equal value already exists in the shared component value array. If it does, Unity moves the entity to a chunk that stores the index of the existing value. Otherwise, Unity adds the new value to the array and moves the entity to a new chunk that stores the index of this new value. Both cases are [structural changes](concepts-structural-changes.md). To change how Unity compares shared component values when deciding which chunk to use, refer to [Override the default comparison behavior](#override-the-default-comparison-behavior).

Unity stores unmanaged and managed shared components separate from one another and makes unmanaged shared components available to [Burst compiled](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html) code via the unmanaged shared component APIs (such as [`SetUnmanagedSharedComponentData`](xref:Unity.Entities.EntityManager.SetUnmanagedSharedComponentData*)). For more information, see [Optimize shared components](components-shared-optimize.md).

## Override the default comparison behavior
To change how ECS compares instances of a shared component, implement [`IEquatable<YourSharedComponent>`](https://docs.microsoft.com/en-us/dotnet/api/system.iequatable-1.equals) for the shared component. If you do this, ECS uses your implementation to check if instances of the shared component are equal. If the shared component is unmanaged, you can add the [`[BurstCompile]`](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html?subfolder=/api/Unity.Burst.BurstCompileAttribute.html) attribute to the shared component struct, the `Equals` method, and the `GetHashCode` method to improve performance.

## Share shared components between worlds

For managed objects that are resource intensive to create and keep, such as a blob asset, you can use shared components to only store one copy of that object across all [worlds](concepts-worlds.md). To do this, implement the [IRefCounted](xref:Unity.Entities.IRefCounted) interface with  [`Retain`](xref:Unity.Entities.IRefCounted.Retain) and [`Release`](xref:Unity.Entities.IRefCounted.Release). Implement `Retain` and `Release` so that these methods properly manage the lifetime of the underlying resource. If the shared component is unmanaged, you can add the [`[BurstCompile]`](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html?subfolder=/api/Unity.Burst.BurstCompileAttribute.html) attribute to the shared component struct, the `Retain` method, and the `Release` method to improve performance.

## Don't modify objects referenced by a shared component

To work correctly, shared components rely on you using the Entities API to change their values. This includes referenced objects. If a shared component contains a reference type or pointer, be careful not to modify the referenced object without using the Entities API.

## Additional resources

* [Optimize shared components](components-shared-optimize.md)