using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Entities.Editor.Serialization;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    internal class SystemScheduleWindow : DOTSEditorWindow, IHasCustomMenu
    {
        internal static class Contents
        {
            public static readonly string WindowName = L10n.Tr("Systems");
            public static readonly string ShowPlayerLoopString = L10n.Tr("Show Player Loop");
            public static readonly string ShowAllWorldsString = L10n.Tr("Show All Worlds");
            public static readonly string AllWorldsLabel = L10n.Tr("All Worlds");
            public static readonly string System = L10n.Tr("System");
            public static readonly string Scheduling = L10n.Tr("Scheduling");
            public static readonly string SchedulingTooltip = L10n.Tr("Shows systems that have an [UpdateBefore] or [UpdateAfter] relationship with the currently selected system.");
            public static readonly string UpdateBeforeSchedulingTooltip = L10n.Tr("The selected system declares an [UpdateBefore] attribute targeting this system.");
            public static readonly string UpdateAfterSchedulingTooltip = L10n.Tr("The selected system declares an [UpdateAfter] attribute targeting this system.");
            public static readonly string UpdateBeforeReverseSchedulingTooltip =
                L10n.Tr("This system declares an [UpdateBefore] attribute targeting the selected system.");
            public static readonly string UpdateAfterReverseSchedulingTooltip =
                L10n.Tr("This system declares an [UpdateAfter] attribute targeting the selected system.");
            public static readonly string SystemTooltip = L10n.Tr("System name.");
            public static readonly string Namespace = L10n.Tr("Namespace");
            public static readonly string NamespaceTooltip = L10n.Tr("Namespace to which this system belongs.");
            public static readonly string EntityCount = L10n.Tr("Entity Count");
            public static readonly string EntityCountTooltip = L10n.Tr("The number of entities that match the queries at the end of the frame.");
            public static readonly string Time = L10n.Tr("Time (ms)");
            public static readonly string TimeTooltip = L10n.Tr("System running time");
            public static readonly string EntitiesPreferencesString = L10n.Tr("Entities Preferences");
            public static readonly string EntitiesPreferencesPath = "Preferences/Entities";
            public static readonly string ViewOption = L10n.Tr("View Options");
            public static readonly string Setting = L10n.Tr("Setting");
            public static readonly string NoSystemSelectedMessage = L10n.Tr("Select a system on the left to see details.");
            public static readonly string ToggleDetailViewTooltip = L10n.Tr("Show or hide the system detail view.");
            public static readonly string BackButtonTooltip = L10n.Tr("Go back in selection history");
            public static readonly string ForwardButtonTooltip = L10n.Tr("Go forward in selection history");
            public static readonly string ShowUnityNamespaceSystemsString = L10n.Tr("Show Unity-Namespaced Systems");
        }

        static readonly ProfilerMarker k_OnUpdateMarker = new ($"{nameof(SystemScheduleWindow)}.{nameof(OnUpdate)}");

        VisualElement m_Root;
        CenteredMessageElement m_NoWorld;
        SystemTreeView m_SystemTreeView;
        PropertyElement m_SystemInspectorView;
        ScrollView m_SystemInspectorScrollView;
        CenteredMessageElement m_NoSystemSelected;
        TwoPaneSplitView m_BodyView;
        bool m_HasSelectedSystem;
        VisualElement m_WorldSelector;
        VisualElement m_EmptySelectorWhenShowingFullPlayerLoop;
        // internal for tests.
        internal Button m_BackButton;
        internal Button m_ForwardButton;
        internal const int k_NavigationHistoryCapacity = 10;
        internal readonly List<SystemProxy> m_NavigationHistory = new();
        internal int m_NavigationIndex = -1;
        bool m_NavigatingHistory;
        internal WorldProxyManager WorldProxyManager; // internal for tests.
        PlayerLoopSystemGraph m_LocalSystemGraph;
        int m_LastWorldVersion;
        bool m_ViewChange;
        bool m_GraphChange;

        public SystemSearchView SystemSearchView { get; private set; }
        SearchFieldElement m_SearchField;

        WorldProxy m_SelectedWorldProxy;

        /// <summary>
        /// The systems window configuration. This is data which is managed externally by settings, tests or users but drives internal behaviours.
        /// </summary>
        [GeneratePropertyBag]
        public class SystemsWindowConfiguration
        {
            [CreateProperty] public bool Show0sInEntityCountAndTimeColumn = false;
            [CreateProperty] public bool ShowMorePrecisionForRunningTime = false;
            public bool ShowPlayerLoop;
            public bool ShowAllWorlds;
            public bool ShowDetailView = true;
            public bool ShowUnityNamespaceSystems = true;
        }

        // Internal for tests.
        internal SystemsWindowConfiguration m_Configuration;

        [MenuItem(Constants.MenuItems.SystemScheduleWindow, false, Constants.MenuItems.SystemScheduleWindowPriority)]
        static void OpenWindow()
        {
            var window = GetWindow<SystemScheduleWindow>();
            window.Show();
        }

        public SystemScheduleWindow() : base(Analytics.Window.Systems) { }

        /// <summary>
        /// Build the GUI for the system window.
        /// </summary>
        protected override void OnCreate()
        {
            Resources.AddCommonVariables(rootVisualElement);
            UnityEditor.Search.SearchElement.AppendStyleSheets(rootVisualElement);

            titleContent = EditorGUIUtility.TrTextContent(Contents.WindowName, EditorIcons.System);
            minSize = Constants.MinWindowSize;

            m_Root = new VisualElement();
            m_Root.AddToClassList(UssClasses.SystemScheduleWindow.WindowRoot);
            rootVisualElement.Add(m_Root);

            m_NoWorld = new CenteredMessageElement() { Message = NoWorldMessageContent };
            rootVisualElement.Add(m_NoWorld);
            m_NoWorld.Hide();

            m_Configuration = UserSettings<SystemsWindowPreferenceSettings>.GetOrCreate(Constants.Settings.SystemsWindow).Configuration;

            Resources.Templates.SystemSchedule.AddStyles(m_Root);
            Resources.Templates.DotsEditorCommon.AddStyles(m_Root);

            WorldProxyManager = new WorldProxyManager();
            m_LocalSystemGraph = new PlayerLoopSystemGraph
            {
                WorldProxyManager = WorldProxyManager
            };
            WorldProxyManager.CreateWorldProxiesForAllWorlds();

            CreateToolBar(m_Root);

            m_BodyView = new TwoPaneSplitView()
            {
                name = "BodySplitView",
                viewDataKey = nameof(SystemScheduleWindow) + "_BodySplitView",
                orientation = TwoPaneSplitViewOrientation.Horizontal,
                fixedPaneInitialDimension = 1024f
            };

            m_Root.Add(m_BodyView);
            m_BodyView.Add(CreateTreeView());
            m_BodyView.Add(CreateInspectorView());

            Selection.selectionChanged += OnGlobalSelectionChanged;
        }

        protected override void OnCleanup()
        {
            WorldProxyManager?.Dispose();
            m_SystemTreeView?.Dispose();

            Selection.selectionChanged -= OnGlobalSelectionChanged;
        }

        void CreateToolBar(VisualElement root)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.Wrapper);
            Resources.Templates.SystemScheduleToolbar.Clone(toolbar);
            var leftSide = toolbar.Q(className: UssClasses.SystemScheduleWindow.Toolbar.LeftSide);
            var rightSide = toolbar.Q(className: UssClasses.SystemScheduleWindow.Toolbar.RightSide);

            m_WorldSelector = CreateWorldSelector();
            m_EmptySelectorWhenShowingFullPlayerLoop = new Label(Contents.AllWorldsLabel);
            m_EmptySelectorWhenShowingFullPlayerLoop.AddToClassList("unity-toolbar-menu");
            leftSide.Add(m_WorldSelector);
            leftSide.Add(m_EmptySelectorWhenShowingFullPlayerLoop);

            m_BackButton = new Button(() => NavigateHistory(-1)) { tooltip = Contents.BackButtonTooltip };
            m_BackButton.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.NavigationButton);
            m_BackButton.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.BackButton);
            rightSide.Add(m_BackButton);

            rightSide.Add(CreateToolbarSeparator());

            m_ForwardButton = new Button(() => NavigateHistory(+1)) { tooltip = Contents.ForwardButtonTooltip };
            m_ForwardButton.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.NavigationButton);
            m_ForwardButton.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.ForwardButton);
            rightSide.Add(m_ForwardButton);

            rightSide.Add(CreateToolbarSeparator());

            UpdateNavigationButtons();

            var detailViewToggle = new Button(ToggleDetailView)
            {
                tooltip = Contents.ToggleDetailViewTooltip,
                style = { backgroundImage = EditorGUIUtility.IconContent("UnityEditor.InspectorWindow").image as Texture2D }
            };
            detailViewToggle.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.DetailViewToggle);
            rightSide.Add(detailViewToggle);

            rightSide.Add(CreateToolbarSeparator());

            var dropdownSettings = InspectorUtility.CreateDropdownSettings(UssClasses.DotsEditorCommon.SettingsIcon);
            AppendOptionMenu(dropdownSettings.menu);

            UpdateWorldSelectorDisplay();
            rightSide.Add(dropdownSettings);

            root.Add(toolbar);
            AddSearchField(toolbar);
        }

        void AppendOptionMenu(DropdownMenu menu)
        {
            menu.AppendAction(Contents.ViewOption, null, DropdownMenuAction.Status.Disabled);

            menu.AppendAction(Contents.ShowPlayerLoopString, a =>
            {
                m_Configuration.ShowPlayerLoop = !m_Configuration.ShowPlayerLoop;
                m_SystemTreeView.ShowPlayerLoop = m_Configuration.ShowPlayerLoop;

                if (World.All.Count > 0)
                    RebuildTreeView();
            }, a => m_Configuration.ShowPlayerLoop ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendAction(Contents.ShowAllWorldsString, a =>
            {
                m_Configuration.ShowAllWorlds = !m_Configuration.ShowAllWorlds;
                WorldProxyManager.IsFullPlayerLoop = m_Configuration.ShowAllWorlds;

                UpdateWorldSelectorDisplay();

                if (World.All.Count > 0)
                    RebuildTreeView();
            }, a => m_Configuration.ShowAllWorlds ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendAction(Contents.ShowUnityNamespaceSystemsString, a =>
            {
                m_Configuration.ShowUnityNamespaceSystems = !m_Configuration.ShowUnityNamespaceSystems;
                m_SystemTreeView.ShowUnityNamespaceSystems = m_Configuration.ShowUnityNamespaceSystems;

                if (World.All.Count > 0)
                    RebuildTreeView();
            }, a => m_Configuration.ShowUnityNamespaceSystems ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            menu.AppendSeparator();

            // Setting
            menu.AppendAction(Contents.Setting, null, DropdownMenuAction.AlwaysDisabled);
            menu.AppendAction(Contents.EntitiesPreferencesString, a =>
            {
                SettingsService.OpenUserPreferences(Contents.EntitiesPreferencesPath);
            });
        }

        void AddSearchField(VisualElement root)
        {
            SystemSearchView = new SystemSearchView(this);
            m_SearchField = new SearchFieldElement("SystemSearch", SystemSearchView, SearchQueryBuilderViewFlags.Default);
            root.Add(m_SearchField);
        }

        void UpdateWorldSelectorDisplay()
        {
            m_WorldSelector.SetVisibility(!m_Configuration.ShowAllWorlds);
            m_EmptySelectorWhenShowingFullPlayerLoop.SetVisibility(m_Configuration.ShowAllWorlds);
        }

        VisualElement CreateTreeView()
        {
            m_SystemTreeView = new SystemTreeView
            {
                viewDataKey = nameof(SystemScheduleWindow),
                style = { flexGrow = 1 },
                LocalSystemGraph = m_LocalSystemGraph
            };
            UpdateConfigurations();
            m_SystemTreeView.SetSelection();
            m_SystemTreeView.RebuildColumns();
            m_SystemTreeView.systemSelectionChanged += UpdateSelectedSystem;
            return m_SystemTreeView;
        }

        internal void UpdateSelectedSystem(SystemProxy systemProxy)
        {
            if (systemProxy.World != null && systemProxy.World.IsCreated)
            {
                var content = new SystemContent(systemProxy.World, systemProxy);
                m_SystemInspectorView.SetTarget(new SystemContentDisplay(content));
                m_HasSelectedSystem = true;
                PushNavigationHistory(systemProxy);
            }
            else
            {
                m_SystemInspectorView.SetTarget(default(SystemContentDisplay));
                m_HasSelectedSystem = false;
            }
            UpdateInspectorVisibility();
            m_SystemInspectorView.ForceReload();
        }

        void PushNavigationHistory(SystemProxy systemProxy)
        {
            if (m_NavigatingHistory)
                return;

            if (m_NavigationIndex >= 0 && m_NavigationHistory[m_NavigationIndex].Equals(systemProxy))
                return;

            if (m_NavigationIndex < m_NavigationHistory.Count - 1)
                m_NavigationHistory.RemoveRange(m_NavigationIndex + 1, m_NavigationHistory.Count - m_NavigationIndex - 1);

            m_NavigationHistory.Add(systemProxy);

            while (m_NavigationHistory.Count > k_NavigationHistoryCapacity)
                m_NavigationHistory.RemoveAt(0);

            m_NavigationIndex = m_NavigationHistory.Count - 1;
            UpdateNavigationButtons();
        }

        internal void NavigateHistory(int delta)
        {
            var target = m_NavigationIndex + delta;
            while (target >= 0 && target < m_NavigationHistory.Count && !m_NavigationHistory[target].Valid)
            {
                m_NavigationHistory.RemoveAt(target);
                if (delta < 0)
                {
                    target--;
                    m_NavigationIndex--; // entry was before current position; shift to keep pointing at the same item
                }
            }

            if (target < 0 || target >= m_NavigationHistory.Count)
            {
                UpdateNavigationButtons();
                return;
            }

            m_NavigationIndex = target;
            var systemProxy = m_NavigationHistory[target];

            m_NavigatingHistory = true;
            try
            {
                if (!m_SystemTreeView.TrySelectSystem(systemProxy))
                {
                    // Item not present in the current tree (filtered out, world changed, etc.).
                    // Still update the inspector so the user gets feedback.
                    SystemTreeView.SelectedSystem = systemProxy;
                    UpdateSelectedSystem(systemProxy);
                }
            }
            finally
            {
                m_NavigatingHistory = false;
            }

            UpdateNavigationButtons();
        }

        void UpdateNavigationButtons()
        {
            if (m_BackButton == null || m_ForwardButton == null)
                return;

            m_BackButton.SetEnabled(m_NavigationIndex > 0);
            m_ForwardButton.SetEnabled(m_NavigationIndex < m_NavigationHistory.Count - 1);
        }

        internal void ClearNavigationHistory()
        {
            m_NavigationHistory.Clear();
            m_NavigationIndex = -1;
            UpdateNavigationButtons();
        }

        static VisualElement CreateToolbarSeparator()
        {
            var separator = new VisualElement();
            separator.AddToClassList(UssClasses.SystemScheduleWindow.Toolbar.ToolbarSeparator);
            return separator;
        }

        void UpdateConfigurations()
        {
            m_SystemTreeView.ShowMorePrecisionForRunningTime = m_Configuration.ShowMorePrecisionForRunningTime;
            m_SystemTreeView.Show0sInEntityCountAndTimeColumn = m_Configuration.Show0sInEntityCountAndTimeColumn;
            m_SystemTreeView.ShowUnityNamespaceSystems = m_Configuration.ShowUnityNamespaceSystems;
            m_SystemTreeView.ShowPlayerLoop = m_Configuration.ShowPlayerLoop;
        }

        VisualElement CreateInspectorView()
        {
            var container = new VisualElement { style = { flexGrow = 1 } };

            m_SystemInspectorScrollView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            m_SystemInspectorView = new PropertyElement();
            Resources.AddCommonVariables(m_SystemInspectorView);

            Resources.Templates.ContentProvider.System.AddStyles(m_SystemInspectorView);
            m_SystemInspectorView.AddToClassList(UssClasses.Content.SystemInspector.SystemContainer);

            m_SystemInspectorScrollView.Add(m_SystemInspectorView);
            container.Add(m_SystemInspectorScrollView);

            m_NoSystemSelected = new CenteredMessageElement { Message = Contents.NoSystemSelectedMessage };
            container.Add(m_NoSystemSelected);

            UpdateInspectorVisibility();
            return container;
        }

        void ToggleDetailView()
        {
            m_Configuration.ShowDetailView = !m_Configuration.ShowDetailView;
            UpdateInspectorVisibility();
        }

        void UpdateInspectorVisibility()
        {
            m_SystemInspectorScrollView.SetVisibility(m_HasSelectedSystem);
            m_NoSystemSelected.SetVisibility(!m_HasSelectedSystem);

            if (m_Configuration.ShowDetailView)
                m_BodyView.UnCollapse();
            else
                m_BodyView.CollapseChild(1);
        }

        void UpdatePreferences()
        {
            if (m_SystemTreeView.ShowMorePrecisionForRunningTime != m_Configuration.ShowMorePrecisionForRunningTime)
                m_SystemTreeView.ShowMorePrecisionForRunningTime = m_Configuration.ShowMorePrecisionForRunningTime;

            if (m_SystemTreeView.Show0sInEntityCountAndTimeColumn != m_Configuration.Show0sInEntityCountAndTimeColumn)
                m_SystemTreeView.Show0sInEntityCountAndTimeColumn = m_Configuration.Show0sInEntityCountAndTimeColumn;
        }

        public void StopSearch() => m_SystemTreeView.StopSearch();
        public void SetResults(IList<SearchItem> results) => m_SystemTreeView.SetResults(results);

        // internal for test.
        internal void RebuildTreeView()
        {
            m_SystemTreeView.Refresh(m_Configuration.ShowAllWorlds ? null : m_SelectedWorldProxy);
        }

        internal void ForceUpdate()
        {
            if (m_SystemTreeView == null || WorldProxyManager == null)
                return;

            UpdatePreferences();

            // Force all active updaters to rebuild their proxies
            foreach (var updater in WorldProxyManager.GetAllWorldProxyUpdaters())
            {
                if (!updater.IsActive())
                    continue;

                updater.ResetWorldProxy();
            }

            // Rebuild graph and tree view
            m_LocalSystemGraph.BuildCurrentGraph();
            RebuildTreeView();

            m_GraphChange = false;
            m_ViewChange = false;
        }

        protected override void OnUpdate()
        {
            using (k_OnUpdateMarker.Auto())
            {
                if (m_SystemTreeView == null || WorldProxyManager == null)
                    return;

                UpdatePreferences();

                if (SystemSearchView != null)
                    SystemSearchView.position = position;

                foreach (var updater in WorldProxyManager.GetAllWorldProxyUpdaters())
                {
                    if (updater.IsActive() && updater.IsDirty())
                    {
                        m_GraphChange = true;
                        updater.SetClean();
                    }
                }

                if (m_GraphChange)
                    m_LocalSystemGraph.BuildCurrentGraph();

                if (m_GraphChange || m_ViewChange)
                    RebuildTreeView();

                m_GraphChange = false;
                m_ViewChange = false;
            }
        }

        protected override void OnWorldsChanged(bool containsAnyWorld)
        {
            m_Root.SetVisibility(containsAnyWorld);
            m_NoWorld.SetVisibility(!containsAnyWorld);

            if (m_SystemTreeView == null)
                return;

            WorldProxyManager.IsFullPlayerLoop = m_Configuration.ShowAllWorlds;
            WorldProxyManager.CreateWorldProxiesForAllWorlds();

            if (SelectedWorld != null && SelectedWorld.IsCreated)
            {
                m_SelectedWorldProxy = WorldProxyManager.GetWorldProxyForGivenWorld(SelectedWorld);
                WorldProxyManager.SetSelectedWorldProxy(m_SelectedWorldProxy);
            }

            if (m_Configuration.ShowAllWorlds)
                m_GraphChange = true;
        }

        protected override void OnWorldSelected(World world)
        {
            if (world == null || !world.IsCreated)
                return;

            if (m_Configuration.ShowAllWorlds)
                return;

            m_SelectedWorldProxy = WorldProxyManager.GetWorldProxyForGivenWorld(world);
            WorldProxyManager.SetSelectedWorldProxy(m_SelectedWorldProxy);
            SystemSearchView.SetWorld(world);

            ClearNavigationHistory();

            m_ViewChange = true;
        }

        public static void HighlightSystem(SystemProxy systemProxy)
        {
            SystemTreeView.SelectedSystem = systemProxy;

            if (HasOpenInstances<SystemScheduleWindow>())
            {
                var systemWindow = GetWindow<SystemScheduleWindow>();
                systemWindow.m_SystemTreeView.SetSelection();
                systemWindow.UpdateSelectedSystem(SystemTreeView.SelectedSystem);
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            if (Unsupported.IsDeveloperMode())
            {
                menu.AddItem(new GUIContent($"Debug..."), false, () =>
                    SelectionUtility.ShowInWindow(new SystemsWindowDebugContentProvider()));
            }
        }

        void OnGlobalSelectionChanged()
        {
            if (Selection.activeObject is InspectorContent content && content.Content.Name.Equals(Contents.System))
                return;

            SystemTreeView.SelectedSystem = default;
            m_SystemTreeView.MultiColumnTreeViewElement.ClearSelection();
        }
    }
}
