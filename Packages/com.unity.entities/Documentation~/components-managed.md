---
uid: components-managed
---

# Managed components

> [!NOTE]
> Managed components are deprecated. A component is managed if it's a `class`, or a `struct` that contains managed (reference-type) fields. Both forms are deprecated. Use [unmanaged components](components-unmanaged.md) instead, and reference `UnityEngine.Object` instances with [`UnityObjectRef<T>`](reference-unity-objects.md).

A managed component is an `IComponentData` type that the runtime treats as managed: either a `class`, or a `struct` that contains managed (reference-type) fields such as `string` or other classes. Unlike unmanaged components, you can't access managed components in [jobs](xref:JobSystem) or [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest) compiled code, they require garbage collection, and they're more resource-intensive to store and access. Use [unmanaged components](components-unmanaged.md) instead.

## Additional resources

* [Unmanaged components overview](components-unmanaged.md)
* [Reference Unity objects in your code](reference-unity-objects.md)
