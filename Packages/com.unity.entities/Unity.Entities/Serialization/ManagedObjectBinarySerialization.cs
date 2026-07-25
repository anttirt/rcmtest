using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;
using Unity.Entities.Serialization.Binary;
using UnityEngine;
using UnityEngine.Assertions;

[assembly: InternalsVisibleTo("Unity.Scenes")]

[assembly: GeneratePropertyBagsForTypesQualifiedWith(typeof(Unity.Entities.ISharedComponentData))]
[assembly: GeneratePropertyBagsForTypesQualifiedWith(typeof(Unity.Entities.IComponentData), TypeGenerationOptions.ReferenceType)]

namespace Unity.Entities.Serialization
{
    struct SerializedKeyFrame
    {
        public float Time;
        public float Value;
        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;
        public int WeightedMode;

        public SerializedKeyFrame(UnityEngine.Keyframe kf)
        {
            Time = kf.time;
            Value = kf.value;
            InTangent = kf.inTangent;
            OutTangent = kf.outTangent;
            InWeight = kf.inWeight;
            OutWeight = kf.outWeight;
            WeightedMode = (int)kf.weightedMode;
        }

        public static implicit operator UnityEngine.Keyframe(SerializedKeyFrame kf)
        {
            return new UnityEngine.Keyframe(kf.Time, kf.Value, kf.InTangent, kf.OutTangent, kf.InWeight, kf.OutWeight)
            {
                weightedMode = (UnityEngine.WeightedMode) kf.WeightedMode
            };
        }

        public static implicit operator SerializedKeyFrame(UnityEngine.Keyframe kf)
        {
            return new SerializedKeyFrame(kf);
        }
    }

    /// <summary>
    /// Writer to write managed objects to a <see cref="UnsafeAppendBuffer"/> stream.
    /// </summary>
    /// <remarks>
    /// This is used as a wrapper around <see cref="Unity.Entities.Serialization.Binary.BinarySerialization"/> with a custom layer for <see cref="UnityEngine.Object"/>.
    /// </remarks>
    unsafe class ManagedObjectBinaryWriter : Unity.Entities.Serialization.Binary.IContravariantBinaryAdapter<UnityEngine.Object>,
        IBinaryAdapter<UnityEngine.AnimationCurve>,
        Unity.Entities.Serialization.Binary.IBinaryAdapter<UntypedUnityObjectRef>
    {
        readonly UnsafeAppendBuffer* m_Stream;
        readonly BinarySerializationParameters m_Params;

        private UnityObjectRefMap m_UnityObjectRefs;

        /// <summary>
        /// Initializes a new instance of <see cref="ManagedObjectBinaryWriter"/> which can be used to write managed objects to the given stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="unityObjectRefs">Map to track object references. Pass <see cref="UnityObjectRefMap.None"/> to skip tracking (object refs serialize as -1).</param>
        public ManagedObjectBinaryWriter(UnsafeAppendBuffer* stream, UnityObjectRefMap unityObjectRefs)
        {
            m_Stream = stream;
            m_Params = new BinarySerializationParameters
            {
                UserDefinedAdapters = new List<IBinaryAdapter> {this},
                State = new BinarySerializationState(),
            };
            m_UnityObjectRefs = unityObjectRefs;
        }

        /// <summary>
        /// Adds a custom adapter to the writer.
        /// </summary>
        /// <param name="adapter">The custom adapter to add.</param>
        public void AddAdapter(IBinaryAdapter adapter) => m_Params.UserDefinedAdapters.Add(adapter);

        /// <summary>
        /// Writes the given boxed object to the binary stream.
        /// </summary>
        /// <remarks>
        /// Any <see cref="UnityEngine.Object"/> references are added to the object table and can be retrieved by calling <see cref="GetUnityObjects"/>.
        /// </remarks>
        /// <param name="obj">The object to serialize.</param>
        public void WriteObject(object obj)
        {
            var parameters = m_Params;
            parameters.SerializedType = obj?.GetType();
            BinarySerialization.ToBinary(m_Stream, obj, parameters);
        }

        void Unity.Entities.Serialization.Binary.IBinaryAdapter<UntypedUnityObjectRef>.Serialize(in BinarySerializationContext<UntypedUnityObjectRef> context, UntypedUnityObjectRef value)
        {
            // Returns the index of the EntityId in the map, or -1 if invalid (serializes as "no reference")
            var index = m_UnityObjectRefs.Add(value.entityId);
            context.Writer->Add(index);
        }

        public UntypedUnityObjectRef Deserialize(in BinaryDeserializationContext<UntypedUnityObjectRef> context)
        {
            throw new InvalidOperationException($"Deserialize should never be invoked by {nameof(ManagedObjectBinaryWriter)}");
        }

        void Unity.Entities.Serialization.Binary.IContravariantBinaryAdapter<UnityEngine.Object>.Serialize(IBinarySerializationContext context, UnityEngine.Object value)
        {
            // Returns the index of the EntityId in the map, or -1 if invalid (serializes as "no reference")
            var index = value != null ? m_UnityObjectRefs.Add(value.GetEntityId()) : -1;
            context.Writer->Add(index);
        }

        object Unity.Entities.Serialization.Binary.IContravariantBinaryAdapter<UnityEngine.Object>.Deserialize(IBinaryDeserializationContext context)
        {
            throw new InvalidOperationException($"Deserialize should never be invoked by {nameof(ManagedObjectBinaryWriter)}");
        }

        void IBinaryAdapter<UnityEngine.AnimationCurve>.Serialize(in BinarySerializationContext<UnityEngine.AnimationCurve> context, UnityEngine.AnimationCurve value)
        {
            if (value != null)
            {
                context.Writer->Add(value.length);
                context.Writer->Add(value.preWrapMode);
                context.Writer->Add(value.postWrapMode);

                for (int i = 0, count = value.length; i < count; ++i)
                {
                    context.Writer->Add((SerializedKeyFrame) value[i]);
                }
            }
            else
            {
                context.Writer->Add(-1);
            }
        }

        UnityEngine.AnimationCurve IBinaryAdapter<UnityEngine.AnimationCurve>.Deserialize(in BinaryDeserializationContext<UnityEngine.AnimationCurve> context)
        {
            throw new InvalidOperationException($"Deserialize should never be invoked by {nameof(ManagedObjectBinaryWriter)}");
        }
    }

    /// <summary>
    /// Reader to read managed objects from a <see cref="UnsafeAppendBuffer.Reader"/> stream.
    /// </summary>
    /// <remarks>
    /// This is used as a wrapper around <see cref="Unity.Entities.Serialization.Binary.BinarySerialization"/> with a custom layer for <see cref="UnityEngine.Object"/>.
    /// </remarks>
    unsafe class ManagedObjectBinaryReader : Unity.Entities.Serialization.Binary.IContravariantBinaryAdapter<UnityEngine.Object>,
        IBinaryAdapter<UnityEngine.AnimationCurve>,
        Unity.Entities.Serialization.Binary.IBinaryAdapter<UntypedUnityObjectRef>
    {
        readonly UnsafeAppendBuffer.Reader* m_Stream;
        readonly BinarySerializationParameters m_Params;
        readonly NativeArray<EntityId> m_UnityObjects;
        readonly List<UnityEngine.Object> m_UnityObjectsArray;

        /// <summary>
        /// Initializes a new instance of <see cref="ManagedObjectBinaryReader"/> which can be used to read managed objects from the given stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="unityObjects">The table containing all <see cref="UnityEngine.Object"/> references. This is produce by the <see cref="ManagedObjectBinaryWriter"/>.</param>
        public ManagedObjectBinaryReader(UnsafeAppendBuffer.Reader* stream, NativeArray<EntityId> unityObjects)
        {
            Assert.IsTrue(unityObjects.IsCreated, "unityObjects array must be initialized before use");
            m_Stream = stream;
            m_Params = new BinarySerializationParameters
            {
                UserDefinedAdapters = new List<IBinaryAdapter> {this},
                State = new BinarySerializationState(),
            };
            m_UnityObjects = unityObjects;
            m_UnityObjectsArray = new List<UnityEngine.Object>(m_UnityObjects.Length);
            Resources.EntityIdsToObjectList(unityObjects, m_UnityObjectsArray);
        }

        /// <summary>
        /// Adds a custom adapter to the reader.
        /// </summary>
        /// <param name="adapter">The custom adapter to add.</param>
        public void AddAdapter(IBinaryAdapter adapter) => m_Params.UserDefinedAdapters.Add(adapter);


        /// <summary>
        /// Reads from the binary stream and returns the next object.
        /// </summary>
        /// <remarks>
        /// The type is given as a hint to the serializer to avoid writing root type information.
        /// </remarks>
        /// <param name="type">The root type.</param>
        /// <returns>The deserialized object value.</returns>
        public object ReadObject(Type type)
        {
            var parameters = m_Params;
            parameters.SerializedType = type;
            return BinarySerialization.FromBinary<object>(m_Stream, parameters);
        }

        void Unity.Entities.Serialization.Binary.IContravariantBinaryAdapter<UnityEngine.Object>.Serialize(IBinarySerializationContext context, UnityEngine.Object value)
        {
            throw new InvalidOperationException($"Serialize should never be invoked by {nameof(ManagedObjectBinaryReader)}.");
        }

        object Unity.Entities.Serialization.Binary.IContravariantBinaryAdapter<UnityEngine.Object>.Deserialize(IBinaryDeserializationContext context)
        {
            var index = context.Reader->ReadNext<int>();

            if (index == -1)
                return null;

            if (!m_UnityObjects.IsCreated)
                throw new ArgumentException("We are reading a UnityEngine.Object however no ObjectTable was provided to the ManagedObjectBinaryReader.");

            if ((uint)index >= m_UnityObjects.Length)
                throw new ArgumentException("We are reading a UnityEngine.Object but the deserialized index is out of range for the given object table.");

            return m_UnityObjectsArray[index];
        }

        void IBinaryAdapter<UnityEngine.AnimationCurve>.Serialize(in BinarySerializationContext<UnityEngine.AnimationCurve> context, UnityEngine.AnimationCurve value)
        {
            throw new InvalidOperationException($"Serialize should never be invoked by {nameof(ManagedObjectBinaryReader)}.");
        }

        UnityEngine.AnimationCurve IBinaryAdapter<UnityEngine.AnimationCurve>.Deserialize(in BinaryDeserializationContext<UnityEngine.AnimationCurve> context)
        {
            var length = context.Reader->ReadNext<int>();
            if (length >= 0)
            {
                var preMode = context.Reader->ReadNext<UnityEngine.WrapMode>();
                var postMode = context.Reader->ReadNext<UnityEngine.WrapMode>();
                var ac = new UnityEngine.AnimationCurve()
                {
                    preWrapMode = preMode,
                    postWrapMode = postMode
                };
                for (int i = 0; i < length; ++i)
                    ac.AddKey(context.Reader->ReadNext<SerializedKeyFrame>());

                return ac;
            }

            return null;
        }

        public void Serialize(in BinarySerializationContext<UntypedUnityObjectRef> context, UntypedUnityObjectRef value)
        {
            throw new InvalidOperationException($"Serialize should never be invoked by {nameof(ManagedObjectBinaryReader)}.");
        }

        public UntypedUnityObjectRef Deserialize(in BinaryDeserializationContext<UntypedUnityObjectRef> context)
        {
            var index = context.Reader->ReadNext<int>();

            if (index == -1)
                return default;

            if (!m_UnityObjects.IsCreated)
                throw new ArgumentException("We are reading a UnityEngine.Object however no ObjectTable was provided to the ManagedObjectBinaryReader.");

            if ((uint)index >= m_UnityObjects.Length)
                throw new ArgumentException("We are reading a UnityEngine.Object but the deserialized index is out of range for the given object table.");

            return new UntypedUnityObjectRef { entityId = m_UnityObjects[index] };
        }
    }
}
