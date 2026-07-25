#if !UNITY_DISABLE_MANAGED_COMPONENTS

using System;
using NUnit.Framework;
using Unity.Burst;
using UnityEngine;
using static Unity.Entities.SystemAPI;
#pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
using static Unity.Entities.SystemAPI.ManagedAPI;
#pragma warning restore 0618
namespace Unity.Entities.Tests.TestSystemAPI
{
    /// <summary>
    /// Make sure this matches <see cref="TestSystemBaseManaged"/>.
    /// </summary>
    [TestFixture]
    public class TestISystemManaged : ECSTestsFixture
    {
        [SetUp]
        public void SetUp() {
            World.CreateSystem<TestISystemManagedSystem>();
        }

        unsafe ref TestISystemManagedSystem GetTestSystemUnsafe()
        {
            var systemHandle = World.GetExistingSystem<TestISystemManagedSystem>();
            if (systemHandle == default)
                throw new InvalidOperationException("This system does not exist any more");
            return ref World.Unmanaged.GetUnsafeSystemRef<TestISystemManagedSystem>(systemHandle);
        }

        unsafe ref SystemState GetSystemStateRef()
        {
            var systemHandle = World.GetExistingSystem<TestISystemManagedSystem>();
            var statePtr = World.Unmanaged.ResolveSystemState(systemHandle);
            if (statePtr == null)
                throw new InvalidOperationException("No system state exists any more for this SystemRef");
            return ref *statePtr;
        }

        #region Query Access
        [Test]
        public void Query([Values] SystemAPIAccess access, [Values(1,2)] int queryArgumentCount) => GetTestSystemUnsafe().QuerySetup(ref GetSystemStateRef(), queryArgumentCount, access);
        #endregion

        #region Component Access

        [Test]
        public void GetComponent([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponent(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void TryGetComponent([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestTryGetComponent(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void HasComponent([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestHasComponent(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void GetComponentForSystem([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponentForSystem(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void HasComponentForSystem([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestHasComponentForSystem(ref GetSystemStateRef(), access, memberUnderneath);
        [Test]
        public void IsComponentEnabled() => GetTestSystemUnsafe().TestIsComponentEnabled(ref GetSystemStateRef());
        [Test]
        public void SetComponentEnabled() => GetTestSystemUnsafe().TestSetComponentEnabled(ref GetSystemStateRef());
        [Test]
        public void IsComponentEnabledForSystem() => GetTestSystemUnsafe().TestIsComponentEnabledForSystem(ref GetSystemStateRef());
        [Test]
        public void SetComponentEnabledForSystem() => GetTestSystemUnsafe().TestSetComponentEnabledForSystem(ref GetSystemStateRef());

        #endregion

        #region Singleton

        [Test]
        public void GetSingleton([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingleton(ref GetSystemStateRef(), access);
        [Test]
        public void GetSingletonWithSystemEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonWithSystemEntity(ref GetSystemStateRef(), access);
        [Test]
        public void TryGetSingleton([Values] SystemAPIAccess access, [Values] TypeArgumentExplicit typeArgumentExplicit) => GetTestSystemUnsafe().TestTryGetSingleton(ref GetSystemStateRef(), access, typeArgumentExplicit);
        [Test]
        public void GetSingletonEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonEntity(ref GetSystemStateRef(), access);
        [Test]
        public void TryGetSingletonEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestTryGetSingletonEntity(ref GetSystemStateRef(), access);
        [Test]
        public void HasSingleton([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestHasSingleton(ref GetSystemStateRef(), access);
        #endregion
    }

    partial struct TestISystemManagedSystem : ISystem
    {
        #region Query Access
        [BurstCompile]
        public void QuerySetup(ref SystemState state, int queryArgumentCount, SystemAPIAccess access)
        {
            for (var i = 1; i <= 10; i++)
            {
                var e = state.EntityManager.CreateEntity();
                var t = new GameObject().transform;
                t.position = new Vector3(i,i,i);
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                state.EntityManager.AddComponentObject(e, t);
                state.EntityManager.AddComponentObject(e, new EcsTestManagedComponent{value=i.ToString()});
                #pragma warning restore 0618
            }

            Assert.AreEqual(55*queryArgumentCount, queryArgumentCount switch
            {
                1 => Query1(ref state, access),
                2 => Query2(ref state, access),
                _ => throw new ArgumentOutOfRangeException(nameof(queryArgumentCount), queryArgumentCount, null)
            });
        }

        [BurstCompile]
        int Query1(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    foreach (var transformRef in SystemAPI.Query<ManagedAPI.UnityEngineComponent<Transform>>())
                    #pragma warning restore 0618
                        sum += (int) transformRef.Value.position.x;
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    foreach (var transformRef in Query<ManagedAPI.UnityEngineComponent<Transform>>())
                    #pragma warning restore 0618
                        sum += (int) transformRef.Value.position.x;
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query2(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    foreach (var (transformRef, dataRef) in SystemAPI.Query<ManagedAPI.UnityEngineComponent<Transform>, EcsTestManagedComponent>())
                    #pragma warning restore 0618
                    {
                        sum += (int)transformRef.Value.position.x;
                        sum += int.Parse(dataRef.value);
                    }
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    foreach (var (transformRef, dataRef) in Query<ManagedAPI.UnityEngineComponent<Transform>, EcsTestManagedComponent>())
                    #pragma warning restore 0618
                    {
                        sum += (int)transformRef.Value.position.x;
                        sum += int.Parse(dataRef.value);
                    }
                    break;
            }
            return sum;
        }
        #endregion

        #region Component Access
        [BurstCompile]
        public void TestGetComponent(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity();
            var t = new GameObject().transform;
            t.position = new Vector3(0, 2, 0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(e, t);
            #pragma warning restore 0618

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.GetComponent<Transform>(e).position, Is.EqualTo(t.position));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.GetComponent<Transform>(e).position, Is.EqualTo(t.position));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.GetComponent<Transform>(e), Is.EqualTo(t));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.GetComponent<Transform>(e), Is.EqualTo(t));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestTryGetComponent(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            var e = state.EntityManager.CreateEntity();
            var t = new GameObject().transform;
            t.position = new Vector3(0, 2, 0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(e, t);
            #pragma warning restore 0618

            try
            {
                Transform foundTransform = null;
                switch (memberUnderneath)
                {
                    case MemberUnderneath.WithMemberUnderneath:
                        switch (access)
                        {
                            case SystemAPIAccess.SystemAPI:
                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(SystemAPI.ManagedAPI.TryGetComponent<Transform>(e, out foundTransform),
                                #pragma warning restore 0618
                                    Is.True);
                                Assert.That(foundTransform.position, Is.EqualTo(t.position));

                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(SystemAPI.ManagedAPI.TryGetComponent(e, out foundTransform), Is.True);
                                #pragma warning restore 0618
                                Assert.That(foundTransform.position, Is.EqualTo(t.position));
                                break;
                            case SystemAPIAccess.Using:
                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(ManagedAPI.TryGetComponent<Transform>(e, out foundTransform), Is.True);
                                #pragma warning restore 0618
                                Assert.That(foundTransform.position, Is.EqualTo(t.position));

                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(ManagedAPI.TryGetComponent(e, out foundTransform), Is.True);
                                #pragma warning restore 0618
                                Assert.That(foundTransform.position, Is.EqualTo(t.position));
                                break;
                        }

                        break;
                    case MemberUnderneath.WithoutMemberUnderneath:
                        switch (access)
                        {
                            case SystemAPIAccess.SystemAPI:
                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(SystemAPI.ManagedAPI.TryGetComponent<Transform>(e, out foundTransform),
                                #pragma warning restore 0618
                                    Is.True);
                                Assert.That(foundTransform, Is.EqualTo(t));

                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(SystemAPI.ManagedAPI.TryGetComponent(e, out foundTransform), Is.True);
                                #pragma warning restore 0618
                                Assert.That(foundTransform, Is.EqualTo(t));
                                break;
                            case SystemAPIAccess.Using:
                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(ManagedAPI.TryGetComponent<Transform>(e, out foundTransform), Is.True);
                                #pragma warning restore 0618
                                Assert.That(foundTransform, Is.EqualTo(t));

                                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                                Assert.That(ManagedAPI.TryGetComponent(e, out foundTransform), Is.True);
                                #pragma warning restore 0618
                                Assert.That(foundTransform, Is.EqualTo(t));
                                break;
                        }

                        break;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        [BurstCompile]
        public void TestHasComponent(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity(typeof(EcsTestManagedComponent));

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<EcsTestManagedComponent>(e).GetHashCode(), Is.EqualTo(1));
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<Transform>(e).GetHashCode(), Is.EqualTo(0));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.HasComponent<EcsTestManagedComponent>(e).GetHashCode(), Is.EqualTo(1));
                            Assert.That(ManagedAPI.HasComponent<Transform>(e).GetHashCode(), Is.EqualTo(0));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<EcsTestManagedComponent>(e), Is.EqualTo(true));
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<Transform>(e), Is.EqualTo(false));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.HasComponent<EcsTestManagedComponent>(e), Is.EqualTo(true));
                            Assert.That(ManagedAPI.HasComponent<Transform>(e), Is.EqualTo(false));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestIsComponentEnabled(ref SystemState state)
        {
            var e = state.EntityManager.CreateEntity();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(e, new EcsTestManagedComponentEnableable());
            Assert.That(ManagedAPI.IsComponentEnabled<EcsTestManagedComponentEnableable>(e), Is.EqualTo(true));
            #pragma warning restore 0618
        }

        [BurstCompile]
        public void TestSetComponentEnabled(ref SystemState state)
        {
            var e = state.EntityManager.CreateEntity();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(e, new EcsTestManagedComponentEnableable());
            SetComponentEnabled<EcsTestManagedComponentEnableable>(e, false);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsTestManagedComponentEnableable>(e), Is.EqualTo(false));
            ManagedAPI.SetComponentEnabled<EcsTestManagedComponentEnableable>(e, true);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsTestManagedComponentEnableable>(e), Is.EqualTo(true));
            #pragma warning restore 0618
        }

        [BurstCompile]
        public void TestGetComponentForSystem(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            var t = new GameObject().transform;
            t.position = new Vector3(0, 2, 0);
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(state.SystemHandle, t);
            #pragma warning restore 0618

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.GetComponent<Transform>(state.SystemHandle).position, Is.EqualTo(t.position));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.GetComponent<Transform>(state.SystemHandle).position, Is.EqualTo(t.position));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.GetComponent<Transform>(state.SystemHandle), Is.EqualTo(t));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.GetComponent<Transform>(state.SystemHandle), Is.EqualTo(t));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestHasComponentForSystem(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestManagedComponent>());

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<EcsTestManagedComponent>(state.SystemHandle).GetHashCode(), Is.EqualTo(1));
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<Transform>(state.SystemHandle).GetHashCode(), Is.EqualTo(0));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.HasComponent<EcsTestManagedComponent>(state.SystemHandle).GetHashCode(), Is.EqualTo(1));
                            Assert.That(ManagedAPI.HasComponent<Transform>(state.SystemHandle).GetHashCode(), Is.EqualTo(0));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<EcsTestManagedComponent>(state.SystemHandle), Is.EqualTo(true));
                            Assert.That(SystemAPI.ManagedAPI.HasComponent<Transform>(state.SystemHandle), Is.EqualTo(false));
                            #pragma warning restore 0618
                            break;
                        case SystemAPIAccess.Using:
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            Assert.That(ManagedAPI.HasComponent<EcsTestManagedComponent>(state.SystemHandle), Is.EqualTo(true));
                            Assert.That(ManagedAPI.HasComponent<Transform>(state.SystemHandle), Is.EqualTo(false));
                            #pragma warning restore 0618
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestIsComponentEnabledForSystem(ref SystemState state)
        {
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(state.SystemHandle, new EcsTestManagedComponentEnableable());
            Assert.That(ManagedAPI.IsComponentEnabled<EcsTestManagedComponentEnableable>(state.SystemHandle), Is.EqualTo(true));
            #pragma warning restore 0618
        }

        [BurstCompile]
        public void TestSetComponentEnabledForSystem(ref SystemState state)
        {
            state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestManagedComponentEnableable>());
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            ManagedAPI.SetComponentEnabled<EcsTestManagedComponentEnableable>(state.SystemHandle, false);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsTestManagedComponentEnableable>(state.SystemHandle.m_Entity), Is.EqualTo(false));
            ManagedAPI.SetComponentEnabled<EcsTestManagedComponentEnableable>(state.SystemHandle, true);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsTestManagedComponentEnableable>(state.SystemHandle.m_Entity), Is.EqualTo(true));
            #pragma warning restore 0618
        }
        #endregion

        #region Singleton Access
        [BurstCompile]
        public void TestGetSingleton(ref SystemState state, SystemAPIAccess access)
        {
            var e = state.EntityManager.CreateEntity();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentData(e, new EcsTestManagedComponent{value = "cake"});
            #pragma warning restore 0618
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.AreEqual(SystemAPI.ManagedAPI.GetSingleton<EcsTestManagedComponent>().value, "cake");
                    #pragma warning restore 0618
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.AreEqual(ManagedAPI.GetSingleton<EcsTestManagedComponent>().value, "cake");
                    #pragma warning restore 0618
                    break;
            }
        }

        [BurstCompile]
        public void TestGetSingletonWithSystemEntity(ref SystemState state, SystemAPIAccess access)
        {
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentObject(state.SystemHandle, new EcsTestManagedComponent{value = "cake"});
            #pragma warning restore 0618
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.AreEqual(SystemAPI.ManagedAPI.GetSingleton<EcsTestManagedComponent>().value, "cake");
                    #pragma warning restore 0618
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.AreEqual(ManagedAPI.GetSingleton<EcsTestManagedComponent>().value, "cake");
                    #pragma warning restore 0618
                    break;
            }
        }

        [BurstCompile]
        public void TestTryGetSingleton(ref SystemState state, SystemAPIAccess access, TypeArgumentExplicit typeArgumentExplicit)
        {
            var e = state.EntityManager.CreateEntity();
            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
            state.EntityManager.AddComponentData(e, new EcsTestManagedComponent{value = "cake"});
            #pragma warning restore 0618

            switch (access)
            {
                case SystemAPIAccess.SystemAPI when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(SystemAPI.ManagedAPI.TryGetSingleton<EcsTestManagedComponent>(out var valSystemAPITypeArgumentShown));
                    #pragma warning restore 0618
                    Assert.AreEqual(valSystemAPITypeArgumentShown.value, "cake");
                    break;
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(SystemAPI.ManagedAPI.TryGetSingleton(out EcsTestManagedComponent valSystemAPITypeArgumentHidden));
                    #pragma warning restore 0618
                    Assert.AreEqual(valSystemAPITypeArgumentHidden.value, "cake");
                    break;
                case SystemAPIAccess.Using when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(ManagedAPI.TryGetSingleton<EcsTestManagedComponent>(out var valUsingTypeArgumentShown));
                    #pragma warning restore 0618
                    Assert.AreEqual(valUsingTypeArgumentShown.value, "cake");
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(ManagedAPI.TryGetSingleton(out EcsTestManagedComponent valUsingTypeArgumentHidden));
                    #pragma warning restore 0618
                    Assert.AreEqual(valUsingTypeArgumentHidden.value, "cake");
                    break;
            }
        }

        [BurstCompile]
        public void TestGetSingletonEntity(ref SystemState state, SystemAPIAccess access)
        {
            var e1 = state.EntityManager.CreateEntity(typeof(EcsTestManagedComponent));
            var e2 = Entity.Null;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    e2 = SystemAPI.ManagedAPI.GetSingletonEntity<EcsTestManagedComponent>();
                    #pragma warning restore 0618
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    e2 = ManagedAPI.GetSingletonEntity<EcsTestManagedComponent>();
                    #pragma warning restore 0618
                    break;
            }
            Assert.AreEqual(e1, e2);
        }

        [BurstCompile]
        public void TestTryGetSingletonEntity(ref SystemState state, SystemAPIAccess access)
        {
            var e1 = state.EntityManager.CreateEntity(typeof(EcsTestManagedComponent));
            var e2 = Entity.Null;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(SystemAPI.ManagedAPI.TryGetSingletonEntity<EcsTestManagedComponent>(out e2));
                    #pragma warning restore 0618
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(ManagedAPI.TryGetSingletonEntity<EcsTestManagedComponent>(out e2));
                    #pragma warning restore 0618
                    break;
            }
            Assert.AreEqual(e1, e2);
        }

        [BurstCompile]
        public void TestHasSingleton(ref SystemState state, SystemAPIAccess access)
        {
            state.EntityManager.CreateEntity(typeof(EcsTestManagedComponent));

            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(SystemAPI.ManagedAPI.HasSingleton<EcsTestManagedComponent>());
                    #pragma warning restore 0618
                    break;
                case SystemAPIAccess.Using:
                    #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                    Assert.True(ManagedAPI.HasSingleton<EcsTestManagedComponent>());
                    #pragma warning restore 0618
                    break;
            }
        }
        #endregion
    }
}
#endif
