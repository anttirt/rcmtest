namespace Unity.Entities.Editor.Tests
{
    partial class TestSystemsForControls
    {
        public partial class SystemA : SystemBase
        {
            protected override void OnUpdate()
            {
                foreach (var guid in SystemAPI.Query<RefRO<EntityGuid>>()) { }
            }
        }

        [UpdateBefore(typeof(SystemA))]
        public partial class SystemB : SystemBase
        {
            protected override void OnUpdate()
            {
                foreach (var guid in SystemAPI.Query<RefRO<EntityGuid>>()) { }
            }
        }

        public partial class SystemC : SystemBase
        {
            protected override void OnUpdate()
            {
            }
        }
    }
}
