# Scene instancing

To create multiple instances of the same scene in a [world](concepts-worlds.md), use [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) with the flag [`SceneLoadFlags.NewInstance`](xref:Unity.Entities.SceneLoadFlags). This is useful if for example you have a different tiles (each tile represented by a scene) and you want to populate the world with those tiles.

When you create a scene in this way, the scene meta entity returned from the load call will refer to the newly created instance.

The instances from a scene are exact copies of each other, because the streaming system loads the exact same data multiple times from the entity scene file. To make sure that each instance isn't exactly the same, you can apply a unique transform on each instance by combining the [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup) system group with the [`RequestSceneLoaded.ImportEntity`](xref:Unity.Entities.RequestSceneLoaded) field. You can apply any other kind of changes to the entities in the scene, not just a transform.

>[!NOTE]
> Any [custom section metadata](streaming-meta-entities.md#custom-section-metadata) is exactly the same on each instance because the meta data is stored in the entity scene file.

## ProcessAfterLoadGroup system group

The loading of a section doesn't happen in the main world, but on a separate world called the **streaming world**. Each [section](streaming-scene-sections.md) loads into its own streaming world. When the load is complete, the content of the streaming world is moved into the main world.

The system group [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup) runs in the streaming world when all the content is loaded, but before the final move into the main world is performed. You can add custom systems into that group to apply transformations to scene instances.

For example, you could create a system to offset all the entities in the instance to a certain position of the world. In this case you need to pass to the system the offset that you want to apply to the instance. This offset can't be stored inside the entity scene file because it needs to be different for each instance. You can use [`RequestSceneLoaded.ImportEntity`](xref:Unity.Entities.RequestSceneLoaded) to deliver this data into the streaming world.

## RequestSceneLoaded.ImportEntity

[`RequestSceneLoaded`](xref:Unity.Entities.RequestSceneLoaded) exposes an `ImportEntity` field referencing a regular entity in the main world that holds the per-instance data you want to deliver into the streaming world. During section loading, the streaming system copies that referenced entity (and all of its components) into the section's streaming world just before [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup) runs. Your `[WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]` systems can then query for the imported components as if they were native to the streaming world.

The most convenient way to set `ImportEntity` is to pass it inline when you start the load, through [`SceneSystem.LoadParameters.ImportEntity`](xref:Unity.Scenes.SceneSystem.LoadParameters). The streaming system then writes the same value into `RequestSceneLoaded` on the scene meta entity (and propagates it to each section meta entity at auto-load time). You can also write `RequestSceneLoaded` directly on a section meta entity if you want a per-section value that overrides the scene-level one.

The imported entity follows the same lifetime rules as any other entity in the streaming world. If your `ProcessAfterLoad` system consumes it as a one-shot data carrier and you don't want it to leak into the main world, destroy it inside that system after reading. Otherwise, it receives a [`SceneTag`](xref:Unity.Entities.SceneTag) and is moved into the main world together with the rest of the section's entities, where it persists until you destroy it (or until the section is unloaded).

You own the source data entity in the main world. Destroy it yourself when it is no longer needed, but keep it alive until the scene load completes. The streaming system only reads from it. If `ImportEntity` is [`Entity.Null`](xref:Unity.Entities.Entity), no import is performed. A non-null reference to an entity that does not exist (because it was never created, or because it was destroyed before the scene finished loading) is treated as a programming error: the import is skipped and an error is logged.

> [!NOTE]
> The previous API, the `PostLoadCommandBuffer` managed [`IComponentData`](xref:Unity.Entities.IComponentData), is deprecated. Prefer [`RequestSceneLoaded.ImportEntity`](xref:Unity.Entities.RequestSceneLoaded) in new code. To migrate an existing call site: build the entity in the main world directly with the components that the old [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) was creating, then pass it as `ImportEntity` in the [`LoadParameters`](xref:Unity.Scenes.SceneSystem.LoadParameters) you give to [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*).

### Scene instancing overview

As a summary, these are the steps to instantiate scenes and apply unique transformations to them:

1. Build the per-instance data:
   1. Create a regular entity in the main world.
   1. Add components to it carrying the unique instance information.
1. Use [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) with the flag [`SceneLoadFlags.NewInstance`](xref:Unity.Entities.SceneLoadFlags) to load a scene, passing your data entity as `ImportEntity` in the [`LoadParameters`](xref:Unity.Scenes.SceneSystem.LoadParameters).
1. Write a system to apply the unique transformation to the instanced scene:
   1. Create the system and assign it to the [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup).
   1. Query the instance information from the imported entity (its components appear in the streaming world).
   1. Use that information to apply the transforms to the entities in the instance.

For example, to instantiate a scene at a certain position in the world you can do the following:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_instancing1_2)]

The code above uses a component called `PostLoadOffset` to store the offset to apply to the instance.

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_instance_data)]

Finally, use this system to apply the transformation:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_instancing3)]

## Addional resources

* [Custom section metadata](streaming-meta-entities.md#custom-section-metadata)
* [Scene sections](streaming-scene-sections.md)
