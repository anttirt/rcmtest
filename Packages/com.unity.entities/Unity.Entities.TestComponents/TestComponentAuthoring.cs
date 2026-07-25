using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestComponentAuthoring : MonoBehaviour
    {
        public int IntValue;
        public Material Material;

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public class ManagedTestComponent : IComponentData
        {
            public Material Material;
        }
#endif
        public struct UnmanagedTestComponent : IComponentData
        {
            public int IntValue;
        }

        class Baker : Baker<TestComponentAuthoring>
        {
            public override void Bake(TestComponentAuthoring authoring)
            {
                // This test might require transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TestComponentAuthoring.UnmanagedTestComponent
                {
                    IntValue = authoring.IntValue
                });
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                AddComponentObject(entity, new TestComponentAuthoring.ManagedTestComponent
                #pragma warning restore 0618
                {
                    Material = authoring.Material
                });
#endif
            }
        }
    }
}
