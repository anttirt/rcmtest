using System;
using System.Text.Json;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Unity.Entities.UI
{
    [Serializable]
    class SerializableContent
    {
        [SerializeField, HideInInspector] string m_Data;

        [NonSerialized] public ContentProvider Provider;

        public string Name => Provider?.Name;

        public void Load()
        {
            if (string.IsNullOrEmpty(m_Data) || Provider == null)
                return;

            try
            {
                var loaded = EntitiesJson.Deserialize(m_Data, Provider.GetType());
                if (loaded is ContentProvider provider)
                    Provider = provider;
            }
            catch (JsonException exception)
            {
                Debug.LogException(exception);
            }
        }

        public void Save()
        {
            m_Data = Provider != null
                ? EntitiesJson.Serialize(Provider, Provider.GetType())
                : string.Empty;
        }
    }
}
