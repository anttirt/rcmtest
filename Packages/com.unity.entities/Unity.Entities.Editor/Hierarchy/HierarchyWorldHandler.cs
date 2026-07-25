using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Unity.Hierarchy;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    internal class HierarchyWorldHandler : HierarchyNodeTypeHandler, IHierarchyEditorNodeTypeHandler
    {
        const string k_WorldUssClass = "hierarchy-item--world-node";
        const string k_StyleSheetPath = "Packages/com.unity.entities/Editor Default Resources/uss/Hierarchy/hierarchy-entity-item.uss";

        StyleSheet m_StyleSheet;
        StyleSheet m_ThemeStyleSheet;

        readonly Dictionary<World, Unity.Hierarchy.HierarchyNode> m_WorldToNodeMap = new();
        readonly Dictionary<Unity.Hierarchy.HierarchyNode, World> m_NodeToWorldMap = new();

        HierarchySubSceneRuntimeHandler m_SubSceneHandler;
        HierarchyEntityHandler m_EntityHandler;

        public override string GetNodeTypeName() => nameof(World);

        public override Unity.Hierarchy.HierarchyNodeFlags GetDefaultNodeFlags(in Unity.Hierarchy.HierarchyNode node, Unity.Hierarchy.HierarchyNodeFlags defaultFlags = Unity.Hierarchy.HierarchyNodeFlags.None)
        {
            return Unity.Hierarchy.HierarchyNodeFlags.Expanded | defaultFlags;
        }

        protected override void Initialize()
        {
            base.Initialize();

            // World nodes are only relevant in MainStage and PrefabStage (GameObject editing modes).
            // Skip event subscriptions for other stages like VisualElementEditingStage.
            if (StageUtility.GetCurrentStage() is not (MainStage or PrefabStage))
                return;

            SetSearchAlwaysVisible(true);

            if (World.DefaultGameObjectInjectionWorld == null)
                DefaultWorldInitialization.DefaultLazyEditModeInitialize();

            UpdateHierarchySystem.OnRemoveWorldNode += RemoveWorldNode;

            // Make sure to register new worlds after they are being initialized (ie: entering/exiting play mode)
            EditorApplication.playModeStateChanged += OnPlayModeStateChange;
            EditorApplication.update += OnUpdate;
        }

        protected override void Dispose(bool disposing)
        {
            m_WorldToNodeMap.Clear();
            m_NodeToWorldMap.Clear();

            UpdateHierarchySystem.OnRemoveWorldNode -= RemoveWorldNode;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
            EditorApplication.update -= OnUpdate;
            m_SubSceneHandler = null;
            m_EntityHandler = null;

            // Destroy the UpdateHierarchySystem and ExplicitFilterSystem systems when disposing the World handler.
            foreach (var world in World.s_AllWorlds)
            {
                var systemGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();

                var explicitFilterSystem = world.GetExistingSystemManaged<ExplicitFilterSystem>();
                if (explicitFilterSystem != null)
                {
                    systemGroup?.RemoveSystemFromUpdateList(explicitFilterSystem);
                    world.DestroySystem(explicitFilterSystem.SystemHandle);
                }

                var updateHierarchySystem = world.GetExistingSystemManaged<UpdateHierarchySystem>();
                if (updateHierarchySystem != null)
                {
                    systemGroup?.RemoveSystemFromUpdateList(updateHierarchySystem);
                    world.DestroySystem(updateHierarchySystem.SystemHandle);
                }
            }

            base.Dispose(disposing);
        }

        void OnUpdate()
        {
            UpdateRegisteredWorlds();
        }

        internal void RegisterAllHierarchySystems() => UpdateRegisteredWorlds();

        protected override void UpdateEnd()
        {
            base.UpdateEnd();
            RepositionWorldNodesToEnd();
        }

        void RepositionWorldNodesToEnd()
        {
            if (m_WorldToNodeMap.Count == 0)
                return;

            var childCount = Hierarchy.GetChildrenCount(Hierarchy.Root);
            if (childCount == 0)
                return;

            // World nodes should be within the last N positions, where N is the number of world nodes
            var worldNodeCount = m_WorldToNodeMap.Count;
            var firstValidIndex = childCount - worldNodeCount;

            // Reposition world nodes that are not already within the last N positions
            foreach (var worldNode in m_WorldToNodeMap.Values)
            {
                if (!Hierarchy.Exists(worldNode))
                    continue;

                var currentIndex = Hierarchy.GetChildIndex(worldNode);
                if (currentIndex < firstValidIndex)
                {
                    Hierarchy.SetParent(worldNode, Hierarchy.Root, childCount - 1);
                }
            }
        }

        void UpdateRegisteredWorlds()
        {
            var filterMask = HierarchyEntitiesSettings.GetTypesOfWorldsShown();
            foreach (var world in World.s_AllWorlds)
            {
                if (m_WorldToNodeMap.ContainsKey(world))
                    continue;
                if ((GetMainFlag(world) & filterMask) == 0)
                    continue;

                var systemGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

                // Make sure TransformSystemGroup is created, since UpdateHierarchySystem is updating after it.
                // Without creating the system group, we will see warnings when UpdateHierarchySystem cannot find the TransformSystemGroup.
                if (world.GetExistingSystemManaged<TransformSystemGroup>() == null)
                {
                    var transformSystemGroup = world.CreateSystemManaged<TransformSystemGroup>();
                    systemGroup.AddSystemToUpdateList(transformSystemGroup);
                }

                var hierarchySystem = world.GetOrCreateSystem<UpdateHierarchySystem>();
                systemGroup.AddSystemToUpdateList(hierarchySystem);
                hierarchySystem.Update(world.Unmanaged);

                var explicitFilterSystem = world.GetOrCreateSystemManaged<ExplicitFilterSystem>();
                systemGroup.AddSystemToUpdateList(explicitFilterSystem);
            }
        }

        internal static WorldFlags GetMainFlag(World world)
        {
            if ((world.Flags & WorldFlags.Shadow) != 0) return WorldFlags.Shadow;
            if ((world.Flags & WorldFlags.Conversion) != 0) return WorldFlags.Conversion;
            if ((world.Flags & WorldFlags.Live) != 0) return WorldFlags.Live;
            if ((world.Flags & WorldFlags.Streaming) != 0) return WorldFlags.Streaming;
            if ((world.Flags & WorldFlags.Staging) != 0) return WorldFlags.Staging;

            return WorldFlags.None;
        }

        void OnPlayModeStateChange(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (World.DefaultGameObjectInjectionWorld == null)
                    DefaultWorldInitialization.DefaultLazyEditModeInitialize();
            }
            RegisterAllHierarchySystems();
        }

        [InitializeOnLoadMethod, UsedImplicitly]
        internal static void RegisterHierarchyHandlers()
        {
            EditorApplication.delayCall += Unity.Hierarchy.Editor.HierarchyWindow.RegisterNodeTypeHandler<HierarchyWorldHandler>;
        }

        [UsedImplicitly]
        internal static void UnregisterHierarchyHandlers()
        {
            Unity.Hierarchy.Editor.HierarchyWindow.UnregisterNodeTypeHandler<HierarchyWorldHandler>();
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
            if (!m_NodeToWorldMap.TryGetValue(item.Node, out var world))
                item.AddToClassList(k_WorldUssClass);
            else
            {
                item.EnableInClassList(k_WorldUssClass, true);
            }
        }

        protected override void OnUnbindItem(HierarchyViewItem item)
        {
            item.EnableInClassList(k_WorldUssClass, false);
        }

        internal Unity.Hierarchy.HierarchyNode GetOrCreateWorldNode(World world)
        {
            if (m_WorldToNodeMap.TryGetValue(world, out var worldNode))
                return worldNode;

            // CommandList.Add appends to the end by default, no SetParent needed
            CommandList.Add(Hierarchy.Root, out worldNode);

            m_WorldToNodeMap.Add(world, worldNode);
            m_NodeToWorldMap.Add(worldNode, world);
            CommandList.SetName(worldNode, world.Name);

            return worldNode;
        }

        internal Unity.Hierarchy.HierarchyNode GetWorldNode(World world)
        {
            return m_WorldToNodeMap.GetValueOrDefault(world);
        }

        internal void RemoveWorldNode(World world)
        {
            if (m_WorldToNodeMap.TryGetValue(world, out var worldNode))
            {
                m_WorldToNodeMap.Remove(world);
                m_NodeToWorldMap.Remove(worldNode);

                // Clear all the entities from the entity handler mappings
                if (m_EntityHandler == null)
                    m_EntityHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchyEntityHandler>();
                m_EntityHandler.ClearMappings(world);

                if (m_SubSceneHandler == null)
                    m_SubSceneHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchySubSceneRuntimeHandler>();
                m_SubSceneHandler.ClearMappings(world);

                // Remove the world node from the hierarchy. This will also remove all children entity nodes
                CommandList.Remove(worldNode);
            }
        }

        #region IHierarchyEditorNodeTypeHandler

        bool IHierarchyEditorNodeTypeHandler.CanSetName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.OnSetName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node, string name) => false;

        string IHierarchyEditorNodeTypeHandler.GetDisplayName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node)
        {
            return m_NodeToWorldMap.TryGetValue(node, out var world) ? world.Name : Hierarchy.GetName(in node);
        }

        bool IHierarchyEditorNodeTypeHandler. CanDoubleClick(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
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
        bool IHierarchyEditorNodeTypeHandler. AcceptParent(HierarchyView view, in Unity.Hierarchy.HierarchyNode parent) => false;
        bool IHierarchyEditorNodeTypeHandler.AcceptChild(HierarchyView view, in Unity.Hierarchy.HierarchyNode child) => false;

        bool IHierarchyEditorNodeTypeHandler.CanStartDrag(HierarchyView view, ReadOnlySpan<Unity.Hierarchy.HierarchyNode> nodes)
        {
            bool canStartDrag = false;
            foreach (var node in nodes)
            {
                if (!m_NodeToWorldMap.TryGetValue(node, out var world))
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
