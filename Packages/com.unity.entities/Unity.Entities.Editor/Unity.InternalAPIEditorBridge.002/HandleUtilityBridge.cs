using System;
using UnityEngine;
using UnityEditor;

namespace Unity.Editor.Bridge
{
    static class HandleUtilityBridge
    {
        /// <summary>
        /// Registers a resolver function that converts entity indices to EntityIds.
        /// </summary>
        /// <param name="method">A function that takes an entity index and returns the corresponding EntityId, or EntityId.None if not found.</param>
        public static void RegisterEntityIdFromIndexResolver(Func<int, EntityId> method) => HandleUtility.getEntityIdFromIndex += method;
    }
}