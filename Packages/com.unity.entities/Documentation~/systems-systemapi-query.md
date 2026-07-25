---
uid: entities-systems-systemapi-query
---

# Iterate over component data with SystemAPI.Query

To iterate through a collection of data on the main thread, you can use the [`SystemAPI.Query<T>`](xref:Unity.Entities.SystemAPI.Query*) method in both [`ISystem`](systems-isystem.md) and [`SystemBase`](systems-systembase.md) system types. It uses C#’s idiomatic `foreach` syntax.

You can overload the method with up to seven type parameters. The supported type parameters are:

* `IAspect`
* `IComponentData`
* `ISharedComponentData`
* `DynamicBuffer<T>`
* `RefRO<T>`
* `RefRW<T>`
* `EnabledRefRO<T>` where T : `IEnableableComponent`, `IComponentData`
* `EnabledRefRW<T>` where T : `IEnableableComponent`, `IComponentData`

## SystemAPI.Query implementation

Whenever you invoke `SystemAPI.Query<T>`, the source generator solution creates an `EntityQuery` field on the system itself. It also caches an `EntityQuery` that consists of the queried types, with their respective read-write/read-only access modes in this field. During compilation, the source-generation solution replaces the `SystemAPI.Query<T>` invocation in a `foreach` statement with an enumerator that iterates through the cached query’s data.

Additionally, the source-generation solution caches all the required type handles, and automatically injects `TypeHandle.Update(SystemBase system)` or `TypeHandle.Update(ref SystemState state)` as necessary before every `foreach`. This ensures that the type handles are safe to use.

The source generators also generate code to automatically complete all necessary read and read-write dependencies before each `foreach` statement.

## Query data

The following is an example that uses `SystemAPI.Query` to iterate through every entity that has both `LocalTransform` and `RotationSpeed` components: 

[!code-cs[](../DocCodeSamples.Tests/SystemAPIExamples.cs#query-data)]

Because the example modifies the `LocalTransform` data, it's wrapped inside `RefRW<T>`, as a read-write reference. However, because it only reads the `RotationSpeed` data, it uses `RefRO<T>`. `RefRO<T>` usage is entirely optional and you can use the following instead as valid code: 

[!code-cs[](../DocCodeSamples.Tests/SystemAPIExamples.cs#query-data-alt)]

[`RefRW<T>.ValueRW`](xref:Unity.Entities.RefRW`1.ValueRW*), [`RefRW<T>.ValueRO`](xref:Unity.Entities.RefRW`1.ValueRO*), and [`RefRO<T>.ValueRO`](xref:Unity.Entities.RefRO`1.ValueRO*) all return a reference to the component. When called, `ValueRW` conducts a safety check for read-write access, and `ValueRO` does the same for read-only access.

## Accessing entities in the foreach statement

`Unity.Entities.Entity` isn't a supported type parameter. Every query is already an implicit filter of all existing entities. To get access to the entity, use [`WithEntityAccess`](xref:Unity.Entities.QueryEnumerable`1.WithEntityAccess*). For example:

[!code-cs[](../DocCodeSamples.Tests/SystemAPIExamples.cs#entity-access)]

Note that the `Entity` argument comes last in the returned tuple.

## Filter the query

You can chain additional methods onto `SystemAPI.Query` to filter or refine which entities match. The following methods are available:

| **Method** | **Description** |
|---|---|
| `WithAll<T>` | Requires components that must be present and enabled. |
| `WithAny<T>` | Requires at least one of the specified components to be present and enabled. |
| `WithNone<T>` | Excludes entities that have the specified component present and enabled. The query still matches entities where the component is absent or disabled. |
| `WithDisabled<T>` | Requires [enableable components](components-enableable.md) that must be present and disabled. |
| `WithPresent<T>` | Requires components that must be present, whether or not they're enabled. Use this for [enableable components](components-enableable.md) when you want to match entities regardless of each component's enabled state. |
| `WithAbsent<T>` | Excludes entities that have the specified component, regardless of whether it's enabled or disabled. |
| `WithChangeFilter<T>` | Only matches entities in chunks where the specified component has potentially changed since the last time the system ran. |
| `WithOptions` | Specifies additional [`EntityQueryOptions`](xref:Unity.Entities.EntityQueryOptions) for the query. |
| `WithSharedComponentFilter<T>` | Filters entities by a specific unmanaged [shared component](components-shared.md) value. |
| `WithSharedComponentFilterManaged<T>` | Filters entities by a specific managed [shared component](components-shared.md) value. |
| `WithEntityAccess` | Includes the `Entity` reference in the returned tuple. |

Most filter methods accept up to three type parameters. `WithEntityAccess` and `WithOptions` don't take type parameters. To add more component types, chain multiple calls together. For example:

```csharp
foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>()
    .WithAll<RotationSpeed>()
    .WithPresent<MyEnableableTag>()
    .WithNone<Disabled>())
```

## Known limitations

`SystemAPI.Query` has the following limitations, which are outlined below.

### Dynamic buffer read-only limitation

`DynamicBuffer<T>` type parameters in `SystemAPI.Query<T>` are read-write access by default. However, if you want read-only access, you have to create your own implementation, similar to the following:

[!code-cs[](../DocCodeSamples.Tests/SystemAPIExamples.cs#dynamic-buffer)]

### Reusing SystemAPI.Query

You can't store `SystemAPI.Query<T>` in a variable and then use it in multiple `foreach` statements: there isn't a way to reuse `SystemAPI.Query`. This is because the implementation of the API relies on knowing what the query types are at compile time. The source-generation solution doesn't know at compile-time what `EntityQuery` to generate and cache, which type handles to call `Update` on, nor which dependencies to complete.
