using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DocCodeSamples.Tests
{
#region unityobjectref-example
    public class AnimatorAuthoring : MonoBehaviour
    {
        public GameObject AnimatorPrefab;

        public class AnimatorBaker : Baker<AnimatorAuthoring>
        {
            public override void Bake(AnimatorAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(e, new AnimatorPrefabRef
                {
                    Prefab = authoring.AnimatorPrefab
                });
            }
        }
    }

    public struct AnimatorPrefabRef : IComponentData
    {
        public UnityObjectRef<GameObject> Prefab;
    }

    public struct AnimatorInstanceRef : IComponentData
    {
        public UnityObjectRef<Animator> Animator;
    }

#endregion

#region unityobjectref-spawn-system-example
    public partial struct SpawnAnimatedCubeSystem : ISystem
    {
        EntityQuery m_Query;

        public void OnCreate(ref SystemState state)
        {
            m_Query = SystemAPI.QueryBuilder()
                .WithAll<AnimatorPrefabRef, LocalToWorld>()
                .WithNone<AnimatorInstanceRef>()
                .Build();
            state.RequireForUpdate(m_Query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entities = m_Query.ToEntityArray(state.WorldUpdateAllocator);

            foreach (var entity in entities)
            {
                var prefabRef = SystemAPI.GetComponent<AnimatorPrefabRef>(entity);
                var worldTransform = SystemAPI.GetComponent<LocalToWorld>(entity);

                //Instantiate the GO and place it at the entity's transform
                var rotatingCube = Object.Instantiate(prefabRef.Prefab.Value);
                rotatingCube.transform.SetPositionAndRotation(worldTransform.Position, worldTransform.Rotation);

                //Add the animator to the entity
                state.EntityManager.AddComponentData(entity, new AnimatorInstanceRef
                {
                    Animator = rotatingCube.GetComponent<Animator>()
                });
            }
        }
    }

#endregion

#region unityobjectref-anim-system-example
    public partial struct ModulateAnimatorSpeedSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var sineSpeed = 1f + math.sin((float)SystemAPI.Time.ElapsedTime);

            //Query and modify the speed of the Animator
            foreach (var instanceRef in SystemAPI.Query<AnimatorInstanceRef>())
            {
                instanceRef.Animator.Value.speed = sineSpeed;
            }
        }
    }
#endregion
}
