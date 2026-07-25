---
uid: accessing-looking-up-data
---

# Look up arbitrary data

The most efficient way to access and change data is to use a [system](concepts-systems.md) together with an [entity query](systems-entityquery.md) that runs in a job. This maximizes parallelism and minimizes cache misses. Use that fast path for the bulk of transformations. However, there are times when you might need to access a component on an arbitrary entity at an arbitrary point in your system update.

You can look up data in an entity's [`IComponentData`](xref:Unity.Entities.IComponentData) and its [dynamic buffers](components-buffer-introducing.md).

## Look up entity data in a system

Inside a `SystemBase` you can iterate entities on the main thread with [`SystemAPI.Query`](systems-systemapi-query.md) and then perform targeted lookups on arbitrary entities using:

* `SystemAPI.HasComponent<T>(Entity)`
* `SystemAPI.GetComponent<T>(Entity)`
* `GetComponentLookup<T>(bool isReadOnly)`
* `GetBufferLookup<T>(bool isReadOnly)`

For example, the following code uses `GetComponent<T>(Entity)` to get a `Target` component, which has an entity field that identifies the entity to target. It then rotates the tracking entities towards their target:

[!code-cs[lookup-foreach](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-foreach)]

If you want to access data stored in a dynamic buffer on an arbitrary entity (not necessarily part of the current iteration), create a local `BufferLookup<T>` (via `GetBufferLookup<T>(true)` for read-only lookup). Capture it in the `foreach` loop and use it to test for the buffer and fetch it:

[!code-cs[lookup-foreach-buffer](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-foreach-buffer)]

## Look up entity data in a job

To access component data at random in a job struct such as [`IJobChunk`](xref:Unity.Entities.IJobChunk), use one of the following types:  

* [`ComponentLookup`](xref:Unity.Entities.ComponentLookup`1)
* [`BufferLookup`](xref:Unity.Entities.BufferLookup`1 )

These types get an array-like interface to component, indexed by [`Entity`](xref:Unity.Entities.Entity) object. You can also use `ComponentLookup` to determine whether an entity's [enableable components](components-enableable-intro.md) are enabled or disabled, or to toggle the state of these components.

To use them, declare a field of type `ComponentLookup` or `BufferLookup`, set the value of the field, and then schedule the job.

For example, you can use the `ComponentLookup` field to look up the world position of entities:

[!code-cs[lookup-ijobchunk-declare](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk-declare)]

>[!NOTE]
>This declaration uses the [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnlyAttribute.html) attribute. Always declare `ComponentLookup` objects as read-only unless you want to write to the components you access.
    
The following example illustrates how to set the data fields and schedule the job:

[!code-cs[lookup-ijobchunk-set](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk-set)]

To look up the value of a component, use an entity object inside the job's `Execute` method:

[!code-cs[lookup-ijobchunk-read](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk-read)]
  
The following, full example shows a system that moves entities that have a `Target` field that contains the entity object of their target towards the current location of the target:
 
[!code-cs[lookup-ijobchunk](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk)]

## Data access errors

If the data you look up overlaps the data you want to read and write to  in the job, then random access might lead to race conditions. 

You can mark an accessor object with the [`NativeDisableParallelForRestriction`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html) attribute, if you're sure that there's no overlap between the entity data you want to read or write to directly, and the specific entity data you want to read or write to at random.
