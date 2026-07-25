using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Codec = Unity.Core.Compression.Codec;

namespace Unity.Entities
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct RuntimeBlobHeaderRef
    {
        [FieldOffset(0)]
        internal long m_BlobAssetRefStorage;
        public ref DotsSerialization.BlobHeader Value => ref UnsafeUtility.As<long, BlobAssetReference<DotsSerialization.BlobHeader>>(ref m_BlobAssetRefStorage).Value;
        public static implicit operator RuntimeBlobHeaderRef(BlobAssetReference<DotsSerialization.BlobHeader> assetRef)
        {
            RuntimeBlobHeaderRef ret = default;
            UnsafeUtility.As<long, BlobAssetReference<DotsSerialization.BlobHeader>>(ref ret.m_BlobAssetRefStorage) = assetRef;
            return ret;
        }
        public static implicit operator BlobAssetReference<DotsSerialization.BlobHeader>(RuntimeBlobHeaderRef clip)
        {
            return UnsafeUtility.As<long, BlobAssetReference<DotsSerialization.BlobHeader>>(ref clip.m_BlobAssetRefStorage);
        }

        public unsafe RuntimeBlobHeaderRef Resolve(BlobAssetOwner blobAssetOwner)
        {
            var blobAssetRef = new BlobAssetReference<DotsSerialization.BlobHeader>();
            blobAssetRef.m_data.m_Ptr = (byte*) blobAssetOwner.BlobAssetBatchPtr + m_BlobAssetRefStorage;
            return blobAssetRef;
        }
    }

    /// <summary>
    /// This component contains data relative to a <see cref="SceneSection"/>.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    // This struct use an explicit layout to guard it against BUR-2491
    public struct SceneSectionData : IComponentData
    {
        /// <summary>
        /// Represents the unique GUID to identify the scene where the section is.
        /// </summary>
        [FieldOffset(0)]
        public Hash128          SceneGUID;
        /// <summary>
        /// Represents the scene section index inside the scene.
        /// </summary>
        [FieldOffset(16)]
        public int              SubSectionIndex;
        /// <summary>
        /// Represents the file size for the compressed section.
        /// </summary>
        [FieldOffset(20)]
        public int              FileSize;
        /// <summary>
        /// Represents the number of Unity Objects referenced in the section.
        /// </summary>
        [FieldOffset(24)]
        public int              ObjectReferenceCount;
        /// <summary>
        /// Represents the scene section bounding volume.
        /// </summary>
        [FieldOffset(28)]
        public MinMaxAABB       BoundingVolume;
        [FieldOffset(52)]
        internal Codec          Codec;
        [FieldOffset(56)]
        internal int            DecompressedFileSize;

        // For sections above section 0, this is the count of entities in section 0.
        [FieldOffset(60)]
        internal int            ExternalEntitiesRefRange;

        [FieldOffset(64)]
        internal RuntimeBlobHeaderRef BlobHeader;
    }

    /// <summary>
    /// This component identifies the entity which holds the meta data components that belong to the section with the specified <see cref="SceneSectionIndex"/>.
    /// </summary>
    /// <remarks>
    /// These meta data components are serialized into the entity scene header and are added to the
    /// section entities after the scene is resolved at runtime.
    /// </remarks>
    public struct SectionMetadataSetup : ISharedComponentData
    {
        /// <summary>
        /// Represents the scene section index inside the scene.
        /// </summary>
        public int SceneSectionIndex;
    }

    /// <summary>
    /// Component that references a scene.
    /// </summary>
    /// <remarks>
    /// This component uses the unique GUID to identify the scene.
    /// </remarks>
    public struct SceneReference : IComponentData, IEquatable<SceneReference>
    {
        /// <summary>
        /// Unique GUID to identify the scene.
        /// </summary>
        public Hash128 SceneGUID;

        /// <summary>
        /// Builds a <see cref="SceneReference"/> from an <see cref="EntitySceneReference"/>.
        /// </summary>
        /// <param name="sceneReference">The <see cref="EntitySceneReference"/> to reference.</param>
        public SceneReference(EntitySceneReference sceneReference)
        {
            SceneGUID = sceneReference.Id .GlobalId.AssetGUID;
        }

        /// <summary>
        /// Compares two <see cref="SceneReference"/> instances to determine if they are equal.
        /// </summary>
        /// <param name="other">A <see cref="SceneReference"/> to compare with.</param>
        /// <returns>Returns true if <paramref name="other"/> contains the same SceneGUID.</returns>
        public bool Equals(SceneReference other)
        {
            return SceneGUID.Equals(other.SceneGUID);
        }

        /// <summary>
        /// Computes a hashcode to support hash-based collections.
        /// </summary>
        /// <returns>The computed hash.</returns>
        public override int GetHashCode()
        {
            return SceneGUID.GetHashCode();
        }
    }

    /// <summary>
    /// This component contains the root entity of a prefab
    /// </summary>
    public struct PrefabRoot : IComponentData
    {
        /// <summary>
        /// The root entity of a prefab.
        /// </summary>
        public Entity Root;
    }

    /// <summary>
    /// Identifies the <see cref="SceneSection"/> where the entity belongs to.
    /// </summary>
    [System.Serializable]
    public struct SceneSection : ISharedComponentData, IEquatable<SceneSection>
    {
        /// <summary>
        /// Unique GUID that identifies the scene where the section is.
        /// </summary>
        public Hash128        SceneGUID;
        /// <summary>
        /// Scene section index inside the scene.
        /// </summary>
        public int            Section;

        /// <summary>
        /// Compares two <see cref="SceneSection"/> instances to determine if they are equal.
        /// </summary>
        /// <param name="other">A <see cref="SceneSection"/>  to compare with.</param>
        /// <returns>True if <paramref name="other"/> contains the same scene GUID and section index.</returns>
        public bool Equals(SceneSection other)
        {
            return SceneGUID.Equals(other.SceneGUID) && Section == other.Section;
        }

        /// <summary>
        /// Computes a hashcode to support hash-based collections.
        /// </summary>
        /// <returns>The computed hash.</returns>
        public override int GetHashCode()
        {
            return (SceneGUID.GetHashCode() * 397) ^ Section;
        }
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    /// <summary>
    /// Component that contains an <see cref="EntityCommandBuffer"/>, which is used to execute commands after a scene is loaded.
    /// </summary>
    /// <remarks>This component includes a reference counter. When the reference counter is equal to 0,
    /// the <see cref="CommandBuffer"/> is disposed of.</remarks>
    [Obsolete("PostLoadCommandBuffer is deprecated. Build the per-instance data on a regular entity in the main world, then pass it via SceneSystem.LoadParameters.ImportEntity (or set RequestSceneLoaded.ImportEntity on the scene or section meta entity). The streaming system copies the referenced entity into the per-section streaming world for ProcessAfterLoadGroup systems to query.")]
    public class PostLoadCommandBuffer : IComponentData, IDisposable, ICloneable
    {
        /// <summary>
        /// Represents an <see cref="EntityCommandBuffer"/>.
        /// </summary>
        public EntityCommandBuffer CommandBuffer;
        private int RefCount;

        /// <summary>
        /// Initializes and returns an instance of PostLoadCommandBuffer.
        /// </summary>
        public PostLoadCommandBuffer()
        {
            RefCount = 1;
        }

        /// <summary>
        /// Decrements the reference counter. When the reference counter reaches 0, the <see cref="CommandBuffer"/> is disposed.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Decrement(ref RefCount) == 0)
                CommandBuffer.Dispose();
        }

        /// <summary>
        /// Increments the reference counter and returns a reference to the component.
        /// </summary>
        /// <returns>Returns a reference to the <see cref="PostLoadCommandBuffer"/> component.</returns>
        public object Clone()
        {
            Interlocked.Increment(ref RefCount);
            return this;
        }
    }
#endif

    /// <summary>
    /// Contains flags that control the load process for sub-scenes.
    /// </summary>
    [Flags]
    public enum SceneLoadFlags
    {
        /// <summary>
        /// Prevents adding a RequestSceneLoaded to the SubScene section entities when it gets created. If loading a GameObject scene, setting this flag is equivalent to setting activateOnLoad to false.
        /// </summary>
        DisableAutoLoad = 1,
        /// <summary>
        /// Disable asynchronous importing, and wait for the SubScene to be fully converted (only relevant in-Editor) and its header loaded.
        /// </summary>
        /// <remarks>
        /// For fully synchronous scene loading, both <see cref="BlockOnImport"/> and <see cref="BlockOnStreamIn"/> must be set.
        /// </remarks>
        BlockOnImport = 2,
        /// <summary>
        /// Disable asynchronous streaming, SubScene section will be fully loaded during the next update of the streaming system
        /// </summary>
        /// <remarks>
        /// For fully synchronous scene loading, both <see cref="BlockOnImport"/> and <see cref="BlockOnStreamIn"/> must be set.
        /// </remarks>
        BlockOnStreamIn = 4,
        // TODO: Remove this RemovedAfter 2021-02-05 (DOTS-3380)
        // SceneLoadFlags.LoadAdditive is deprecated. Scenes loaded through the SceneSystem are always loaded Additively. This previously was only used when using LiveLink with GameObjects.
        /// <summary>
        /// [DEPRECATED] Set whether to load additive or not. This only applies to GameObject based scenes, not subscenes.
        /// </summary>
        LoadAdditive = 8,
        /// <summary>
        /// Loads a new instance of the subscene
        /// </summary>
        NewInstance = 16,
    }

    /// <summary>
    /// A component that requests the load of a sub scene.
    /// </summary>
    /// <remarks>
    /// Add this to a scene meta entity to request the scene be loaded; remove it to
    /// request unload. The streaming system propagates <see cref="RequestSceneLoaded"/>
    /// from the scene meta entity to its section meta entities when
    /// <see cref="SceneLoadFlags.DisableAutoLoad"/> is not set, so per-section load
    /// requests typically inherit the scene-level fields automatically.
    /// </remarks>
    public struct RequestSceneLoaded : IComponentData
    {
        /// <summary>
        /// Contains flags that control the load process for sub scenes.
        /// </summary>
        public SceneLoadFlags LoadFlags;

        /// <summary>
        /// Optional main-world entity to copy into the per-section streaming world before
        /// <see cref="Unity.Scenes.ProcessAfterLoadGroup"/> runs.
        /// </summary>
        /// <remarks>
        /// Use this to deliver per-instance runtime data to ProcessAfterLoad systems. Build
        /// a data entity in the main world carrying whatever components your ProcessAfterLoad
        /// system needs to read, then set <see cref="ImportEntity"/> on the scene meta
        /// entity (typically via <see cref="Unity.Scenes.SceneSystem.LoadParameters.ImportEntity"/>
        /// at load time). The streaming system copies that entity and all of its components
        /// into the section's loading world; ProcessAfterLoad systems then query for the
        /// imported components as if they were native to the streaming world.
        ///
        /// The imported entity follows the same lifetime rules as any other entity in the
        /// streaming world: your <c>ProcessAfterLoad</c> system may consume and destroy it,
        /// and any imported entity that is still alive after
        /// <see cref="Unity.Scenes.ProcessAfterLoadGroup"/> runs is tagged with
        /// <see cref="SceneTag"/> and moved into the main world together with the rest of
        /// the section's entities. Destroy it inside your <c>ProcessAfterLoad</c> system if
        /// you only want it to live for the duration of the load.
        ///
        /// You own the source entity in the main world; the streaming system only reads from
        /// it. Destroy the source entity yourself when it is no longer needed, but keep it
        /// alive until the scene load completes. <see cref="ImportEntity"/> may be
        /// <see cref="Entity.Null"/>, in which case no import is performed. A non-null
        /// reference to an entity that does not exist (because it was never created, or
        /// because it was destroyed before the scene finished loading) is treated as a
        /// programming error: the import is skipped and an error is logged.
        ///
        /// Because <see cref="RequestSceneLoaded"/> is automatically propagated from the
        /// scene meta entity to its sections, setting <see cref="ImportEntity"/> on the
        /// scene meta entity causes that entity to be imported into every section's
        /// streaming world. Override the per-section value by writing
        /// <see cref="RequestSceneLoaded"/> on the individual section meta entity if you
        /// need different (or no) import data for specific sections.
        /// </remarks>
        public Entity ImportEntity;
    }
}
