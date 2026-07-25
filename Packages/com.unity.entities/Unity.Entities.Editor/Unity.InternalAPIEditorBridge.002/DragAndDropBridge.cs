using UnityEditor;
using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class DragAndDropBridge
    {
        public static DragAndDropVisualMode DropOnHierarchyWindow(EntityId dropTargetEntityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            return DragAndDrop.DropOnHierarchyWindow(dropTargetEntityId, dropMode, parentForDraggedObjects, perform);
        }
    }
}
