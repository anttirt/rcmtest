# Entities Preferences reference

The **Preferences** window in the Unity Editor (**Unity** > **Settings**) contains some specific Entities settings, as follows:

## Hierarchy window

|**Property**|**Description**|
|---|---|
|**Update Mode**|Set how to update the [Entities Hierarchy](editor-hierarchy-window.md) window: <ul><li>**Synchronous:** Updates the hierarchy in a blocking manner. Data is always up to date, but might impact performance.</li><li>**Asynchronous:** Updates the hierarchy in a non-blocking manner, over multiple frames if needed. Data might be stale for a few frames, but the impact on performance is minimized.</li></ul>|
|**Minimum Milliseconds Between Hierarchy Update Cycle**|Set the minimum amount of time to wait between hierarchy update cycles in milliseconds. Increase this value to update the [Entities Hierarchy](editor-hierarchy-window.md) window less frequently, which has a lower impact on performance.|
|**Exclude Unnamed Nodes For Search**|Excludes unnamed entities in the results of searching by string in the [Entities Hierarchy](editor-hierarchy-window.md) window. If there are a lot of unnamed entities, this can speed up searching.|
|**Advanced Search**|Enables advanced query syntax in the [Entities Hierarchy](editor-hierarchy-window.md) window search field, including component filters (all/none/any), shared component filters, entity index tokens, [`EntityQueryOptions`](xref:Unity.Entities.EntityQueryOptions), and autocomplete. Default: on.|
|**Show Hidden Entities**|Displays entities in the [Hierarchy](editor-hierarchy-world-node.md) window that are hidden by default, such as internal system entities. Default: off.|
|**Type of Worlds Shown**|Controls which [world types](xref:Unity.Entities.WorldFlags) are visible in the [Hierarchy](editor-hierarchy-world-node.md) window. Default: `Live`.|

## Systems window

|**Property**|**Description**|
|---|---|
|**Show 0s in Entity Count And Time Column**|Displays `0` in the `Entity Count` column when a system doesn't match any entities. If you disable this property, Unity displays nothing in the `Entity Count` column when a system doesn't match any entities.|
|**Show More Precision For Running Time**|Increases the precision from 2 to 4 decimal places for the system running times in the `Time (ms)` column.|

## Baking

|**Property**|**Description**|
|---|---|
|**Scene View Mode**| Select what the Scene view displays for entities inside open subscenes:<ul><li>**Authoring Data**: The Scene view displays the authoring GameObjects, rendered using the render pipeline configured in the project. Baked entities are only visible in the Game view.</li><li>**Runtime Data**: The Scene view displays the baked entities, rendered using the [Entities Graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest) system. Authoring GameObjects are hidden.</li></ul> |
|**Live Baking Logging**| Enable this property to output a log of live baking triggers. This can help diagnose what causes [baking](baking-overview.md) to happen.|
|**Clear Entity cache**|Forces Unity to re-bake all subscenes the next time they're loaded in the Editor, or when making a Player build.|

## Journaling (deprecated)

[!include[](includes/journaling-deprecation.md)]

|**Property**|**Description**|
|---|---|
|**Enabled**|Enable [Journaling data](entities-journaling.md) recording.|
|**Total Memory MB**|Set the amount of memory in megabytes allocated to store Journaling record data. Once full, new records overwrite older records.|
|**Post Process**|Post-process journaling data in the Journaling window. This includes operations such as converting `GetComponentDataRW` to `SetComponentData` when possible.|

## Advanced

|**Property**|**Description**|
|---|---|
|**Show Advanced Worlds**|Displays advanced worlds in the different world dropdowns. Advanced worlds are specialized worlds like the Staging world or the Streaming world which serve as support to the [main worlds](concepts-worlds.md).|