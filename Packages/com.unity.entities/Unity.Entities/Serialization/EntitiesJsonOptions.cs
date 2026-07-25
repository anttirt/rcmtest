using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Unity.Properties;
using UnityEngine;

namespace Unity.Entities.Serialization
{
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> for Entities JSON (clipboard, settings, journaling, content provider state).
    /// </summary>
    internal static class EntitiesJsonOptions
    {
        static readonly JsonConverter[] s_SharedConverters =
        {
            new FixedString32BytesJsonConverter(),
            new FixedString64BytesJsonConverter(),
            new FixedString128BytesJsonConverter(),
            new FixedString512BytesJsonConverter(),
            new FixedString4096BytesJsonConverter(),
            new Hash128JsonConverter(),
            new EntityGuidJsonConverter(),
        };

        /// <summary>
        /// Default options used by every Entities JSON caller (clipboard, settings, content provider state, journaling).
        /// camelCase names, case-insensitive deserialization, pretty-printed output, includes public fields, and
        /// applies the same member-selection rules as <c>Unity.Properties.Internal.ReflectedPropertyBagProvider</c>
        /// so types authored against Unity.Properties keep round-tripping without changes.
        /// </summary>
        public static JsonSerializerOptions Default { get; } = Create();

        static JsonSerializerOptions Create()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                IncludeFields = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers = { ApplyEntitiesAttributeContract }
                },
            };

            foreach (var converter in s_SharedConverters)
                options.Converters.Add(converter);

#if UNITY_EDITOR
            // Restore the GlobalObjectId-based serialization for UnityEngine.Object that com.unity.serialization
            // provided via JsonAdapter<UnityEngine.Object>.
            options.Converters.Add(new UnityObjectJsonConverterFactory());
#endif

            return options;
        }

        // Mirrors Unity.Properties' ReflectedPropertyBagProvider.GetPropertyMembers member-selection rules on top of
        // System.Text.Json's default contract, so System.Text.Json picks up exactly the members Unity.Properties
        // would have walked. Without this, public auto-properties returning non-serializable types (System.Type,
        // UnityEngine.Object) on ContentProvider subclasses crash SerializableContent.Save().
        static void ApplyEntitiesAttributeContract(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;

            var handled = new HashSet<MemberInfo>();

            // Pass 1: prune System.Text.Json's default selection down to what Unity.Properties would walk.
            // Public fields stay; public properties only stay when explicitly tagged.
            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var member = typeInfo.Properties[i].AttributeProvider as MemberInfo;
                if (member == null)
                    continue;

                if (!ShouldKeepDefaultMember(member))
                {
                    typeInfo.Properties.RemoveAt(i);
                    continue;
                }

                handled.Add(member);
            }

            // Pass 2: add non-public members that opt in via [CreateProperty] / [SerializeField] / [SerializeReference].
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = typeInfo.Type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (IsCompilerGenerated(field))
                        continue;
                    if (!IsOptedIn(field) || IsOptedOut(field))
                        continue;
                    if (!handled.Add(field))
                        continue;

                    var fieldRef = field;
                    var name = ApplyNamingPolicy(field.Name, typeInfo.Options);
                    var info = typeInfo.CreateJsonPropertyInfo(field.FieldType, name);
                    info.Get = obj => fieldRef.GetValue(obj);
                    info.Set = (obj, value) => fieldRef.SetValue(obj, value);
                    typeInfo.Properties.Add(info);
                }

                foreach (var prop in t.GetProperties(flags))
                {
                    if (!IsOptedIn(prop) || IsOptedOut(prop))
                        continue;
                    if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                        continue;
                    if (!handled.Add(prop))
                        continue;

                    var propRef = prop;
                    var name = ApplyNamingPolicy(prop.Name, typeInfo.Options);
                    var info = typeInfo.CreateJsonPropertyInfo(prop.PropertyType, name);
                    info.Get = obj => propRef.GetValue(obj);
                    if (propRef.CanWrite)
                        info.Set = (obj, value) => propRef.SetValue(obj, value);
                    typeInfo.Properties.Add(info);
                }
            }
        }

        // Mirrors ReflectedPropertyBagProvider's filter for already-discovered (public) members:
        //  - any [DontCreateProperty] / [DontSerialize] / [NonSerialized] removes it
        //  - public fields stay implicitly
        //  - public properties stay only if explicitly opted in
        static bool ShouldKeepDefaultMember(MemberInfo member)
        {
            if (IsOptedOut(member))
                return false;

            if (member is FieldInfo)
                return true;

            return IsOptedIn(member);
        }

        static bool IsOptedIn(MemberInfo member)
            => member.IsDefined(typeof(CreatePropertyAttribute), inherit: true)
               || member.IsDefined(typeof(SerializeField), inherit: true)
               || member.IsDefined(typeof(SerializeReference), inherit: true);

        static bool IsOptedOut(MemberInfo member)
            => member.IsDefined(typeof(DontSerializeAttribute), inherit: true)
               || member.IsDefined(typeof(DontCreatePropertyAttribute), inherit: true)
               || member.IsDefined(typeof(NonSerializedAttribute), inherit: true);

        static bool IsCompilerGenerated(MemberInfo member)
            => member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: true);

        static string ApplyNamingPolicy(string name, JsonSerializerOptions options)
            => options.PropertyNamingPolicy?.ConvertName(name) ?? name;
    }
}
