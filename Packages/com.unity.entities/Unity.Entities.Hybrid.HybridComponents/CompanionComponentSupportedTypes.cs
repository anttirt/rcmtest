using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
#if HDRP_7_0_0_OR_NEWER
using UnityEngine.Rendering.HighDefinition;
#endif
#if URP_7_0_0_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif

using Unity.Entities;

[assembly: RegisterUnityEngineComponentType(typeof(Light))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<Light>))]
#pragma warning disable 0618 // Type or member is obsolete
[assembly: RegisterUnityEngineComponentType(typeof(LightProbeProxyVolume))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<LightProbeProxyVolume>))]
#pragma warning restore 0618
[assembly: RegisterUnityEngineComponentType(typeof(ReflectionProbe))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<ReflectionProbe>))]
[assembly: RegisterUnityEngineComponentType(typeof(TextMesh))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<TextMesh>))]
[assembly: RegisterUnityEngineComponentType(typeof(MeshRenderer))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<MeshRenderer>))]
[assembly: RegisterUnityEngineComponentType(typeof(SpriteRenderer))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<SpriteRenderer>))]
[assembly: RegisterUnityEngineComponentType(typeof(VisualEffect))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<VisualEffect>))]
[assembly: RegisterUnityEngineComponentType(typeof(AudioSource))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<AudioSource>))]
[assembly: RegisterUnityEngineComponentType(typeof(LODGroup))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<LODGroup>))]
[assembly: RegisterUnityEngineComponentType(typeof(Rigidbody))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<Rigidbody>))]
[assembly: RegisterUnityEngineComponentType(typeof(Collider))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<Collider>))]
[assembly: RegisterUnityEngineComponentType(typeof(GameObject))]
[assembly: RegisterUnityEngineComponentType(typeof(Transform))]
[assembly: RegisterUnityEngineComponentType(typeof(SphereCollider))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<SphereCollider>))]
[assembly: RegisterUnityEngineComponentType(typeof(BoxCollider))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<BoxCollider>))]
[assembly: RegisterUnityEngineComponentType(typeof(CapsuleCollider))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<CapsuleCollider>))]
[assembly: RegisterUnityEngineComponentType(typeof(MeshCollider))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<MeshCollider>))]
#if PARTICLE_SYSTEM_MODULE
[assembly: RegisterUnityEngineComponentType(typeof(ParticleSystem))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<ParticleSystem>))]
[assembly: RegisterUnityEngineComponentType(typeof(ParticleSystemRenderer))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<ParticleSystemRenderer>))]
#endif
#if SRP_7_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(Volume))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<Volume>))]
#endif
#if URP_7_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(UnityEngine.Rendering.Universal.DecalProjector))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<UnityEngine.Rendering.Universal.DecalProjector>))]
#endif
#if HDRP_7_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(UnityEngine.Rendering.HighDefinition.DecalProjector))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<UnityEngine.Rendering.HighDefinition.DecalProjector>))]
[assembly: RegisterUnityEngineComponentType(typeof(HDAdditionalLightData))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<HDAdditionalLightData>))]
[assembly: RegisterUnityEngineComponentType(typeof(HDAdditionalReflectionData))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<HDAdditionalReflectionData>))]
[assembly: RegisterUnityEngineComponentType(typeof(PlanarReflectionProbe))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<PlanarReflectionProbe>))]
[assembly: RegisterUnityEngineComponentType(typeof(LocalVolumetricFog))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<LocalVolumetricFog>))]
#endif
#if URP_7_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(UniversalAdditionalLightData))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<UniversalAdditionalLightData>))]
#endif
#if HYBRID_ENTITIES_CAMERA_CONVERSION
[assembly: RegisterUnityEngineComponentType(typeof(Camera))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<Camera>))]
#if HDRP_7_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(HDAdditionalCameraData))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<HDAdditionalCameraData>))]
#endif
#if URP_7_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(UniversalAdditionalCameraData))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<UniversalAdditionalCameraData>))]
#endif
#endif
#if SRP_17_0_0_OR_NEWER
[assembly: RegisterUnityEngineComponentType(typeof(ProbeVolume))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<ProbeVolume>))]
[assembly: RegisterUnityEngineComponentType(typeof(ProbeVolumePerSceneData))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<ProbeVolumePerSceneData>))]
#endif

[assembly: InternalsVisibleTo("Unity.Entities.Hybrid")]
namespace Unity.Entities.Conversion
{
    internal class CompanionComponentSupportedTypes
    {
        public static ComponentType[] Types =
        {
            typeof(Light),
#pragma warning disable 0618 // Type or member is obsolete
            typeof(LightProbeProxyVolume),
#pragma warning restore 0618
            typeof(ReflectionProbe),
            typeof(TextMesh),
            typeof(MeshRenderer),
            typeof(SpriteRenderer),
            typeof(VisualEffect),
            typeof(AudioSource),
            typeof(SphereCollider),
            typeof(BoxCollider),
            typeof(CapsuleCollider),
            typeof(MeshCollider),
#if PARTICLE_SYSTEM_MODULE
            typeof(ParticleSystem),
            typeof(ParticleSystemRenderer),
#endif
#if SRP_7_0_0_OR_NEWER
            typeof(Volume),

#endif
#if URP_7_0_0_OR_NEWER
            typeof(UnityEngine.Rendering.Universal.DecalProjector),
#endif
#if HDRP_7_0_0_OR_NEWER
            typeof(UnityEngine.Rendering.HighDefinition.DecalProjector),
            typeof(HDAdditionalLightData),
            typeof(HDAdditionalReflectionData),
            typeof(PlanarReflectionProbe),
            typeof(LocalVolumetricFog),
#endif
#if URP_7_0_0_OR_NEWER
            typeof(UniversalAdditionalLightData),
#endif
#if HYBRID_ENTITIES_CAMERA_CONVERSION
            typeof(Camera),
#if HDRP_7_0_0_OR_NEWER
            typeof(HDAdditionalCameraData),
#endif
#if URP_7_0_0_OR_NEWER
            typeof(UniversalAdditionalCameraData),
#endif
#endif
#if SRP_17_0_0_OR_NEWER
            typeof(ProbeVolume),
            typeof(ProbeVolumePerSceneData),
#endif
        };
    }
}
