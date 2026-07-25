#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Record type enumeration.
        /// </summary>
        public enum RecordType : int
        {
            WorldCreated,
            WorldDestroyed,
            SystemAdded,
            SystemRemoved,
            CreateEntity,
            DestroyEntity,
            AddComponent,
            RemoveComponent,
            EnableComponent,
            DisableComponent,
            SetComponentData,
            SetSharedComponentData,
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            SetComponentObject,
            #pragma warning restore 0618
            SetBuffer,
            GetComponentDataRW,
            GetComponentObjectRW,
            GetBufferRW,
            BakingRecord,
        }
    }
}
#endif
