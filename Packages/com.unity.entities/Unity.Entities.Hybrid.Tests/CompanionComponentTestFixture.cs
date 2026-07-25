using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests
{
    public class CompanionComponentTestFixture
    {
        protected World m_PreviousWorld;
        protected World m_World;
        protected EntityManager m_Manager;

        protected List<GameObject> m_GameObjects = new List<GameObject>();

        protected GameObject CreateGameObject()
        {
            var go = new GameObject();
            m_GameObjects.Add(go);
            return go;
        }

        /// <summary>
        /// Fetches a live companion component for <paramref name="entity"/> via all three readers
        /// and asserts they return the same instance: the GameObject path via <see cref="CompanionLink"/>,
        /// the managed entity-store reader, and the unmanaged <see cref="CompanionComponent{T}"/> mirror.
        /// Use at every test site that reads a companion component, so any drift between the managed
        /// slot and the unmanaged mirror surfaces immediately.
        /// </summary>
        public static T AssertCompanionReadersAgree<T>(EntityManager em, Entity entity) where T : Component
        {
            var go = em.GetComponentData<CompanionLink>(entity).Companion.Value;
            Assert.IsFalse(go == null, "CompanionLink resolves to a null/destroyed GameObject");
            var viaLink = go.GetComponent<T>();
            #pragma warning disable 0618 // managed slot is one of the APIs under test.
            var viaManaged = em.GetComponentObject<T>(entity);
            #pragma warning restore 0618
            var viaUnmanaged = em.GetCompanion<T>(entity);
            Assert.IsNotNull(viaLink, $"CompanionLink GameObject has no {typeof(T).Name} component");
            Assert.AreSame(viaLink, viaManaged,
                $"managed-slot {nameof(EntityManager.GetComponentObject)}<{typeof(T).Name}> disagrees with the CompanionLink path");
            Assert.AreSame(viaLink, viaUnmanaged,
                $"unmanaged {nameof(CompanionComponentExtensions.GetCompanion)}<{typeof(T).Name}> disagrees with the CompanionLink path");
            return viaLink;
        }

        [SetUp]
        virtual public void Setup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = TestWorldSetup.CreateEntityWorld("Test World", false);
            World.DefaultGameObjectInjectionWorld = m_World;
            m_Manager = m_World.EntityManager;
        }

        [TearDown]
        virtual public void TearDown()
        {
            if (m_World.IsCreated)
            {
                m_World.Dispose();
                m_World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }

            foreach (var go in m_GameObjects)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
