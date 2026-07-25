using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities.Tests
{
    /// <summary>
    /// Test authoring component that references a prefab and calls GetComponents in its baker.
    /// Used to test that adding unrelated GameObjects to a subscene doesn't trigger unnecessary rebakes
    /// when the baker uses GetComponents and has prefab references.
    /// </summary>
    [AddComponentMenu("")]
    public class TestGetComponentsWithPrefabReferenceAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public struct ComponentData : IComponentData
        {
            public int ColliderCount;
        }

        public struct EntityIdElement : IBufferElementData
        {
            public static implicit operator EntityId(EntityIdElement e)
            {
                return e.Value;
            }

            public static implicit operator EntityIdElement(EntityId e)
            {
                return new EntityIdElement { Value = e };
            }

            public EntityId Value;
        }

        class Baker : Baker<TestGetComponentsWithPrefabReferenceAuthoring>
        {
            public override void Bake(TestGetComponentsWithPrefabReferenceAuthoring authoring)
            {
                // Register the prefab reference
                if (authoring.Prefab != null)
                {
                    GetEntity(authoring.Prefab, TransformUsageFlags.None);
                }

                // Call GetComponents on the authoring - this creates a GetComponentsDependency
                List<Collider> found = new List<Collider>();
                GetComponents<Collider>(found);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ComponentData { ColliderCount = found.Count });

                DynamicBuffer<EntityIdElement> buffer = AddBuffer<EntityIdElement>(entity);
                foreach (var component in found)
                {
                    buffer.Add(component.GetEntityId());
                }
            }
        }
    }
}
