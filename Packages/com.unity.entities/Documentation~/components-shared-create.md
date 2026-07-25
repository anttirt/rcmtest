# Create a shared component

You can create unmanaged shared components.

Before you create a shared component, make sure you understand how they work and their performance implications. Refer to [Introducing shared components](components-shared-introducing.md) for an overview and when to use them, and [Optimize shared components](components-shared-optimize.md) for performance considerations.

## Create an unmanaged shared component

To create an unmanaged shared component, create a struct that implements the marker interface `ISharedComponentData`.

The following code sample shows an unmanaged shared component:

[!code-cs[Create an unmanaged shared component](../DocCodeSamples.Tests/CreateComponentExamples.cs#shared-unmanaged)]

To override the way that a shared component is checked for equality, you can implement the `IEquatable<>` interface, and ensure `public override int GetHashCode()` is implemented. Entities then internally uses these methods to compare shared components for equality, and therefore partitions entities differently that way. You can also put `[BurstCompile]` on these methods, and they will be compiled with Burst if they comply with Burst's restrictions.

## Managed shared components (deprecated)

> [!NOTE]
> Managed shared components are deprecated. A shared component is managed if it's a `class`, or a `struct` that contains managed (reference-type) fields such as `string`. Make your shared component a fully unmanaged `struct` instead, and use [`UnityObjectRef<T>`](reference-unity-objects.md) to reference `UnityEngine.Object` instances.
