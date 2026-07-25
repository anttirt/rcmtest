using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class SharedComponentDataWithUnityEngineObject : ECSTestsFixture
    {
        #pragma warning disable EA0017 // intentionally managed shared components
        struct CorrectHashCode : ISharedComponentData , IEquatable<CorrectHashCode>
        {
            public UnityEngine.Object Target;

            public bool Equals(CorrectHashCode other)
            {
                return Target == other.Target;
            }

            public override int GetHashCode()
            {
                return ReferenceEquals(Target, null) ? 0 : Target.GetHashCode();
            }
        }

        struct IncorrectHashCode : ISharedComponentData, IEquatable<IncorrectHashCode>
        {
            public UnityEngine.Object Target;

            public bool Equals(IncorrectHashCode other)
            {
                return Target == other.Target;
            }

            // Target == null can not be used because destroying the object will result in a different hashcode
            public override int GetHashCode()
            {
                return Target == null ? 0 : Target.GetHashCode();
            }
        }
        #pragma warning restore EA0017


        // https://github.com/Unity-Technologies/dots/issues/1813
        [Test]
        public void CorrectlyImplementedHashCodeWorksWithDestroy()
        {
            var e = m_Manager.CreateEntity();
            var obj = new TextAsset();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddSharedComponentManaged(e, new CorrectHashCode { Target = obj });
            #pragma warning restore 0618
            UnityEngine.Object.DestroyImmediate(obj);
            m_Manager.DestroyEntity(e);
        }

        [Test]
        public void IncorrectlyImplementedHashWorksWithDestroy()
        {
            var e = m_Manager.CreateEntity();
            var obj = new TextAsset();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddSharedComponentManaged(e, new IncorrectHashCode { Target = obj });
            #pragma warning restore 0618
            UnityEngine.Object.DestroyImmediate(obj);

            m_Manager.DestroyEntity(e);
            m_Manager.Debug.CheckInternalConsistency();

            Assert.IsTrue(m_Manager.Debug.IsSharedComponentManagerEmpty());
        }

        [Test]
        public void CorrectlyImplementedHashCodeWorksWithFilters()
        {
            var e = m_Manager.CreateEntity();
            var obj = new TextAsset();
            var sharedComponent = new CorrectHashCode {Target = obj};
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddSharedComponentManaged(e, sharedComponent);
            #pragma warning restore 0618
            UnityEngine.Object.DestroyImmediate(obj);

            var query = m_Manager.CreateEntityQuery(typeof(CorrectHashCode));

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            query.SetSharedComponentFilterManaged(sharedComponent);
            #pragma warning restore 0618
            Assert.AreEqual(0, query.CalculateEntityCount());
        }

        [Test]
        public void IncorrectlyImplementedHashCodeDoesntWorksWithFilters()
        {
            var e = m_Manager.CreateEntity();
            var obj = new TextAsset();
            var sharedComponent = new IncorrectHashCode {Target = obj};
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            m_Manager.AddSharedComponentManaged(e, sharedComponent);
            #pragma warning restore 0618
            UnityEngine.Object.DestroyImmediate(obj);

            var query = m_Manager.CreateEntityQuery(typeof(IncorrectHashCode));

            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            query.SetSharedComponentFilterManaged(sharedComponent);
            #pragma warning restore 0618
            Assert.AreEqual(0, query.CalculateEntityCount());
        }
    }
}
