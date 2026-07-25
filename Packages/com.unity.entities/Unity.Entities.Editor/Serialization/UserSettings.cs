using System;
using System.Collections.Generic;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor.Serialization
{
    /// <summary>
    /// Wrapper around <see cref="EditorUserSettings"/> that persists arbitrary objects as JSON
    /// keyed by a string. Embedded copy of the former <c>Unity.Serialization.Editor.UserSettings&lt;T&gt;</c>.
    /// </summary>
    static class UserSettings<T> where T : class, new()
    {
        static readonly Dictionary<string, T> s_Cache = new Dictionary<string, T>();

        static UserSettings()
        {
            EditorApplication.quitting += Save;
            AssemblyReloadEvents.beforeAssemblyReload += Save;
        }

        public static void Clear(string key)
        {
            s_Cache.Remove(key);
            EditorUserSettings.SetConfigValue(key, null);
        }

        public static T GetOrCreate(string key)
        {
            if (s_Cache.TryGetValue(key, out var value))
                return value;

            var json = EditorUserSettings.GetConfigValue(key) ?? string.Empty;
            try
            {
                value = string.IsNullOrEmpty(json) ? new T() : EntitiesJson.Deserialize<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(UserSettings<T>)}<{typeof(T).Name}>: Could not load at key `{key}`.\nException `{exception}`");
                value = new T();
            }

            s_Cache.Add(key, value);
            return value;
        }

        static void Save()
        {
            foreach (var kvp in s_Cache)
                EditorUserSettings.SetConfigValue(kvp.Key, EntitiesJson.Serialize(kvp.Value));
        }
    }
}
