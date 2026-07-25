using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities
{
    /// <summary>
    /// An unmanaged component that references a UnityEngine.Component attached to an entity.
    /// </summary>
    /// <remarks>
    /// Register each type you use with <see cref="RegisterGenericComponentTypeAttribute"/>, for
    /// example <c>[assembly: RegisterGenericComponentType(typeof(CompanionComponent&lt;Light&gt;))]</c>.
    /// </remarks>
    public struct CompanionComponent<T> : IComponentData where T : Component
    {
        /// <summary>
        /// The reference to the UnityEngine.Component that this component exposes on the entity.
        /// </summary>
        public UnityObjectRef<T> CompanionRef;
    }

    /// <summary>
    /// Extension methods for reading companion components from an EntityManager.
    /// </summary>
    public static class CompanionComponentExtensions
    {
        /// <summary>
        /// Returns the UnityEngine.Component of type T referenced by the entity's companion component.
        /// </summary>
        /// <remarks>
        /// Resolving the reference is a managed call, so call this method from the main thread. Throws
        /// if <paramref name="entity"/> has no <see cref="CompanionComponent{T}"/>.
        /// </remarks>
        /// <param name="entityManager">The entity manager that stores <paramref name="entity"/>.</param>
        /// <param name="entity">The entity to read the companion component from.</param>
        /// <returns>The <typeparamref name="T"/> component referenced by the entity's <see cref="CompanionComponent{T}"/>.</returns>
        public static T GetCompanion<T>(this EntityManager entityManager, Entity entity)
            where T : Component
        {
            return entityManager.GetComponentData<CompanionComponent<T>>(entity).CompanionRef;
        }
    }

    internal static class CompanionComponentTypeIndices
    {
        static readonly HashSet<TypeIndex> s_Set = new();

        internal static void RegisterIfWrapper(Type type, TypeIndex typeIndex)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
                return;
            if (type.GetGenericTypeDefinition() != typeof(CompanionComponent<>))
                return;
            s_Set.Add(typeIndex);
        }

        internal static void Clear() => s_Set.Clear();

        public static bool Contains(TypeIndex typeIndex) => s_Set.Contains(typeIndex);
    }
}
