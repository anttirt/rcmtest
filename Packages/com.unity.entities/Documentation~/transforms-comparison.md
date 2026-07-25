# Transforms comparison

Many of the transform operations available in the [`UnityEngine.Transform`](xref:UnityEngine.Transform) class have equivalents in the Entities API, with differences in syntax and, in some cases, in approach.

The main-thread code examples assume they run inside the `OnUpdate(ref SystemState state)` method of [`ISystem`](systems-isystem.md). Some code blocks include the `state` parameter without using it to match the `OnUpdate` method signature. For more information, refer to [Using transforms](transforms-using.md).

> [!NOTE]
> The main-thread examples show individual operations on arbitrary entities. In performance-sensitive code, avoid calling several random-access APIs, such as [`SystemAPI.GetComponent<T>`](xref:Unity.Entities.SystemAPI.GetComponent*), as separate helper methods. When you process many entities, prefer a query or job that iterates transform components directly, such as an `IJobEntity` with an `Execute(in LocalToWorld localToWorld)` parameter, so Unity can access the data by chunk. If you need several values from one arbitrary entity, get the component once and reuse the local value. For example, fetch [`LocalToWorld`](xref:Unity.Transforms.LocalToWorld) once, then derive the entity's forward, right, and up vectors from that value.

Many sections include a multithreaded implementation. Use these examples when you need to run transform logic across many entities, or when you need to access component data from a job.

For a multithreaded implementation, choose the access pattern that matches the data the job needs:

* To read or write a component on the entity that the job processes, use a parameter of the `Execute` method. Declare the parameter with the `in` keyword for read-only access (for example, `in LocalToWorld`), or with the `ref` keyword for write access (for example, `ref LocalTransform`).
* To read a component on a different entity, or to read an optional component on the entity that the job processes, declare a read-only [`ComponentLookup<T>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct.
* To read from an optional dynamic buffer such as the [`Child`](xref:Unity.Transforms.Child) buffer, declare a read-only [`BufferLookup<T>`](xref:Unity.Entities.BufferLookup`1) field on the job struct.
* To add, remove, or change a component from a job, queue the change on an [`EntityCommandBuffer.ParallelWriter`](xref:Unity.Entities.EntityCommandBuffer.ParallelWriter) instance.

The world-space code examples read from the [`LocalToWorld`](xref:Unity.Transforms.LocalToWorld) component. Its matrix can be stale or include graphical smoothing offsets while [`SimulationSystemGroup`](xref:Unity.Entities.SimulationSystemGroup) is running. For details and for the [`TransformHelpers.ComputeWorldTransformMatrix`](xref:Unity.Transforms.TransformHelpers.ComputeWorldTransformMatrix*) alternative, refer to [the `LocalToWorld` component](transforms-concepts.md#the-localtoworld-component) section.

The examples assume entities have uniform scale. The `Scale` field on [`LocalTransform`](xref:Unity.Transforms.LocalTransform) is a single scalar. The [`PostTransformMatrix`](xref:Unity.Transforms.PostTransformMatrix) component stores non-uniform scale, which the examples below ignore. For the non-uniform case, refer to [the `PostTransformMatrix` component](transforms-concepts.md#the-posttransformmatrix-component) section.

Many of the methods on the [`TransformHelpers`](xref:Unity.Transforms.TransformHelpers) class are declared as C# extension methods on the [`float4x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float4x4.html) struct. The examples on this page call them as instance methods on the matrix value. For example, `localToWorld.Value.Translation()` is equivalent to [`TransformHelpers.Translation(localToWorld.Value)`](xref:Unity.Transforms.TransformHelpers.Translation*).

The page also mentions UnityEngine transform [properties](#properties-with-no-equivalent) and [methods](#methods-with-no-equivalent) that have no equivalent in Entities API.

This page contains the following sections:

* [UnityEngine transform property equivalents](#property-equivalents)
* [UnityEngine transform method equivalents](#method-equivalents)

## <a id="property-equivalents"></a>UnityEngine transform property equivalents

The following sections show how to get or set common [`UnityEngine.Transform`](xref:UnityEngine.Transform) properties with Entities components and transform helper methods. Most property getters read either [`LocalTransform`](xref:Unity.Transforms.LocalTransform) for local-space data or [`LocalToWorld`](xref:Unity.Transforms.LocalToWorld) for world-space data.

This section covers the following properties:

* [`childCount`](#childcount)
* [`forward`](#forward)
* [`localPosition`](#localposition)
* [`localRotation`](#localrotation)
* [`localScale`](#localscale)
* [`localToWorldMatrix`](#localtoworldmatrix)
* [`lossyScale`](#lossyscale)
* [`parent`](#parent)
* [`position`](#position)
* [`right`](#right)
* [`root`](#root)
* [`rotation`](#rotation)
* [`up`](#up)
* [`worldToLocalMatrix`](#worldtolocalmatrix)

### <a id="childcount"></a>UnityEngine property: childCount

With the Entities API, to achieve an outcome similar to the [`Transform.childCount`](xref:UnityEngine.Transform.childCount) property, use [`SystemAPI.HasBuffer`](xref:Unity.Entities.SystemAPI.HasBuffer*) and [`SystemAPI.GetBuffer`](xref:Unity.Entities.SystemAPI.GetBuffer*). Entities without children don't have a [`Child`](xref:Unity.Transforms.Child) buffer, so check for it first to match `Transform.childCount` returning `0` for childless transforms:

**Main-thread implementation:**

[!code-cs[childCount](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#childCount)]

**<a id="childcount-job"></a>Multithreaded implementation:**

`SystemAPI.GetBuffer` and `SystemAPI.HasBuffer` can only run on the main thread, so you can't use them inside jobs. To access the child count from a job, use a read-only [`BufferLookup<Child>`](xref:Unity.Entities.BufferLookup`1):

[!code-cs[childCount-job](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#childCount-job)]

### <a id="forward"></a>UnityEngine property: forward

With the Entities API, to achieve an outcome similar to the [`Transform.forward`](xref:UnityEngine.Transform.forward) property, use the Mathematics package [`normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.normalize.html) function with the [`LocalToWorld.Forward`](xref:Unity.Transforms.LocalToWorld.Forward) vector. You can omit the call to the `normalize` method if every entity in the transform hierarchy has `Scale` values set to `1`:

**Main-thread implementation:**

[!code-cs[forward](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#forward)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="localposition"></a>UnityEngine property: localPosition

With the Entities API, to achieve an outcome similar to the [`Transform.localPosition`](xref:UnityEngine.Transform.localPosition) property, use [`LocalTransform.Position`](xref:Unity.Transforms.LocalTransform.Position):

**Main-thread implementation:**

[!code-cs[localPosition](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localPosition)]

**<a id="localposition-job"></a>Multithreaded implementation:**

`SystemAPI.GetComponent` can only run on the main thread, so you can't use it inside jobs. To access `LocalTransform.Position` from a job, get the `LocalTransform` component as an `Execute` method parameter:

[!code-cs[localPosition-job](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localPosition-job)]

**Setting the localPosition property:**

To set the local position, get the current [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component, then assign it a new value returned by the [`WithPosition`](xref:Unity.Transforms.LocalTransform.WithPosition*) method. The `WithPosition` method returns a copy that preserves the original `Scale` and `Rotation` fields, matching how the `Transform.localPosition` property preserves local rotation and local scale:

[!code-cs[localPosition-set](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localPosition-set)]

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job).

### <a id="localrotation"></a>UnityEngine property: localRotation

With the Entities API, to achieve an outcome similar to the [`Transform.localRotation`](xref:UnityEngine.Transform.localRotation) property, use [`LocalTransform.Rotation`](xref:Unity.Transforms.LocalTransform.Rotation):

**Main-thread implementation:**

[!code-cs[localRotation](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localRotation)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalTransform`](#localposition-job).

**Setting the localRotation property:**

To set the local rotation, get the current [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component, then assign it a new value returned by the [`WithRotation`](xref:Unity.Transforms.LocalTransform.WithRotation*) method. The `WithRotation` method returns a copy that preserves the original `Position` and `Scale` fields, matching how the `Transform.localRotation` property preserves local position and local scale:

[!code-cs[localRotation-set](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localRotation-set)]

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job).

### <a id="localscale"></a>UnityEngine property: localScale

With the Entities API, to achieve an outcome similar to the [`Transform.localScale`](xref:UnityEngine.Transform.localScale) property, use [`LocalTransform.Scale`](xref:Unity.Transforms.LocalTransform.Scale). For the non-uniform case, refer to [the `PostTransformMatrix` component](transforms-concepts.md#the-posttransformmatrix-component) section.

**Main-thread implementation:**

[!code-cs[localScale](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localScale)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalTransform`](#localposition-job).

**Setting the localScale property:**

To set the local scale, get the current [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component, then assign it a new value returned by the [`WithScale`](xref:Unity.Transforms.LocalTransform.WithScale*) method. Because these examples assume uniform scale, the `WithScale` method takes a single scalar and returns a copy that preserves the original `Position` and `Rotation` fields. For the non-uniform case, refer to [the `PostTransformMatrix` component](transforms-concepts.md#the-posttransformmatrix-component) section.

[!code-cs[localScale-set](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localScale-set)]

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job).

### <a id="localtoworldmatrix"></a>UnityEngine property: localToWorldMatrix

With the Entities API, to achieve an outcome similar to the [`Transform.localToWorldMatrix`](xref:UnityEngine.Transform.localToWorldMatrix) property, use the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix:

**Main-thread implementation:**

[!code-cs[localToWorldMatrix](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#localToWorldMatrix)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="lossyscale"></a>UnityEngine property: lossyScale

With the Entities API, to achieve an outcome similar to the [`Transform.lossyScale`](xref:UnityEngine.Transform.lossyScale) property, get the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`Scale`](xref:Unity.Transforms.TransformHelpers.Scale*) method on it to extract the per-axis scale values. Because the examples assume uniform scale, all three values are equal:

**Main-thread implementation:**

[!code-cs[lossyScale](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#lossyScale)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="parent"></a>UnityEngine property: parent

With the Entities API, to achieve an outcome similar to the [`Transform.parent`](xref:UnityEngine.Transform.parent) property, use the [`SystemAPI.TryGetComponent`](xref:Unity.Entities.SystemAPI.TryGetComponent*) method with the [`Parent`](xref:Unity.Transforms.Parent) component, and return [`Parent.Value`](xref:Unity.Transforms.Parent.Value) when the component is present. Root entities don't have a `Parent` component, so return [`Entity.Null`](xref:Unity.Entities.Entity.Null) in that case to match `Transform.parent` returning `null`:

**Main-thread implementation:**

[!code-cs[parent](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#parent)]

**<a id="parent-job"></a>Multithreaded implementation:**

Inside a parallel job, use a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1):

[!code-cs[parent-job](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#parent-job)]

### <a id="position"></a>UnityEngine property: position

With the Entities API, to achieve an outcome similar to the [`Transform.position`](xref:UnityEngine.Transform.position) property, use [`LocalToWorld.Position`](xref:Unity.Transforms.LocalToWorld.Position):

**Main-thread implementation:**

[!code-cs[position](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#position)]

**<a id="position-job"></a>Multithreaded implementation:**

`SystemAPI.GetComponent` can only run on the main thread, so you can't use it inside jobs. To access `LocalToWorld.Position` (or any other field of the `LocalToWorld` component) from a job, get the `LocalToWorld` component as an `Execute` method parameter:

[!code-cs[position-job](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#position-job)]

**Setting the position property:**

To set the world-space position, write to the entity's [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component. If the entity has a parent, convert the world-space position into the local space of the parent entity first using the [`InverseTransformPoint`](xref:Unity.Transforms.TransformHelpers.InverseTransformPoint*) method on the parent entity's `LocalToWorld` matrix:

[!code-cs[position-set](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#position-set)]

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job). For the parent branch, also declare a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) field and a read-only [`ComponentLookup<LocalToWorld>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="right"></a>UnityEngine property: right

With the Entities API, to achieve an outcome similar to the [`Transform.right`](xref:UnityEngine.Transform.right) property, use the Mathematics package [`normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.normalize.html) function with the [`LocalToWorld.Right`](xref:Unity.Transforms.LocalToWorld.Right) vector. You can omit the call to the `normalize` method if every entity in the transform hierarchy has `Scale` values set to `1`:

**Main-thread implementation:**

[!code-cs[right](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#right)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="root"></a>UnityEngine property: root

With the Entities API, to achieve an outcome similar to the [`Transform.root`](xref:UnityEngine.Transform.root) property, use [`Parent.Value`](xref:Unity.Transforms.Parent.Value) in a loop that follows the chain of parent references until it reaches an entity with no [`Parent`](xref:Unity.Transforms.Parent) component (a root entity). A root entity has no `Parent` to begin with, so the loop returns the starting entity unchanged, matching `Transform.root` returning the transform itself when called on a root:

**Main-thread implementation:**

[!code-cs[root](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#root)]


**Multithreaded implementation:**

For a multithreaded version, use a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) in a job, as shown in the example for looking up [`Parent`](#parent-job), looping with `TryGetComponent` until it returns `false`.

### <a id="rotation"></a>UnityEngine property: rotation

With the Entities API, to achieve an outcome similar to the [`Transform.rotation`](xref:UnityEngine.Transform.rotation) property, get the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`Rotation`](xref:Unity.Transforms.TransformHelpers.Rotation*) method on it:

**Main-thread implementation:**

[!code-cs[rotation](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#rotation)]


**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

**Setting the rotation property:**

To set the world-space rotation, write to the entity's [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component. If the entity has a parent, convert the world-space rotation into the local space of the parent entity first using the [`InverseTransformRotation`](xref:Unity.Transforms.TransformHelpers.InverseTransformRotation*) method on the parent entity's `LocalToWorld` matrix:

[!code-cs[rotation-set](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#rotation-set)]

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job). For the parent branch, also declare a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) field and a read-only [`ComponentLookup<LocalToWorld>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="up"></a>UnityEngine property: up

With the Entities API, to achieve an outcome similar to the [`Transform.up`](xref:UnityEngine.Transform.up) property, use the Mathematics package [`normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.normalize.html) function with the [`LocalToWorld.Up`](xref:Unity.Transforms.LocalToWorld.Up) vector. You can omit the call to the `normalize` method if every entity in the transform hierarchy has `Scale` values set to `1`:

**Main-thread implementation:**

[!code-cs[up](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#up)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="worldtolocalmatrix"></a>UnityEngine property: worldToLocalMatrix

With the Entities API, to achieve an outcome similar to the [`Transform.worldToLocalMatrix`](xref:UnityEngine.Transform.worldToLocalMatrix) property, use the Mathematics package [`inverse`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.inverse.html) function with the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix:

**Main-thread implementation:**

[!code-cs[worldToLocalMatrix](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#worldToLocalMatrix)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### Properties with no equivalent

The following properties have no equivalent in the Entities API:

* [`eulerAngles`](xref:UnityEngine.Transform.eulerAngles)
* [`localEulerAngles`](xref:UnityEngine.Transform.localEulerAngles)
* [`hasChanged`](xref:UnityEngine.Transform.hasChanged)
* [`hierarchyCapacity`](xref:UnityEngine.Transform.hierarchyCapacity). Not needed, since there is no limit to the number of children an entity can have.
* [`hierarchyCount`](xref:UnityEngine.Transform.hierarchyCount)

## <a id="method-equivalents"></a>UnityEngine transform method equivalents

The following sections show how to reproduce common [`UnityEngine.Transform`](xref:UnityEngine.Transform) method behavior with Entities APIs. Most methods either read transform components, update the [`LocalTransform`](xref:Unity.Transforms.LocalTransform) struct, or queue structural changes for parent-child relationships.

This section covers the following methods:

* [`GetChild`](#getchild)
* [`GetLocalPositionAndRotation`](#getlocalpositionandrotation)
* [`GetPositionAndRotation`](#getpositionandrotation)
* [`InverseTransformDirection`](#inversetransformdirection)
* [`InverseTransformPoint`](#inversetransformpoint)
* [`InverseTransformVector`](#inversetransformvector)
* [`IsChildOf`](#ischildof)
* [`LookAt`](#lookat)
* [`Rotate`](#rotate)
* [`RotateAround`](#rotatearound)
* [`SetLocalPositionAndRotation`](#setlocalpositionandrotation)
* [`SetPositionAndRotation`](#setpositionandrotation)
* [`TransformDirection`](#transformdirection)
* [`TransformPoint`](#transformpoint)
* [`TransformVector`](#transformvector)
* [`Translate`](#translate)

### <a id="getchild"></a>UnityEngine method: GetChild

With the Entities API, to achieve an outcome similar to the [`Transform.GetChild`](xref:UnityEngine.Transform.GetChild(System.Int32)) method, use [`SystemAPI.HasBuffer`](xref:Unity.Entities.SystemAPI.HasBuffer*) and [`SystemAPI.GetBuffer`](xref:Unity.Entities.SystemAPI.GetBuffer*):

**Main-thread implementation:**

[!code-cs[GetChild](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#GetChild)]

**Multithreaded implementation:**

For a multithreaded version, use a read-only [`BufferLookup<Child>`](xref:Unity.Entities.BufferLookup`1) in a job, as shown in the example for looking up [`Child`](#childcount-job).

### <a id="getlocalpositionandrotation"></a>UnityEngine method: GetLocalPositionAndRotation

With the Entities API, to achieve an outcome similar to the [`Transform.GetLocalPositionAndRotation`](https://docs.unity3d.com/ScriptReference/Transform.GetLocalPositionAndRotation.html) method, use [`LocalTransform.Position`](xref:Unity.Transforms.LocalTransform.Position) and [`LocalTransform.Rotation`](xref:Unity.Transforms.LocalTransform.Rotation):

**Main-thread implementation:**

[!code-cs[GetLocalPositionAndRotation](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#GetLocalPositionAndRotation)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalTransform`](#localposition-job).

### <a id="getpositionandrotation"></a>UnityEngine method: GetPositionAndRotation

With the Entities API, to achieve an outcome similar to the [`Transform.GetPositionAndRotation`](https://docs.unity3d.com/ScriptReference/Transform.GetPositionAndRotation.html) method, get the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`Translation`](xref:Unity.Transforms.TransformHelpers.Translation*) and [`Rotation`](xref:Unity.Transforms.TransformHelpers.Rotation*) methods on it:

**Main-thread implementation:**

[!code-cs[GetPositionAndRotation](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#GetPositionAndRotation)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="inversetransformdirection"></a>UnityEngine method: InverseTransformDirection

With the Entities API, to achieve an outcome similar to the [`Transform.InverseTransformDirection`](xref:UnityEngine.Transform.InverseTransformDirection(UnityEngine.Vector3)) method, get the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`Rotation`](xref:Unity.Transforms.TransformHelpers.Rotation*) method on it to extract the entity's world-space rotation, then apply the inverse of that rotation to the `direction` vector with the Mathematics package [`mul`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.mul.html) and [`inverse`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.inverse.html) functions. `Transform.InverseTransformDirection` ignores scale and preserves the length of the `direction` argument. Applying only the inverse rotation produces the same result regardless of the entity's scale, so no normalization or length compensation is needed:

**Main-thread implementation:**

[!code-cs[InverseTransformDirection](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#InverseTransformDirection)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="inversetransformpoint"></a>UnityEngine method: InverseTransformPoint

With the Entities API, to achieve an outcome similar to the [`Transform.InverseTransformPoint`](xref:UnityEngine.Transform.InverseTransformPoint(UnityEngine.Vector3)) method, get the entity's [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`InverseTransformPoint`](xref:Unity.Transforms.TransformHelpers.InverseTransformPoint*) method on it to apply the inverse of that matrix to the `position` vector:

**Main-thread implementation:**

[!code-cs[InverseTransformPoint](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#InverseTransformPoint)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="inversetransformvector"></a>UnityEngine method: InverseTransformVector

With the Entities API, to achieve an outcome similar to the [`Transform.InverseTransformVector`](xref:UnityEngine.Transform.InverseTransformVector(UnityEngine.Vector3)) method, get the entity's [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`InverseTransformDirection`](xref:Unity.Transforms.TransformHelpers.InverseTransformDirection*) method on it to apply the inverse of that matrix to the `vector` argument. Despite sharing a name with `Transform.InverseTransformDirection`, this method applies scale, so its behavior matches `Transform.InverseTransformVector` instead:

**Main-thread implementation:**

[!code-cs[InverseTransformVector](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#InverseTransformVector)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="ischildof"></a>UnityEngine method: IsChildOf

With the Entities API, to achieve an outcome similar to the [`Transform.IsChildOf`](xref:UnityEngine.Transform.IsChildOf(UnityEngine.Transform)) method, use [`Parent.Value`](xref:Unity.Transforms.Parent.Value) in a loop that follows the chain of parent references and compares each entity to the candidate, stopping at the first entity with no [`Parent`](xref:Unity.Transforms.Parent) component. The loop starts at the entity, so it returns `true` when the candidate is the entity itself, its parent entity, or any ancestor, matching the behavior of `Transform.IsChildOf`:

**Main-thread implementation:**

[!code-cs[IsChildOf](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#IsChildOf)]

**Multithreaded implementation:**

For a multithreaded version, use a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) in a job, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="lookat"></a>UnityEngine method: LookAt

With the Entities API, to achieve a behavior similar to the [`Transform.LookAt`](xref:UnityEngine.Transform.LookAt(UnityEngine.Transform)) method, use the [`LookAtRotation`](xref:Unity.Transforms.TransformHelpers.LookAtRotation*) method from [`TransformHelpers`](xref:Unity.Transforms.TransformHelpers) to compute the rotation in world space. If the entity has a parent, convert the world-space rotation into the local space of the parent entity:

**Main-thread implementation:**

[!code-cs[LookAt](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#LookAt)]

**Multithreaded implementation:**

For a multithreaded version, get the entity's `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job). Also modify the entity's `LocalTransform` component as another `Execute` method parameter, as shown in the example for modifying [`LocalTransform`](#translate-job). To look up the parent entity and read its `LocalToWorld` component, declare a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) field and a read-only [`ComponentLookup<LocalToWorld>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="rotate"></a>UnityEngine method: Rotate

With the Entities API, to achieve a behavior similar to the [`Transform.Rotate`](xref:UnityEngine.Transform.Rotate(UnityEngine.Vector3)) method, the approach depends on the rotation space.

Rotations in the Entities transform system are always quaternions and angles are always in radians. Use [`quaternion.Euler`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.quaternion.Euler.html) from the Mathematics package to convert Euler angles into a quaternion, and [`math.radians`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.radians.html) to convert degrees into radians.

When `Transform.Rotate` is called with `Space.Self` as its `relativeTo` argument (the default), or when the entity has no parent, use [`LocalTransform.Rotate`](xref:Unity.Transforms.LocalTransform.Rotate*), which post-multiplies the rotation in local space:

**Main-thread implementation:**

[!code-cs[Rotate-self](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#Rotate-self)]

When `Transform.Rotate` is called with `Space.World` as its `relativeTo` argument, and the entity might have a parent, convert the rotation into the local space of the parent entity first, then pre-multiply:

[!code-cs[Rotate-world](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#Rotate-world)]

**Multithreaded implementation:**

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job). For the `Space.World` form, also declare a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) field and a read-only [`ComponentLookup<LocalToWorld>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="rotatearound"></a>UnityEngine method: RotateAround

With the Entities API, to achieve a behavior similar to the [`Transform.RotateAround`](xref:UnityEngine.Transform.RotateAround(UnityEngine.Vector3,System.Single)) method, convert the `angleDegrees` and `axis` arguments to the units the Mathematics package expects. If the entity has a parent, also convert the `point` and `axis` arguments into the local space of the parent entity.

The [`quaternion.AxisAngle`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.quaternion.AxisAngle.html) method expects a unit-length axis and an angle in radians. Use [`math.normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.normalize.html) to make the `axis` vector unit-length, and use [`math.radians`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.radians.html) to convert the `angleDegrees` argument to radians.

If the entity has a parent, also convert the `point` and `axis` vectors into the local space of the parent entity. Call the [`InverseTransformPoint`](xref:Unity.Transforms.TransformHelpers.InverseTransformPoint*) method on the parent entity's `LocalToWorld` matrix to apply the inverse of that matrix to the `point` vector. Call the [`InverseTransformDirection`](xref:Unity.Transforms.TransformHelpers.InverseTransformDirection*) method on the same matrix to apply the same inverse to the `axis` vector, then renormalize the transformed `axis` vector because uniform scale changes its length:

**Main-thread implementation:**

[!code-cs[RotateAround](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#RotateAround)]

**Multithreaded implementation:**

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job). For the parent branch, also declare a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) field and a read-only [`ComponentLookup<LocalToWorld>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="setlocalpositionandrotation"></a>UnityEngine method: SetLocalPositionAndRotation

With the Entities API, to achieve a behavior similar to the [`Transform.SetLocalPositionAndRotation`](xref:UnityEngine.Transform.SetLocalPositionAndRotation(UnityEngine.Vector3,UnityEngine.Quaternion)) method, get the entity's current [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component, then write back a new value built with the [`WithPosition`](xref:Unity.Transforms.LocalTransform.WithPosition*) and [`WithRotation`](xref:Unity.Transforms.LocalTransform.WithRotation*) instance methods. Both methods return a copy that preserves the original `Scale` field. This matches the behavior of `Transform.SetLocalPositionAndRotation`, which leaves local scale untouched. Don't use the [`LocalTransform.FromPositionRotation`](xref:Unity.Transforms.LocalTransform.FromPositionRotation*) factory method as a substitute, because it hard-codes `Scale` to `1` and would unintentionally reset the entity's scale:

**Main-thread implementation:**

[!code-cs[SetLocalPositionAndRotation](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#SetLocalPositionAndRotation)]

**Multithreaded implementation:**

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job and assign the result of `WithPosition(...).WithRotation(...)` to it, as shown in the example for modifying [`LocalTransform`](#translate-job).

### <a id="setpositionandrotation"></a>UnityEngine method: SetPositionAndRotation

With the Entities API, to achieve a behavior similar to the [`Transform.SetPositionAndRotation`](xref:UnityEngine.Transform.SetPositionAndRotation(UnityEngine.Vector3,UnityEngine.Quaternion)) method, the approach depends on whether the entity has a parent.

If the entity has no parent, its local space and world space coincide, so get the current [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component and write back a new value built with the [`WithPosition`](xref:Unity.Transforms.LocalTransform.WithPosition*) and [`WithRotation`](xref:Unity.Transforms.LocalTransform.WithRotation*) instance methods. Both methods preserve the original `Scale` field. This matches the behavior of `Transform.SetPositionAndRotation`, which leaves scale untouched. Don't use the [`LocalTransform.FromPositionRotation`](xref:Unity.Transforms.LocalTransform.FromPositionRotation*) factory method as a substitute, because it hard-codes `Scale` to `1` and would unintentionally reset the entity's scale:

**Main-thread implementation:**

[!code-cs[SetPositionAndRotation-no-parent](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#SetPositionAndRotation-no-parent)]

If the entity might have a parent, convert the world-space `position` and `rotation` arguments into the local space of the parent entity first:

[!code-cs[SetPositionAndRotation-parent](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#SetPositionAndRotation-parent)]

**Multithreaded implementation:**

For a multithreaded version, modify the `LocalTransform` component as an `Execute` method parameter in a job, as shown in the example for modifying [`LocalTransform`](#translate-job). For the parent branch, also declare a read-only [`ComponentLookup<Parent>`](xref:Unity.Entities.ComponentLookup`1) field and a read-only [`ComponentLookup<LocalToWorld>`](xref:Unity.Entities.ComponentLookup`1) field on the job struct, as shown in the example for looking up [`Parent`](#parent-job).

### <a id="transformdirection"></a>UnityEngine method: TransformDirection

With the Entities API, to achieve an outcome similar to the [`Transform.TransformDirection`](xref:UnityEngine.Transform.TransformDirection(UnityEngine.Vector3)) method, get the [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`Rotation`](xref:Unity.Transforms.TransformHelpers.Rotation*) method on it to extract the entity's world-space rotation, then apply that rotation to the `direction` vector with the Mathematics package [`mul`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.mul.html) function. `Transform.TransformDirection` ignores scale and preserves the length of the `direction` argument. Applying only the rotation produces the same result regardless of the entity's scale, so no normalization or length compensation is needed:

**Main-thread implementation:**

[!code-cs[TransformDirection](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#TransformDirection)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="transformpoint"></a>UnityEngine method: TransformPoint

With the Entities API, to achieve an outcome similar to the [`Transform.TransformPoint`](xref:UnityEngine.Transform.TransformPoint(UnityEngine.Vector3)) method, get the entity's [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`TransformPoint`](xref:Unity.Transforms.TransformHelpers.TransformPoint*) method on it to apply that matrix to the `position` vector:

**Main-thread implementation:**

[!code-cs[TransformPoint](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#TransformPoint)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="transformvector"></a>UnityEngine method: TransformVector

With the Entities API, to achieve an outcome similar to the [`Transform.TransformVector`](xref:UnityEngine.Transform.TransformVector(UnityEngine.Vector3)) method, get the entity's [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) matrix and call the [`TransformDirection`](xref:Unity.Transforms.TransformHelpers.TransformDirection*) method on it to apply that matrix to the `vector` argument. Despite sharing a name with `Transform.TransformDirection`, this method applies scale, so its behavior matches `Transform.TransformVector` instead:

**Main-thread implementation:**

[!code-cs[TransformVector](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#TransformVector)]

**Multithreaded implementation:**

For a multithreaded version, get the `LocalToWorld` component as an `Execute` method parameter in a job, as shown in the example for reading [`LocalToWorld`](#position-job).

### <a id="translate"></a>UnityEngine method: Translate

With the Entities API, to achieve a behavior similar to the [`Transform.Translate`](xref:UnityEngine.Transform.Translate(UnityEngine.Vector3)) method, the approach depends on the translation space.

When called with `Space.Self` as its `relativeTo` argument (the default), `Transform.Translate` moves the transform along its own rotated axes, not the world axes. To match this behavior, apply the entity's [`LocalTransform.Rotation`](xref:Unity.Transforms.LocalTransform.Rotation) to the `translation` argument before adding it to `LocalTransform.Position`. Applying only the entity's own `Rotation` produces the correct self-space translation whether or not the entity has a parent. If the entity has a parent, the parent entity's rotation appears both when converting the `translation` argument from local space to world space and when converting back to the local space of the parent entity, and those two contributions cancel out:

**Main-thread implementation:**

[!code-cs[Translate-self](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#Translate-self)]

When `Transform.Translate` is called with `Space.World` as its `relativeTo` argument, and the entity might have a parent, convert the `translation` argument into the local space of the parent entity first:

[!code-cs[Translate-world](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#Translate-world)]

**<a id="translate-job"></a>Multithreaded implementation:**

Inside a parallel job, modify the entity's `LocalTransform` component as an `Execute` method parameter, then apply `LocalTransform.Rotation` to the job's `Translation` field and add the result to `LocalTransform.Position`, the same way as in the main-thread version. The parallel scheduler guarantees that no two workers process the same entity at the same time, so the job doesn't need a [`ComponentLookup<LocalTransform>`](xref:Unity.Entities.ComponentLookup`1) when it only modifies the entity that it processes:

[!code-cs[Translate-job](../DocCodeSamples.Tests/TransformsComparisonExamples.cs#Translate-job)]

### Methods with no equivalent

The following methods have no equivalent in the Entities API:

* [`Find`](xref:UnityEngine.Transform.Find(System.String))
* [`GetSiblingIndex`](xref:UnityEngine.Transform.GetSiblingIndex). The order of child entities is arbitrary.
* [`SetAsFirstSibling`](xref:UnityEngine.Transform.SetAsFirstSibling). The order of child entities is arbitrary.
* [`SetAsLastSibling`](xref:UnityEngine.Transform.SetAsLastSibling). The order of child entities is arbitrary.
* [`SetSiblingIndex`](xref:UnityEngine.Transform.SetSiblingIndex(System.Int32)). The order of child entities is arbitrary.

## Additional resources

* [Using transforms](transforms-using.md)
