using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class GetComponentTransformBaker : Baker<TestComponentAuthoring>
    {
        public struct Vector3Element : IComponentData
        {
            public static implicit operator float3(Vector3Element e)
            {
                return e.Value;
            }

            public static implicit operator Vector3Element(float3 e)
            {
                return new Vector3Element {Value = e};
            }

            public float3 Value;
        }

        public override void Bake(TestComponentAuthoring authoring)
        {
            var position = GetComponent<Transform>().position;

            // This test might require transform components
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Vector3Element { Value =  position });
        }
    }

    public class GetParentBaker : Baker<TestComponentAuthoring>
	{
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

		public override void Bake(TestComponentAuthoring authoring)
		{
            var parent = GetParent();

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
			DynamicBuffer<EntityIdElement> buffer = AddBuffer<EntityIdElement>(entity);
            if (parent)
                buffer.Add(parent.GetEntityId());
        }
	}

    public class GetParentsBaker : Baker<TestComponentAuthoring>
    {
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

        public override void Bake(TestComponentAuthoring authoring)
        {
            var parents = GetParents();

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<EntityIdElement> buffer = AddBuffer<EntityIdElement>(entity);
            if (parents != null)
            {
                foreach (var parent in parents)
                {
                    buffer.Add(parent.GetEntityId());
                }
            }
        }
    }

    public class GetChildCountBaker : Baker<TestComponentAuthoring>
    {
        public struct IntComponent : IComponentData
        {
            public int Value;
        }

        public override void Bake(TestComponentAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new IntComponent() { Value = GetChildCount() });
        }
    }

    public class GetChildBaker : Baker<TestComponentAuthoring>
    {
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

        public override void Bake(TestComponentAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<EntityIdElement> buffer = AddBuffer<EntityIdElement>(entity);
            if (authoring.transform.childCount > 0)
            {
                var child = GetChild(0);
                buffer.Add(child.GetEntityId());
            }
        }
    }

    public partial class GetChildrenBaker : Baker<TestComponentAuthoring>
    {
        public static bool Recursive;

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

        public override void Bake(TestComponentAuthoring authoring)
        {
            var children = GetChildren(Recursive);

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<EntityIdElement> buffer = AddBuffer<EntityIdElement>(entity);
            if (children != null)
            {
                foreach (var child in children)
                {
                    buffer.Add(child.GetEntityId());
                }
            }
        }
    }
}
