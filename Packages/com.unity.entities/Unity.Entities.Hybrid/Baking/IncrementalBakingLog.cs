using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Conversion;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Baking
{
    [StructLayout(LayoutKind.Explicit)]
    struct BakerJournalingEntry
    {
        [FieldOffset(0)] public int recordTypeInt;
        [FieldOffset(4)] public EntityId intValue;
#if UNITY_EDITOR
        [FieldOffset(4)] public GUID guidValue;
#endif
        [FieldOffset(4)] public ComponentBakeTrigger triggerValue;

        public BakeRecordType RecordType
        {
            get { return (BakeRecordType) recordTypeInt; }
            set { recordTypeInt = (int)value;  }
        }
    }

    struct ComponentBakeTrigger : IEquatable<ComponentBakeTrigger>
    {
        public EntityId AuthoringComponentId;
        public ComponentBakeReason BakeReason;
        public EntityId ReasonId;
        public Hash128 ReasonGuid;
        public TypeIndex BakingUnityTypeIndex;

        public bool Equals(ComponentBakeTrigger other)
        {
            return AuthoringComponentId == other.AuthoringComponentId && BakeReason == other.BakeReason && ReasonId == other.ReasonId && ReasonGuid.Equals(other.ReasonGuid) && BakingUnityTypeIndex.Equals(other.BakingUnityTypeIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is ComponentBakeTrigger other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AuthoringComponentId, (int) BakeReason, ReasonId, ReasonGuid, BakingUnityTypeIndex);
        }
    }

    enum ComponentBakeReason
    {
        NewComponent,
        ComponentChanged,
        GetComponentChanged,
        GetComponentStructuralChange,
        GetComponentsStructuralChange,
        GetHierarchySingleStructuralChange,
        GetHierarchyStructuralChange,
        ObjectExistStructuralChange,
        ReferenceChanged,
        GameObjectPropertyChange,
        GameObjectStaticChange,
        ReferenceChangedOnDisk,
        ActiveChanged,
        UpdatePrefabInstance,
    }

    enum BakeRecordType
    {
        ChangedGameObjects,
        NewGameObjects,
        DestroyedGameObjects,

        NewComponents,
        ChangedComponents,
        DestroyedComponents,

        ChangedAssets,

#if UNITY_EDITOR
        ChangedAssetsOnDisk,
#endif

        ComponentBakeTriggers
    }

    internal static class IncrementalBakingLog
    {
        private static ulong lastJournalingRecordIndex;

        internal sealed class SharedEnabled { internal static readonly SharedStatic<bool> Ref = SharedStatic<bool>.GetOrCreate<SharedEnabled>(); }

        internal static ref bool Enabled => ref SharedEnabled.Ref.Data;

        public static void Begin()
        {
#if UNITY_EDITOR
            Enabled = LiveConversionSettings.IsLiveBakingLoggingEnabled;
#else
            Enabled = false;
#endif

#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
#pragma warning disable 0618            
            lastJournalingRecordIndex = EntitiesJournaling.RecordIndex;
#pragma warning restore 0618            
#endif
            if (!Enabled)
                return;
        }

        public static void End()
        {
            if (!Enabled)
                return;
        }

        public static void RecordGameObjectChanged(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.ChangedGameObjects, entityId);
        }

        public static void RecordGameObjectNew(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.NewGameObjects, entityId);
        }

        public static void RecordGameObjectDestroyed(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.DestroyedGameObjects, entityId);
        }

        public static void RecordComponentNew(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.NewComponents, entityId);
        }

        public static void RecordComponentChanged(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.ChangedComponents, entityId);
        }

        public static void RecordComponentDestroyed(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.DestroyedComponents, entityId);
        }

        public static void RecordAssetChanged(EntityId entityId)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.ChangedAssets, entityId);
        }

#if UNITY_EDITOR
        public static void RecordAssetChangedOnDisk(GUID guid)
        {
            if (!Enabled)
                return;
            AddToJournaling(BakeRecordType.ChangedAssetsOnDisk, guid);
        }
#endif

        public static void RecordComponentBake(EntityId componentId, ComponentBakeReason reason, EntityId reasonId, TypeIndex unityTypeIndex)
        {
            if (!Enabled)
                return;

            var entry = new BakerJournalingEntry();
            entry.RecordType = BakeRecordType.ComponentBakeTriggers;
            entry.triggerValue = new ComponentBakeTrigger
            {
                AuthoringComponentId = componentId,
                BakeReason = reason,
                ReasonId = reasonId,
                ReasonGuid = default,
                BakingUnityTypeIndex = unityTypeIndex
            };

            AddToJournaling(entry);
        }

#if UNITY_EDITOR
        public static void RecordComponentBake(EntityId componentId, ComponentBakeReason reason, GUID reasonGuid, TypeIndex unityTypeIndex)
        {
            if (!Enabled)
                return;

            var entry = new BakerJournalingEntry();
            entry.RecordType = BakeRecordType.ComponentBakeTriggers;
            entry.triggerValue = new ComponentBakeTrigger
            {
                AuthoringComponentId = componentId,
                BakeReason = reason,
                ReasonId = EntityId.None,
                ReasonGuid = reasonGuid,
                BakingUnityTypeIndex = unityTypeIndex
            };
            AddToJournaling(entry);
        }
#endif

        private static void AddToJournaling(BakeRecordType recordType, EntityId value)
        {
            var entry = new BakerJournalingEntry();
            entry.RecordType = recordType;
            entry.intValue = value;

            AddToJournaling(entry);
        }

#if UNITY_EDITOR
        private static void AddToJournaling(BakeRecordType recordType, GUID guid)
        {
            var entry = new BakerJournalingEntry();
            entry.RecordType = recordType;
            entry.guidValue = guid;

            AddToJournaling(entry);
        }
#endif

        private static void AddToJournaling(BakerJournalingEntry entry)
        {
#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
#pragma warning disable 0618            
            unsafe
            {
                int entrySize = UnsafeUtility.SizeOf<BakerJournalingEntry>();
                EntitiesJournaling.AddRecord(
                    recordType: EntitiesJournaling.RecordType.BakingRecord,
                    worldSequenceNumber: default,
                    executingSystem: default,
                    entities: default,
                    entityCount: default,
                    data: UnsafeUtility.AddressOf(ref entry),
                    dataLength: entrySize);
            }
#pragma warning restore 0618            
#endif
        }

        public struct JournalBakingInfo : IDisposable
        {
            public UnsafeParallelHashSet<EntityId> ChangedGameObjects;
            public UnsafeParallelHashSet<EntityId> NewGameObjects;
            public UnsafeParallelHashSet<EntityId> DestroyedGameObjects;

            public UnsafeParallelHashSet<EntityId> NewComponents;
            public UnsafeParallelHashSet<EntityId> ChangedComponents;
            public UnsafeParallelHashSet<EntityId> DestroyedComponents;

            public UnsafeParallelHashSet<EntityId> ChangedAssets;

#if UNITY_EDITOR
            public UnsafeParallelHashSet<GUID> ChangedAssetsOnDisk;
#endif

            public UnsafeParallelHashMap<ComponentBakeTrigger, int> ComponentBakeTriggersCount;
            public UnsafeParallelMultiHashMap<EntityId, ComponentBakeTrigger> ComponentBakeTriggers;

            bool isCreated;

            public JournalBakingInfo(Allocator allocator)
            {
                ChangedGameObjects = new UnsafeParallelHashSet<EntityId>(10, allocator);
                NewGameObjects = new UnsafeParallelHashSet<EntityId>(10, allocator);
                DestroyedGameObjects = new UnsafeParallelHashSet<EntityId>(10, allocator);

                NewComponents = new UnsafeParallelHashSet<EntityId>(10, allocator);
                ChangedComponents = new UnsafeParallelHashSet<EntityId>(10, allocator);
                DestroyedComponents = new UnsafeParallelHashSet<EntityId>(10, allocator);

                ChangedAssets = new UnsafeParallelHashSet<EntityId>(10, allocator);

#if UNITY_EDITOR
                ChangedAssetsOnDisk = new UnsafeParallelHashSet<GUID>(10, allocator);
#endif

                ComponentBakeTriggersCount = new UnsafeParallelHashMap<ComponentBakeTrigger, int>(1024, allocator);
                ComponentBakeTriggers = new UnsafeParallelMultiHashMap<EntityId, ComponentBakeTrigger>(1024, allocator);

                isCreated = true;
            }

            public void Dispose()
            {
                if (!isCreated)
                    return;

                ChangedGameObjects.Dispose();
                NewGameObjects.Dispose();
                DestroyedGameObjects.Dispose();

                NewComponents.Dispose();
                ChangedComponents.Dispose();
                DestroyedComponents.Dispose();

                ChangedAssets.Dispose();
#if UNITY_EDITOR
                ChangedAssetsOnDisk.Dispose();
#endif

                ComponentBakeTriggersCount.Dispose();
                ComponentBakeTriggers.Dispose();

                isCreated = false;
            }

            public void Reset()
            {
                ChangedGameObjects.Clear();
                NewGameObjects.Clear();
                DestroyedGameObjects.Clear();

                NewComponents.Clear();
                ChangedComponents.Clear();
                DestroyedComponents.Clear();

                ChangedAssets.Clear();
#if UNITY_EDITOR
                ChangedAssetsOnDisk.Clear();
#endif

                ComponentBakeTriggersCount.Clear();
                ComponentBakeTriggers.Clear();
            }
        }

        private static JournalBakingInfo ReadRecords()
        {
            JournalBakingInfo info = new JournalBakingInfo(Allocator.Persistent);

#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
#pragma warning disable 0618            
            unsafe
            {
                // We want only baking records with an index that's greater than lastJournalingRecordIndex
                var records = EntitiesJournaling.GetRecords(EntitiesJournaling.Ordering.Ascending)
                    .WithRecordType(EntitiesJournaling.RecordType.BakingRecord)
                    .Where(r => r.Index > lastJournalingRecordIndex);

                foreach (var record in records)
                {
                    void* dataPtr = record.DataPtr;

                    // Validate data length to be sure we don't read beyond
                    if (record.DataLength != UnsafeUtility.SizeOf<BakerJournalingEntry>())
                        continue; // This is a silent error, we don't want to spam the console if it ever happens

                    var bakerEntry = UnsafeUtility.AsRef<BakerJournalingEntry>(dataPtr);
                    switch (bakerEntry.RecordType)
                    {
                        case BakeRecordType.ChangedGameObjects:
                            info.ChangedGameObjects.Add(bakerEntry.intValue);
                            break;
                        case BakeRecordType.NewGameObjects:
                            info.NewGameObjects.Add(bakerEntry.intValue);
                            break;
                        case BakeRecordType.DestroyedGameObjects:
                            info.DestroyedGameObjects.Add(bakerEntry.intValue);
                            break;
                        case BakeRecordType.NewComponents:
                            info.NewComponents.Add(bakerEntry.intValue);
                            break;
                        case BakeRecordType.ChangedComponents:
                            info.ChangedComponents.Add(bakerEntry.intValue);
                            break;
                        case BakeRecordType.DestroyedComponents:
                            info.DestroyedComponents.Add(bakerEntry.intValue);
                            break;
                        case BakeRecordType.ChangedAssets:
                            info.ChangedAssets.Add(bakerEntry.intValue);
                            break;
    #if UNITY_EDITOR
                        case BakeRecordType.ChangedAssetsOnDisk:
                            info.ChangedAssetsOnDisk.Add(bakerEntry.guidValue);
                            break;
    #endif
                        case BakeRecordType.ComponentBakeTriggers:
                        {
                            if (!info.ComponentBakeTriggersCount.TryGetValue(bakerEntry.triggerValue, out var count))
                            {
                                info.ComponentBakeTriggers.Add(bakerEntry.triggerValue.AuthoringComponentId, bakerEntry.triggerValue);
                            }
                            info.ComponentBakeTriggersCount[bakerEntry.triggerValue] = count + 1;
                        }

                        break;
                    }
                }
            }
#pragma warning restore 0618            
#endif
            return info;
        }

        public static void WriteLog(ref GameObjectComponents gameObjectComponents)
        {
            if (!Enabled)
                return;

            var sb = new StringBuilder();

            using var bakerRecords = ReadRecords();
            sb.AppendLine($"Incremental Baking Log: Frame {Time.frameCount}");
            sb.AppendLine($"==============================\n");

            // Write all changed GameObjects
            // -----------------------------
            if (bakerRecords.ChangedGameObjects.Count() > 0)
            {
                sb.AppendLine($"Changed GameObjects: {bakerRecords.ChangedGameObjects.Count()}");
                sb.AppendLine($"------------------------------");
                foreach (var gameObjectId in bakerRecords.ChangedGameObjects)
                {
                    WriteGameObject(ref gameObjectComponents, sb, gameObjectId);
                }
                sb.AppendLine($"------------------------------\n");
            }

            // Write all new GameObjects
            // -------------------------
            if (bakerRecords.NewGameObjects.Count() > 0)
            {
                sb.AppendLine($"New GameObjects: {bakerRecords.NewGameObjects.Count()}");
                sb.AppendLine($"------------------------------");
                foreach (var gameObjectId in bakerRecords.NewGameObjects)
                {
                    WriteGameObject(ref gameObjectComponents, sb, gameObjectId);
                }
                sb.AppendLine($"------------------------------\n");
            }

            // Write all changed Components
            // -------------------------
            if (bakerRecords.ChangedComponents.Count() > 0)
            {
                sb.AppendLine($"Changed Components: {bakerRecords.ChangedComponents.Count()}");
                sb.AppendLine($"------------------------------");
                foreach (var componentId in bakerRecords.ChangedComponents)
                {
                    WriteComponent(sb, componentId);
                }
                sb.AppendLine($"------------------------------\n");
            }

            // Write all new Components
            // -------------------------
            if (bakerRecords.NewComponents.Count() > 0)
            {
                sb.AppendLine($"New Components: {bakerRecords.NewComponents.Count()}");
                sb.AppendLine($"------------------------------");
                foreach (var componentId in bakerRecords.NewComponents)
                {
                    WriteComponent(sb, componentId);
                }
                sb.AppendLine($"------------------------------\n");
            }

            // Write dependency triggers
            // -------------------------
            using var authoringIds = NativeParallelHashMapExtensions.GetUniqueKeyArray(bakerRecords.ComponentBakeTriggers, Allocator.Temp).Item1;
            if (authoringIds.Length > 0)
            {
                sb.AppendLine($"Bake Reasons");
                sb.AppendLine($"------------------------------");
                foreach (var authoringId in authoringIds)
                {
                    var obj = Resources.EntityIdToObject(authoringId);
                    if (obj != null)
                    {
                        if (obj is GameObject go)
                        {
                            sb.AppendLine($"GameObject: {go.name} ({go.GetEntityId()})");
                        }

                        if (obj is Component component)
                        {
                            sb.AppendLine($"Type: {component.GetType().Name}");
                            sb.AppendLine(
                                $"GameObject: {component.gameObject.name} ({component.gameObject.GetEntityId()})");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"GameObject/Component: Not Available, possibly deleted ({authoringId})");
                    }

                    sb.AppendLine($"EntityId: {authoringId}");
                    sb.AppendLine("Why did I bake?:");
                    foreach (var trigger in bakerRecords.ComponentBakeTriggers.GetValuesForKey(authoringId))
                    {
                        var typeInfo = TypeManager.GetTypeInfo(trigger.BakingUnityTypeIndex);
                        switch (trigger.BakeReason)
                        {
                            case ComponentBakeReason.NewComponent:
                            {
                                sb.Append(
                                    $"\tNew Component - {typeInfo.DebugTypeName} ({trigger.AuthoringComponentId})");
                                break;
                            }
                            case ComponentBakeReason.ComponentChanged:
                            {
                                sb.Append(
                                    $"\tComponent Changed - {typeInfo.DebugTypeName} ({trigger.AuthoringComponentId})");
                                break;
                            }
                            case ComponentBakeReason.GetComponentChanged:
                            {
                                sb.Append($"\tGetComponent({typeInfo.DebugTypeName}) Changed ({trigger.ReasonId})");
                                break;
                            }
                            case ComponentBakeReason.GetComponentStructuralChange:
                            {
                                sb.Append(
                                    $"\tGetComponent({typeInfo.DebugTypeName}) Structural Change ({trigger.ReasonId})");
                                break;
                            }
                            case ComponentBakeReason.GetComponentsStructuralChange:
                            {
                                sb.Append($"\tGetComponents({typeInfo.DebugTypeName}) Structural Change");
                                break;
                            }
                            case ComponentBakeReason.GetHierarchySingleStructuralChange:
                            {
                                sb.Append(
                                    $"\tHierarchy({typeInfo.DebugTypeName}) Structural Change ({trigger.ReasonId})");
                                break;
                            }
                            case ComponentBakeReason.GetHierarchyStructuralChange:
                            {
                                sb.Append($"\tHierarchy({typeInfo.DebugTypeName}) Structural Change");
                                break;
                            }
                            case ComponentBakeReason.ObjectExistStructuralChange:
                            {
                                sb.Append(
                                    $"\tObjectExist({typeInfo.DebugTypeName}) Structural Change ({trigger.ReasonId})");
                                break;
                            }
                            case ComponentBakeReason.ReferenceChanged:
                            {
                                sb.Append($"\tReference() Changed ({trigger.ReasonId})");
                                break;
                            }
                            case ComponentBakeReason.GameObjectPropertyChange:
                            {
                                sb.Append($"\tGameObject Property() Changed ({trigger.ReasonId})");
                                break;
                            }

                            case ComponentBakeReason.GameObjectStaticChange:
                            {
                                sb.Append($"\tGameObject IsStatic() Changed ({trigger.ReasonId})");
                                break;
                            }
#if UNITY_EDITOR
                            case ComponentBakeReason.ReferenceChangedOnDisk:
                            {
                                var path = AssetDatabase.GUIDToAssetPath(trigger.ReasonGuid);
                                sb.Append($"\tReference({path}) Changed On Disk ({trigger.ReasonGuid})");
                                break;
                            }
#endif
                            case ComponentBakeReason.ActiveChanged:
                            {
                                var gameObject = (GameObject) Resources.EntityIdToObject(trigger.ReasonId);
                                if (gameObject != null)
                                {
                                    sb.Append($"\tIsActive() Changed ({gameObject.name}, {trigger.ReasonId})");
                                }
                                else
                                {
                                    sb.Append($"\tIsActive() Changed ({trigger.ReasonId})");
                                }

                                break;
                            }
                            case ComponentBakeReason.UpdatePrefabInstance:
                            {
                                var gameObject = (GameObject) Resources.EntityIdToObject(trigger.ReasonId);
                                if (gameObject != null)
                                {
                                    sb.Append(
                                        $"\tUpdatePrefabInstance ({gameObject.name}, {trigger.ReasonId}) - Caused when a prefab asset is instance in a SubScene and the original asset is modified, thus updating the non-overridden properties in the scene instance.");
                                }
                                else
                                {
                                    sb.Append(
                                        $"\tUpdatePrefabInstance ({trigger.ReasonId}) - Caused when a prefab asset is instance in a SubScene and the original asset is modified, thus updating the non-overridden properties in the scene instance.");
                                }

                                break;
                            }
                        }

                        int count = bakerRecords.ComponentBakeTriggersCount[trigger];
                        sb.AppendLine($"\t-\t Event Count: {count}");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine($"------------------------------\n");
            }

            Debug.Log(sb.ToString());
        }

        static void WriteComponent(StringBuilder sb, EntityId componentId)
        {
            var component = (Component) Resources.EntityIdToObject(componentId);
            sb.AppendLine($"Type: {component.GetType().Name}");
            sb.AppendLine($"EntityId: {componentId}");
            sb.AppendLine($"GameObject: {component.gameObject.name} ({component.gameObject.GetEntityId()})");
        }

        static void WriteGameObject(ref GameObjectComponents gameObjectComponents, StringBuilder sb, EntityId gameObjectId)
        {
            var gameObject = (GameObject)Resources.EntityIdToObject(gameObjectId);
            sb.AppendLine($"Name: {gameObject.name}");
            sb.AppendLine($"EntityId: {gameObjectId}");
            sb.AppendLine($"Scene: {gameObject.scene.name}");

            sb.AppendLine($"Components: ");
            foreach (var componentData in gameObjectComponents.GetComponents(gameObjectId))
            {
                var typeInfo = TypeManager.GetTypeInfo(componentData.TypeIndex);
                sb.AppendLine($"\t{typeInfo.Type.Name} ({componentData.EntityId})");
            }
        }

    }
}
