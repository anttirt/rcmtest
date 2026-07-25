using System;
using System.Text;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Hierarchy;
using Unity.Profiling;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The hierarchy node type handler for Entity SubScenes.
    /// </summary>    
    internal class HierarchySubSceneRuntimeHandler : HierarchyNodeTypeHandler, IHierarchyEditorNodeTypeHandler
    {
        const string k_EntityUssClass = "hierarchy-item--subscene-node";
        const string k_StyleSheetPath = "Packages/com.unity.entities/Editor Default Resources/uss/Hierarchy/hierarchy-entity-item.uss";

        StyleSheet m_StyleSheet;
        StyleSheet m_ThemeStyleSheet;

        NativeHashMap<Entity, Unity.Hierarchy.HierarchyNode> m_EntityToNodeMap;
        NativeHashMap<Unity.Hierarchy.HierarchyNode, Entity> m_NodeToEntityMap;

        HierarchyWorldHandler m_WorldHandler;

        static readonly ProfilerMarker k_AllocatingHierarchyNodesMarker = new ("HierarchySubSceneRuntimeHandler.AllocatingNewHierarchyNodes");
        static readonly ProfilerMarker k_CommandAddNodesMarker = new ("HierarchySubSceneRuntimeHandler.CommandAddNodes");
        static readonly ProfilerMarker k_CommandSetNamesMarker = new ("HierarchySubSceneRuntimeHandler.CommandSetNames");

        public override string GetNodeTypeName() => "SubSceneRuntime";

        protected override void Initialize()
        {
            base.Initialize();

            // SubScene nodes are only relevant in MainStage and PrefabStage (GameObject editing modes).
            // Skip event subscriptions for other stages like VisualElementEditingStage.
            if (StageUtility.GetCurrentStage() is not (MainStage or PrefabStage))
                return;

            UpdateHierarchySystem.OnAddSubSceneNodes += AddSubSceneNodes;
            UpdateHierarchySystem.OnRemoveEntityNodes += RemoveEntityNodes;
            UpdateHierarchySystem.OnResizeEntityHandlerMappingsCapacity += ResizeMappings;
            Unity.Editor.Bridge.GameObjectChangeTrackerBridge.GameObjectsChanged += OnGameObjectsChanged;

            m_EntityToNodeMap = new NativeHashMap<Entity, Unity.Hierarchy.HierarchyNode>(1, Allocator.Persistent);
            m_NodeToEntityMap = new NativeHashMap<Unity.Hierarchy.HierarchyNode, Entity>(1, Allocator.Persistent);

            m_WorldHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchyWorldHandler>();
        }

        protected override void Dispose(bool disposing)
        {
            m_EntityToNodeMap.Dispose();
            m_NodeToEntityMap.Dispose();

            UpdateHierarchySystem.OnAddSubSceneNodes -= AddSubSceneNodes;
            UpdateHierarchySystem.OnRemoveEntityNodes -= RemoveEntityNodes;
            UpdateHierarchySystem.OnResizeEntityHandlerMappingsCapacity -= ResizeMappings;
            Unity.Editor.Bridge.GameObjectChangeTrackerBridge.GameObjectsChanged -= OnGameObjectsChanged;

            m_WorldHandler = null;
            base.Dispose(disposing);
        }

        StyleSheet StyleSheet
        {
            get
            {
                if (!m_StyleSheet)
                    m_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetPath);
                return m_StyleSheet;
            }
        }

        StyleSheet ThemeStyleSheet
        {
            get
            {
                if (!m_ThemeStyleSheet)
                {
                    var path = k_StyleSheetPath;
                    var index = path.LastIndexOf(".uss", StringComparison.OrdinalIgnoreCase);
                    if (EditorGUIUtility.isProSkin)
                        path = path.Insert(index, "_dark");
                    else
                        path = path.Insert(index, "_light");
                    m_ThemeStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }

                return m_ThemeStyleSheet;
            }
        }

        [InitializeOnLoadMethod, UsedImplicitly]
        internal static void RegisterHierarchyHandlers()
        {
            EditorApplication.delayCall += Unity.Hierarchy.Editor.HierarchyWindow.RegisterNodeTypeHandler<HierarchySubSceneRuntimeHandler>;
        }

        [UsedImplicitly]
        internal static void UnregisterHierarchyHandlers()
        {
            Unity.Hierarchy.Editor.HierarchyWindow.UnregisterNodeTypeHandler<HierarchySubSceneRuntimeHandler>();
        }

        void AddSubSceneNodes(World world, NativeArray<HierarchyEntityNodeData> nodes, string[] names)
        {
            if (nodes.Length == 0)
                return;

            var parentNode = m_WorldHandler.GetOrCreateWorldNode(world);
            AddNodes(parentNode, nodes, names);
        }

        void AddNodes(Unity.Hierarchy.HierarchyNode parentNode, NativeArray<HierarchyEntityNodeData> nodes, string[] names)
        {
            // Allocate new HierarchyNodes to add
            k_AllocatingHierarchyNodesMarker.Begin();
            var addedNodes = new NativeList<HierarchyEntityNodeData>(nodes.Length, Allocator.Temp);
            var addedNodeNameIndices = new NativeList<int>(nodes.Length, Allocator.Temp);
            int addedNodesCount = 0;
            var newHierarchyNodes = new NativeList<Unity.Hierarchy.HierarchyNode>(nodes.Length, Allocator.Temp);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!m_EntityToNodeMap.ContainsKey(nodes[i].Entity))
                {
                    var hierarchyNode = new Unity.Hierarchy.HierarchyNode();
                    newHierarchyNodes.Add(hierarchyNode);
                    addedNodes.Add(nodes[i]);
                    addedNodeNameIndices.Add(i);
                    addedNodesCount++;
                }
            }

            k_AllocatingHierarchyNodesMarker.End();

            k_CommandAddNodesMarker.Begin();
            // Create all children in a batch command
            CommandList.Add(parentNode, newHierarchyNodes.AsSpan());
            k_CommandAddNodesMarker.End();

            for (int i = 0; i < addedNodesCount; i++)
            {
                // Update mappings after the HierarchyNodes have been created. They are created in the command
                m_EntityToNodeMap.Add(addedNodes[i].Entity, newHierarchyNodes[i]);
                m_NodeToEntityMap.Add(newHierarchyNodes[i], addedNodes[i].Entity);
            }

            // Set names to all children nodes
            // TODO: A batch command will make it faster to update each node name.
            k_CommandSetNamesMarker.Begin();
            for (int i = 0; i < addedNodesCount; i++)
            {
                CommandList.SetName(newHierarchyNodes[i], names[addedNodeNameIndices[i]]);
            }

            k_CommandSetNamesMarker.End();

            addedNodes.Dispose();
            addedNodeNameIndices.Dispose();
            newHierarchyNodes.Dispose();
        }

        void ResizeMappings(int count)
        {
            if(m_EntityToNodeMap.Capacity < m_EntityToNodeMap.Count + count)
                m_EntityToNodeMap.Capacity = m_EntityToNodeMap.Count + count;
            if(m_NodeToEntityMap.Capacity < m_NodeToEntityMap.Count + count)
                m_NodeToEntityMap.Capacity = m_NodeToEntityMap.Count + count;
        }

        void OnGameObjectsChanged(in NativeArray<Unity.Editor.Bridge.GameObjectChangeTrackerEvent> events)
        {
            foreach (var evt in events)
            {
                if ((evt.EventType & Unity.Editor.Bridge.GameObjectChangeTrackerEventType.UnloadedSubSceneWasRenamed) !=
                    Unity.Editor.Bridge.GameObjectChangeTrackerEventType.UnloadedSubSceneWasRenamed)
                    continue;

                var gameObject = EditorUtility.EntityIdToObject(evt.EntityId) as GameObject;
                if (gameObject == null || !gameObject.TryGetComponent<SubScene>(out var renamedSubScene))
                    continue;

                var renamedGuid = renamedSubScene.SceneGUID;
                foreach (var kvp in m_EntityToNodeMap)
                {
                    var entity = kvp.Key;
                    var node = kvp.Value;
                    foreach (var world in World.All)
                    {
                        var em = world.EntityManager;
                        if (!em.Exists(entity) || !em.HasComponent<SubScene>(entity))
                            continue;

                        if (!em.HasComponent<SceneReference>(entity) ||
                            em.GetComponentData<SceneReference>(entity).SceneGUID != renamedGuid)
                            continue;

                        CommandList.SetName(node, UpdateHierarchySystem.GetSubSceneName(em, entity));
                        break;
                    }
                }
            }
        }

        void RemoveEntityNodes(NativeList<Entity> entitiesToRemove)
        {
            foreach (var entity in entitiesToRemove)
            {
                if (m_EntityToNodeMap.TryGetValue(entity, out var node))
                {
                    CommandList.Remove(node);
                    m_EntityToNodeMap.Remove(entity);
                    m_NodeToEntityMap.Remove(node);
                }
            }
        }

        internal void ClearMappings(World world)
        {
            var entityManager = world.EntityManager;
            var allEntities = m_EntityToNodeMap.GetKeyArray(Allocator.Temp);
            foreach (var entity in allEntities)
            {
                if (entityManager.Exists(entity))
                {
                    var node = m_EntityToNodeMap[entity];
                    m_EntityToNodeMap.Remove(entity);
                    m_NodeToEntityMap.Remove(node);
                }
            }
        }

        internal bool TryGetSubSceneNode(Entity subScene, out Unity.Hierarchy.HierarchyNode subSceneNode)
        {
            subSceneNode = default;
            return m_EntityToNodeMap.TryGetValue(subScene, out subSceneNode);
        }
        
        internal Unity.Hierarchy.HierarchyNode GetOrCreateSubSceneNode(World world, Entity subScene)
        {
            if (m_EntityToNodeMap.TryGetValue(subScene, out var subSceneNode))
                return subSceneNode;

            CommandList.Add(Hierarchy.Root, out subSceneNode);
            m_EntityToNodeMap.Add(subScene, subSceneNode);
            m_NodeToEntityMap.Add(subSceneNode, subScene);

            var subSceneName = UpdateHierarchySystem.GetSubSceneName(world.EntityManager, subScene);
            CommandList.SetName(subSceneNode, subSceneName);

            return subSceneNode;
        }        

        protected override void OnBindView(HierarchyView view)
        {
            view.StyleContainer.styleSheets.Add(StyleSheet);
            view.StyleContainer.styleSheets.Add(ThemeStyleSheet);
        }

        protected override void OnUnbindView(HierarchyView view)
        {
            // The StyleSheet and ThemeStyleSheet can be null when the Entities package is being removed from a project
            if (StyleSheet != null)
                view.StyleContainer.styleSheets.Remove(StyleSheet);
            if (ThemeStyleSheet != null)
                view.StyleContainer.styleSheets.Remove(ThemeStyleSheet);
            base.OnUnbindView(view);
        }

        protected override void OnBindItem(HierarchyViewItem item)
        {
            if (!m_NodeToEntityMap.TryGetValue(item.Node, out _))
            {
                item.AddToClassList(k_EntityUssClass);
            }

            item.EnableInClassList(k_EntityUssClass, true);
            Resources.AddCommonVariables(item);

            // TODO: Add support for more selection types like scrolling (ie: HierarchyGlobalSelectionHandler.SyncGlobalSelectionFromViewModel)
            item.RegisterCallback<ClickEvent, Unity.Hierarchy.HierarchyNode>(SelectEntityNode, item.Node);
        }

        protected override void OnUnbindItem(HierarchyViewItem item)
        {
            item.EnableInClassList(k_EntityUssClass, false);
            item.UnregisterCallback<ClickEvent, Unity.Hierarchy.HierarchyNode>(SelectEntityNode);
        }

        void SelectEntityNode(ClickEvent evt, Unity.Hierarchy.HierarchyNode node)
        {
            if (evt.clickCount >= 1)
            {
                if (m_NodeToEntityMap.TryGetValue(node, out var entity))
                {
                    foreach (var world in World.All)
                    {
                        var em = world.EntityManager;
                        if (em.Exists(entity))
                        {
                            EntitySelectionProxy.SelectEntity(world, entity);
                            break;
                        }
                    }
                }
            }
        }

        #region IHierarchyEditorNodeTypeHandler

        bool IHierarchyEditorNodeTypeHandler.CanSetName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.OnSetName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node, string name) => false;
        string IHierarchyEditorNodeTypeHandler.GetDisplayName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => Hierarchy.GetName(in node);
        bool IHierarchyEditorNodeTypeHandler.CanDoubleClick(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.OnDoubleClick(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.CanCut(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnCut(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanCopy(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnCopy(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanPaste(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnPaste(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanPasteAsChild(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnPasteAsChild(HierarchyView view, bool keepWorldPos) => false;
        bool IHierarchyEditorNodeTypeHandler.CanDuplicate(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnDuplicate(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanDelete(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnDelete(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.AcceptParent(HierarchyView view, in Unity.Hierarchy.HierarchyNode parent) => false;
        bool IHierarchyEditorNodeTypeHandler.AcceptChild(HierarchyView view, in Unity.Hierarchy.HierarchyNode child) => false;

        bool IHierarchyEditorNodeTypeHandler.CanStartDrag(HierarchyView view, ReadOnlySpan<Unity.Hierarchy.HierarchyNode> nodes)
        {
            var canStartDrag = false;
            foreach (var node in nodes)
            {
                if (!m_NodeToEntityMap.TryGetValue(node, out _))
                    canStartDrag = true;
            }

            return canStartDrag;
        }

        void IHierarchyEditorNodeTypeHandler.OnStartDrag(in HierarchyViewDragAndDropSetupData data) { }
        DragVisualMode IHierarchyEditorNodeTypeHandler.CanReorder(in HierarchyViewDragAndDropHandlingData data) => DragVisualMode.None;
        void IHierarchyEditorNodeTypeHandler.OnReorder(in HierarchyViewDragAndDropHandlingData data) { }
        DragVisualMode IHierarchyEditorNodeTypeHandler.CanAcceptDrop(in HierarchyViewDragAndDropHandlingData data) => DragVisualMode.None;
        DragVisualMode IHierarchyEditorNodeTypeHandler.OnAcceptDrop(in HierarchyViewDragAndDropHandlingData data) => DragVisualMode.None;
        bool IHierarchyEditorNodeTypeHandler.CanFindReferences(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnFindReferences(HierarchyView view) => false;
        void IHierarchyEditorNodeTypeHandler.GetTooltip(HierarchyViewItem item, bool isFiltering, StringBuilder tooltip) { }
        void IHierarchyEditorNodeTypeHandler.PopulateContextMenu(HierarchyView view, HierarchyViewItem item, DropdownMenu menu) { }

        #endregion
    }
}
