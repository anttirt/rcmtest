using Unity.Entities;

namespace Unity.Entities.Tests
{
    // This assembly does NOT have [assembly: DisableAutoCreation], so this system's [DisableAutoCreation]
    // is explicit on the type itself, not inherited from the assembly level.
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    public partial class ExplicitlyDisabledSystemForTesting : SystemBase
    {
        protected override void OnUpdate()
        {
        }
    }
}
