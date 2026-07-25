---
uid: systems-write-groups
---

# Write groups

Write groups provide a mechanism for one system to override another, even when you can't change the other system. 

A common ECS pattern is for a system to read one set of **input** components and write to another component as its **output**. However, you might want to override the output of a system, and use a different system based on a different set of inputs to update the output components. 

The write group of a target component type consists of all other component types that ECS applies the [`WriteGroup` attribute](xref:Unity.Entities.WriteGroupAttribute) to, with that target component type as the argument. As a system creator, you can use write groups so that your system's users can exclude entities that your system would otherwise select and process. This filtering mechanism lets system users update components for the excluded entities based on their own logic, while letting your system operate as usual on the rest.

## Using write groups

To use write groups, you must use the [write group filter option](xref:Unity.Entities.EntityQueryOptions) on the queries in your system. This excludes all entities from the query that have a component from a write group of any of the components that are writable in the query.

To override a system that uses write groups, mark your own component types as part of the write group of the output components of that system. The original system ignores any entities that have your components and you can update the data of those entities with your own systems. 

## Write groups example

In this example, you use an external package to color all characters in your game depending on their state of health. For this, there are two components in the package: `HealthComponent` and `ColorComponent`:

[!code-cs[HealthComponent and ColorComponent](../DocCodeSamples.Tests/WriteGroupsExample.cs#health-color-components)]

There are also two systems in the package:
 1. The `ComputeColorFromHealthSystem`, which reads from `HealthComponent` and writes to `ColorComponent`.
 1. The `RenderWithColorComponent`, which reads from `ColorComponent`.

To represent when a player uses a power-up and their character becomes invincible, you attach an `InvincibleTagComponent` to the character's entity. In this case, the character's color should change to a separate, different color, which the above example doesn't accommodate. 

You can create your own system to override the `ColorComponent` value, but ideally `ComputeColorFromHealthSystem` wouldn't compute the color for your entity to begin with. It should ignore any entity that has `InvincibleTagComponent`. This becomes more relevant when there are thousands of players on the screen. 

This system is from another package which isn't aware of the `InvincibleTagComponent`, so this is when a write group is useful. It allows a system to ignore entities in a query when you know that the values it computes would be overridden anyway. There are two things you need to support this:

1. Mark the `InvincibleTagComponent` as part of the write group of `ColorComponent`:

    [!code-cs[InvincibleTagComponent](../DocCodeSamples.Tests/WriteGroupsExample.cs#invincible-tag)]

    The write group of `ColorComponent` consists of all component types that have the `WriteGroup` attribute with `typeof(ColorComponent)` as the argument.

1. The `ComputeColorFromHealthSystem` must explicitly support write groups. To achieve this, the system needs to build a query with the `EntityQueryOptions.FilterWriteGroup` option:

    [!code-cs[ComputeColorFromHealthSystem](../DocCodeSamples.Tests/WriteGroupsExample.cs#compute-color-system)]

    The key part is the query built in `OnCreate` with `.WithOptions(EntityQueryOptions.FilterWriteGroup)`. This example uses an `IJobEntity` to process entities matching that query:

    [!code-cs[ComputeColorFromHealthJob](../DocCodeSamples.Tests/WriteGroupsExample.cs#compute-color-job)]

When this executes, the following happens:
   1. The system detects that you write to `ColorComponent` because the query uses `WithAllRW<ColorComponent>()`.
   1. It looks up the write group of `ColorComponent` and finds the `InvincibleTagComponent` in it.
   1. It excludes all entities that have an `InvincibleTagComponent`.

The benefit is that this allows the system to exclude entities based on a type that's unknown to the system and might live in a different package.

> [!NOTE]
> For more examples, see the `Unity.Transforms` code, which uses write groups for every component it updates, including `LocalTransform`.

## Create write groups

To create write groups, add the `WriteGroup` attribute to the declarations of each component type in the write group. The `WriteGroup` attribute takes one parameter, which is the type of component that the components in the group uses to update. A single component can be a member of more than one write group.

For example, if you have a system that writes to component `W` whenever there are components `A` or `B` on an entity, then you can define a write group for `W` as follows:

[!code-cs[Write group W with A and B](../DocCodeSamples.Tests/WriteGroupsExample.cs#write-group-abc)]

You don't add the target of the write group (component `W` in the example above) to its own write group.

## Enabling write group filtering

To enable write group filtering, build your query with the `FilterWriteGroup` option. Here's an example using `IJobEntity`:

[!code-cs[AddingJob](../DocCodeSamples.Tests/WriteGroupsExample.cs#adding-job)]

The system creates a query with `FilterWriteGroup` enabled and schedules the job:

[!code-cs[AddingSystem](../DocCodeSamples.Tests/WriteGroupsExample.cs#adding-system)]

Alternatively, you can use `SystemAPI.Query` with the `WithOptions` method:

[!code-cs[AddingSystemWithQuery](../DocCodeSamples.Tests/WriteGroupsExample.cs#adding-system-query)]

When you enable write group filtering in a query, the query adds all components in a write group of a writable component to the `None` list of the query unless you explicitly add them to the `All` or `Any` lists. As a result, the query only selects an entity if it explicitly requires every component on that entity from a particular write group. If an entity has one or more additional components from that write group, the query rejects it.

In the example code above, the query:
    * Excludes any entity that has component `A`, because `W` is writable and `A` is part of the write group of `W`.
    * Doesn't exclude any entity that has component `B`. Even though `B` is part of the write group of `W`, it's also explicitly specified in the query.

## Overriding another system that uses write groups

If a system uses write group filtering in its queries, you can use your own system to override that system and write to those components. To override the system, add your own components to the write groups of the components to which the other system writes. 

Because write group filtering excludes any components in the write group that the query doesn't explicitly require, the other system ignores any entities that have your components.

For example, if you want to set the orientation of your entities by specifying the angle and axis of rotation, you can create a component and a system to convert the angle and axis values into a quaternion and write that to the `LocalTransform` component. 

To prevent the `Unity.Transforms` systems from updating `LocalTransform`, no matter what other components besides yours are present, you can put your component in the write group of `LocalTransform`:

[!code-cs[RotationAngleAxis component](../DocCodeSamples.Tests/WriteGroupsExample.cs#rotation-angle-axis-component)]

You can then update any entities with the `RotationAngleAxis` component without contention:

[!code-cs[RotationAngleAxisJob](../DocCodeSamples.Tests/WriteGroupsExample.cs#rotation-angle-axis-job)]

Then create a system to schedule it:

[!code-cs[RotationAngleAxisSystem](../DocCodeSamples.Tests/WriteGroupsExample.cs#rotation-angle-axis-system)]

## Extending another system that uses write groups

If you want to extend another system rather than override it, or if you want to allow future systems to override or extend your system, then you can enable write group filtering on your own system. When you enable write group filtering, the query automatically excludes all entities that have any write group component that isn't explicitly included in the query. To process those entities, you must create queries that explicitly specify the write group components you want to handle.

The previous example defined a write group that contains components `A` and `B` and targets component `W`. If you add a new component, called `C`, to the write group, then the new system that knows about `C` can query for entities that contain `C` and it doesn't matter if those entities also have components `A` or `B`. 

[!code-cs[Component C in write group](../DocCodeSamples.Tests/WriteGroupsExample.cs#write-group-c)]

However, if the new system also enables write group filtering, that's no longer true. If you only require component `C`, then write group filtering excludes any entities with either `A` or `B`. Instead, you must explicitly query for each combination of components that make sense. 

> [!TIP]
> You can use the `WithAny` clause of the query when appropriate.

[!code-cs[Extended write group system](../DocCodeSamples.Tests/WriteGroupsExample.cs#extended-system)]

If there are any entities that contain combinations of components in the write group that aren't explicitly mentioned, then the system that writes to the target of the write group, and its filters, doesn't handle them. However, if there are any of these type of entities, it's most likely a logical error in the program, and they shouldn't exist.
