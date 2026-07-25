using JetBrains.Annotations;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    readonly struct ComponentContentDisplay
    {
        [CreateProperty, UsedImplicitly]
        Header Header { get; }

        public ComponentContentDisplay(ComponentContent content)
        {
            Header = new Header(ComponentsUtility.GetComponentDisplayName(TypeUtility.GetTypeDisplayName(content.Type)), "component-type__icon");
        }
    }
}
