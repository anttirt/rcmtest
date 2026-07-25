using System;
using System.Text.Json;

namespace Unity.Entities.Serialization
{
    /// <summary>
    /// Thin helpers around <see cref="JsonSerializer"/> using <see cref="EntitiesJsonOptions"/>.
    /// </summary>
    internal static class EntitiesJson
    {
        public static string Serialize<T>(T value, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Serialize(value, options ?? EntitiesJsonOptions.Default);
        }

        public static string Serialize(object value, Type inputType, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Serialize(value, inputType, options ?? EntitiesJsonOptions.Default);
        }

        public static T Deserialize<T>(string json, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(json, options ?? EntitiesJsonOptions.Default);
        }

        public static object Deserialize(string json, Type returnType, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize(json, returnType, options ?? EntitiesJsonOptions.Default);
        }
    }
}
