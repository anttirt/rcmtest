namespace Unity.Entities.Editor
{
    internal static class EntityQueryUtility
    {
        public static string[] CollectComponentTypesFromSystemQuery(SystemProxy systemProxy) => systemProxy.GetComponentTypesUsedByQueries();
    }
}
