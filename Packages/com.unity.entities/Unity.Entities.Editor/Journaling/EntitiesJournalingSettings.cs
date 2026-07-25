using Unity.Properties;
using Unity.Entities.Serialization;

namespace Unity.Entities.Editor
{
    [DOTSEditorPreferencesSetting(Constants.Settings.Journaling)]
    class EntitiesJournalingSettings : ISetting
    {
        [CreateProperty, DontSerialize]
        public bool Enabled
        {
#if !DISABLE_ENTITIES_JOURNALING
#pragma warning disable 0618            
            get => EntitiesJournaling.Preferences.Enabled;
            set
            {
                EntitiesJournaling.Preferences.Enabled = value;
                EntitiesJournaling.Enabled = value;
            }
#pragma warning restore 0618            
#else
            get => false;
            set { }
#endif
        }

        [CreateProperty, DontSerialize]
        public int TotalMemoryMB
        {
#if !DISABLE_ENTITIES_JOURNALING
#pragma warning disable 0618            
            get => EntitiesJournaling.Preferences.TotalMemoryMB;
            set => EntitiesJournaling.Preferences.TotalMemoryMB = value;
#pragma warning restore 0618            
#else
            get => 0;
            set { }
#endif
        }

        [CreateProperty, DontSerialize]
        public bool PostProcess
        {
#if !DISABLE_ENTITIES_JOURNALING
#pragma warning disable 0618
            get => EntitiesJournaling.Preferences.PostProcess;
            set => EntitiesJournaling.Preferences.PostProcess = value;
#pragma warning restore 0618
#else
            get => false;
            set { }
#endif
        }

        public void OnSettingChanged(PropertyPath path)
        {
        }

        public string[] GetSearchKeywords()
        {
            return ISetting.GetSearchKeywordsFromType(GetType());
        }
    }


#if DISABLE_ENTITIES_JOURNALING
    class EntitiesJournalingSettingsInspector : Unity.Entities.UI.Inspector<EntitiesJournalingSettings>
    {
        public override UnityEngine.UIElements.VisualElement Build()
        {
            var root = base.Build();
            root.SetEnabled(false);
            return root;
        }
    }
#endif
}
