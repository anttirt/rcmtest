using System;
using JetBrains.Annotations;
using Unity.Editor.Bridge;
using Unity.Properties;
using UnityEditor;

namespace Unity.Entities.Editor
{
    [DOTSEditorPreferencesSetting(Constants.Settings.Hierarchy), UsedImplicitly]
    internal class HierarchyEntitiesSettings : ISetting
    {
        const WorldFlags k_DefaultWorldsShown = WorldFlags.Live;
        
        public bool ShowHiddenEntities = false;
        public WorldFlags TypeOfWorldsShown = k_DefaultWorldsShown;
        
        public static bool GetShowHiddenEntities() => GetBoolValue(nameof(ShowHiddenEntities));
        public static void SetShowHiddenEntities(bool val) => SetBoolValue(nameof(ShowHiddenEntities), val);

        public static WorldFlags GetTypesOfWorldsShown()
        {
            var val = GetIntValue(nameof(TypeOfWorldsShown));
            if (val != int.MinValue)
                return (WorldFlags) val;
            return k_DefaultWorldsShown;
        }

        public static void SetTypesOfWorldsShown(WorldFlags val) => SetIntValue(nameof(TypeOfWorldsShown), (int)val);

        static bool GetBoolValue(string key)
        {
            var setting = EditorUserSettings.GetConfigValue(key);
            return !string.IsNullOrEmpty(setting) && Convert.ToBoolean(setting);
        }

        static void SetBoolValue(string key, bool val)
        {
            EditorUserSettings.SetConfigValue(key, val ? "true" : "false");
        }
        
        static int GetIntValue(string key)
        {
            var setting = EditorUserSettings.GetConfigValue(key);
            return !string.IsNullOrEmpty(setting) ? Convert.ToInt32(setting) : int.MinValue;
        }
        
        static void SetIntValue(string key, int val)
        {
            EditorUserSettings.SetConfigValue(key, val.ToString());
        }        
        
        public string[] GetSearchKeywords() => ISetting.GetSearchKeywordsFromType(GetType());

        public void OnSettingChanged(PropertyPath path)
        {
            switch (path.ToString())
            {
                case nameof(ShowHiddenEntities):
                    ShowHiddenEntities = !GetShowHiddenEntities();
                    SetShowHiddenEntities(ShowHiddenEntities);
                    break;
                case nameof(TypeOfWorldsShown):
                    SetTypesOfWorldsShown(TypeOfWorldsShown);
                    break;                
            }

            var window = EditorWindow.GetWindow<Unity.Hierarchy.Editor.HierarchyWindow>();
            window.ReloadHostView();
        }
    }
}
