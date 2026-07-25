using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities
{
    internal class IncrementalBakingChangeTracker : IDisposable
    {
        internal NativeList<EntityId> DeletedEntityIds;
        internal NativeParallelHashSet<EntityId> ChangedEntityIds;
        internal NativeParallelHashSet<EntityId> BakeHierarchyEntityIds;
        internal NativeParallelHashSet<EntityId> ForceBakeHierarchyEntityIds;
        internal NativeParallelHashMap<EntityId, EntityId> ParentChangeEntityIds;
        internal NativeList<EntityId> ChangedAssets;
        internal NativeList<EntityId> DeletedAssets;
        internal readonly HashSet<Component> ComponentChanges;
        private readonly List<Component> ValidComponents;
        internal NativeList<EntityId> ParentWithChildrenOrderChangedEntityIds;
        internal bool LightBakingChanged;

        public IncrementalBakingChangeTracker()
        {
            DeletedEntityIds = new NativeList<EntityId>(Allocator.Persistent);
            ChangedEntityIds = new NativeParallelHashSet<EntityId>(10, Allocator.Persistent);
            BakeHierarchyEntityIds = new NativeParallelHashSet<EntityId>(10, Allocator.Persistent);
            ForceBakeHierarchyEntityIds = new NativeParallelHashSet<EntityId>(10, Allocator.Persistent);
            ParentChangeEntityIds = new NativeParallelHashMap<EntityId, EntityId>(10, Allocator.Persistent);
            ChangedAssets = new NativeList<EntityId>(Allocator.Persistent);
            DeletedAssets = new NativeList<EntityId>(Allocator.Persistent);
            ComponentChanges = new HashSet<Component>();
            ValidComponents = new List<Component>();
            ParentWithChildrenOrderChangedEntityIds = new NativeList<EntityId>(Allocator.Persistent);
            LightBakingChanged = false;
        }

        internal void Clear()
        {
            DeletedEntityIds.Clear();
            ChangedEntityIds.Clear();
            BakeHierarchyEntityIds.Clear();
            ForceBakeHierarchyEntityIds.Clear();
            ParentChangeEntityIds.Clear();
            ChangedAssets.Clear();
            DeletedAssets.Clear();
            ComponentChanges.Clear();
            ValidComponents.Clear();
            ParentWithChildrenOrderChangedEntityIds.Clear();
            LightBakingChanged = false;
        }

        internal bool HasAnyChanges()
        {
            return DeletedEntityIds.Length > 0 ||
                !ChangedEntityIds.IsEmpty ||
                !BakeHierarchyEntityIds.IsEmpty ||
                !ForceBakeHierarchyEntityIds.IsEmpty ||
                ComponentChanges.Count > 0 ||
                !ParentChangeEntityIds.IsEmpty ||
                ChangedAssets.Length > 0 ||
                DeletedAssets.Length > 0 ||
                ParentWithChildrenOrderChangedEntityIds.Length > 0 ||
                LightBakingChanged;
        }

        internal void FillBatch(ref IncrementalBakingBatch batch)
        {
            batch.DeletedEntityIds = DeletedEntityIds.AsArray();
            batch.ChangedEntityIds = ChangedEntityIds.ToNativeArray(Allocator.Temp);
            batch.BakeHierarchyEntityIds = BakeHierarchyEntityIds.ToNativeArray(Allocator.Temp);
            batch.ForceBakeHierarchyEntityIds = ForceBakeHierarchyEntityIds.ToNativeArray(Allocator.Temp);
            batch.ParentChangeEntityIds = ParentChangeEntityIds;
            batch.ChangedAssets = ChangedAssets.AsArray();
            batch.DeletedAssets = DeletedAssets.AsArray();
            batch.ChangedComponents = ValidComponents;
            batch.ParentWithChildrenOrderChangedEntityIds = ParentWithChildrenOrderChangedEntityIds.AsArray();
            // We don't need RecreateInstanceIds unless an previously baked entity has been deleted
            batch.RecreateInstanceIds = default;
            batch.LightBakingChanged = LightBakingChanged;
            ValidComponents.AddRange(ComponentChanges);
        }

        public void Dispose()
        {
            if (DeletedEntityIds.IsCreated)
                DeletedEntityIds.Dispose();
            if (ChangedEntityIds.IsCreated)
                ChangedEntityIds.Dispose();
            if (BakeHierarchyEntityIds.IsCreated)
                BakeHierarchyEntityIds.Dispose();
            if (ForceBakeHierarchyEntityIds.IsCreated)
                ForceBakeHierarchyEntityIds.Dispose();
            if (ParentChangeEntityIds.IsCreated)
                ParentChangeEntityIds.Dispose();
            if (ChangedAssets.IsCreated)
                ChangedAssets.Dispose();
            if (DeletedAssets.IsCreated)
                DeletedAssets.Dispose();
            if (ParentWithChildrenOrderChangedEntityIds.IsCreated)
                ParentWithChildrenOrderChangedEntityIds.Dispose();
        }

        public void MarkAssetChanged(EntityId assetEntityId) => ChangedAssets.Add(assetEntityId);

        public void MarkRemoved(EntityId emtityId)
        {
            BakeHierarchyEntityIds.Remove(emtityId);
            ForceBakeHierarchyEntityIds.Remove(emtityId);
            ChangedEntityIds.Remove(emtityId);
            ParentChangeEntityIds.Remove(emtityId);
            DeletedEntityIds.Add(emtityId);
        }

        public void MarkParentChanged(EntityId entityId, EntityId parentEntityId)
        {
            if (!ParentChangeEntityIds.TryAdd(entityId, parentEntityId))
            {
                ParentChangeEntityIds.Remove(entityId);
                ParentChangeEntityIds.Add(entityId, parentEntityId);
            }
        }

        public void MarkComponentChanged(Component c) => ComponentChanges.Add(c);
        public void MarkBakeHierarchy(EntityId entityId) => BakeHierarchyEntityIds.Add(entityId);
        public void MarkForceBakeHierarchy(EntityId entityId) => ForceBakeHierarchyEntityIds.Add(entityId);
        public void MarkChanged(EntityId entityId) => ChangedEntityIds.Add(entityId);

        public void MarkChildrenOrderChange(EntityId entityId) =>
            ParentWithChildrenOrderChangedEntityIds.Add(entityId);

        public void MarkLightBakingChanged() => LightBakingChanged = true;
    }

    /// <summary>
    /// Represents a fine-grained description of changes that happened since the last conversion.
    /// </summary>
    internal struct IncrementalBakingBatch : IDisposable
    {
        /// <summary>
        /// Instance IDs of all GameObjects that were deleted.
        /// Note that this can overlap with any of the other collections.
        /// </summary>
        public NativeArray<EntityId> DeletedEntityIds;

        /// <summary>
        /// Instance IDs of all GameObjects that were changed.
        /// /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<EntityId> ChangedEntityIds;

        /// <summary>
        /// Instance IDs of all GameObjects that should have the entire hierarchy below them reconverted.
        /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<EntityId> BakeHierarchyEntityIds;

        /// <summary>
        /// Instance IDs of all GameObjects that should have the entire hierarchy below them reconverted.
        /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<EntityId> ForceBakeHierarchyEntityIds;

        /// <summary>
        /// Instance IDs of all GameObjects that have lost their Primary Entity
        /// Note that this might include IDs of destroyed GameObjects.
        /// </summary>
        public NativeArray<EntityId> RecreateInstanceIds;

        /// <summary>
        /// Maps instance IDs of GameObjects to the instance ID of their last recorded parent if the parenting changed.
        /// Note that this might included instance IDs of destroyed GameObjects on either side.
        /// </summary>
        public NativeParallelHashMap<EntityId, EntityId> ParentChangeEntityIds;

        /// <summary>
        /// Contains the instance IDs of all assets that were changed since the last conversion.
        /// </summary>
        public NativeArray<EntityId> ChangedAssets;

        /// <summary>
        /// Contains the GUIDs of all assets that were deleted since the last conversion.
        /// </summary>
        public NativeArray<EntityId> DeletedAssets;

        /// <summary>
        /// Contains a list of all components that were changed since the last conversion. Note that the components
        /// might have been destroyed in the mean time.
        /// </summary>
        public List<Component> ChangedComponents;

        /// <summary>
        /// Contains all the instance ids of the parents with children being reordered
        /// </summary>
        public NativeArray<EntityId> ParentWithChildrenOrderChangedEntityIds;

        /// <summary>
        /// True if the lights have been baked, meaning that the components that depend on light mapping should be updated
        /// </summary>
        public bool LightBakingChanged;

        public void Dispose()
        {
            DeletedEntityIds.Dispose();
            ChangedEntityIds.Dispose();
            BakeHierarchyEntityIds.Dispose();
            ForceBakeHierarchyEntityIds.Dispose();
            ParentChangeEntityIds.Dispose();
            ChangedAssets.Dispose();
            DeletedAssets.Dispose();
            ParentWithChildrenOrderChangedEntityIds.Dispose();
            if (RecreateInstanceIds.IsCreated)
                RecreateInstanceIds.Dispose();
        }
#if UNITY_EDITOR
        internal string FormatSummary()
        {
            var sb = new StringBuilder();
            FormatSummary(sb);
            return sb.ToString();
        }

        internal void FormatSummary(StringBuilder sb)
        {
            sb.AppendLine(nameof(IncrementalBakingBatch));

            sb.Append(nameof(LightBakingChanged));
            sb.Append(": ");
            sb.AppendLine(LightBakingChanged.ToString());

            PrintOut(sb, nameof(DeletedEntityIds), DeletedEntityIds);
            PrintOut(sb, nameof(ChangedEntityIds), ChangedEntityIds);
            PrintOut(sb, nameof(BakeHierarchyEntityIds), BakeHierarchyEntityIds);
            PrintOut(sb, nameof(ForceBakeHierarchyEntityIds), ForceBakeHierarchyEntityIds);
            if (RecreateInstanceIds.IsCreated)
                PrintOut(sb, nameof(RecreateInstanceIds), RecreateInstanceIds);
            PrintOut(sb, nameof(ChangedAssets), ChangedAssets);
            PrintOut(sb, nameof(DeletedAssets), DeletedAssets);

            if (ChangedComponents.Count > 0)
            {
                sb.Append(nameof(ChangedComponents));
                sb.Append(": ");
                sb.Append(ChangedComponents.Count);
                sb.AppendLine();
                foreach (var c in ChangedComponents)
                {
                    sb.Append('\t');
                    sb.Append(c.ToString());
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            if (!ParentChangeEntityIds.IsEmpty)
            {
                sb.Append(nameof(ParentChangeEntityIds));
                sb.Append(": ");
                sb.Append(ParentChangeEntityIds.Count());
                sb.AppendLine();
                foreach (var kvp in ParentChangeEntityIds)
                {
                    sb.Append('\t');
                    sb.Append(kvp.Key.ToString());
                    sb.Append(" (");
                    {
                        var obj = EditorUtility.EntityIdToObject(kvp.Key);
                        if (obj == null)
                            sb.Append("null");
                        else
                            sb.Append(obj.name);
                    }
                    sb.Append(") reparented to ");
                    sb.Append(kvp.Value.ToString());
                    sb.Append(" (");
                    {
                        var obj = EditorUtility.EntityIdToObject(kvp.Value);
                        if (obj == null)
                            sb.Append("null");
                        else
                            sb.Append(obj.name);
                    }
                    sb.AppendLine(")");
                }
            }
        }

        static void PrintOut(StringBuilder sb, string name, NativeArray<EntityId> entityIds)
        {
            if (entityIds.Length == 0)
                return;
            sb.Append(name);
            sb.Append(": ");
            sb.Append(entityIds.Length);
            sb.AppendLine();
            for (int i = 0; i < entityIds.Length; i++)
            {
                sb.Append('\t');
                sb.Append(entityIds[i].ToString());
                sb.Append(" - ");
                var obj = EditorUtility.EntityIdToObject(entityIds[i]);
                if (obj == null)
                    sb.AppendLine("(null)");
                else
                    sb.AppendLine(obj.name);
            }

            sb.AppendLine();
        }
#endif
    }
}
