using System;
using System.Collections.Generic;
using Unity.Entities.Serialization;
using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.UI
{
    class UiPersistentState
    {
        public const string Key = "unity-platforms__ui-persistent-state";

        internal struct PaginationData
        {
            public int PaginationSize;
            public int CurrentPage;
        }

        [CreateProperty]
        readonly Dictionary<int, bool> FoldoutState = new Dictionary<int, bool>();

        [CreateProperty]
        readonly Dictionary<int, PaginationData> PaginationState = new Dictionary<int, PaginationData>();

        static UiPersistentState s_Cached;

        static UiPersistentState()
        {
            EditorApplication.quitting += Save;
            AssemblyReloadEvents.beforeAssemblyReload += Save;
        }

        //[MenuItem("Properties/UI/Clear PersistentState")]
        public static void ClearState()
        {
            s_Cached = null;
            EditorUserSettings.SetConfigValue(Key, null);
        }

        public static void SetFoldoutState(Type type, PropertyPath path, bool foldout)
        {
            if (null == type || path.IsEmpty)
                return;

            GetOrLoad().FoldoutState[ComputeHash(type, path)] = foldout;
        }

        public static bool GetFoldoutState(Type type, PropertyPath path, bool defaultValue = false)
        {
            if (null == type || path.IsEmpty)
                return defaultValue;

            return GetOrLoad().FoldoutState.TryGetValue(ComputeHash(type, path), out var foldout) ? foldout : defaultValue;
        }

        public static void SetPaginationState(Type type, PropertyPath path, int size, int page)
        {
            if (null == type || path.IsEmpty)
                return;

            GetOrLoad().PaginationState[ComputeHash(type, path)] = new PaginationData {PaginationSize = size, CurrentPage = page};
        }

        public static PaginationData GetPaginationState(Type type, PropertyPath path)
        {
            if (null == type || path.IsEmpty)
                return default;

            return GetOrLoad().PaginationState.TryGetValue(ComputeHash(type, path), out var data) ? data : default;
        }

        static int ComputeHash(Type type, PropertyPath path)
        {
            var hash = 19;
            hash = hash * 31 + type.FullName.GetHashCode();
            hash = hash * 31 + path.GetHashCode();
            return hash;
        }

        static UiPersistentState GetOrLoad()
        {
            if (s_Cached != null)
                return s_Cached;

            var json = EditorUserSettings.GetConfigValue(Key) ?? string.Empty;
            try
            {
                s_Cached = string.IsNullOrEmpty(json) ? new UiPersistentState() : EntitiesJson.Deserialize<UiPersistentState>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(UiPersistentState)}: Could not load at key `{Key}`.\nException `{exception}`");
                s_Cached = new UiPersistentState();
            }

            return s_Cached;
        }

        static void Save()
        {
            if (s_Cached == null)
                return;
            EditorUserSettings.SetConfigValue(Key, EntitiesJson.Serialize(s_Cached));
        }
    }
}
