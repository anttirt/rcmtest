using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Entities.Tests
{
    // These tests cover the behavior of Companion Components when no Companion GameObject is involved,
    // so when instantiation is expected to share references instead of cloning the components.
    // The tests related to instantiation with a Companion GameObject are in CompanionComponentConversionTests.cs
    class InstantiateCompanionComponentTests : ECSTestsFixture
    {
        interface ISimpleValue
        {
            int Value { get; set; }
        }

        class EcsTestUnityObject : UnityEngine.Object, ISimpleValue
        {
            public int Value { get; set; }
        }

        class EcsTestMonoBehaviour : MonoBehaviour, ISimpleValue
        {
            public int Value { get; set; }
        }

        void InstantiateReferenceComponent<T>(T component) where T : ISimpleValue
        {
            var entity = m_Manager.CreateEntity();

            component.Value = 123;
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddComponentObject(entity, component);
            Assert.AreEqual(123, m_Manager.GetComponentObject<T>(entity).Value);
            #pragma warning restore 0618

            // Check that we store a reference by changing the value and compare it the one stored in the component
            component.Value = 234;
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(234, m_Manager.GetComponentObject<T>(entity).Value);
            #pragma warning restore 0618

            // The expected behavior is both the initial entity and the instance share the same object
            var instance = m_Manager.Instantiate(entity);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(234, m_Manager.GetComponentObject<T>(instance).Value);
            #pragma warning restore 0618

            // Change the value of the initial ComponentObject and check both instances have this new value
            component.Value = 456;
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreEqual(456, m_Manager.GetComponentObject<T>(entity).Value);
            Assert.AreEqual(456, m_Manager.GetComponentObject<T>(instance).Value);
            #pragma warning restore 0618

            // Must be the same object
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            Assert.AreSame(m_Manager.GetComponentObject<T>(entity), m_Manager.GetComponentObject<T>(instance));
            #pragma warning restore 0618
        }

        [Test]
        public void InstantiateUnityObjectAsReferenceComponent()
        {
            InstantiateReferenceComponent(new EcsTestUnityObject());
        }

        [Test]
        public void InstantiateMonoBehaviourAsReferenceComponent()
        {
            var gameObject = new GameObject();
            InstantiateReferenceComponent(gameObject.AddComponent<EcsTestMonoBehaviour>());
            GameObject.DestroyImmediate(gameObject);
        }
    }
}
