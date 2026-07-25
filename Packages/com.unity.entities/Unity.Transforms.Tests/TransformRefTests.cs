//#define TRANSFORMREF_ECB_SUPPORTED
//#define TRANSFORMREF_IJOBENTITY_SUPPORTED

#if ENABLE_TRANSFORMREF
using Unity.Mathematics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine.TestTools.Utils;
using UnityEngine;

namespace Unity.Transforms.Tests
{
    class Float3EqualityComparer : IEqualityComparer<float3>
    {
        const float k_DefaultError = 0.0001f;
        readonly float AllowedError;

        static readonly Float3EqualityComparer m_Instance = new Float3EqualityComparer();

        public static Float3EqualityComparer Instance { get { return m_Instance; } }

        Float3EqualityComparer() : this(k_DefaultError) {}

        public Float3EqualityComparer(float allowedError)
        {
            AllowedError = allowedError;
        }

        public bool Equals(float3 expected, float3 actual)
        {
            return Utils.AreFloatsEqual(expected.x, actual.x, AllowedError) &&
                Utils.AreFloatsEqual(expected.y, actual.y, AllowedError) &&
                Utils.AreFloatsEqual(expected.z, actual.z, AllowedError);
        }

        public int GetHashCode(float3 vec3)
        {
            return 0;
        }
    }

    class Float4EqualityComparer : IEqualityComparer<float4>
    {
        const float k_DefaultError = 0.0001f;
        readonly float AllowedError;

        static readonly Float4EqualityComparer m_Instance = new Float4EqualityComparer();

        public static Float4EqualityComparer Instance { get { return m_Instance; } }

        Float4EqualityComparer() : this(k_DefaultError) {}

        public Float4EqualityComparer(float allowedError)
        {
            AllowedError = allowedError;
        }

        public bool Equals(float4 expected, float4 actual)
        {
            return Utils.AreFloatsEqual(expected.x, actual.x, AllowedError) &&
                   Utils.AreFloatsEqual(expected.y, actual.y, AllowedError) &&
                   Utils.AreFloatsEqual(expected.z, actual.z, AllowedError) &&
                   Utils.AreFloatsEqual(expected.w, actual.w, AllowedError);
        }

        public int GetHashCode(float4 vec4)
        {
            return 0;
        }
    }

    class MathQuaternionEqualityComparer : IEqualityComparer<quaternion>
    {
        const float k_DefaultError = 0.00001f;
        readonly float AllowedError;

        static readonly MathQuaternionEqualityComparer m_Instance = new MathQuaternionEqualityComparer();

        public static MathQuaternionEqualityComparer Instance { get { return m_Instance; } }

        MathQuaternionEqualityComparer() : this(k_DefaultError) {}

        public MathQuaternionEqualityComparer(float allowedError)
        {
            AllowedError = allowedError;
        }

        public bool Equals(quaternion expected, quaternion actual)
        {
            if (math.lengthsq(expected) < AllowedError)
            {
                // Expected quaternion is probably default (0,0,0,0), so just compare component-wise
                return Utils.AreFloatsEqual(expected.value.x, actual.value.x, AllowedError) &&
                       Utils.AreFloatsEqual(expected.value.y, actual.value.y, AllowedError) &&
                       Utils.AreFloatsEqual(expected.value.z, actual.value.z, AllowedError) &&
                       Utils.AreFloatsEqual(expected.value.w, actual.value.w, AllowedError);
            }

            return Mathf.Abs(math.dot(expected, actual)) > (1.0f - AllowedError);
        }

        public int GetHashCode(quaternion quaternion)
        {
            return 0;
        }
    }

    /// <summary>
    /// Tests for TransformRef functionality using pure ECS entities (no GameObjects).
    /// These tests verify that TransformRef works with entities created via EntityManager.CreateEntity()
    /// and demonstrates parenting operations, hierarchy management, and job system integration.
    /// </summary>
    partial class TransformRefTests : ECSTestsFixture
    {
        [Test]
        public void AddDefaultTransform_Works()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity);

            var transformRef = m_Manager.GetTransformRef(entity);

            Assert.That(transformRef.LocalPosition, Is.EqualTo(float3.zero).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(quaternion.identity).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(new float3(1f, 1f, 1f)).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void SetTransformRW_Works()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);

            transformRef.LocalPosition = new float3(1f, 2f, 3f);
            transformRef.LocalRotation = quaternion.RotateX(math.PI);
            transformRef.LocalScale = new float3(2f, 2f, 2f);

            TransformRef transformRef2 = m_Manager.GetTransformRef(entity);

            Assert.That(transformRef2.LocalPosition, Is.EqualTo(new float3(1f, 2f, 3f)).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef2.LocalRotation, Is.EqualTo(new quaternion(1f, 0f, 0f, 0f)).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef2.LocalScale, Is.EqualTo(new float3(2f,2f,2f)).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks]
        public void SetTransformRO_DoesntWriteBack()
        {
            var entity = m_Manager.CreateEntity();
            var transformRefRW = m_Manager.AddTransform(entity,
                new float3(1f, 2f, 3f),
                quaternion.RotateX(math.PIHALF),
                new float3(2f, 2f, 2f));

            var transformRefRO = m_Manager.GetTransformRef(entity, isReadOnly:true);

            // Attempting to write should throw
            Assert.Throws<InvalidOperationException>(() => { transformRefRO.LocalPosition = new float3(2f, 4f, 6f); });
            Assert.Throws<InvalidOperationException>(() => { transformRefRO.LocalRotation = quaternion.RotateX(math.PIHALF * 0.5f); });
            Assert.Throws<InvalidOperationException>(() => { transformRefRO.LocalScale = new float3(4f, 4f, 4f); });

            // Original values should remain
            var transformRefRO2 = m_Manager.GetTransformRef(entity, isReadOnly:true);

            Assert.That(transformRefRO2.LocalPosition, Is.EqualTo(new float3(1f, 2f, 3f)).Using(Float3EqualityComparer.Instance));
            // Use Float4EqualityComparer because this is an invalid quaternion
            Assert.That(transformRefRO2.LocalRotation, Is.EqualTo(quaternion.RotateX(math.PIHALF)).Using(Float4EqualityComparer.Instance));
            Assert.That(transformRefRO2.LocalScale, Is.EqualTo(new float3(2f, 2f, 2f)).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void TransformRef_StructuralChanges_InvalidatesExistingRefs()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent, new float3(1, 2, 3));
            {
                var parentTransform = m_Manager.GetTransformRef(parent);
                // Creating another entity with a TransformRef invalidates any existing TransformRef instances
                var childTransform = m_Manager.AddTransform(child, new float3(4, 5, 6));
                Assert.That(() => { _ = parentTransform.LocalPosition; },
                    Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains("Attempted to access TransformTypeHandle which has been invalidated by a structural change"));
            }
            {
                var childTransform = m_Manager.AddTransform(child, new float3(4, 5, 6));
                var parentTransform = m_Manager.GetTransformRef(parent);
                // Modifying hierarchies invalidates existing transformRefs
                m_Manager.SetParent(child, parent, preserveWorldTransform: false);
                Assert.That(() => { _ = parentTransform.LocalPosition; },
                    Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains("Attempted to access TransformTypeHandle which has been invalidated by a structural change"));
                Assert.That(() => { _ = childTransform.LocalPosition; },
                    Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains("Attempted to access TransformTypeHandle which has been invalidated by a structural change"));
            }
        }

        public enum SetTransformTestMode
        {
            EntityManager = 1,
#if TRANSFORMREF_ECB_SUPPORTED
            EntityCommandBuffer = 2,
            EntityCommandBufferParallel = 3,
#endif
        }

        [Test]
        public void TransformRef_AddTransform_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();

            float3 expectedPosition = float3.zero;
            quaternion expectedRotation = quaternion.identity;
            float3 expectedScale = new float3(1f, 1f, 1f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    m_Manager.AddTransform(entity);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.AddTransform(entity);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.AddTransform(17, entity);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_AddTransformT_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();

            float3 expectedPosition = new float3(11f, 12f, 13f);
            quaternion expectedRotation = quaternion.identity;
            float3 expectedScale = new float3(1f, 1f, 1f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    m_Manager.AddTransform(entity, expectedPosition);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.AddTransform(entity, expectedPosition);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.AddTransform(17, entity, expectedPosition);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_AddTransformTR_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();

            float3 expectedPosition = new float3(11f, 12f, 13f);
            quaternion expectedRotation = quaternion.RotateX(math.PIHALF);
            float3 expectedScale = new float3(1f, 1f, 1f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    m_Manager.AddTransform(entity, expectedPosition, expectedRotation);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.AddTransform(entity, expectedPosition, expectedRotation);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.AddTransform(17, entity, expectedPosition, expectedRotation);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_AddTransformTRS_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();

            float3 expectedPosition = new float3(11f, 12f, 13f);
            quaternion expectedRotation = quaternion.identity;
            float3 expectedScale = new float3(2f, 3f, 4f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    m_Manager.AddTransform(entity, expectedPosition, expectedRotation, expectedScale);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.AddTransform(entity, expectedPosition, expectedRotation, expectedScale);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.AddTransform(17, entity, expectedPosition, expectedRotation, expectedScale);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetLocalPosition_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            float3 expectedPosition = new float3(11f, 12f, 13f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(entity);
                    tr.LocalPosition = expectedPosition;
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetLocalPosition(entity, expectedPosition);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetLocalPosition(17, entity, expectedPosition);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(originalRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetLocalRotation_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            quaternion expectedRotation = quaternion.RotateY(math.PI);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(entity);
                    tr.LocalRotation = expectedRotation;
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetLocalRotation(entity, expectedRotation);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetLocalRotation(17, entity, expectedRotation);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(originalPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetLocalScale_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            float3 expectedScale = new float3(-1, -2, -3);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(entity);
                    tr.LocalScale = expectedScale;
                    break;
#if TRANSFORMREF_ECB_SUPPORTED

                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetLocalScale(entity, expectedScale);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetLocalScale(17, entity, expectedScale);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(originalPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(originalRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_GetLocalPositionAndRotation_Works()
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            float3 expectedPosition = new float3(1f, 2f, 3f);
            quaternion expectedRotation = quaternion.RotateX(math.PI);

            transformRef.LocalPosition = expectedPosition;
            transformRef.LocalRotation = expectedRotation;

            transformRef.GetLocalPositionAndRotation(out float3 outPosition, out quaternion outRotation);
            Assert.That(outPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(outRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetLocalPositionAndRotation_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            float3 expectedPosition = new float3(1f, 2f, 3f);
            quaternion expectedRotation = quaternion.RotateX(math.PI);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(entity);
                    tr.SetLocalPositionAndRotation(expectedPosition, expectedRotation);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetLocalPositionAndRotation(entity, expectedPosition, expectedRotation);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetLocalPositionAndRotation(17, entity, expectedPosition, expectedRotation);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetLocalTransform_TRS_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            float3 expectedPosition = new float3(1f, 2f, 3f);
            quaternion expectedRotation = quaternion.RotateX(math.PI);
            float3 expectedScale = new float3(4f, 5f, 6f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(entity);
                    tr.SetLocalTransform(expectedPosition, expectedRotation, expectedScale);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetLocalTransform(entity, expectedPosition, expectedRotation, expectedScale);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetLocalTransform(17, entity, expectedPosition, expectedRotation, expectedScale);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetLocalTransform_Matrix_Works([Values] SetTransformTestMode testMode)
        {
            var entity = m_Manager.CreateEntity();
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PIHALF);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(entity, originalPosition, originalRotation, originalScale);

            float3 expectedPosition = new float3(1f, 2f, 3f);
            quaternion expectedRotation = quaternion.RotateX(math.PI);
            float3 expectedScale = new float3(4f, 5f, 6f);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(entity);
                    tr.SetLocalTransform(float4x4.TRS(expectedPosition, expectedRotation, expectedScale));
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetLocalTransform(entity, float4x4.TRS(expectedPosition, expectedRotation, expectedScale));
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetLocalTransform(17, entity, float4x4.TRS(expectedPosition, expectedRotation, expectedScale));
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            TransformRef transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(expectedPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(transformRef.LocalRotation, Is.EqualTo(expectedRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(transformRef.LocalScale, Is.EqualTo(expectedScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_GetWorldPositionAndRotation_Works()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent, new float3(1,2,3), quaternion.RotateX(math.PIHALF));
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PI);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(child, originalPosition, originalRotation, originalScale);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var childTransform = m_Manager.GetTransformRef(child);

            var childLTW = childTransform.ComputeLocalToWorld();
            math.decompose(new AffineTransform(childLTW), out float3 expectedWorldPosition, out quaternion expectedWorldRotation, out float3 _);

            childTransform.GetWorldPositionAndRotation(out float3 worldPosition, out quaternion worldRotation);
            Assert.That(worldPosition, Is.EqualTo(expectedWorldPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(worldRotation, Is.EqualTo(expectedWorldRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(childTransform.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_SetWorldPositionAndRotation_Works([Values] SetTransformTestMode testMode)
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent, new float3(1, 2, 3), quaternion.RotateX(math.PIHALF));
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PI);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(child, originalPosition, originalRotation, originalScale);
            m_Manager.SetParent(child, parent, preserveWorldTransform: false);
            var expectedWorldPosition = new float3(4, 5, 6);
            var expectedWorldRotation = quaternion.RotateZ(-math.PI2);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var ecbp = ecb.AsParallelWriter();
            switch (testMode)
            {
                case SetTransformTestMode.EntityManager:
                    TransformRef tr = m_Manager.GetTransformRef(child);
                    tr.SetWorldPositionAndRotation(expectedWorldPosition, expectedWorldRotation);
                    break;
#if TRANSFORMREF_ECB_SUPPORTED
                case SetTransformTestMode.EntityCommandBuffer:
                    ecb.SetWorldPositionAndRotation(child, expectedWorldPosition, expectedWorldRotation);
                    break;
                case SetTransformTestMode.EntityCommandBufferParallel:
                    ecbp.SetWorldPositionAndRotation(17, child, expectedWorldPosition, expectedWorldRotation);
                    break;
#endif
            }
            ecb.Playback(m_Manager);

            var childTransform = m_Manager.GetTransformRef(child);
            var childLTW = childTransform.ComputeLocalToWorld();
            math.decompose(new AffineTransform(childLTW), out float3 worldPosition, out quaternion worldRotation, out float3 _);
            Assert.That(worldPosition, Is.EqualTo(expectedWorldPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(worldRotation, Is.EqualTo(expectedWorldRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(childTransform.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        unsafe public void SetParent_Works()
        {
            var entity1 = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();

            m_Manager.AddTransform(entity1);
            m_Manager.AddTransform(entity2, new float3(3f, 4f, 5f));

            m_Manager.SetParent(entity2, entity1, preserveWorldTransform:false);

            // SetParent invalidates existing transformrefs
            var parent = m_Manager.GetTransformRef(entity1);
            var child = m_Manager.GetTransformRef(entity2);

            // Bleeding implementation, but we need to ensure component is updated when it changes hierarchy
            Assert.AreEqual((IntPtr)parent.m_TransformUnion->_UnsafeTransformHierarchyPointer,
                (IntPtr)child.m_TransformUnion->_UnsafeTransformHierarchyPointer);

            Assert.That(parent.LocalPosition, Is.EqualTo(new float3(0f, 0f, 0f)).Using(Float3EqualityComparer.Instance));
            Assert.That(parent.LocalRotation, Is.EqualTo(quaternion.identity).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(parent.LocalScale, Is.EqualTo(new float3(1f, 1f, 1f)).Using(Float3EqualityComparer.Instance));

            // Verify child relationship via TransformRef
            Assert.AreEqual(1, parent.GetChildCount());
            Assert.AreEqual(entity2, parent.GetChild(0));

            Assert.That(child.LocalPosition, Is.EqualTo(new float3(3f, 4f, 5f)).Using(Float3EqualityComparer.Instance));
            Assert.That(child.LocalRotation, Is.EqualTo(quaternion.identity).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(child.LocalScale, Is.EqualTo(new float3(1f, 1f, 1f)).Using(Float3EqualityComparer.Instance));

            // Verify parent relationship via TransformRef
            Assert.AreEqual(entity1, child.GetParent());

            Assert.That(child.ComputeLocalToWorld().Translation(),
                Is.EqualTo(new float3(3f, 4f, 5f)).Using(Float3EqualityComparer.Instance));

            // Test hierarchy transformation
            parent.LocalPosition = new float3(1f, 2f, 3f);

            Assert.That(child.ComputeLocalToWorld().Translation(),
                Is.EqualTo(new float3(4f, 6f, 8f)).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        unsafe public void SetParent_PreserveWorldTransform_Works()
        {
            var entity1 = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();

            m_Manager.AddTransform(entity1, new float3(10f, 10f, 10f));
            m_Manager.AddTransform(entity2, new float3(3f, 4f, 5f));

            m_Manager.SetParent(entity2, entity1, preserveWorldTransform:true);

            var parent = m_Manager.GetTransformRef(entity1);
            var child = m_Manager.GetTransformRef(entity2);

            // Ensure component is updated when it changes hierarchy
            Assert.AreEqual((IntPtr)parent.m_TransformUnion->_UnsafeTransformHierarchyPointer,
                (IntPtr)child.m_TransformUnion->_UnsafeTransformHierarchyPointer);

            Assert.That(parent.LocalPosition, Is.EqualTo(new float3(10f, 10f, 10f)).Using(Float3EqualityComparer.Instance));
            Assert.That(parent.LocalRotation, Is.EqualTo(quaternion.identity).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(parent.LocalScale, Is.EqualTo(new float3(1f, 1f, 1f)).Using(Float3EqualityComparer.Instance));

            // Verify child relationship via TransformRef
            Assert.AreEqual(1, parent.GetChildCount());
            Assert.AreEqual(entity2, parent.GetChild(0));

            Assert.That(child.LocalPosition, Is.EqualTo(new float3(-7f, -6f, -5f)).Using(Float3EqualityComparer.Instance));
            Assert.That(child.LocalRotation, Is.EqualTo(quaternion.identity).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(child.LocalScale, Is.EqualTo(new float3(1f, 1f, 1f)).Using(Float3EqualityComparer.Instance));

            // Verify parent relationship via TransformRef
            Assert.AreEqual(entity1, child.GetParent());

            Assert.That(child.ComputeLocalToWorld().Translation(),
                Is.EqualTo(new float3(3f, 4f, 5f)).Using(Float3EqualityComparer.Instance));

            parent.LocalPosition = new float3(11f, 12f, 13f);

            // Parent moved, child should move in world-space by the same amount
            Assert.That(child.ComputeLocalToWorld().Translation(),
                Is.EqualTo(new float3(4f, 6f, 8f)).Using(Float3EqualityComparer.Instance));

            // Unparenting child should preserve world transform
            m_Manager.SetParent(entity2, Entity.Null, preserveWorldTransform:true);

            // SetParent invalidates existing transformrefs
            parent = m_Manager.GetTransformRef(entity1);
            child = m_Manager.GetTransformRef(entity2);

            Assert.That(child.LocalPosition, Is.EqualTo(new float3(4f, 6f, 8f)).Using(Float3EqualityComparer.Instance));
            Assert.That(child.LocalRotation, Is.EqualTo(quaternion.identity).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(child.LocalScale, Is.EqualTo(new float3(1f, 1f, 1f)).Using(Float3EqualityComparer.Instance));

            Assert.That(child.ComputeLocalToWorld().Translation(),
                Is.EqualTo(new float3(4f, 6f, 8f)).Using(Float3EqualityComparer.Instance));

            // Verify hierarchy was removed via TransformRef
            Assert.AreEqual(0, parent.GetChildCount());
            Assert.AreEqual(Entity.Null, child.GetParent());
        }

        [Test]
        public void TransformRef_GetParent_WithParent_ReturnsParentEntity()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var childRef = m_Manager.GetTransformRef(child);
            Assert.AreEqual(parent, childRef.GetParent());
        }

        [Test]
        public void TransformRef_GetParent_RootEntity_ReturnsEntityNull()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(Entity.Null, parentRef.GetParent());
        }

        [Test]
        public void TransformRef_GetParent_NoHierarchy_ReturnsEntityNull()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity);

            var transformRef = m_Manager.GetTransformRef(entity);
            Assert.AreEqual(Entity.Null, transformRef.GetParent());
        }

        [Test]
        public void TransformRef_GetChildCount_NoChildren_ReturnsZero()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity);

            var transformRef = m_Manager.GetTransformRef(entity);
            Assert.AreEqual(0, transformRef.GetChildCount());
        }

        [Test]
        public void TransformRef_GetChildCount_OneChild_ReturnsOne()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(1, parentRef.GetChildCount());
        }

        [Test]
        public void TransformRef_GetChildCount_MultipleChildren_ReturnsCorrectCount()
        {
            var parent = m_Manager.CreateEntity();
            var child1 = m_Manager.CreateEntity();
            var child2 = m_Manager.CreateEntity();
            var child3 = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child1);
            m_Manager.AddTransform(child2);
            m_Manager.AddTransform(child3);

            m_Manager.SetParent(child1, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child2, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child3, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(3, parentRef.GetChildCount());
        }

        [Test]
        public void TransformRef_GetChild_ValidIndex_ReturnsChildEntity()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(child, parentRef.GetChild(0));
        }

        [Test]
        public void TransformRef_GetChild_NegativeIndex_ReturnsEntityNull()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(Entity.Null, parentRef.GetChild(-1));
        }

        [Test]
        public void TransformRef_GetChild_IndexOutOfBounds_ReturnsEntityNull()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(Entity.Null, parentRef.GetChild(1));
            Assert.AreEqual(Entity.Null, parentRef.GetChild(100));
        }

        [Test]
        public void TransformRef_GetChild_NoHierarchy_ReturnsEntityNull()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity);

            var transformRef = m_Manager.GetTransformRef(entity);
            Assert.AreEqual(Entity.Null, transformRef.GetChild(0));
        }

        [Test]
        public void TransformRef_GetChildEntities_WithChildren_ReturnsNativeArray()
        {
            var parent = m_Manager.CreateEntity();
            var child1 = m_Manager.CreateEntity();
            var child2 = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child1);
            m_Manager.AddTransform(child2);

            m_Manager.SetParent(child1, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child2, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            using (var children = parentRef.GetChildEntities(Allocator.Temp))
            {
                Assert.AreEqual(2, children.Length);
                bool foundChild1 = false, foundChild2 = false;
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] == child1) foundChild1 = true;
                    if (children[i] == child2) foundChild2 = true;
                }
                Assert.IsTrue(foundChild1, "child1 not found in array");
                Assert.IsTrue(foundChild2, "child2 not found in array");
            }
        }

        [Test]
        public void TransformRef_GetChildEntities_NoChildren_ReturnsEmptyArray()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity);

            var transformRef = m_Manager.GetTransformRef(entity);
            using (var children = transformRef.GetChildEntities(Allocator.Temp))
            {
                Assert.AreEqual(0, children.Length);
            }
        }

        [Test]
        public void EntityManager_SetParent_EstablishesParentChildRelationship()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            var childRef = m_Manager.GetTransformRef(child);

            Assert.AreEqual(parent, childRef.GetParent());
            Assert.AreEqual(1, parentRef.GetChildCount());
            Assert.AreEqual(child, parentRef.GetChild(0));
        }

        [Test]
        public void EntityManager_SetParent_ToNull_RemovesParentRelationship()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child, Entity.Null, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            var childRef = m_Manager.GetTransformRef(child);

            Assert.AreEqual(Entity.Null, childRef.GetParent());
            Assert.AreEqual(0, parentRef.GetChildCount());
        }

        [Test]
        public void EntityManager_SetParent_Reparenting_UpdatesBothHierarchies()
        {
            var oldParent = m_Manager.CreateEntity();
            var newParent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(oldParent);
            m_Manager.AddTransform(newParent);
            m_Manager.AddTransform(child);

            m_Manager.SetParent(child, oldParent, preserveWorldTransform: false);
            m_Manager.SetParent(child, newParent, preserveWorldTransform: false);

            var oldParentRef = m_Manager.GetTransformRef(oldParent);
            var newParentRef = m_Manager.GetTransformRef(newParent);
            var childRef = m_Manager.GetTransformRef(child);

            Assert.AreEqual(newParent, childRef.GetParent());
            Assert.AreEqual(0, oldParentRef.GetChildCount());
            Assert.AreEqual(1, newParentRef.GetChildCount());
            Assert.AreEqual(child, newParentRef.GetChild(0));
        }

        [Test]
        public void EntityManager_SetParent_WithMultipleChildren_Works()
        {
            var parent = m_Manager.CreateEntity();
            var child1 = m_Manager.CreateEntity();
            var child2 = m_Manager.CreateEntity();
            var child3 = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child1);
            m_Manager.AddTransform(child2);
            m_Manager.AddTransform(child3);

            m_Manager.SetParent(child1, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child2, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child3, parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            var child1Ref = m_Manager.GetTransformRef(child1);
            var child2Ref = m_Manager.GetTransformRef(child2);
            var child3Ref = m_Manager.GetTransformRef(child3);

            Assert.AreEqual(3, parentRef.GetChildCount());
            Assert.AreEqual(parent, child1Ref.GetParent());
            Assert.AreEqual(parent, child2Ref.GetParent());
            Assert.AreEqual(parent, child3Ref.GetParent());
        }

        [Test]
        public void EntityManager_DetachChildren_DetachesAllChildren()
        {
            var parent = m_Manager.CreateEntity();
            var child1 = m_Manager.CreateEntity();
            var child2 = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child1);
            m_Manager.AddTransform(child2);

            m_Manager.SetParent(child1, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child2, parent, preserveWorldTransform: false);

            m_Manager.DetachChildren(parent, preserveWorldTransform: false);

            var parentRef = m_Manager.GetTransformRef(parent);
            var child1Ref = m_Manager.GetTransformRef(child1);
            var child2Ref = m_Manager.GetTransformRef(child2);

            Assert.AreEqual(0, parentRef.GetChildCount());
            Assert.AreEqual(Entity.Null, child1Ref.GetParent());
            Assert.AreEqual(Entity.Null, child2Ref.GetParent());
        }

        [Test]
        public void EntityManager_DetachChildren_PreservesWorldTransform()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent, new float3(10f, 10f, 10f));
            m_Manager.AddTransform(child, new float3(5f, 5f, 5f));

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            // Child's world position is now (15, 15, 15)
            var childRefBefore = m_Manager.GetTransformRef(child);
            var worldPosBefore = childRefBefore.ComputeLocalToWorld().Translation();
            Assert.That(worldPosBefore, Is.EqualTo(new float3(15f, 15f, 15f)).Using(Float3EqualityComparer.Instance));

            m_Manager.DetachChildren(parent, preserveWorldTransform: true);

            // Child should maintain world position (15, 15, 15) as new local position
            var childRefAfter = m_Manager.GetTransformRef(child);
            Assert.That(childRefAfter.LocalPosition, Is.EqualTo(new float3(15f, 15f, 15f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childRefAfter.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(15f, 15f, 15f)).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void EntityManager_DetachChildren_NoChildren_DoesNothing()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddTransform(entity, new float3(1f, 2f, 3f));

            Assert.DoesNotThrow(() => m_Manager.DetachChildren(entity, preserveWorldTransform: false));

            var transformRef = m_Manager.GetTransformRef(entity);
            Assert.That(transformRef.LocalPosition, Is.EqualTo(new float3(1f, 2f, 3f)).Using(Float3EqualityComparer.Instance));
        }

        struct MoveForwardJobChunk : IJobChunk
        {
            public TransformTypeHandle transformTypeHandle;
            public float deltaZ;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                TransformAccessor transformAccessor = chunk.GetTransformAccessor(ref transformTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while(enumerator.NextEntityIndex(out var i))
                {
                    TransformRef transformRef = transformAccessor[i];
                    transformRef.LocalPosition += new float3(0f, 0f, deltaZ);
                }
            }

        }

        public enum ScheduleMode
        {
            Parallel, ParallelByRef, Single, SingleByRef, Run
        }

        [Test]
        public void SetTransformRef_IJobChunk_Works([Values] ScheduleMode scheduleMode)
        {
            var parentEntity = m_Manager.CreateEntity();
            var childEntity = m_Manager.CreateEntity();
            var looseEntity = m_Manager.CreateEntity();

            m_Manager.AddTransform(parentEntity, new float3(1f, 1f, 1f));
            m_Manager.AddTransform(childEntity, new float3(2f, 2f, 2f));
            m_Manager.AddTransform(looseEntity, new float3(4f, 4f, 4f));

            m_Manager.SetParent(childEntity, parentEntity, preserveWorldTransform:false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .Build(m_Manager);

            var transformTypeHandle = m_Manager.GetTransformTypeHandle(isReadOnly:false);
            var job = new MoveForwardJobChunk
            {
                deltaZ = 1f,
                transformTypeHandle = transformTypeHandle
            };
            switch (scheduleMode)
            {
                case ScheduleMode.Run:
                    job.Run(query);
                    break;
                case ScheduleMode.Single:
                    job.Schedule(query, new JobHandle()).Complete();
                    break;
                case ScheduleMode.SingleByRef:
                    job.ScheduleByRef(query, new JobHandle()).Complete();
                    break;
                case ScheduleMode.Parallel:
                    job.ScheduleParallel(query, new JobHandle()).Complete();
                    break;
                case ScheduleMode.ParallelByRef:
                    job.ScheduleParallelByRef(query, new JobHandle()).Complete();
                    break;
            }

            var parentTransform = m_Manager.GetTransformRef(parentEntity);
            var childTransform = m_Manager.GetTransformRef(childEntity);
            var looseTransform = m_Manager.GetTransformRef(looseEntity);

            Assert.That(parentTransform.LocalPosition, Is.EqualTo(new float3(1f, 1f, 2f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childTransform.LocalPosition, Is.EqualTo(new float3(2f, 2f, 3f)).Using(Float3EqualityComparer.Instance));
            Assert.That(looseTransform.LocalPosition, Is.EqualTo(new float3(4f, 4f, 5f)).Using(Float3EqualityComparer.Instance));

            Assert.That(parentTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(1f, 1f, 2f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(3f, 3f, 5f)).Using(Float3EqualityComparer.Instance));
            Assert.That(looseTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(4f, 4f, 5f)).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        public void TransformRef_WithQueryChangeFilter_Works()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var archetypeB = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData2));
            var e1 = m_Manager.CreateEntity(archetypeA);
            var e2 = m_Manager.CreateEntity(archetypeB);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<TransformRef>().Build(m_Manager);
            Assert.AreEqual(2, query.CalculateEntityCount());
            query.SetChangedVersionFilter(ComponentType.ReadWrite<TransformRef>());
            const int oldVersion = 17, newVersion = 23;
            m_Manager.Debug.SetGlobalSystemVersion(oldVersion);
            query.SetChangedFilterRequiredVersion(oldVersion);
            // Bump the system version. No changes should be detected.
            m_Manager.Debug.SetGlobalSystemVersion(newVersion);
            Assert.AreEqual(0, query.CalculateEntityCount());
            // Modify an entity and make sure it now matches the query.
            var tr1 = m_Manager.GetTransformRef(e1);
            tr1.LocalPosition += new float3(1, 0, 0);
            var changedEntities = query.ToEntityArray(Allocator.Temp).ToArray();
            CollectionAssert.AreEqual(new[] { e1 }, changedEntities);
        }

#if TRANSFORMREF_IJOBENTITY_SUPPORTED
        [BurstCompile(CompileSynchronously = true)]
        [DisableAutoCreation]
        partial struct MoveForwardJobEntityTestSystem : ISystem
        {
            public partial struct MoveForwardJobEntity : IJobEntity
            {
                public float deltaZ;
                void Execute(ref TransformRef transform)
                {
                    transform.LocalPosition += new float3(0f, 0f, deltaZ);
                }
            }

            private EntityQuery _query;
            public void OnCreate(ref SystemState state)
            {
                _query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<TransformRef>()
                    .Build(ref state);
            }

            public void OnUpdate(ref SystemState state)
            {
                new MoveForwardJobEntity
                {
                    deltaZ = 1f,
                }.Run(_query);
            }
        }
#endif

        [Test]
        public void RemoveTransformRef_Works()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddTransform(e);
            Assert.IsTrue(m_Manager.HasComponent<TransformRef>(e));
            m_Manager.RemoveComponent<TransformRef>(e);
            Assert.IsFalse(m_Manager.HasComponent<TransformRef>(e));
        }

#if TRANSFORMREF_IJOBENTITY_SUPPORTED
        [Test]
        public void SetTransformRef_IJobEntity_Works()
        {
            var parentEntity = m_Manager.CreateEntity();
            var childEntity = m_Manager.CreateEntity();
            var looseEntity = m_Manager.CreateEntity();

            m_Manager.AddTransform(parentEntity, new float3(1f, 1f, 1f));
            m_Manager.AddTransform(childEntity, new float3(2f, 2f, 2f));
            m_Manager.AddTransform(looseEntity, new float3(4f, 4f, 4f));

            m_Manager.SetParent(childEntity, parentEntity, preserveWorldTransform: false);

            var sys = World.CreateSystem<MoveForwardJobEntityTestSystem>();
            sys.Update(World.Unmanaged);

            var parentTransform = m_Manager.GetTransformRef(parentEntity);
            var childTransform = m_Manager.GetTransformRef(childEntity);
            var looseTransform = m_Manager.GetTransformRef(looseEntity);

            Assert.That(parentTransform.LocalPosition, Is.EqualTo(new float3(1f, 1f, 2f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childTransform.LocalPosition, Is.EqualTo(new float3(2f, 2f, 3f)).Using(Float3EqualityComparer.Instance));
            Assert.That(looseTransform.LocalPosition, Is.EqualTo(new float3(4f, 4f, 5f)).Using(Float3EqualityComparer.Instance));

            Assert.That(parentTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(1f, 1f, 2f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(3f, 3f, 5f)).Using(Float3EqualityComparer.Instance));
            Assert.That(looseTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(4f, 4f, 5f)).Using(Float3EqualityComparer.Instance));
        }

        [BurstCompile]
        [DisableAutoCreation]
        partial struct MoveForwardForeachTestSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                var deltaZ = 1.0f;
                foreach (var transformItor in SystemAPI.Query<TransformRef>())
                {
                    // C# doesn't let you modify foreach iteration variables, even in cases where it should be safe.
                    // In this instance, TransformRef is just a pointer, and all of its setter properties just write to
                    // things through that pointer; the actual ref object is never modified.
                    // To work around this issue, we use the same trick that's necessary for DynamicBuffer in IFE:
                    // make a local copy of the iteration variable inside the loop, and use the local copy
                    // for all "mutable" operations.
                    var transform = transformItor;
                    transform.LocalPosition += new float3(0f, 0f, deltaZ);
                }
            }
        }

        [Test]
        public void SetTransformRef_ForEach_Works()
        {
            var parentEntity = m_Manager.CreateEntity();
            var childEntity = m_Manager.CreateEntity();
            var looseEntity = m_Manager.CreateEntity();

            m_Manager.AddTransform(parentEntity, new float3(1f, 1f, 1f));
            m_Manager.AddTransform(childEntity, new float3(2f, 2f, 2f));
            m_Manager.AddTransform(looseEntity, new float3(4f, 4f, 4f));

            m_Manager.SetParent(childEntity, parentEntity, preserveWorldTransform: false);

            var sys = World.CreateSystem<MoveForwardForeachTestSystem>();
            sys.Update(World.Unmanaged);

            var parentTransform = m_Manager.GetTransformRef(parentEntity);
            var childTransform = m_Manager.GetTransformRef(childEntity);
            var looseTransform = m_Manager.GetTransformRef(looseEntity);

            Assert.That(parentTransform.LocalPosition, Is.EqualTo(new float3(1f, 1f, 2f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childTransform.LocalPosition, Is.EqualTo(new float3(2f, 2f, 3f)).Using(Float3EqualityComparer.Instance));
            Assert.That(looseTransform.LocalPosition, Is.EqualTo(new float3(4f, 4f, 5f)).Using(Float3EqualityComparer.Instance));

            Assert.That(parentTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(1f, 1f, 2f)).Using(Float3EqualityComparer.Instance));
            Assert.That(childTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(3f, 3f, 5f)).Using(Float3EqualityComparer.Instance));
            Assert.That(looseTransform.ComputeLocalToWorld().Translation(), Is.EqualTo(new float3(4f, 4f, 5f)).Using(Float3EqualityComparer.Instance));
        }
#endif

        [BurstCompile]
        [DisableAutoCreation]
        partial struct TransformRefFromSystemAPITestSystem : ISystem
        {
            private EntityQuery _query;
            void OnCreate(ref SystemState state)
            {
                _query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData, TransformRef>().Build(ref state);
            }

            void OnUpdate(ref SystemState state)
            {
                var entities = _query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
                var e = entities[0];
                var tr = state.GetTransformRef(e, isReadOnly:false);
                tr.LocalPosition = -tr.LocalPosition;
                tr.LocalRotation = math.conjugate(tr.LocalRotation); ;
                tr.LocalScale = -tr.LocalScale;
            }
        }

        [Test]
        public void GetTransformRef_FromSystemAPI_Works()
        {
            var pos = new float3(1, 2, 3);
            var rot = quaternion.RotateX(math.PI / 2);
            var scale = new float3(1.1f, 1.2f, 1.3f);

            var archetype = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e = m_Manager.CreateEntity(archetype);
            var tr = m_Manager.GetTransformRef(e);
            tr.LocalPosition = pos;
            tr.LocalRotation = rot;
            tr.LocalScale = scale;

            var sys = World.CreateSystem<TransformRefFromSystemAPITestSystem>();
            sys.Update(World.Unmanaged);

            Assert.That(-pos, Is.EqualTo(tr.LocalPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(math.conjugate(rot), Is.EqualTo(tr.LocalRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(-scale, Is.EqualTo(tr.LocalScale).Using(Float3EqualityComparer.Instance));
        }

        [BurstCompile]
        [DisableAutoCreation]
        partial struct TransformLookupFromStateTestSystem : ISystem
        {
            private TransformLookup _transformLookup;
            private EntityQuery _query;
            void OnCreate(ref SystemState state)
            {
                _transformLookup = state.GetTransformLookup(false);
                _query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData, TransformRef>().Build(ref state);
            }

            void OnUpdate(ref SystemState state)
            {
                var entities = _query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
                var e = entities[0];
                var tr = _transformLookup[e];
                tr.LocalPosition = -tr.LocalPosition;
                tr.LocalRotation = math.conjugate(tr.LocalRotation);;
                tr.LocalScale = -tr.LocalScale;
            }
        }

        [Test]
        public void GetTransformLookup_FromSystemState_Works()
        {
            var pos = new float3(1, 2, 3);
            var rot = quaternion.RotateX(math.PI/2);
            var scale = new float3(1.1f, 1.2f, 1.3f);

            var archetype = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e = m_Manager.CreateEntity(archetype);
            var tr = m_Manager.GetTransformRef(e);
            tr.LocalPosition = pos;
            tr.LocalRotation = rot;
            tr.LocalScale = scale;

            var sys = World.CreateSystem<TransformLookupFromStateTestSystem>();
            sys.Update(World.Unmanaged);
            Assert.That(-pos, Is.EqualTo(tr.LocalPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(math.conjugate(rot), Is.EqualTo(tr.LocalRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(-scale, Is.EqualTo(tr.LocalScale).Using(Float3EqualityComparer.Instance));
        }

        [BurstCompile]
        [DisableAutoCreation]
        partial struct TransformLookupFromSystemAPITestSystem : ISystem
        {
            private EntityQuery _query;
            void OnCreate(ref SystemState state)
            {
                _query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData, TransformRef>().Build(ref state);
            }

            void OnUpdate(ref SystemState state)
            {
                var transformLookup = state.GetTransformLookup(false);
                var entities = _query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
                var e = entities[0];
                var tr = transformLookup[e];
                tr.LocalPosition = -tr.LocalPosition;
                tr.LocalRotation = math.conjugate(tr.LocalRotation); ;
                tr.LocalScale = -tr.LocalScale;
            }
        }

        [Test]
        public void GetTransformLookup_FromSystemAPI_Works()
        {
            var pos = new float3(1, 2, 3);
            var rot = quaternion.RotateX(math.PI / 2);
            var scale = new float3(1.1f, 1.2f, 1.3f);

            var archetype = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e = m_Manager.CreateEntity(archetype);
            var tr = m_Manager.GetTransformRef(e);
            tr.LocalPosition = pos;
            tr.LocalRotation = rot;
            tr.LocalScale = scale;

            var sys = World.CreateSystem<TransformLookupFromSystemAPITestSystem>();
            sys.Update(World.Unmanaged);
            Assert.That(-pos, Is.EqualTo(tr.LocalPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(math.conjugate(rot), Is.EqualTo(tr.LocalRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(-scale, Is.EqualTo(tr.LocalScale).Using(Float3EqualityComparer.Instance));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks]
        public void TransformLookup_WritingToReadOnly_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e = m_Manager.CreateEntity(archetype);
            var transforms = m_Manager.GetTransformLookup(isReadOnly:true);
            var tr = transforms[e];
            Assert.Throws<InvalidOperationException>(() => tr.LocalPosition = float3.zero);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks]
        public void TransformLookup_EntityHasNoTransformComponent_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var e = m_Manager.CreateEntity(archetype);

            var transforms = m_Manager.GetTransformLookup(isReadOnly:true);
            Assert.Throws<ArgumentException>(() => { var tr = transforms[e]; });
        }

        [Test]
        public void TransformLookup_TryGetTransform_Works()
        {
            var pos = new float3(1, 2, 3);
            var rot = quaternion.RotateX(math.PI/2);
            var scale = new float3(1.1f, 1.2f, 1.3f);

            var archetype1 = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e1 = m_Manager.CreateEntity(archetype1);
            var tr = m_Manager.GetTransformRef(e1);
            tr.LocalPosition = pos;
            tr.LocalRotation = rot;
            tr.LocalScale = scale;
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var e2 = m_Manager.CreateEntity(archetype2);
            var e3 = Entity.Null;

            var transforms = m_Manager.GetTransformLookup(isReadOnly:true);
            // e1 has a valid transform
            Assert.IsTrue(transforms.TryGetTransform(e1, out TransformRef t1));
            Assert.IsTrue(t1.IsValid);
            Assert.IsTrue(transforms.TryGetTransform(e1, out t1, entityExists: out var t1EntityExists));
            Assert.IsTrue(t1.IsValid);
            Assert.IsTrue(t1EntityExists);
            Assert.That(pos, Is.EqualTo(t1.LocalPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(rot.value, Is.EqualTo(t1.LocalRotation.value).Using(Float4EqualityComparer.Instance));
            Assert.That(scale, Is.EqualTo(t1.LocalScale).Using(Float3EqualityComparer.Instance));
            // e2 does not
            Assert.IsFalse(transforms.TryGetTransform(e2, out TransformRef t2));
            Assert.IsFalse(t2.IsValid);
            Assert.IsFalse(transforms.TryGetTransform(e2, out t2, out var t2EntityExists));
            Assert.IsFalse(t2.IsValid);
            Assert.IsTrue(t2EntityExists);
            // e3 is a null entity
            Assert.IsFalse(transforms.TryGetTransform(e3, out TransformRef t3));
            Assert.IsFalse(t2.IsValid);
            Assert.IsFalse(transforms.TryGetTransform(e3, out t3, out var t3EntityExists));
            Assert.IsFalse(t3.IsValid);
            Assert.IsFalse(t3EntityExists);
        }

        [Test]
        public void TransformLookup_HasTransform_Works()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e1 = m_Manager.CreateEntity(archetype1);
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var e2 = m_Manager.CreateEntity(archetype2);
            var e3 = Entity.Null;

            var transforms = m_Manager.GetTransformLookup(isReadOnly:true);
            // e1 has a valid transform
            Assert.IsTrue(transforms.HasTransform(e1));
            Assert.IsTrue(transforms.HasTransform(e1, out var e1EntityExists));
            Assert.IsTrue(e1EntityExists);
            // e2 does not
            Assert.IsFalse(transforms.HasTransform(e2));
            Assert.IsFalse(transforms.HasTransform(e2, out var e2EntityExists));
            Assert.IsTrue(e2EntityExists);
            // e3 is a null entity
            Assert.IsFalse(transforms.HasTransform(e3));
            Assert.IsFalse(transforms.HasTransform(e3, out var e3EntityExists));
            Assert.IsFalse(e3EntityExists);
        }

        [Test]
        public void TransformLookup_EntityExists_Works([Values]bool destroyEntity)
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e1 = m_Manager.CreateEntity(archetype1);
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var e2 = m_Manager.CreateEntity(archetype2);
            var e3 = Entity.Null;

            var transforms = m_Manager.GetTransformLookup(isReadOnly:true);
            // e1 has a valid entity
            Assert.IsTrue(transforms.EntityExists(e1));
            if (destroyEntity)
            {
                m_Manager.DestroyEntity(e1);
                transforms = m_Manager.GetTransformLookup(isReadOnly:true);
                Assert.IsFalse(transforms.EntityExists(e1));
            }
            // as does e2
            Assert.IsTrue(transforms.EntityExists(e2));
            // e3 is a null entity
            Assert.IsFalse(transforms.EntityExists(e3));
        }

        [Test]
        public void TransformLookup_DidChange_Works()
        {
            var transforms = m_Manager.GetTransformLookup(isReadOnly:true);

            var archetype = m_Manager.CreateArchetype(typeof(TransformRef), typeof(EcsTestData));
            var e = m_Manager.CreateEntity(archetype);

            uint changeVersion = ChangeVersionUtility.InitialGlobalSystemVersion;
            Assert.IsFalse(transforms.DidChange(e, changeVersion));

            // Incrementing system version without any further transform changes doesn't trigger a change
            m_Manager.Debug.IncrementGlobalSystemVersion();
            Assert.IsFalse(transforms.DidChange(e, changeVersion));

            // Getting R/W access to the transform data at this point (even without any writes) does trigger a change
            var tr1 = m_Manager.GetTransformRef(e);
            Assert.IsTrue(transforms.DidChange(e, changeVersion));
        }

        [Test]
        public void TransformRef_SetParent_WithTransformSystemsUpdate_Works()
        {
            World.CreateSystem<TransformRefLocalToWorldSystem>();
            World.CreateSystem<LocalToWorldSystem>();
            World.CreateSystem<ParentSystem>();
            World.CreateSystem<TransformSystemGroup>();
            Type[] systems = new Type[]
            {
                typeof(TransformRefLocalToWorldSystem),
                typeof(LocalToWorldSystem),
                typeof(ParentSystem),
                typeof(TransformSystemGroup),
            };
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);

            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent, new float3(1,2,3), quaternion.RotateX(math.PIHALF));
            float3 originalPosition = new float3(4, 5, 6);
            quaternion originalRotation = quaternion.RotateZ(math.PI);
            float3 originalScale = new float3(1, 2, 3);
            m_Manager.AddTransform(child, originalPosition, originalRotation, originalScale);

            Assert.DoesNotThrow(() =>
            {
                m_Manager.World.Update();
            });

            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            Assert.DoesNotThrow(() =>
            {
                m_Manager.World.Update();
            });

            var childTransform = m_Manager.GetTransformRef(child);
            var childLTW = childTransform.ComputeLocalToWorld();
            math.decompose(new AffineTransform(childLTW), out float3 expectedWorldPosition, out quaternion expectedWorldRotation, out float3 _);

            Assert.DoesNotThrow(() =>
            {
                m_Manager.World.Update();
            });

            childTransform.GetWorldPositionAndRotation(out float3 worldPosition, out quaternion worldRotation);
            Assert.That(worldPosition, Is.EqualTo(expectedWorldPosition).Using(Float3EqualityComparer.Instance));
            Assert.That(worldRotation, Is.EqualTo(expectedWorldRotation).Using(MathQuaternionEqualityComparer.Instance));
            Assert.That(childTransform.LocalScale, Is.EqualTo(originalScale).Using(Float3EqualityComparer.Instance));

            Assert.DoesNotThrow(() =>
            {
                m_Manager.World.Update();
            });
        }

        // Job that reads hierarchy information (GetParent, GetChildCount, GetChild) from TransformRef
        struct ReadHierarchyJob : IJobChunk
        {
            public TransformTypeHandle TransformTypeHandle;
            public NativeArray<Entity> ParentResults;
            public NativeArray<int> ChildCountResults;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                TransformAccessor transformAccessor = chunk.GetTransformAccessor(ref TransformTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    TransformRef transformRef = transformAccessor[i];
                    ParentResults[i] = transformRef.GetParent();
                    ChildCountResults[i] = transformRef.GetChildCount();
                }
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void SetParent_WhileJobUsingHierarchyMethods_IsScheduled_CompletesJob()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            var newParent = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);
            m_Manager.AddTransform(newParent);
            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .Build(m_Manager);

            int entityCount = query.CalculateEntityCount();
            var parentResults = new NativeArray<Entity>(entityCount, Allocator.TempJob);
            var childCountResults = new NativeArray<int>(entityCount, Allocator.TempJob);

            var transformTypeHandle = m_Manager.GetTransformTypeHandle(isReadOnly: false);
            var job = new ReadHierarchyJob
            {
                TransformTypeHandle = transformTypeHandle,
                ParentResults = parentResults,
                ChildCountResults = childCountResults
            };

            var jobHandle = job.Schedule(query, default);
            query.AddDependency(jobHandle);

            // SetParent triggers a structural change which completes all pending jobs.
            // This ensures the job reading hierarchy data completes before the hierarchy is modified.
            m_Manager.SetParent(child, newParent, preserveWorldTransform: false);

            // The job should have been auto-completed by the structural change
            Assert.IsTrue(jobHandle.IsCompleted);

            var childRef = m_Manager.GetTransformRef(child);
            Assert.AreEqual(newParent, childRef.GetParent());

            parentResults.Dispose();
            childCountResults.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void DetachChildren_WhileJobUsingHierarchyMethods_IsScheduled_CompletesJob()
        {
            var parent = m_Manager.CreateEntity();
            var child1 = m_Manager.CreateEntity();
            var child2 = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child1);
            m_Manager.AddTransform(child2);
            m_Manager.SetParent(child1, parent, preserveWorldTransform: false);
            m_Manager.SetParent(child2, parent, preserveWorldTransform: false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .Build(m_Manager);

            int entityCount = query.CalculateEntityCount();
            var parentResults = new NativeArray<Entity>(entityCount, Allocator.TempJob);
            var childCountResults = new NativeArray<int>(entityCount, Allocator.TempJob);

            var transformTypeHandle = m_Manager.GetTransformTypeHandle(isReadOnly: false);
            var job = new ReadHierarchyJob
            {
                TransformTypeHandle = transformTypeHandle,
                ParentResults = parentResults,
                ChildCountResults = childCountResults
            };

            var jobHandle = job.Schedule(query, default);
            query.AddDependency(jobHandle);

            // DetachChildren triggers a structural change which completes all pending jobs.
            // This ensures the job reading hierarchy data completes before the hierarchy is modified.
            m_Manager.DetachChildren(parent, preserveWorldTransform: false);

            // The job should have been auto-completed by the structural change
            Assert.IsTrue(jobHandle.IsCompleted);

            // Verify the detachment actually happened
            var parentRef = m_Manager.GetTransformRef(parent);
            Assert.AreEqual(0, parentRef.GetChildCount());
            var child1Ref = m_Manager.GetTransformRef(child1);
            Assert.AreEqual(Entity.Null, child1Ref.GetParent());

            parentResults.Dispose();
            childCountResults.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void AccessingTransformRef_AfterSetParent_WithStaleTypeHandle_Throws()
        {
            var parent = m_Manager.CreateEntity();
            var child = m_Manager.CreateEntity();
            m_Manager.AddTransform(parent);
            m_Manager.AddTransform(child);

            // Get a TransformTypeHandle before the structural change
            var transformTypeHandle = m_Manager.GetTransformTypeHandle(isReadOnly: false);

            // SetParent is a structural change that invalidates type handles
            m_Manager.SetParent(child, parent, preserveWorldTransform: false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TransformRef>()
                .Build(m_Manager);

            int entityCount = query.CalculateEntityCount();
            var parentResults = new NativeArray<Entity>(entityCount, Allocator.TempJob);
            var childCountResults = new NativeArray<int>(entityCount, Allocator.TempJob);

            // Using the stale handle should throw
            var job = new ReadHierarchyJob
            {
                TransformTypeHandle = transformTypeHandle,
                ParentResults = parentResults,
                ChildCountResults = childCountResults
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                job.Run(query);
            });

            parentResults.Dispose();
            childCountResults.Dispose();
        }
    }
}
#endif
