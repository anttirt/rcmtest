#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        public enum Ordering
        {
            Ascending,
            Descending
        }
    }
}
#endif
