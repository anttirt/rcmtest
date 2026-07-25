using System;
using System.Collections.Generic;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using static Unity.Entities.StructuralChangesProfiler;
using TreeView = UnityEngine.UIElements.TreeView;

namespace Unity.Entities.Editor
{
    partial class StructuralChangesProfilerModule
    {
        class StructuralChangesProfilerModuleView
        {
            static readonly string s_StructuralChanges = L10n.Tr("Structural Changes");
            static readonly string s_All = L10n.Tr("All");
            static readonly string s_Cost = L10n.Tr("Cost (ms)");
            static readonly string s_Count = L10n.Tr("Count");
            static readonly string s_CreateEntity = L10n.Tr(k_CreateEntityCounterName);
            static readonly string s_DestroyEntity = L10n.Tr(k_DestroyEntityCounterName);
            static readonly string s_AddComponent = L10n.Tr(k_AddComponentCounterName);
            static readonly string s_RemoveComponent = L10n.Tr(k_RemoveComponentCounterName);
            static readonly string s_SetSharedComponent = L10n.Tr(k_SetSharedComponentCounterName);

            static readonly VisualElementTemplate s_WindowTemplate = PackageResources.LoadTemplate("ProfilerModules/structural-changes-profiler-window");

            static readonly ObjectPool<VisualElement> k_CellLabelPool = new (() => new VisualElement());

            TreeViewItemData<StructuralChangesProfilerTreeViewItemData>[] m_StructuralChangesDataSource;
            readonly List<TreeViewItemData<StructuralChangesProfilerTreeViewItemData>> m_StructuralChangesDataFiltered = new ();

            VisualElement m_Window;
            Label m_Message;
            VisualElement m_Content;
            SearchElement m_SearchElement;
            MultiColumnTreeView m_TreeView;

            public TreeViewItemData<StructuralChangesProfilerTreeViewItemData>[] StructuralChangesDataSource
            {
                get => m_StructuralChangesDataSource;
                set => m_StructuralChangesDataSource = value;
            }

            public bool HasStructuralChangesDataSource => m_StructuralChangesDataSource?.Length > 0;

            public Action SearchFinished { get; set; }

            public VisualElement Create()
            {
                m_Window = s_WindowTemplate.Clone();

                var toolbar = m_Window.Q<Toolbar>("toolbar");
                m_SearchElement = toolbar.Q<SearchElement>("search");
                m_SearchElement.AddSearchDataCallback<StructuralChangesProfilerTreeViewItemData>(data =>
                {
                    var result = new string[3];
                    result[0] = ObjectNames.NicifyVariableName(data.Type.ToString());
                    result[1] = data.WorldName;
                    result[2] = data.SystemName;
                    return result;
                });
                m_SearchElement.AddSearchFilterCallbackWithPopupItem<StructuralChangesProfilerTreeViewItemData, double>("cost", data => data.ElapsedNanoseconds * 1e-6, s_Cost);
                m_SearchElement.FilterPopupWidth = 250;

                var searchHandler = new SearchHandler<TreeViewItemData<StructuralChangesProfilerTreeViewItemData>>(m_SearchElement)
                {
                    Mode = SearchHandlerType.async
                };
                searchHandler.SetSearchDataProvider(() => m_StructuralChangesDataSource);
                searchHandler.OnBeginSearch += _ =>
                {
                    m_StructuralChangesDataFiltered.Clear();
                };
                searchHandler.OnFilter += (query, filteredData) =>
                {
                    m_StructuralChangesDataFiltered.AddRange(filteredData);
                    SearchFinished?.Invoke();
                };
                // Work-around: OnFilter is not called if the search result yields no data.
                // So for the moment, we force a refresh in OnEndSearch, which means 2 refresh :(
                searchHandler.OnEndSearch += query =>
                {
                    SearchFinished?.Invoke();
                };

                m_Message = m_Window.Q<Label>("message");
                m_Message.text = s_NoFrameDataAvailable;

                m_Content = m_Window.Q("content");
                m_Content.SetVisibility(false);

                var container = m_Content.Q("tree-view-container");

                m_TreeView = new MultiColumnTreeView()
                {
                    name = "StructuralChangeModuleTreeView",
                    fixedItemHeight = 18,
                    autoExpand = true,
                    viewDataKey = "full-view",
                    selectionType = SelectionType.Single
                };
                m_TreeView.AddToClassList("structural-changes-profiler-window__tree-view");
                CreateColumns(m_TreeView);
                container.Add(m_TreeView);

                return m_Window;
            }

            void CreateColumns(MultiColumnTreeView treeView)
            {
                const string headerStr = "Header";

                var structuralColumn = new Column()
                {
                    name = s_StructuralChanges,
                    makeHeader = MakeHeaderLabel,
                    bindHeader = e =>
                    {
                        var label = e.Q<Label>(headerStr);
                        label.text = s_StructuralChanges;
                    },
                    makeCell = MakeCellLabel,
                    bindCell = BindStructuralItem,
                    destroyCell = DestroyCellLabel,
                    resizable = true,
                    minWidth = 100,
                    width = 300
                };

                var costColumn = new Column()
                {
                    name = s_Cost,
                    makeHeader = MakeHeaderLabel,
                    bindHeader = e =>
                    {
                        var label = e.Q<Label>(headerStr);
                        label.text = s_Cost;
                    },
                    makeCell = MakeCellLabel,
                    bindCell = BindCostItem,
                    destroyCell = DestroyCellLabel,
                    resizable = true,
                    width = 100
                };

                var countColumn = new Column()
                {
                    name = s_Count,
                    makeHeader = MakeHeaderLabel,
                    bindHeader = e =>
                    {
                        var label = e.Q<Label>(headerStr);
                        label.text = s_Count;
                    },
                    makeCell = MakeCellLabel,
                    bindCell = BindCountItem,
                    destroyCell = DestroyCellLabel,
                    resizable = true,
                    width = 100
                };

                treeView.columns.Add(structuralColumn);
                treeView.columns.Add(costColumn);
                treeView.columns.Add(countColumn);
            }

            static VisualElement MakeHeaderLabel()
            {
                var label = new Label
                {
                    name = "Header",
                };
                label.AddToClassList("structural-changes-profiler-tree-view-header");
                return label;
            }

            static VisualElement MakeCellLabel()
            {
                var element = k_CellLabelPool.Get();
                var label = new Label
                {
                    name = "Cell"
                };
                element.Add(label);
                return element;
            }

            static void DestroyCellLabel(VisualElement element)
            {
                k_CellLabelPool.Release(element);
            }

            void BindStructuralItem(VisualElement element, int index)
            {
                var itemData = m_TreeView.GetItemDataForIndex<StructuralChangesProfilerTreeViewItem>(index);
                element.Q<Label>("Cell").text = itemData.displayName;
            }

            void BindCostItem(VisualElement element, int index)
            {
                var itemData = m_TreeView.GetItemDataForIndex<StructuralChangesProfilerTreeViewItem>(index);
                element.Q<Label>("Cell").text = FormattingUtility.NsToMsString(itemData.totalElapsedNanoseconds);
            }

            void BindCountItem(VisualElement element, int index)
            {
                var itemData = m_TreeView.GetItemDataForIndex<StructuralChangesProfilerTreeViewItem>(index);
                element.Q<Label>("Cell").text = FormattingUtility.CountToString(itemData.totalCount);
            }

            public void Update()
            {
                if (m_Window == null)
                    return;

                var itemId = 0;
                var rootItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem>(itemId++, new StructuralChangesProfilerTreeViewItem() { displayName = s_All });
                var createEntityItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem> (itemId++, new StructuralChangesProfilerTreeViewItem() { displayName = s_CreateEntity });
                var destroyEntityItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem> (itemId++, new StructuralChangesProfilerTreeViewItem() { displayName = s_DestroyEntity });
                var addComponentItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem> (itemId++, new StructuralChangesProfilerTreeViewItem() { displayName = s_AddComponent });
                var removeComponentItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem> (itemId++, new StructuralChangesProfilerTreeViewItem() { displayName = s_RemoveComponent });
                var setSharedComponentItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem> (itemId++, new StructuralChangesProfilerTreeViewItem() { displayName = s_SetSharedComponent });

                foreach (var item in m_StructuralChangesDataFiltered)
                {
                    TreeViewItemData<StructuralChangesProfilerTreeViewItem> eventItem;
                    switch (item.data.Type)
                    {
                        case StructuralChangeType.CreateEntity:
                            eventItem = createEntityItem;
                            break;
                        case StructuralChangeType.DestroyEntity:
                            eventItem = destroyEntityItem;
                            break;
                        case StructuralChangeType.AddComponent:
                            eventItem = addComponentItem;
                            break;
                        case StructuralChangeType.RemoveComponent:
                            eventItem = removeComponentItem;
                            break;
                        case StructuralChangeType.SetSharedComponent:
                            eventItem = setSharedComponentItem;
                            break;
                        default:
                            throw new NotImplementedException(item.data.Type.ToString());
                    }

                    TreeViewItemData<StructuralChangesProfilerTreeViewItem> worldItem = default;
                    var foundWorld = false;
                    foreach (var child in eventItem.children)
                    {
                        if (child.data.displayName == item.data.WorldName)
                        {
                            worldItem = child;
                            foundWorld = true;
                            break;
                        }
                    }

                    if (!foundWorld)
                    {
                        worldItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem>(itemId++, new StructuralChangesProfilerTreeViewItem() {displayName = item.data.WorldName});
                        TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(eventItem, worldItem);
                    }

                    TreeViewItemData<StructuralChangesProfilerTreeViewItem> systemItem = default;
                    var foundSystem = false;
                    foreach (var child in eventItem.children)
                    {
                        if (child.data.displayName == item.data.SystemName)
                        {
                            systemItem = child;
                            foundSystem = true;
                            break;
                        }
                    }

                    if (!foundSystem)
                    {
                        systemItem = new TreeViewItemData<StructuralChangesProfilerTreeViewItem>(itemId++, new StructuralChangesProfilerTreeViewItem() {displayName = item.data.SystemName});
                        TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(worldItem, systemItem);
                    }

                    systemItem.data.totalElapsedNanoseconds += item.data.ElapsedNanoseconds;
                    systemItem.data.totalCount++;
                    worldItem.data.totalElapsedNanoseconds += item.data.ElapsedNanoseconds;
                    worldItem.data.totalCount++;
                    eventItem.data.totalElapsedNanoseconds += item.data.ElapsedNanoseconds;
                    eventItem.data.totalCount++;
                    rootItem.data.totalElapsedNanoseconds += item.data.ElapsedNanoseconds;
                    rootItem.data.totalCount++;
                }

                if (createEntityItem.hasChildren)
                    TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(rootItem, createEntityItem);
                if (destroyEntityItem.hasChildren)
                    TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(rootItem, destroyEntityItem);
                if (addComponentItem.hasChildren)
                    TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(rootItem, addComponentItem);
                if (removeComponentItem.hasChildren)
                    TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(rootItem, removeComponentItem);
                if (setSharedComponentItem.hasChildren)
                    TreeViewItemDataBridge<StructuralChangesProfilerTreeViewItem>.AddChild(rootItem, setSharedComponentItem);

                AddLeafCountRecursive(rootItem);

                if (rootItem.hasChildren)
                {
                    m_TreeView.SetRootItems(new [] { rootItem });
                    m_TreeView.ExpandItem(rootItem.id);
                }
                else
                {
                    m_TreeView.Clear();
                }

                m_Message.SetVisibility(false);
                m_Content.SetVisibility(true);
            }

            public void Search()
            {
                if (m_Window == null)
                    return;

                m_SearchElement.Search();
            }

            public void Clear(string message)
            {
                if (m_Window == null)
                    return;

                m_StructuralChangesDataSource = null;
                m_StructuralChangesDataFiltered.Clear();
                m_TreeView.Clear();
                m_Message.SetVisibility(true);
                m_Message.text = message;
                m_Content.SetVisibility(false);
            }

            int AddLeafCountRecursive(TreeViewItemData<StructuralChangesProfilerTreeViewItem> item)
            {
                var count = item.hasChildren ? 0 : 1;
                foreach (var child in item.children)
                    count += AddLeafCountRecursive(child);
                if (item.hasChildren)
                    item.data.displayName += $" ({count})";
                return count;
            }
        }
    }
}
