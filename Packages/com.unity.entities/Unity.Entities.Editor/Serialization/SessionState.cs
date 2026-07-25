using System;
using System.Collections.Generic;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor.Serialization
{
    /// <summary>
    /// Wrapper around <see cref="UnityEditor.SessionState"/> that persists arbitrary objects as JSON
    /// keyed by a string. Embedded copy of the former <c>Unity.Serialization.Editor.SessionState&lt;T&gt;</c>.
    /// </summary>
    static class SessionState<T> where T : class, new()
    {
        static readonly Dictionary<string, T> s_Cache = new Dictionary<string, T>();

        static SessionState()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Save;
        }

        public static void Clear(string key)
        {
            s_Cache.Remove(key);
            UnityEditor.SessionState.EraseString(key);
        }

        public static T GetOrCreate(string key)
        {
            if (s_Cache.TryGetValue(key, out var value))
                return value;

            var json = UnityEditor.SessionState.GetString(key, string.Empty);
            try
            {
                value = string.IsNullOrEmpty(json) ? new T() : EntitiesJson.Deserialize<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(SessionState<T>)}<{typeof(T).Name}>: Could not load at key `{key}`.\nException `{exception}`");
                value = new T();
            }

            s_Cache.Add(key, value);
            return value;
        }

        static void Save()
        {
            foreach (var kvp in s_Cache)
                UnityEditor.SessionState.SetString(kvp.Key, EntitiesJson.Serialize(kvp.Value));
        }
    }
}
