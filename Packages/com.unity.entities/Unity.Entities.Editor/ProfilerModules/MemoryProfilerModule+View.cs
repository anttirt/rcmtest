using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    partial class MemoryProfilerModule
    {
        public class MemoryProfilerModuleView
        {
            const string k_UserSettingsKey = "Entities" + nameof(MemoryProfiler) + ".";
            const string k_ShowEmptyArchetypesKey = k_UserSettingsKey + nameof(showEmptyArchetypes);

            static readonly string s_ShowEmptyArchetypes = L10n.Tr("Show Empty Archetypes");
            static readonly string s_All = L10n.Tr("All");
            static readonly string s_Archetypes = L10n.Tr("Archetypes");
            static readonly string s_Allocated = L10n.Tr("Allocated");
            static readonly string s_Unused = L10n.Tr("Unused");
            static readonly string s_EntityCount = L10n.Tr("Entities");
            static readonly string s_UnusedEntities = L10n.Tr("Unused Entities");
            static readonly string s_ChunkCount = L10n.Tr("Chunks");
            static readonly string s_ChunkCapacity = L10n.Tr("Chunk Capacity");
            static readonly string s_Components = L10n.Tr("Components");
            static readonly string s_ExternalComponents = L10n.Tr("External Components");
            static readonly string s_ChunkComponents = L10n.Tr("Chunk Components");
            static readonly string s_SharedComponents = L10n.Tr("Shared Components");
            static readonly string s_Unknown = L10n.Tr("Unknown");
            static readonly string s_ComponentSizeInChunkTooltip = L10n.Tr("Component size in chunk.");
            static readonly string s_ComponentsSizeInChunkTooltip = L10n.Tr("Components size in chunk.");
            static readonly string s_ArchetypeBudget = L10n.Tr("Archetype Budget");
            static readonly string s_ArchetypeBudgetCapacity = L10n.Tr("Capacity");
            static readonly string s_ArchetypeBudgetTooltip = L10n.Tr("Memory used to store archetype metadata. Each unique combination of components creates an archetype. Reduce archetype count by using consistent component combinations across entities.");
            static readonly string s_QueryBudget = L10n.Tr("Query Budget");
            static readonly string s_QueryBudgetCapacity = L10n.Tr("Capacity");
            static readonly string s_QueryBudgetTooltip = L10n.Tr("Memory used to store entity query data. Each unique query and archetype match consumes space.");


            static readonly VisualElementTemplate s_WindowTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-window");
            static readonly VisualElementTemplate s_LeftPaneTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-left-pane");
            static readonly VisualElementTemplate s_RightPaneTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-right-pane");
            static readonly VisualElementTemplate s_ComponentTemplate = PackageResources.LoadTemplate("ProfilerModules/memory-profiler-component");

            static readonly ObjectPool<VisualElement> k_CellLabelPool = new (() => new VisualElement());

            MemoryProfilerTreeViewItemData[] m_ArchetypesDataSource;
            Dictionary<ulong, MemoryProfiler.WorldAllocatorData> m_WorldAllocatorDataSource;
            readonly List<MemoryProfilerTreeViewItemData> m_ArchetypesDataFiltered = new ();

            VisualElement m_Window;
            TwoPaneSplitView m_Splitter;

            // Left pane elements
            VisualElement m_LeftPane;
            SearchFieldElement m_SearchField;
            MemoryProfilerSearchView m_SearchView;
            string m_SearchText;
            Label m_Message;
            VisualElement m_Content;
            MultiColumnTreeView m_TreeView;

            // Right pane elements
            VisualElement m_RightPane;
            TextField m_ArchetypeName;
            Label m_EntityCountLabel;
            Label m_EntityCount;
            Label m_UnusedEntityCountLabel;
            Label m_UnusedEntityCount;
            Label m_ChunkCountLabel;
            Label m_ChunkCount;
            Label m_ChunkCapacityLabel;
            Label m_ChunkCapacity;
            FoldoutField m_ComponentsFoldout;
            Label m_ComponentsSizeInChunk;
            Label m_ExternalComponents;
            FoldoutField m_ChunkComponentsFoldout;
            FoldoutField m_SharedComponentsFoldout;

            bool showEmptyArchetypes
            {
                get => EditorUserSettings.GetConfigValue(k_ShowEmptyArchetypesKey) == true.ToString();
                set => EditorUserSettings.SetConfigValue(k_ShowEmptyArchetypesKey, value ? value.ToString() : null);
            }

            public MemoryProfilerTreeViewItemData[] ArchetypesDataSource
            {
                get => m_ArchetypesDataSource;
                set => m_ArchetypesDataSource = value;
            }

            public Dictionary<ulong, MemoryProfiler.WorldAllocatorData> WorldAllocatorDataSource
            {
                get => m_WorldAllocatorDataSource;
                set => m_WorldAllocatorDataSource = value;
            }

            public bool HasArchetypesDataSource => m_ArchetypesDataSource?.Length > 0;

            public Action SearchFinished { get; set; }

            public void Dispose()
            {
                m_SearchView?.Dispose();
                m_SearchView = null;
            }

            public VisualElement Create()
            {
                m_Window = s_WindowTemplate.Clone();
                Resources.Templates.DotsEditorCommon.AddStyles(m_Window);
                UnityEditor.Search.SearchElement.AppendStyleSheets(m_Window);
                m_Splitter = m_Window.Q<TwoPaneSplitView>("splitter");
                CreateViewLeftPane(m_Splitter.Q("left-pane"));
                CreateViewRightPane(m_Splitter.Q("right-pane"));
                return m_Window;
            }

            void CreateViewLeftPane(VisualElement root)
            {
                m_LeftPane = s_LeftPaneTemplate.Clone(root);

                var toolbar = m_LeftPane.Q<Toolbar>("toolbar");
                var options = toolbar.Q<Button>("options");

                var toolbarWrapper = new VisualElement { style = { flexShrink = 0, flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                toolbar.RemoveFromHierarchy();

                m_SearchView = new MemoryProfilerSearchView(this);
                m_SearchField = new SearchFieldElement("ArchetypeSearch", m_SearchView, SearchQueryBuilderViewFlags.Default);
                m_SearchField.style.flexGrow = 1;
                toolbarWrapper.Add(m_SearchField);

                options.RemoveFromHierarchy();
                toolbarWrapper.Add(options);

                m_LeftPane.Insert(0, toolbarWrapper);

                options.clicked += () =>
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(s_ShowEmptyArchetypes), showEmptyArchetypes, () =>
                    {
                        showEmptyArchetypes = !showEmptyArchetypes;
                        ApplyFilters();
                        SearchFinished?.Invoke();
                    });
                    menu.DropDown(options.worldBound);
                };

                m_Message = m_LeftPane.Q<Label>("message");
                m_Message.text = L10n.Tr("No frame data available. Select a frame from the charts above to see its details here.");
                m_Content = m_LeftPane.Q("content");
                m_Content.SetVisibility(false);

                var container = m_Content.Q("tree-view-container");
                m_TreeView = new MultiColumnTreeView()
                {
                    name = "MemoryProfilerModuleTreeView",
                    fixedItemHeight = 18,
                    autoExpand = true,
                    viewDataKey = "full-view",
                    selectionType = SelectionType.Single
                };
                m_TreeView.AddToClassList("memory-profiler-left-pane__tree-view");
                CreateColumns(m_TreeView);
                m_TreeView.selectionChanged += OnTreeViewSelectionChanged;

                container.Add(m_TreeView);
            }

            void CreateColumns(MultiColumnTreeView treeView)
            {
                const string headerStr = "Header";

                var archetypeColumn = new Column()
                {
                    name = s_Archetypes,
                    makeHeader = MakeHeaderLabel,
                    bindHeader = e =>
                    {
                        var label = e.Q<Label>(headerStr);
                        label.text = s_Archetypes;
                    },
                    makeCell = MakeCellLabel,
                    bindCell = BindArchetypeItem,
                    destroyCell = DestroyCellLabel,
                    resizable = true,
                    minWidth = 100,
                    width = 300
                };

                var allocatedColumn = new Column()
                {
                    name = s_Allocated,
                    makeHeader = MakeHeaderLabel,
                    bindHeader = e =>
                    {
                        var label = e.Q<Label>(headerStr);
                        label.text = s_Allocated;
                    },
                    makeCell = MakeCellLabel,
                    bindCell = BindAllocatedItem,
                    destroyCell = DestroyCellLabel,
                    resizable = true,
                    width = 100
                };

                var unusedColumn = new Column()
                {
                    name = s_Unused,
                    makeHeader = MakeHeaderLabel,
                    bindHeader = e =>
                    {
                        var label = e.Q<Label>(headerStr);
                        label.text = s_Unused;
                    },
                    makeCell = MakeCellLabel,
                    bindCell = BindUnusedItem,
                    destroyCell = DestroyCellLabel,
                    resizable = true,
                    width = 100
                };

                treeView.columns.Add(archetypeColumn);
                treeView.columns.Add(allocatedColumn);
                treeView.columns.Add(unusedColumn);
            }

            static VisualElement MakeHeaderLabel()
            {
                var label = new Label
                {
                    name = "Header",
                };
                label.AddToClassList("memory-profiler-left-pane__column-header");
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

            void BindArchetypeItem(VisualElement element, int index)
            {
                var itemData = m_TreeView.GetItemDataForIndex<MemoryProfilerTreeViewItem>(index);
                element.Q<Label>("Cell").text = itemData.displayName;
            }

            void BindAllocatedItem(VisualElement element, int index)
            {
                var itemData = m_TreeView.GetItemDataForIndex<MemoryProfilerTreeViewItem>(index);
                element.Q<Label>("Cell").text = FormattingUtility.BytesToString(itemData.totalAllocatedBytes);
            }

            void BindUnusedItem(VisualElement element, int index)
            {
                var itemData = m_TreeView.GetItemDataForIndex<MemoryProfilerTreeViewItem>(index);
                element.Q<Label>("Cell").text = FormattingUtility.BytesToString(itemData.totalUnusedBytes);
            }

            void CreateViewRightPane(VisualElement root)
            {
                m_RightPane = s_RightPaneTemplate.Clone(root);
                m_RightPane.visible = false;

                var header = m_RightPane.Q("header");
                m_ArchetypeName = header.Q<TextField>("name");

                var content = m_RightPane.Q("content");
                m_EntityCountLabel = content.Q<Label>("entity-count-label");
                m_EntityCountLabel.text = s_EntityCount;
                m_EntityCount = content.Q<Label>("entity-count-value");

                m_UnusedEntityCountLabel = content.Q<Label>("unused-entity-count-label");
                m_UnusedEntityCountLabel.text = s_UnusedEntities;
                m_UnusedEntityCount = content.Q<Label>("unused-entity-count-value");

                m_ChunkCountLabel = content.Q<Label>("chunk-count-label");
                m_ChunkCountLabel.text = s_ChunkCount;
                m_ChunkCount = content.Q<Label>("chunk-count-value");

                m_ChunkCapacityLabel = content.Q<Label>("chunk-capacity-label");
                m_ChunkCapacityLabel.text = s_ChunkCapacity;
                m_ChunkCapacity = content.Q<Label>("chunk-capacity-value");

                m_ComponentsFoldout = content.Q<FoldoutField>("components");
                m_ComponentsFoldout.text = s_Components;
                m_ComponentsFoldout.open = true;

                m_ComponentsSizeInChunk = new Label();
                m_ComponentsSizeInChunk.style.unityTextAlign = TextAnchor.MiddleRight;
                m_ComponentsSizeInChunk.tooltip = s_ComponentsSizeInChunkTooltip;
                m_ComponentsFoldout.value = m_ComponentsSizeInChunk;

                m_ExternalComponents = content.Q<Label>("external-components");
                m_ExternalComponents.text = s_ExternalComponents;

                m_ChunkComponentsFoldout = content.Q<FoldoutField>("chunk-components");
                m_ChunkComponentsFoldout.text = s_ChunkComponents;
                m_ChunkComponentsFoldout.open = true;

                m_SharedComponentsFoldout = content.Q<FoldoutField>("shared-components");
                m_SharedComponentsFoldout.text = s_SharedComponents;
                m_SharedComponentsFoldout.open = true;
            }

            public void Rebuild()
            {
                if (m_Window == null)
                    return;

                var itemId = 0;
                var rootItem = new TreeViewItemData<MemoryProfilerTreeViewItem>(itemId++, new MemoryProfilerTreeViewItem { displayName = s_All });

                var worldItems = new Dictionary<ulong, TreeViewItemData<MemoryProfilerTreeViewItem>>();
                foreach (var archetype in m_ArchetypesDataFiltered)
                {
                    if (!worldItems.ContainsKey(archetype.WorldSequenceNumber))
                    {
                        var worldItem = new TreeViewItemData<MemoryProfilerTreeViewItem>(itemId++, new MemoryProfilerTreeViewItem()
                        {
                            displayName = archetype.WorldName,
                            worldName = archetype.WorldName,
                            worldSequenceNumber = archetype.WorldSequenceNumber,
                            isWorldItem = true
                        });
                        TreeViewItemDataBridge<MemoryProfilerTreeViewItem>.AddChild(rootItem, worldItem);
                        worldItems[archetype.WorldSequenceNumber] = worldItem;
                    }
                }

                foreach (var archetypeData in m_ArchetypesDataFiltered)
                {
                    var worldItem = worldItems[archetypeData.WorldSequenceNumber];

                    TreeViewItemData<MemoryProfilerTreeViewItem> archetypeDataItem = default;
                    var foundArchetypeItem = false;
                    foreach (var item in worldItem.children)
                    {
                        if (item.data.data.StableHash == archetypeData.StableHash)
                        {
                            archetypeDataItem = item;
                            foundArchetypeItem = true;
                            break;
                        }
                    }

                    if (!foundArchetypeItem)
                    {
                        archetypeDataItem = new TreeViewItemData<MemoryProfilerTreeViewItem>(itemId++, new MemoryProfilerTreeViewItem()
                        {
                            displayName = $"Archetype {FormattingUtility.HashToString(archetypeData.StableHash)}",
                            data = archetypeData
                        });
                        TreeViewItemDataBridge<MemoryProfilerTreeViewItem>.AddChild(worldItem, archetypeDataItem);
                    }

                    archetypeDataItem.data.totalAllocatedBytes += archetypeData.AllocatedBytes;
                    archetypeDataItem.data.totalUnusedBytes += archetypeData.UnusedBytes;
                    worldItem.data.totalAllocatedBytes += archetypeData.AllocatedBytes;
                    worldItem.data.totalUnusedBytes += archetypeData.UnusedBytes;
                    rootItem.data.totalAllocatedBytes += archetypeData.AllocatedBytes;
                    rootItem.data.totalUnusedBytes += archetypeData.UnusedBytes;
                }

                var totalArchetypeCount = 0;
                foreach (var kvp in worldItems)
                {
                    var archetypeCount = CountLeaves(kvp.Value);
                    totalArchetypeCount += archetypeCount;

                    if (m_WorldAllocatorDataSource != null &&
                        m_WorldAllocatorDataSource.TryGetValue(kvp.Key, out var allocatorData))
                    {
                        var archetypeUsage = allocatorData.ArchetypeAllocatorBudgetBytes > 0
                            ? (float)allocatorData.ArchetypeAllocatorUsedBytes / allocatorData.ArchetypeAllocatorBudgetBytes
                            : 0f;
                        var queryUsage = allocatorData.EntityQueryAllocatorBudgetBytes > 0
                            ? (float)allocatorData.EntityQueryAllocatorUsedBytes / allocatorData.EntityQueryAllocatorBudgetBytes
                            : 0f;
                        var maxUsage = Math.Max(archetypeUsage, queryUsage);
                        var budgetType = archetypeUsage >= queryUsage ? "archetype" : "query";
                        kvp.Value.data.displayName += $" ({archetypeCount} archetypes, {maxUsage:P0} {budgetType} budget)";
                    }
                    else
                    {
                        kvp.Value.data.displayName += $" ({archetypeCount} archetypes)";
                    }
                }
                rootItem.data.displayName += $" ({totalArchetypeCount} archetypes)";

                if (rootItem.hasChildren)
                {
                    m_TreeView.SetRootItems(new[] { rootItem });
                    m_TreeView.ExpandItem(rootItem.id);
                }
                else
                {
                    m_TreeView.Clear();
                }

                m_Message.SetVisibility(false);
                m_Content.SetVisibility(true);
                SetInspectorValue(m_TreeView.selectedItem as MemoryProfilerTreeViewItem);
            }

            public void Search()
            {
                if (m_Window == null)
                    return;

                ApplyFilters();
                SearchFinished?.Invoke();
            }

            public void OnSearchTextChanged(string searchText)
            {
                m_SearchText = searchText;
                ApplyFilters();
                SearchFinished?.Invoke();
            }

            void ApplyFilters()
            {
                m_ArchetypesDataFiltered.Clear();

                if (m_ArchetypesDataSource == null)
                    return;

                var hasSearchText = !string.IsNullOrEmpty(m_SearchText);
                ParsedQuery<MemoryProfilerTreeViewItemData> query = null;

                if (hasSearchText)
                {
                    query = ArchetypeSearchProvider.queryEngine.ParseQuery(m_SearchText);
                    if (!query.valid)
                    {
                        foreach (var error in query.errors)
                            Debug.LogError(error.reason);
                        hasSearchText = false;
                        query = null;
                    }
                }

                IEnumerable<MemoryProfilerTreeViewItemData> source = m_ArchetypesDataSource;
                if (hasSearchText)
                    source = query.Apply(source);

                foreach (var archetype in source)
                {
                    if (!showEmptyArchetypes && archetype.EntityCount == 0)
                        continue;
                    m_ArchetypesDataFiltered.Add(archetype);
                }

                m_SearchField.ResultsCount = hasSearchText ? m_ArchetypesDataFiltered.Count : null;
            }

            public void Clear(string message)
            {
                if (m_Window == null)
                    return;

                m_ArchetypesDataSource = null;
                m_WorldAllocatorDataSource = null;
                m_ArchetypesDataFiltered.Clear();
                m_TreeView.Clear();
                m_Message.SetVisibility(true);
                m_Message.text = message;
                m_Content.SetVisibility(false);
                SetInspectorValue(null);
            }

            void OnTreeViewSelectionChanged(IEnumerable<object> items)
            {
                SetInspectorValue(items.FirstOrDefault() as MemoryProfilerTreeViewItem);
            }

            void SetInspectorValue(MemoryProfilerTreeViewItem item)
            {
                if (item != null && item.isWorldItem)
                {
                    m_ArchetypeName.value = item.worldName;

                    m_EntityCountLabel.text = s_ArchetypeBudget;
                    m_EntityCountLabel.tooltip = s_ArchetypeBudgetTooltip;
                    m_UnusedEntityCountLabel.text = s_ArchetypeBudgetCapacity;
                    m_UnusedEntityCountLabel.tooltip = s_ArchetypeBudgetTooltip;
                    m_ChunkCountLabel.text = s_QueryBudget;
                    m_ChunkCountLabel.tooltip = s_QueryBudgetTooltip;
                    m_ChunkCapacityLabel.text = s_QueryBudgetCapacity;
                    m_ChunkCapacityLabel.tooltip = s_QueryBudgetTooltip;

                    if (m_WorldAllocatorDataSource != null &&
                        m_WorldAllocatorDataSource.TryGetValue(item.worldSequenceNumber, out var allocatorData))
                    {
                        var archetypeUsage = allocatorData.ArchetypeAllocatorBudgetBytes > 0
                            ? (float)allocatorData.ArchetypeAllocatorUsedBytes / allocatorData.ArchetypeAllocatorBudgetBytes
                            : 0f;
                        var queryUsage = allocatorData.EntityQueryAllocatorBudgetBytes > 0
                            ? (float)allocatorData.EntityQueryAllocatorUsedBytes / allocatorData.EntityQueryAllocatorBudgetBytes
                            : 0f;

                        m_EntityCount.text = $"{archetypeUsage:P0}";
                        m_UnusedEntityCount.text = $"{FormattingUtility.BytesToString((ulong)allocatorData.ArchetypeAllocatorUsedBytes)} / {FormattingUtility.BytesToString((ulong)allocatorData.ArchetypeAllocatorBudgetBytes)}";
                        m_ChunkCount.text = $"{queryUsage:P0}";
                        m_ChunkCapacity.text = $"{FormattingUtility.BytesToString((ulong)allocatorData.EntityQueryAllocatorUsedBytes)} / {FormattingUtility.BytesToString((ulong)allocatorData.EntityQueryAllocatorBudgetBytes)}";
                    }
                    else
                    {
                        m_EntityCount.text = "-";
                        m_UnusedEntityCount.text = "-";
                        m_ChunkCount.text = "-";
                        m_ChunkCapacity.text = "-";
                    }

                    m_ComponentsFoldout.Clear();
                    m_ComponentsFoldout.SetVisibility(false);
                    m_ExternalComponents.SetVisibility(false);
                    m_ChunkComponentsFoldout.Clear();
                    m_ChunkComponentsFoldout.SetVisibility(false);
                    m_SharedComponentsFoldout.Clear();
                    m_SharedComponentsFoldout.SetVisibility(false);
                    m_RightPane.visible = true;
                }
                else if (item != null && item.data.ComponentTypes != null)
                {
                    m_EntityCountLabel.text = s_EntityCount;
                    m_EntityCountLabel.tooltip = string.Empty;
                    m_UnusedEntityCountLabel.text = s_UnusedEntities;
                    m_UnusedEntityCountLabel.tooltip = string.Empty;
                    m_ChunkCountLabel.text = s_ChunkCount;
                    m_ChunkCountLabel.tooltip = string.Empty;
                    m_ChunkCapacityLabel.text = s_ChunkCapacity;
                    m_ChunkCapacityLabel.tooltip = string.Empty;

                    m_ArchetypeName.value = item.displayName;
                    m_EntityCount.text = FormattingUtility.CountToString(item.data.EntityCount);
                    m_UnusedEntityCount.text = FormattingUtility.CountToString(item.data.UnusedEntityCount);
                    m_ChunkCount.text = FormattingUtility.CountToString(item.data.ChunkCount);
                    m_ChunkCapacity.text = FormattingUtility.CountToString(item.data.ChunkCapacity);
                    m_ComponentsFoldout.Clear();
                    m_ExternalComponents.SetVisibility(false);
                    m_ChunkComponentsFoldout.Clear();
                    m_ChunkComponentsFoldout.SetVisibility(false);
                    m_SharedComponentsFoldout.Clear();
                    m_SharedComponentsFoldout.SetVisibility(false);

                    foreach (var typeIndex in item.data.ComponentTypes.OrderByDescending(GetTypeSizeInChunk))
                    {
                        var component = s_ComponentTemplate.Clone();
                        var componentIcon = component.Q<Image>("icon");
                        var componentName = component.Q<Label>("name");
                        var componentSizeInChunk = component.Q<Label>("size");

                        componentSizeInChunk.tooltip = s_ComponentSizeInChunkTooltip;

                        if (typeIndex <= 0)
                        {
                            componentIcon.AddToClassList("memory-profiler-component__icon-component");
                            componentName.text = s_Unknown;
                            componentSizeInChunk.text = "? B";
                        }
                        else
                        {
                            if (TypeManager.IsChunkComponent(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-chunk-component");
                            else if (TypeManager.IsBuffer(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-buffer-component");
                            else if (TypeManager.IsSharedComponentType(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-shared-component");
                            #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                            else if (TypeManager.IsManagedComponent(typeIndex))
                            #pragma warning restore 0618
                                componentIcon.AddToClassList("memory-profiler-component__icon-managed-component");
                            else if (TypeManager.IsZeroSized(typeIndex))
                                componentIcon.AddToClassList("memory-profiler-component__icon-tag-component");
                            else
                                componentIcon.AddToClassList("memory-profiler-component__icon-component");

                            var type = TypeManager.GetType(typeIndex);
                            componentName.text = type?.IsNested == true ? $"{type.DeclaringType.Name}+{type.Name}" : type?.Name ?? s_Unknown;

                            // Chunk and shared components store data outside archetype
                            if (!TypeManager.IsChunkComponent(typeIndex) &&
                                !TypeManager.IsSharedComponentType(typeIndex))
                            {
                                var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                                componentSizeInChunk.text = FormattingUtility.BytesToString((ulong)typeInfo.SizeInChunk);
                            }
                        }

                        if (TypeManager.IsChunkComponent(typeIndex))
                            m_ChunkComponentsFoldout.Add(component);
                        else if (TypeManager.IsSharedComponentType(typeIndex))
                            m_SharedComponentsFoldout.Add(component);
                        else
                            m_ComponentsFoldout.Add(component);
                    }

                    m_ComponentsFoldout.text = $"{s_Components} ({FormattingUtility.CountToString(m_ComponentsFoldout.childCount)})";
                    m_ComponentsSizeInChunk.text = FormattingUtility.BytesToString((ulong)item.data.InstanceSize);

                    m_ExternalComponents.SetVisibility(m_ChunkComponentsFoldout.childCount > 0 || m_SharedComponentsFoldout.childCount > 0);

                    if (m_ChunkComponentsFoldout.childCount > 0)
                    {
                        m_ChunkComponentsFoldout.text = $"{s_ChunkComponents} ({FormattingUtility.CountToString(m_ChunkComponentsFoldout.childCount)})";
                        m_ChunkComponentsFoldout.SetVisibility(true);
                    }

                    if (m_SharedComponentsFoldout.childCount > 0)
                    {
                        m_SharedComponentsFoldout.text = $"{s_SharedComponents} ({FormattingUtility.CountToString(m_SharedComponentsFoldout.childCount)})";
                        m_SharedComponentsFoldout.SetVisibility(true);
                    }

                    m_RightPane.visible = true;
                }
                else
                {
                    m_RightPane.visible = false;
                    m_ArchetypeName.value = null;
                    m_EntityCount.text = null;
                    m_UnusedEntityCount.text = null;
                    m_ChunkCount.text = null;
                    m_ChunkCapacity.text = null;
                    m_ComponentsFoldout.Clear();
                    m_ChunkComponentsFoldout.Clear();
                    m_SharedComponentsFoldout.Clear();
                }
            }

            static int CountLeaves(TreeViewItemData<MemoryProfilerTreeViewItem> item)
            {
                if (!item.hasChildren)
                    return 1;
                var count = 0;
                foreach (var child in item.children)
                    count += CountLeaves(child);
                return count;
            }

            internal static int GetTypeSizeInChunk(TypeIndex typeIndex)
            {
                return typeIndex != TypeIndex.Null ? TypeManager.GetTypeInfo(typeIndex).SizeInChunk : 0;
            }
        }
    }
}
