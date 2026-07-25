using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestGetComponentsInParentAuthoring : MonoBehaviour
    {
        public int value;

	    public struct ComponentTest1 : IComponentData
		{
			public int Field;
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

        class Baker : Baker<TestGetComponentsInParentAuthoring>
        {
            public override void Bake(TestGetComponentsInParentAuthoring authoring)
            {
				List<Collider> found = new List<Collider>();
				GetComponentsInParent<Collider>(found);

                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ComponentTest1() {Field = found.Count});

                DynamicBuffer<EntityIdElement> buffer = AddBuffer<EntityIdElement>(entity);
                foreach (var component in found)
                {
                    buffer.Add(component.GetEntityId());
                }
            }
        }
    }
}
