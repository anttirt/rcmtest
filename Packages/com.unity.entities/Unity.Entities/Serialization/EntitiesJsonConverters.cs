using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Entities.Serialization
{
    internal sealed class FixedString32BytesJsonConverter : JsonConverter<FixedString32Bytes>
    {
        public override FixedString32Bytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(FixedString32Bytes)}, got {reader.TokenType}.");

            return new FixedString32Bytes(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, FixedString32Bytes value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class FixedString64BytesJsonConverter : JsonConverter<FixedString64Bytes>
    {
        public override FixedString64Bytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(FixedString64Bytes)}, got {reader.TokenType}.");

            return new FixedString64Bytes(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, FixedString64Bytes value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class FixedString128BytesJsonConverter : JsonConverter<FixedString128Bytes>
    {
        public override FixedString128Bytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(FixedString128Bytes)}, got {reader.TokenType}.");

            return new FixedString128Bytes(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, FixedString128Bytes value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class FixedString512BytesJsonConverter : JsonConverter<FixedString512Bytes>
    {
        public override FixedString512Bytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(FixedString512Bytes)}, got {reader.TokenType}.");

            return new FixedString512Bytes(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, FixedString512Bytes value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class FixedString4096BytesJsonConverter : JsonConverter<FixedString4096Bytes>
    {
        public override FixedString4096Bytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(FixedString4096Bytes)}, got {reader.TokenType}.");

            return new FixedString4096Bytes(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, FixedString4096Bytes value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class Hash128JsonConverter : JsonConverter<Hash128>
    {
        public override Hash128 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(Hash128)}, got {reader.TokenType}.");

            return new Hash128(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, Hash128 value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    internal sealed class EntityGuidJsonConverter : JsonConverter<EntityGuid>
    {
        public override EntityGuid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {nameof(EntityGuid)}, got {reader.TokenType}.");

            var text = reader.GetString();
            if (!TryParse(text, out var value))
                throw new JsonException($"Invalid {nameof(EntityGuid)} string.");

            return value;
        }

        public override void Write(Utf8JsonWriter writer, EntityGuid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        static bool TryParse(string text, out EntityGuid value)
        {
            value = default;
            if (string.IsNullOrEmpty(text))
                return false;

            var parts = text.Split(':');
            if (parts.Length != 4)
                return false;

            if (!ulong.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var originating))
                return false;

            if (!ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var originatingSub))
                return false;

            if (!uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var namespaceId))
                return false;

            if (!uint.TryParse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var serial))
                return false;

            value = new EntityGuid(EntityId.FromULong(originating), EntityId.FromULong(originatingSub), namespaceId, serial);
            return true;
        }
    }

#if UNITY_EDITOR
    // UnityEngine.Object used to be serialized as GlobalObjectId in com.unity.serialization
    internal sealed class UnityObjectJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeof(UnityEngine.Object).IsAssignableFrom(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(typeof(UnityObjectJsonConverter<>).MakeGenericType(typeToConvert));
    }

    internal sealed class UnityObjectJsonConverter<T> : JsonConverter<T> where T : UnityEngine.Object
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected string for {typeof(T).Name}, got {reader.TokenType}.");

            var text = reader.GetString();
            if (string.IsNullOrEmpty(text))
                return null;

            if (!GlobalObjectId.TryParse(text, out var id) || id.assetGUID.Empty())
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            // Treat "fake null" (destroyed objects) as null so we don't emit a stale GlobalObjectId.
            if (value == null || !value)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(GlobalObjectId.GetGlobalObjectIdSlow(value).ToString());
        }
    }
#endif
}
