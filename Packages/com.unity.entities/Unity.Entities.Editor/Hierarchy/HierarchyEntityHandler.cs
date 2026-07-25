using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Hierarchy;
using Unity.Profiling;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    internal class HierarchyEntityHandler : HierarchyNodeTypeHandler, IHierarchyEditorNodeTypeHandler, Unity.Hierarchy.Editor.IHierarchySearchPropositionProvider
    {
        const string k_EntityUssClass = "hierarchy-item--entity-node";
        const string k_PrefabUssClass = "hierarchy-item--prefab";
        const string k_PrefabRootUssClass = "hierarchy-item--prefab-root";

        const string k_StyleSheetPath =
            "Packages/com.unity.entities/Editor Default Resources/uss/Hierarchy/hierarchy-entity-item.uss";

        StyleSheet m_StyleSheet;
        StyleSheet m_ThemeStyleSheet;

        NativeHashMap<Entity, Unity.Hierarchy.HierarchyNode> m_EntityToNodeMap;
        NativeHashMap<Unity.Hierarchy.HierarchyNode, Entity> m_NodeToEntityMap;
        NativeHashMap<Entity, HierarchyPrefabType> m_EntityToPrefabTypeMap;
        // SequenceNumber, not WorldUnmanaged: copies of a disposed WorldUnmanaged retain a released safety handle.
        NativeHashMap<Entity, ulong> m_EntityToWorld;

        HierarchyWorldHandler m_WorldHandler;
        HierarchySubSceneRuntimeHandler m_SubSceneHandler;
        EntityComponentFilter m_ComponentFilter;

        static readonly ProfilerMarker k_AllocatingHierarchyNodesMarker = new ("HierarchyEntityHandler.AllocatingNewHierarchyNodes");
        static readonly ProfilerMarker k_CommandAddNodesMarker = new ("HierarchyEntityHandler.CommandAddNodes");
        static readonly ProfilerMarker k_CommandSetParentMarker = new ("HierarchyEntityHandler.CommandSetParent");
        static readonly ProfilerMarker k_CommandSetNamesMarker = new ("HierarchyEntityHandler.CommandSetNames");

        public override string GetNodeTypeName() => nameof(Entity);

        protected override void Initialize()
        {
            base.Initialize();

            // Entity nodes are only relevant in MainStage and PrefabStage (GameObject editing modes).
            // Skip event subscriptions for other stages like VisualElementEditingStage.
            if (StageUtility.GetCurrentStage() is not (MainStage or PrefabStage))
                return;

            UpdateHierarchySystem.OnAddEntityNodes += AddEntityNodes;
            UpdateHierarchySystem.OnRemoveEntityNodes += RemoveEntityNodes;
            UpdateHierarchySystem.OnSetParentNode += SetParentNode;
            UpdateHierarchySystem.OnResizeEntityHandlerMappingsCapacity += ResizeMappings;
            ExplicitFilterSystem.OnExplicitFilterChanged += OnExplicitFilterChanged;

            m_EntityToNodeMap = new NativeHashMap<Entity, Unity.Hierarchy.HierarchyNode>(1, Allocator.Persistent);
            m_NodeToEntityMap = new NativeHashMap<Unity.Hierarchy.HierarchyNode, Entity>(1, Allocator.Persistent);
            m_EntityToPrefabTypeMap = new NativeHashMap<Entity, HierarchyPrefabType>(1, Allocator.Persistent);
            m_EntityToWorld = new NativeHashMap<Entity, ulong>(1, Allocator.Persistent);

            m_WorldHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchyWorldHandler>();
            m_SubSceneHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchySubSceneRuntimeHandler>();
            m_ComponentFilter = new EntityComponentFilter();

            // Register all already initialized worlds
            m_WorldHandler.RegisterAllHierarchySystems();
        }

        protected override void Dispose(bool disposing)
        {
            m_EntityToNodeMap.Dispose();
            m_NodeToEntityMap.Dispose();
            m_EntityToPrefabTypeMap.Dispose();
            m_EntityToWorld.Dispose();

            UpdateHierarchySystem.OnAddEntityNodes -= AddEntityNodes;
            UpdateHierarchySystem.OnRemoveEntityNodes -= RemoveEntityNodes;
            UpdateHierarchySystem.OnSetParentNode -= SetParentNode;
            UpdateHierarchySystem.OnResizeEntityHandlerMappingsCapacity -= ResizeMappings;
            ExplicitFilterSystem.OnExplicitFilterChanged -= OnExplicitFilterChanged;

            m_ComponentFilter.Dispose();
            m_ComponentFilter = null;

            m_WorldHandler = null;
            m_SubSceneHandler = null;
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
            EditorApplication.delayCall +=
                Unity.Hierarchy.Editor.HierarchyWindow.RegisterNodeTypeHandler<HierarchyEntityHandler>;
        }

        [UsedImplicitly]
        internal static void UnregisterHierarchyHandlers()
        {
            Unity.Hierarchy.Editor.HierarchyWindow.UnregisterNodeTypeHandler<HierarchyEntityHandler>();
        }

        // For test purposes for now
        internal Unity.Hierarchy.HierarchyNode GetNode(Entity entity)
        {
            if (m_EntityToNodeMap.TryGetValue(entity, out var node))
                return node;
            return Unity.Hierarchy.HierarchyNode.Null;
        }

        // Test-only: add a lingering entity → world mapping to exercise ClearMappings paths
        // that would otherwise require reproducing complex multi-world disposal ordering.
        internal void Internal_AddEntityToWorld(Entity entity, Unity.Hierarchy.HierarchyNode node, ulong worldSequenceNumber)
        {
            m_EntityToNodeMap.TryAdd(entity, node);
            m_NodeToEntityMap.TryAdd(node, entity);
            m_EntityToWorld.TryAdd(entity, worldSequenceNumber);
        }

        void AddEntityNodes(World world, Entity parent, NativeArray<HierarchyEntityNodeData> nodes)
        {
            if (nodes.Length == 0)
                return;

            Unity.Hierarchy.HierarchyNode parentNode;
            if (parent == Entity.Null)
            {
                // If the parent entity doesn't exist create it under the world not
                parentNode = m_WorldHandler.GetOrCreateWorldNode(world);
            }
            else
            {
                if (world.EntityManager.HasComponent<SubScene>(parent) && m_SubSceneHandler != null)
                    parentNode = m_SubSceneHandler.GetOrCreateSubSceneNode(world, parent);
                else
                    m_EntityToNodeMap.TryGetValue(parent, out parentNode);

                if (parentNode == Unity.Hierarchy.HierarchyNode.Null)
                {
                    // If the parent node doesn't exist, create it under to world node
                    var worldNode = m_WorldHandler.GetOrCreateWorldNode(world);
                    CommandList.Add(worldNode, out parentNode);
                    CommandList.SetName(parentNode, parent.ToFixedString().Value);
                    m_EntityToNodeMap.Add(parent, parentNode);
                    m_NodeToEntityMap.Add(parentNode, parent);
                    m_EntityToWorld.Add(parent, world.SequenceNumber);
                }
            }

            AddNodes(world, parentNode, nodes);
        }

        void AddNodes(World world, Unity.Hierarchy.HierarchyNode parentNode, NativeArray<HierarchyEntityNodeData> nodes)
        {
            var worldSequenceNumber = world.SequenceNumber;
            var addedNodes = new NativeList<HierarchyEntityNodeData>(nodes.Length, Allocator.Temp);
            var addedNodesCount = 0;
            var newHierarchyNodes = new NativeList<Unity.Hierarchy.HierarchyNode>(nodes.Length, Allocator.Temp);

            using (k_AllocatingHierarchyNodesMarker.Auto())
            {
                for (var i = 0; i < nodes.Length; i++)
                {
                    if (!m_EntityToNodeMap.ContainsKey(nodes[i].Entity))
                    {
                        // Allocate new HierarchyNodes to add
                        var hierarchyNode = new Unity.Hierarchy.HierarchyNode();
                        newHierarchyNodes.Add(hierarchyNode);
                        addedNodes.Add(nodes[i]);
                        addedNodesCount++;
                    }
                }
            }

            using (k_CommandAddNodesMarker.Auto())
                CommandList.Add(parentNode, newHierarchyNodes.AsSpan());

            for (var i = 0; i < addedNodesCount; i++)
            {
                var entity = addedNodes[i].Entity;
                var hierarchyNode = newHierarchyNodes[i];

                // Update mappings after the HierarchyNodes have been created
                m_EntityToNodeMap.Add(entity, hierarchyNode);
                m_NodeToEntityMap.Add(hierarchyNode, entity);
                m_EntityToPrefabTypeMap.Add(entity, addedNodes[i].PrefabType);
                m_EntityToWorld.Add(entity, worldSequenceNumber);
            }

            // Set names to all children nodes
            // TODO: A batch command will make it faster to update each node name,
            // It should also take a fixed string to we reduce allocation when passing the name to the command
            using (k_CommandSetNamesMarker.Auto())
            {
                for (var i = 0; i < addedNodesCount; i++)
                {
                    var nodeData = addedNodes[i];
                    CommandList.SetName(newHierarchyNodes[i], nodeData.EntityName.ToString());
                }
            }

            addedNodes.Dispose();
            newHierarchyNodes.Dispose();
        }

        void ResizeMappings(int count)
        {
            if(m_EntityToNodeMap.Capacity < m_EntityToNodeMap.Count + count)
                m_EntityToNodeMap.Capacity = m_EntityToNodeMap.Count + count;
            if(m_NodeToEntityMap.Capacity < m_NodeToEntityMap.Count + count)
                m_NodeToEntityMap.Capacity = m_NodeToEntityMap.Count + count;
            if(m_EntityToPrefabTypeMap.Capacity < m_EntityToPrefabTypeMap.Count + count)
                m_EntityToPrefabTypeMap.Capacity = m_EntityToPrefabTypeMap.Count + count;
            if(m_EntityToWorld.Capacity < m_EntityToWorld.Count + count)
                m_EntityToWorld.Capacity = m_EntityToWorld.Count + count;
        }

        void RemoveEntityNodes(NativeList<Entity> entitiesToRemove)
        {
            foreach (var entity in entitiesToRemove)
            {
                if (m_EntityToNodeMap.TryGetValue(entity, out var node))
                {
                    CommandList.Remove(node);
                    m_EntityToNodeMap.Remove(entity);
                    m_EntityToPrefabTypeMap.Remove(entity);
                    m_NodeToEntityMap.Remove(node);
                    m_EntityToWorld.Remove(entity);
                }
            }
        }

        void SetParentNode(World world, NativeList<Entity> entityChildren, NativeList<Entity> entityParents)
        {
            if (entityChildren.Length != entityParents.Length)
            {
                Debug.LogError($"The number of entities being reparented {entityChildren.Length} should be the same as the number of new entity parent {entityParents.Length}");
                return;
            }

            using (k_CommandSetParentMarker.Auto())
            {
                for (var i = 0; i < entityChildren.Length; i++)
                {
                    if (!m_EntityToNodeMap.TryGetValue(entityChildren[i], out var nodeChild))
                    {
                        Debug.LogError($"Failed to find Entity child node: {entityChildren[i]} in {nameof(HierarchyEntityHandler)} mapping");
                        continue;
                    }

                    var parent = entityParents[i];
                    Unity.Hierarchy.HierarchyNode parentNode;

                    // A default parent entity means that the child is now parented underneath its world.
                    if (parent == default)
                        parentNode = m_WorldHandler.GetOrCreateWorldNode(world);
                    else
                    {
                        if (!m_EntityToNodeMap.TryGetValue(parent, out parentNode))
                        {
                            if (!m_SubSceneHandler.TryGetSubSceneNode(parent, out parentNode))
                            {
                                Debug.LogError($"Failed to find Entity parent node: {parent} in {nameof(HierarchyEntityHandler)} mapping");
                                continue;
                            }
                        }
                    }

                    CommandList.SetParent(nodeChild, parentNode);
                }
            }
        }

        internal void ClearMappings(World world)
        {
            var worldSequenceNumber = world.SequenceNumber;
            var allEntities = m_EntityToNodeMap.GetKeyArray(Allocator.Temp);

            foreach (var entity in allEntities)
            {
                // Check if entity belongs to this specific world
                if (m_EntityToWorld.TryGetValue(entity, out var entitySequenceNumber))
                {
                    if (entitySequenceNumber == worldSequenceNumber)
                    {
                        var node = m_EntityToNodeMap[entity];
                        m_EntityToNodeMap.Remove(entity);
                        m_NodeToEntityMap.Remove(node);
                        m_EntityToPrefabTypeMap.Remove(entity);
                        m_EntityToWorld.Remove(entity);
                    }
                }
            }

            allEntities.Dispose();
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
            if (!m_NodeToEntityMap.TryGetValue(item.Node, out var entity))
            {
                item.AddToClassList(k_PrefabUssClass);
                item.AddToClassList(k_PrefabRootUssClass);
                item.AddToClassList(k_EntityUssClass);
            }

            item.EnableInClassList(k_EntityUssClass, true);
            if (m_EntityToPrefabTypeMap.TryGetValue(entity, out var prefabType))
            {
                switch (prefabType)
                {
                    case HierarchyPrefabType.None:
                        item.EnableInClassList(k_PrefabUssClass, false);
                        item.EnableInClassList(k_PrefabRootUssClass, false);
                        break;
                    case HierarchyPrefabType.PrefabRoot:
                        item.EnableInClassList(k_PrefabUssClass, true);
                        item.EnableInClassList(k_PrefabRootUssClass, true);
                        break;
                    case HierarchyPrefabType.PrefabPart:
                        item.EnableInClassList(k_PrefabUssClass, true);
                        item.EnableInClassList(k_PrefabRootUssClass, false);
                        break;
                }
            }

            // TODO: Add support for more selection types like scrolling (ie: HierarchyGlobalSelectionHandler.SyncGlobalSelectionFromViewModel)
            item.RegisterCallback<ClickEvent, Unity.Hierarchy.HierarchyNode>(SelectEntityNode, item.Node);
        }

        protected override void OnUnbindItem(HierarchyViewItem item)
        {
            item.EnableInClassList(k_EntityUssClass, false);
            item.EnableInClassList(k_PrefabUssClass, false);
            item.EnableInClassList(k_PrefabRootUssClass, false);
            item.UnregisterCallback<ClickEvent, Unity.Hierarchy.HierarchyNode>(SelectEntityNode);
        }

        void SelectEntityNode(ClickEvent evt, Unity.Hierarchy.HierarchyNode node)
        {
            if (evt.clickCount < 1)
                return;
            if (!m_NodeToEntityMap.TryGetValue(node, out var entity))
                return;
            if (!m_EntityToWorld.TryGetValue(entity, out var worldSequenceNumber))
                return;

            var world = FindManagedWorld(worldSequenceNumber);
            if (world != null)
                EntitySelectionProxy.SelectEntity(world, entity);
        }

        static World FindManagedWorld(ulong sequenceNumber)
        {
            foreach (var world in World.All)
            {
                if (world.SequenceNumber == sequenceNumber)
                    return world;
            }
            return null;
        }

        protected override void SearchBegin(HierarchySearchQueryDescriptor query)
        {
            // Empty search bar = user wants to view the full hierarchy again. Drop any active
            // explicit filter so the system stops running its diff and other worlds become
            // visible again.
            if (query.Filters.Length == 0 && query.TextValues.Length == 0)
                ClearAnyActiveExplicitFilter();

            if (TryFindExplicitFilter(out var explicitQuery, out var explicitWorld))
                m_ComponentFilter.SetQuery(query, explicitQuery, explicitWorld);
            else
                m_ComponentFilter.SetQuery(query);
            base.SearchBegin(query);
        }

        static void ClearAnyActiveExplicitFilter()
        {
            foreach (var w in World.All)
            {
                if (!w.IsCreated)
                    continue;
                var sys = w.GetExistingSystemManaged<ExplicitFilterSystem>();
                if (sys != null && sys.HasExplicitFilter)
                    sys.ClearExplicitFilterQuery();
            }
        }

        static bool TryFindExplicitFilter(out EntityQuery query, out World world)
        {
            foreach (var w in World.All)
            {
                if (!w.IsCreated)
                    continue;
                var sys = w.GetExistingSystemManaged<ExplicitFilterSystem>();
                if (sys == null || !sys.HasExplicitFilter)
                    continue;
                query = sys.ExplicitFilterQuery;
                world = w;
                return true;
            }
            query = default;
            world = null;
            return false;
        }

        static void OnExplicitFilterChanged(World world, NativeList<Entity> added, NativeList<Entity> removed)
        {
            foreach (var window in EditorWindowBridge.GetActiveEditorWindows<Unity.Hierarchy.Editor.HierarchyWindow>())
            {
                var view = window.View;
                var current = view?.Filter;
                if (string.IsNullOrEmpty(current))
                    continue;
                // Re-assigning Filter calls HierarchyViewModel.SetQuery unconditionally, which
                // re-runs SearchMatch over the (now-changed) entity set without rebuilding the
                // QuickSearch chip — cheaper than going through HierarchyWindow.SetSearchText.
                view.Filter = current;
            }
        }

        protected override bool SearchMatch(in Unity.Hierarchy.HierarchyNode node)
        {
            // Get the entity for this node
            if (!m_NodeToEntityMap.TryGetValue(node, out var entity))
                return false;

            // Get the world for this entity
            if (!m_EntityToWorld.TryGetValue(entity, out var worldSequenceNumber))
                return false;

            var world = FindManagedWorld(worldSequenceNumber);
            if (world == null || !world.IsCreated)
                return false;

            // Check if entity matches the component filter
            return m_ComponentFilter.IsMatch(entity, world.Unmanaged);
        }

        protected override void SearchEnd()
        {
            m_ComponentFilter.Reset();
            base.SearchEnd();
        }

        IEnumerable<SearchProposition> Unity.Hierarchy.Editor.IHierarchySearchPropositionProvider.FetchPropositions(
            HierarchyViewModel viewModel,
            SearchContext context,
            SearchPropositionOptions options)
        {
            var token = options?.tokens?[0];

            // Show id= proposition when search is empty or user typed "id"
            if (string.IsNullOrEmpty(token) || token.Equals("id", StringComparison.OrdinalIgnoreCase) || token.StartsWith("id:", StringComparison.OrdinalIgnoreCase) || token.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SearchProposition(
                    category: "Filter",
                    label: "Entity ID",
                    replacement: "id=",
                    help: "Filter by Entity ID (index or index:version)",
                    icon: SearchUtils.entityIcon
                );
            }

            // Show component propositions when: search is empty, user typed "t", or user typed "t:something"
            // Hide propositions for unrelated searches (e.g., "tag", "test", "mesh")
            if (!string.IsNullOrEmpty(token) && !token.Equals("t", StringComparison.OrdinalIgnoreCase) && !token.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
                yield break;

            var seenTypes = new HashSet<string>();
            const string category = "Components";

            foreach (var typeInfo in TypeManager.AllTypes)
            {
                if (!EntityComponentFilter.IsValidComponentCategory(typeInfo.Category))
                    continue;

                var componentType = typeInfo.Type;
                if (componentType == null)
                    continue;

                var typeName = componentType.Name;
                if (!seenTypes.Add(typeName))
                    continue;

                yield return new SearchProposition(
                    category: category,
                    label: typeName,
                    replacement: $"t:{typeName}",
                    help: $"Filter objects with {typeName}",
                    icon: SearchUtils.entityIcon
                );
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
        UnityEngine.UIElements.DragVisualMode IHierarchyEditorNodeTypeHandler.CanReorder(in HierarchyViewDragAndDropHandlingData data) => UnityEngine.UIElements.DragVisualMode.None;
        void IHierarchyEditorNodeTypeHandler.OnReorder(in HierarchyViewDragAndDropHandlingData data) { }
        UnityEngine.UIElements.DragVisualMode IHierarchyEditorNodeTypeHandler.CanAcceptDrop(in HierarchyViewDragAndDropHandlingData data) => UnityEngine.UIElements.DragVisualMode.None;
        UnityEngine.UIElements.DragVisualMode IHierarchyEditorNodeTypeHandler.OnAcceptDrop(in HierarchyViewDragAndDropHandlingData data) => UnityEngine.UIElements.DragVisualMode.None;
        bool IHierarchyEditorNodeTypeHandler.CanFindReferences(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnFindReferences(HierarchyView view) => false;
        void IHierarchyEditorNodeTypeHandler.GetTooltip(HierarchyViewItem item, bool isFiltering, StringBuilder tooltip) { }
        void IHierarchyEditorNodeTypeHandler.PopulateContextMenu(HierarchyView view, HierarchyViewItem item, DropdownMenu menu) { }

        #endregion
    }
}
