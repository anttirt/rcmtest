using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Assemblies;

namespace Unity.Entities.Serialization
{
    /// <summary>
    /// Marks a field or property to be ignored by Entities' serialization paths
    /// (clipboard, content provider state, journaling export, vendored binary serializer).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class DontSerializeAttribute : Attribute
    {
    }

    /// <summary>
    /// Records a former name for a field, property, struct, or class so that the vendored
    /// binary serializer can resolve types whose qualified name has changed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    internal sealed class FormerNameAttribute : Attribute
    {
        public string OldName { get; }

        public FormerNameAttribute(string oldName)
        {
            OldName = oldName;
        }

        static readonly Dictionary<string, string> s_FormerlySerializedAsToCurrentName = new Dictionary<string, string>();
        static bool s_Registered;

        static void RegisterFormerlySerializedAsTypes()
        {
            if (s_Registered)
                return;

            s_Registered = true;

#if UNITY_EDITOR
            foreach (var type in UnityEditor.TypeCache.GetTypesWithAttribute<FormerNameAttribute>())
            {
                if (type.IsAbstract || type.IsGenericType)
                    continue;

                var attributes = (FormerNameAttribute[])type.GetCustomAttributes(typeof(FormerNameAttribute), false);
                foreach (var attribute in attributes)
                    s_FormerlySerializedAsToCurrentName[attribute.OldName] = $"{type}, {type.Assembly.GetName().Name}";
            }
#else
            foreach (var assembly in CurrentAssemblies.GetLoadedAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsGenericType)
                        continue;

                    foreach (var attribute in type.GetCustomAttributes<FormerNameAttribute>())
                        s_FormerlySerializedAsToCurrentName[attribute.OldName] = $"{type}, {type.Assembly.GetName().Name}";
                }
            }
#endif
        }

        /// <summary>
        /// Gets the current name based on the previous name.
        /// </summary>
        public static bool TryGetCurrentTypeName(string oldName, out string currentName)
        {
            RegisterFormerlySerializedAsTypes();
            return s_FormerlySerializedAsToCurrentName.TryGetValue(oldName, out currentName);
        }
    }

    /// <summary>
    /// Thrown when an error occurs during binary or JSON serialization within Entities.
    /// </summary>
    [Serializable]
    internal class SerializationException : Exception
    {
        public SerializationException(string message) : base(message)
        {
        }
    }
}
