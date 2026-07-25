using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    internal static class TreeViewItemDataBridge<T>
    {
        internal static void AddChild(TreeViewItemData<T> itemData, TreeViewItemData<T> child) => itemData.AddChild(child);
    }
}