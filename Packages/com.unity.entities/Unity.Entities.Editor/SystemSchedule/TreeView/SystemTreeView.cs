using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemTreeView : VisualElement, System.IDisposable
    {
        static readonly string k_NoSystemsFoundTitle = L10n.Tr("No system matches your search");

        // internal for test.
        internal MultiColumnTreeView MultiColumnTreeViewElement { get; }
        internal IList<TreeViewItemData<SystemTreeViewItemData>> TreeViewRootItems { get; } = new List<TreeViewItemData<SystemTreeViewItemData>>();
        // Column Labels
        static readonly ObjectPool<VisualElement> k_CellLabelPool = new (() => new VisualElement());

        internal readonly List<TreeViewItemData<SystemTreeViewItemData>> m_ListViewFilteredItems = new ();

        internal System.Action<SystemProxy> systemSelectionChanged;

        int m_LastSelectedItemId;
        WorldProxy m_WorldProxy;
        readonly CenteredMessageElement m_SearchEmptyMessage;
        int m_ScrollToItemId = -1;

        bool m_IsSearching = false;
        IList<SearchItem> m_SearchResults;

        readonly List<SystemDescriptor> m_AllSystemsForSearch = new();
        readonly Dictionary<string, string[]> m_SystemDependencyMap = new();
        readonly List<SystemDescriptor> m_SearchResultsFlatSystemList = new();

        internal SystemGraph LocalSystemGraph;
        public static SystemProxy SelectedSystem;

        readonly HashSet<int> m_UpdateAfterHighlightedSystems = new();
        readonly HashSet<int> m_UpdateBeforeHighlightedSystems = new();

        readonly HashSet<int> m_UpdateAfterReverseHighlightedSystems = new();
        readonly HashSet<int> m_UpdateBeforeReverseHighlightedSystems = new();

        readonly List<int> m_HighlightedTreeViewIndices = new();
        bool m_IsProcessingSelectionChange;
        VisualElement m_UpArrowIndicator;
        VisualElement m_DownArrowIndicator;
        Label m_UpArrowCount;
        Label m_DownArrowCount;
        bool m_ScrollCallbackRegistered;

        public bool ShowMorePrecisionForRunningTime { get; set; }
        public bool Show0sInEntityCountAndTimeColumn { get; set; }
        public bool ShowUnityNamespaceSystems { get; set; } = true;
        public bool ShowPlayerLoop { get; set; } = true;

        internal static bool ShouldShowNode(IPlayerLoopNode node, WorldProxy worldProxy, bool showUnityNamespaceSystems)
        {
            return ShouldShowNode(node, worldProxy, showUnityNamespaceSystems, true);
        }

        internal static bool ShouldShowNode(IPlayerLoopNode node, WorldProxy worldProxy, bool showUnityNamespaceSystems, bool showPlayerLoop)
        {
            if (node is IPlayerLoopSystemData)
            {
                if (showPlayerLoop)
                    return true;
                return HasVisibleEcsDescendants(node, worldProxy, showUnityNamespaceSystems);
            }

            if (!node.ShowForWorldProxy(worldProxy))
                return false;

            if (showUnityNamespaceSystems)
                return true;

            foreach (var child in node.Children)
            {
                if (ShouldShowNode(child, worldProxy, showUnityNamespaceSystems, showPlayerLoop))
                    return true;
            }

            if (node is ISystemHandleNode systemHandleNode && systemHandleNode.SystemProxy.Valid)
            {
                var ns = systemHandleNode.SystemProxy.Namespace;
                return string.IsNullOrEmpty(ns) || !(ns == "Unity" || ns.StartsWith("Unity.", System.StringComparison.Ordinal) || ns.StartsWith("UnityEngine", System.StringComparison.Ordinal) || ns.StartsWith("UnityEditor", System.StringComparison.Ordinal));
            }

            return false;
        }

        static bool HasVisibleEcsDescendants(IPlayerLoopNode node, WorldProxy worldProxy, bool showUnityNamespaceSystems)
        {
            foreach (var child in node.Children)
            {
                if (child is IPlayerLoopSystemData)
                {
                    if (HasVisibleEcsDescendants(child, worldProxy, showUnityNamespaceSystems))
                        return true;
                }
                else if (ShouldShowNode(child, worldProxy, showUnityNamespaceSystems, false))
                {
                    return true;
                }
            }
            return false;
        }

        Column m_SystemColumn;
        Column m_SchedulingColumn;
        Column m_NamespaceColumn;
        Column m_EntityCountColumn;
        Column m_RunningTimeColumn;

        /// <summary>
        /// Constructor of the tree view.
        /// </summary>
        public SystemTreeView()
        {
            MultiColumnTreeViewElement = new MultiColumnTreeView()
            {
                name = "SystemTreeView",
                fixedItemHeight = Constants.ListView.ItemHeight,
                autoExpand = true,
                viewDataKey = "full-view",
                selectionType = SelectionType.Single,
                style =
                {
                    flexGrow = 1
                }
            };

            CreateColumns();

            MultiColumnTreeViewElement.columns.primaryColumnName = SystemScheduleWindow.Contents.System;
            MultiColumnTreeViewElement.SetRootItems(TreeViewRootItems);

            MultiColumnTreeViewElement.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (m_ScrollToItemId == -1)
                    return;

                var tempId = m_ScrollToItemId;
                m_ScrollToItemId = -1;
                if (MultiColumnTreeViewElement.GetItemDataForId<SystemTreeViewItemData>(tempId) != null)
                    MultiColumnTreeViewElement.ScrollToItemById(tempId);
            });

            MultiColumnTreeViewElement.selectionChanged += OnSelectionChanged;
            Add(MultiColumnTreeViewElement);

            m_SearchEmptyMessage = new CenteredMessageElement { Title = k_NoSystemsFoundTitle };
            m_SearchEmptyMessage.Hide();
            Add(m_SearchEmptyMessage);

            MultiColumnTreeViewElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == MultiColumnTreeViewElement.Q(className: ScrollView.contentAndVerticalScrollUssClassName))
                    Selection.activeObject = null;
            });
        }

        void CreateArrowIndicators()
        {
            if (m_UpArrowIndicator != null)
                return;

            var foldoutIcon = EditorGUIUtility.IconContent("IN foldout on").image as Texture2D;

            m_UpArrowIndicator = new VisualElement();
            m_UpArrowIndicator.AddToClassList("scheduling-arrow-indicator");
            m_UpArrowIndicator.AddToClassList("scheduling-arrow-indicator--up");
            Resources.Templates.SystemScheduleItem.AddStyles(m_UpArrowIndicator);
            m_UpArrowIndicator.style.display = DisplayStyle.None;
            var upArrowIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            upArrowIcon.AddToClassList("scheduling-arrow-indicator__icon");
            upArrowIcon.AddToClassList("scheduling-arrow-indicator__icon--up");
            upArrowIcon.style.backgroundImage = foldoutIcon;
            m_UpArrowCount = new Label { pickingMode = PickingMode.Ignore };
            m_UpArrowCount.AddToClassList("scheduling-arrow-indicator__count-label");
            m_UpArrowIndicator.Add(upArrowIcon);
            m_UpArrowIndicator.Add(m_UpArrowCount);
            m_UpArrowIndicator.RegisterCallback<ClickEvent>(_ => ScrollToNextHighlightedSystem(true));

            m_DownArrowIndicator = new VisualElement();
            m_DownArrowIndicator.AddToClassList("scheduling-arrow-indicator");
            m_DownArrowIndicator.AddToClassList("scheduling-arrow-indicator--down");
            Resources.Templates.SystemScheduleItem.AddStyles(m_DownArrowIndicator);
            m_DownArrowIndicator.style.display = DisplayStyle.None;
            var downArrowIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            downArrowIcon.AddToClassList("scheduling-arrow-indicator__icon");
            downArrowIcon.style.backgroundImage = foldoutIcon;
            m_DownArrowCount = new Label { pickingMode = PickingMode.Ignore };
            m_DownArrowCount.AddToClassList("scheduling-arrow-indicator__count-label");
            m_DownArrowIndicator.Add(downArrowIcon);
            m_DownArrowIndicator.Add(m_DownArrowCount);
            m_DownArrowIndicator.RegisterCallback<ClickEvent>(_ => ScrollToNextHighlightedSystem(false));

            Add(m_UpArrowIndicator);
            Add(m_DownArrowIndicator);

            if (!m_ScrollCallbackRegistered)
            {
                var scrollView = MultiColumnTreeViewElement.Q<ScrollView>();
                if (scrollView != null)
                {
                    scrollView.verticalScroller.valueChanged += _ => UpdateArrowIndicators();
                    m_ScrollCallbackRegistered = true;
                }
            }
        }

        internal void RebuildColumns()
        {
            MultiColumnTreeViewElement.columns.Clear();
            MultiColumnTreeViewElement.columns.Add(m_SystemColumn);
            MultiColumnTreeViewElement.columns.Add(m_SchedulingColumn);
            MultiColumnTreeViewElement.columns.Add(m_NamespaceColumn);
            MultiColumnTreeViewElement.columns.Add(m_EntityCountColumn);
            MultiColumnTreeViewElement.columns.Add(m_RunningTimeColumn);
            Resources.Templates.SystemScheduleItem.AddStyles(MultiColumnTreeViewElement);
        }

        void CreateColumns()
        {
            const string headerStr = "Header";

            m_SystemColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.System,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.System;
                    label.tooltip = SystemScheduleWindow.Contents.SystemTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.System);
                },
                makeCell = MakeTreeViewItem,
                bindCell = BindSystemItem,
                resizable = true,
                optional = false,
                destroyCell = ReleaseTreeViewItem,
                minWidth = 100,
                width = 300
            };

            m_SchedulingColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.Scheduling,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.Scheduling;
                    label.tooltip = SystemScheduleWindow.Contents.SchedulingTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.Scheduling);
                },
                makeCell = MakeSchedulingCell,
                bindCell = BindSchedulingCell,
                resizable = true,
                minWidth = 100,
                width = 300
            };

            m_NamespaceColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.Namespace,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.Namespace;
                    label.tooltip = SystemScheduleWindow.Contents.NamespaceTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.Namespace);
                },
                makeCell = MakeCellLabel,
                bindCell = BindNamespaceCell,
                resizable = true,
                width = 100
            };

            m_EntityCountColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.EntityCount,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.EntityCount;
                    label.tooltip = SystemScheduleWindow.Contents.EntityCountTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.EntityCount);
                },
                makeCell = MakeCellLabel,
                bindCell = BindEntityCountCell,
                resizable = true,
                width = 100
            };

           m_RunningTimeColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.Time,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>("Header");
                    label.text = SystemScheduleWindow.Contents.Time;
                    label.tooltip = SystemScheduleWindow.Contents.TimeTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.Time);
                },
                makeCell = MakeCellLabel,
                bindCell = BindRunningTimeCell,
                resizable = true,
                width = 100
            };
        }
        void OnSelectionChanged(IEnumerable<object> selection)
        {
            SystemTreeViewItemData selectedItem = null;
            foreach (var obj in selection)
            {
                selectedItem = obj as SystemTreeViewItemData;
                if (selectedItem != null)
                    break;
            }
            OnSelectionChanged(selectedItem);
        }

        void OnSelectionChanged(SystemTreeViewItemData selectedItem)
        {
            if (m_IsProcessingSelectionChange)
                return;
            m_IsProcessingSelectionChange = true;
            try
            {
                OnSelectionChangedInternal(selectedItem);
            }
            finally
            {
                m_IsProcessingSelectionChange = false;
            }
        }

        void OnSelectionChangedInternal(SystemTreeViewItemData selectedItem)
        {
            // By selecting a system within Systems window, we need to clear up SelectedSystem which is set only from the outside.
            SelectedSystem = default;

            m_UpdateAfterHighlightedSystems.Clear();
            m_UpdateBeforeHighlightedSystems.Clear();
            m_UpdateBeforeReverseHighlightedSystems.Clear();
            m_UpdateAfterReverseHighlightedSystems.Clear();

            if (selectedItem == null || !selectedItem.SystemProxy.Valid)
            {
                m_HighlightedTreeViewIndices.Clear();
                if (m_UpArrowIndicator != null)
                    m_UpArrowIndicator.style.display = DisplayStyle.None;
                if (m_DownArrowIndicator != null)
                    m_DownArrowIndicator.style.display = DisplayStyle.None;
                MultiColumnTreeViewElement.RefreshItems();
                return;
            }

            m_LastSelectedItemId = selectedItem.id;
            m_ScrollToItemId = selectedItem.id;

            foreach (var dep in selectedItem.GetUpdateBeforeSystemNames())
                m_UpdateBeforeHighlightedSystems.Add(dep.SystemIndex);

            foreach (var dep in selectedItem.GetUpdateAfterSystemNames())
                m_UpdateAfterHighlightedSystems.Add(dep.SystemIndex);

            foreach (var dep in selectedItem.GetUpdateBeforeReverseSystemNames())
                m_UpdateBeforeReverseHighlightedSystems.Add(dep.SystemIndex);

            foreach (var dep in selectedItem.GetUpdateAfterReverseSystemNames())
                m_UpdateAfterReverseHighlightedSystems.Add(dep.SystemIndex);

            MultiColumnTreeViewElement.RefreshItems();

            CreateArrowIndicators();
            BuildHighlightedTreeViewIndices();
            UpdateArrowIndicators();

            systemSelectionChanged?.Invoke(selectedItem.SystemProxy);
        }

        VisualElement MakeHeaderLabel()
        {
            var element = new VisualElement();
            Resources.Templates.SystemScheduleTreeViewHeader.AddStyles(element);
            var label = new Label
            {
                name = "Header",
            };
            element.Add(label);
            return element;
        }

        VisualElement MakeCellLabel()
        {
            var element = k_CellLabelPool.Get();
            Resources.Templates.SystemScheduleItem.AddStyles(element);
            var label = new Label
            {
                name = "Cell"
            };
            element.Add(label);
            return element;
        }

        static readonly string k_UpdateBeforeLabel = "UpdateBefore";
        static readonly string k_UpdateAfterLabel = "UpdateAfter";
        static readonly string k_UpdateBeforeReverseLabel = "UpdateBeforeReverse";
        static readonly string k_UpdateAfterReverseLabel = "UpdateAfterReverse";

        VisualElement MakeSchedulingCell()
        {
            var element = k_CellLabelPool.Get();
            Resources.Templates.SystemScheduleItem.AddStyles(element);
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;

            element.Add(MakeSchedulingPill(k_UpdateBeforeLabel, "scheduling-pill--update-before", SystemScheduleWindow.Contents.UpdateBeforeSchedulingTooltip));
            element.Add(MakeSchedulingPill(k_UpdateAfterLabel, "scheduling-pill--update-after", SystemScheduleWindow.Contents.UpdateAfterSchedulingTooltip));
            element.Add(MakeSchedulingPill(k_UpdateBeforeReverseLabel, "scheduling-pill--update-before-reverse", SystemScheduleWindow.Contents.UpdateBeforeReverseSchedulingTooltip));
            element.Add(MakeSchedulingPill(k_UpdateAfterReverseLabel, "scheduling-pill--update-after-reverse", SystemScheduleWindow.Contents.UpdateAfterReverseSchedulingTooltip));

            return element;
        }

        static Label MakeSchedulingPill(string pillName, string variantClass, string tooltip)
        {
            var pill = new Label { name = pillName };
            pill.AddToClassList("scheduling-pill");
            pill.AddToClassList(variantClass);
            pill.tooltip = tooltip;
            pill.style.display = DisplayStyle.None;
            return pill;
        }

        VisualElement MakeTreeViewItem() => SystemInformationVisualElement.Acquire(this);

        static void ReleaseTreeViewItem(VisualElement ve)
        {
            if(ve  != null)
                ((SystemInformationVisualElement)ve).Release();
        }

        public void StopSearch()
        {
            m_IsSearching = false;
            Refresh();
        }

        public void SetResults(IList<SearchItem> results)
        {
            m_IsSearching = true;
            m_SearchResults = results;
            Refresh();
        }

        public void Refresh(WorldProxy worldProxy)
        {
            m_WorldProxy = worldProxy;

            m_AllSystemsForSearch.Clear();
            m_SystemDependencyMap.Clear();

            RecreateTreeViewRootItems();
            FillSystemDependencyCache(m_AllSystemsForSearch, m_SystemDependencyMap);
            Refresh();
        }

        void RecreateTreeViewRootItems()
        {
            ReleaseAllPooledItems();

            if (World.All.Count > 0)
            {
                var graph = LocalSystemGraph;

                foreach (var node in graph.Roots)
                {
                    if (ShouldShowNode(node, m_WorldProxy, ShowUnityNamespaceSystems, ShowPlayerLoop))
                        AddNodeToTreeView((PlayerLoopSystemGraph)graph, node);
                }

                MultiColumnTreeViewElement.SetRootItems(TreeViewRootItems);
                MultiColumnTreeViewElement.Rebuild();
            }
        }

        void AddNodeToTreeView(PlayerLoopSystemGraph graph, IPlayerLoopNode node)
        {
            var item = SystemTreeViewItemData.Acquire(graph, node, m_WorldProxy, ShowUnityNamespaceSystems, ShowPlayerLoop);
            PopulateAllChildren(item);

            var children = GetAllChildren(item);
            TreeViewRootItems.Add(new TreeViewItemData<SystemTreeViewItemData>(item.id, item, children));
        }

        void PopulateAllChildren(SystemTreeViewItemData item)
        {
            if (item.SystemProxy.Valid)
            {
                var systemForSearch = new SystemDescriptor(item.SystemProxy)
                {
                    Node = item.Node,
                };
                m_AllSystemsForSearch.Add(systemForSearch);
                SystemProxy.BuildSystemDependencyMap(item.SystemProxy, m_SystemDependencyMap);
            }

            if (!item.HasChildren)
                return;

            item.PopulateChildren();

            foreach (var child in item.children)
                PopulateAllChildren(child.data);
        }

        static List<TreeViewItemData<SystemTreeViewItemData>> GetAllChildren(SystemTreeViewItemData item)
        {
            var result = new List<TreeViewItemData<SystemTreeViewItemData>>();
            foreach (var child in item.children)
            {
                var children = GetAllChildren(child.data);
                result.Add(new TreeViewItemData<SystemTreeViewItemData>(child.id, child.data, children));
            }
            return result;
        }

        static void FillSystemDependencyCache(List<SystemDescriptor> descriptors, Dictionary<string, string[]> dependencyMap)
        {
            foreach (var desc in descriptors)
            {
                var dependenciesList = new List<string>();
                foreach (var (system, dependencies) in dependencyMap)
                {
                    if (dependencies != null && System.Array.IndexOf(dependencies, desc.Name) >= 0)
                        dependenciesList.Add(system);
                }
                var dependenciesArr = dependenciesList.ToArray();
                desc.UpdateDependencies(dependenciesArr);
            }
        }

        void BuildFilterResults()
        {
            m_SearchResultsFlatSystemList.Clear();

            if (!m_IsSearching)
                m_SearchResultsFlatSystemList.AddRange(m_AllSystemsForSearch);
            else
            {
                foreach (var result in m_SearchResults)
                {
                    // TODO: Make use of ComponentSystemBase directly
                    var system = (SystemDescriptor)result.data;
                    var index = m_AllSystemsForSearch.FindIndex(x => x.Proxy == system.Proxy);
                    if (index > -1)
                        m_SearchResultsFlatSystemList.Add(m_AllSystemsForSearch[index]);
                }
            }
        }

        void PopulateListViewWithSearchResults()
        {
            BuildFilterResults();

            foreach (var filteredItem in m_ListViewFilteredItems)
            {
                filteredItem.data.Release();
            }
            m_ListViewFilteredItems.Clear();
            foreach (var system in m_SearchResultsFlatSystemList)
            {
                var listViewItems = SystemTreeViewItemData.Acquire(LocalSystemGraph, system.Node, m_WorldProxy, ShowUnityNamespaceSystems, ShowPlayerLoop);
                m_ListViewFilteredItems.Add(new TreeViewItemData<SystemTreeViewItemData>(listViewItems.id, listViewItems));
            }
        }

        /// <summary>
        /// Refresh tree view to update with latest information.
        /// </summary>
        void Refresh()
        {
            // Check if there is search result
            if (m_IsSearching)
            {
                PopulateListViewWithSearchResults();
                var hasSearchResult = m_ListViewFilteredItems.Count > 0;

                MultiColumnTreeViewElement.SetVisibility(hasSearchResult);
                m_SearchEmptyMessage.SetVisibility(!hasSearchResult);
                m_SearchEmptyMessage.Title = k_NoSystemsFoundTitle;
                m_SearchEmptyMessage.Message = string.Empty;
            }
            else
            {
                MultiColumnTreeViewElement.Show();
                m_SearchEmptyMessage.Hide();
            }
            SetSelection();
        }

        public void SetSelection()
        {
            // Update last selected item ID if we have a valid selected system
            if (SelectedSystem.Valid && (m_WorldProxy == null || SelectedSystem.WorldProxy.Equals(m_WorldProxy)) && m_AllSystemsForSearch.Count > 0)
            {
                SystemDescriptor selectedSystem = null;
                foreach (var s in m_AllSystemsForSearch)
                {
                    if (s.Proxy.Equals(SelectedSystem))
                    {
                        selectedSystem = s;
                        break;
                    }
                }
                // Tree view item ids are Node.Hash, not Proxy.SystemIndex.
                if (selectedSystem?.Node != null)
                    m_LastSelectedItemId = selectedSystem.Node.Hash;
            }

            // Set up tree view with appropriate root items and rebuild
            MultiColumnTreeViewElement.ClearSelection();
            MultiColumnTreeViewElement.SetRootItems(m_IsSearching ? m_ListViewFilteredItems : TreeViewRootItems);
            MultiColumnTreeViewElement.Rebuild();

            // Restore selection if we have a valid last selected item
            if (MultiColumnTreeViewElement.GetItemDataForId<SystemTreeViewItemData>(m_LastSelectedItemId) == null)
                return;

            MultiColumnTreeViewElement.SetSelectionByIdWithoutNotify(new []{ m_LastSelectedItemId });
            MultiColumnTreeViewElement.RefreshItems();
            MultiColumnTreeViewElement.ScrollToItemById(m_LastSelectedItemId);
        }

        public bool TrySelectSystem(SystemProxy systemProxy)
        {
            if (!systemProxy.Valid)
                return false;

            SystemDescriptor descriptor = null;
            foreach (var s in m_AllSystemsForSearch)
            {
                if (s.Proxy.Equals(systemProxy))
                {
                    descriptor = s;
                    break;
                }
            }

            if (descriptor?.Node == null)
                return false;

            var id = descriptor.Node.Hash;
            if (MultiColumnTreeViewElement.GetItemDataForId<SystemTreeViewItemData>(id) == null)
                return false;

            SelectedSystem = systemProxy;
            // With-notify variant so OnSelectionChangedInternal runs and refreshes arrows,
            // highlighted dependency rows, and fires systemSelectionChanged for the inspector.
            MultiColumnTreeViewElement.SetSelectionById(new[] { id });
            MultiColumnTreeViewElement.ScrollToItemById(id);
            return true;
        }

        void BindSystemItem(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            var systemInformationElement = element as SystemInformationVisualElement;
            if (null == systemInformationElement)
                return;

            systemInformationElement.IndexInTreeView = index;
            systemInformationElement.Target = progressItem;
        }

        void BindSchedulingCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem == null)
                return;

            var updateBeforeLabel = element.Q<Label>(k_UpdateBeforeLabel);
            var updateAfterLabel = element.Q<Label>(k_UpdateAfterLabel);
            var updateBeforeReverseLabel = element.Q<Label>(k_UpdateBeforeReverseLabel);
            var updateAfterReverseLabel = element.Q<Label>(k_UpdateAfterReverseLabel);

            var systemProxy = progressItem.SystemProxy;
            element.AddToClassList(UssClasses.SystemScheduleWindow.Items.SchedulingNameColumn);

            if (!systemProxy.Valid)
            {
                SetSchedulingLabel(updateBeforeLabel, false, null);
                SetSchedulingLabel(updateAfterLabel, false, null);
                SetSchedulingLabel(updateBeforeReverseLabel, false, null);
                SetSchedulingLabel(updateAfterReverseLabel, false, null);
                return;
            }

            var systemIndex = systemProxy.SystemIndex;

            SetSchedulingLabel(updateBeforeLabel, m_UpdateBeforeHighlightedSystems.Contains(systemIndex), "UpdateBefore");
            SetSchedulingLabel(updateAfterLabel, m_UpdateAfterHighlightedSystems.Contains(systemIndex), "UpdateAfter");
            SetSchedulingLabel(updateBeforeReverseLabel, m_UpdateBeforeReverseHighlightedSystems.Contains(systemIndex), "Scheduled before");
            SetSchedulingLabel(updateAfterReverseLabel, m_UpdateAfterReverseHighlightedSystems.Contains(systemIndex), "Scheduled after");
        }

        static void SetSchedulingLabel(Label label, bool visible, string text)
        {
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            label.text = text;
        }

        void BuildHighlightedTreeViewIndices()
        {
            m_HighlightedTreeViewIndices.Clear();

            if (m_UpdateBeforeHighlightedSystems.Count == 0 &&
                m_UpdateAfterHighlightedSystems.Count == 0 &&
                m_UpdateBeforeReverseHighlightedSystems.Count == 0 &&
                m_UpdateAfterReverseHighlightedSystems.Count == 0)
                return;

            var totalItems = MultiColumnTreeViewElement.GetTreeCount();
            for (var i = 0; i < totalItems; i++)
            {
                var item = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(i);
                if (item?.SystemProxy == null || !item.SystemProxy.Valid)
                    continue;

                var sysIndex = item.SystemProxy.SystemIndex;
                if (m_UpdateBeforeHighlightedSystems.Contains(sysIndex) ||
                    m_UpdateAfterHighlightedSystems.Contains(sysIndex) ||
                    m_UpdateBeforeReverseHighlightedSystems.Contains(sysIndex) ||
                    m_UpdateAfterReverseHighlightedSystems.Contains(sysIndex))
                {
                    m_HighlightedTreeViewIndices.Add(i);
                }
            }
        }

        void UpdateArrowIndicators()
        {
            if (m_UpArrowIndicator == null || m_DownArrowIndicator == null)
                return;

            if (m_HighlightedTreeViewIndices.Count == 0)
            {
                m_UpArrowIndicator.style.display = DisplayStyle.None;
                m_DownArrowIndicator.style.display = DisplayStyle.None;
                return;
            }

            var scrollView = MultiColumnTreeViewElement.Q<ScrollView>();
            if (scrollView == null)
            {
                m_UpArrowIndicator.style.display = DisplayStyle.None;
                m_DownArrowIndicator.style.display = DisplayStyle.None;
                return;
            }

            var scrollOffset = scrollView.scrollOffset.y;
            var viewportHeight = scrollView.contentViewport.layout.height;
            var itemHeight = MultiColumnTreeViewElement.fixedItemHeight;

            if (itemHeight <= 0 || float.IsNaN(viewportHeight) || viewportHeight <= 0)
            {
                m_UpArrowIndicator.style.display = DisplayStyle.None;
                m_DownArrowIndicator.style.display = DisplayStyle.None;
                return;
            }

            var firstVisibleIndex = (int)(scrollOffset / itemHeight);
            var lastVisibleIndex = (int)((scrollOffset + viewportHeight) / itemHeight);

            var aboveCount = 0;
            var belowCount = 0;
            foreach (var idx in m_HighlightedTreeViewIndices)
            {
                if (idx < firstVisibleIndex)
                    aboveCount++;
                else if (idx > lastVisibleIndex)
                    belowCount++;
            }

            if (aboveCount > 0)
            {
                m_UpArrowIndicator.style.display = DisplayStyle.Flex;
                m_UpArrowCount.text = aboveCount > 1 ? $"{aboveCount} dependencies above" : "1 dependency above";
            }
            else
            {
                m_UpArrowIndicator.style.display = DisplayStyle.None;
            }

            if (belowCount > 0)
            {
                m_DownArrowIndicator.style.display = DisplayStyle.Flex;
                m_DownArrowCount.text = belowCount > 1 ? $"{belowCount} dependencies below" : "1 dependency below";
            }
            else
            {
                m_DownArrowIndicator.style.display = DisplayStyle.None;
            }
        }

        void ScrollToNextHighlightedSystem(bool up)
        {
            if (m_HighlightedTreeViewIndices.Count == 0)
                return;

            var scrollView = MultiColumnTreeViewElement.Q<ScrollView>();
            if (scrollView == null)
                return;

            var scrollOffset = scrollView.scrollOffset.y;
            var viewportHeight = scrollView.contentViewport.layout.height;
            var itemHeight = MultiColumnTreeViewElement.fixedItemHeight;

            if (itemHeight <= 0 || float.IsNaN(viewportHeight) || viewportHeight <= 0)
                return;

            var firstVisibleIndex = (int)(scrollOffset / itemHeight);
            var lastVisibleIndex = (int)((scrollOffset + viewportHeight) / itemHeight);

            if (up)
            {
                for (var i = m_HighlightedTreeViewIndices.Count - 1; i >= 0; i--)
                {
                    if (m_HighlightedTreeViewIndices[i] < firstVisibleIndex)
                    {
                        MultiColumnTreeViewElement.ScrollToItem(m_HighlightedTreeViewIndices[i]);
                        return;
                    }
                }
            }
            else
            {
                foreach (var idx in m_HighlightedTreeViewIndices)
                {
                    if (idx > lastVisibleIndex)
                    {
                        MultiColumnTreeViewElement.ScrollToItem(idx);
                        return;
                    }
                }
            }
        }

        void BindNamespaceCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                label.text = progressItem.GetNamespace();
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.NamespaceColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.Namespace);
                if (progressItem.SystemProxy != null && progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
            }
        }

        void BindEntityCountCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                var entityCount = progressItem.GetEntityMatches();
                label.text = entityCount;
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.EntityCountColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.EntityCount);
                if (progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
                if (!Show0sInEntityCountAndTimeColumn && entityCount.Equals("0"))
                {
                    label.Hide();
                }
                else
                {
                    label.Show();
                }
            }
        }

        void BindRunningTimeCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                var runningTime = progressItem.GetRunningTime(ShowMorePrecisionForRunningTime);
                label.text = runningTime;
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.TimeColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.Time);
                if (progressItem.SystemProxy != null && progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
                if (!Show0sInEntityCountAndTimeColumn &&
                    (runningTime.Equals("0.00") || runningTime.Equals("0.0000")))
                {
                    label.Hide();
                }
                else
                {
                    label.Show();
                }
            }
        }

        public void Dispose() => ReleaseAllPooledItems();

        void ReleaseAllPooledItems()
        {
            foreach (var rootItem in TreeViewRootItems)
            {
                rootItem.data.Release();
            }
            TreeViewRootItems.Clear();

            foreach (var filteredItem in m_ListViewFilteredItems)
            {
                filteredItem.data.Release();
            }
            m_ListViewFilteredItems.Clear();
            k_CellLabelPool.Clear();
        }
    }
}
