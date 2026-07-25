using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;
using Unity.Entities.UI;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemQueriesTab : ITabContent
    {
        public string TabName { get; } = L10n.Tr("Queries");

        UnsafeList<EntityQuery> m_LastQueries;
        bool m_IsVisible;

        public void OnTabVisibilityChanged(bool isVisible) => m_IsVisible = isVisible;

        [CreateProperty, HideInInspector, DontSerialize]
        public int Count => QueriesFromSystem.Length;

        public SystemQueriesTab(World world, SystemProxy systemProxy)
        {
            World = world;
            SystemProxy = systemProxy;
            Entities = new SystemEntities(world, systemProxy);
        }

        public World World { get; }
        public SystemProxy SystemProxy { get; }
        public SystemEntities Entities { get; }
        public bool IsVisible => m_IsVisible;

        public unsafe UnsafeList<EntityQuery> QueriesFromSystem
        {
            get
            {
                if (!World.IsCreated || SystemProxy == default || !SystemProxy.Valid)
                    return default;

                var ptr = SystemProxy.StatePointerForQueryResults;
                if (ptr == null)
                    return default;

                var currentQueries = ptr->EntityQueries;
                if (m_LastQueries.Equals(currentQueries))
                    return m_LastQueries;

                m_LastQueries = currentQueries;
                return currentQueries.Length > 0 ? currentQueries : default;
            }
        }
    }

    [UsedImplicitly]
    class SystemQueriesInspector : PropertyInspector<SystemQueriesTab>
    {
        readonly Cooldown m_Cooldown = new Cooldown(TimeSpan.FromMilliseconds(Constants.Inspector.CoolDownTime));
        readonly List<QueryEntry> m_QueryEntries = new List<QueryEntry>();

        static readonly string k_Query = L10n.Tr("Query");
        static readonly string k_Entity = L10n.Tr("entity");
        static readonly string k_Entities = L10n.Tr("entities");
        static readonly string k_Meta = L10n.Tr("meta");

        struct QueryEntry
        {
            public QueryView QueryView;
            public QueryWithEntitiesView EntitiesView;
            public QueryWithEntitiesViewData Data;
            public Button SeeAllButton;
            public int QueryId;
        }

        public override VisualElement Build()
        {
            var root = new VisualElement();
            Resources.Templates.DotsEditorCommon.AddStyles(root);

            var queries = Target.QueriesFromSystem;
            if (queries.Length == 0)
            {
                var noQueryLabel = new Label(L10n.Tr("No Queries."));
                noQueryLabel.AddToClassList(UssClasses.Content.SystemInspector.SystemQueriesEmpty);
                root.Add(noQueryLabel);
                return root;
            }

            var entitiesFromQueries = Target.Entities.EntitiesFromQueries;

            for (var i = 0; i < queries.Length; ++i)
            {
                var queryId = i + 1;
                var queryView = new QueryView(new QueryViewData(queryId, queries[i], Target.SystemProxy, Target.World));
                queryView.Header.AddToClassList(UssClasses.QueryView.HeaderBold);
                queryView.Q<Toggle>().AddToClassList(UssClasses.QueryView.Toggle);
                queryView.Q(className: "unity-foldout__content").AddToClassList(UssClasses.QueryView.FoldoutContentPadding);
                root.Add(queryView);

                QueryWithEntitiesView entitiesView = null;
                QueryWithEntitiesViewData entitiesData = null;
                Button seeAllButton = null;

                if (i < entitiesFromQueries.Count)
                {
                    entitiesData = entitiesFromQueries[i];
                    entitiesView = new QueryWithEntitiesView(entitiesData);
                    entitiesView.HeaderName.text = L10n.Tr("Entities");
                    entitiesView.SetValueWithoutNotify(false);
                    queryView.Add(entitiesView);

                    var capturedData = entitiesData;
                    var capturedQueryId = queryId;
                    var capturedSystemProxy = Target.SystemProxy;
                    seeAllButton = new Button(() =>
                    {
                        if (capturedData.HasChunkComponents)
                        {
                            var filter = EntityQueryToSearchString.Build(capturedData.Query, capturedData.World);
                            var descs = capturedData.Query.GetEntityQueryDescs();
                            MetaChunkEntitySearchProvider.OpenProviderForQuery(filter, capturedData.World, descs);
                            return;
                        }

                        var hierarchyWindow = EditorWindow.GetWindow<Unity.Hierarchy.Editor.HierarchyWindow>();
                        var sys = capturedData.World?.GetOrCreateSystemManaged<ExplicitFilterSystem>();
                        sys?.SetExplicitFilterQuery(capturedData.Query);

                        var label = BuildExplicitFilterLabel(capturedSystemProxy, capturedQueryId);
                        hierarchyWindow.SetSearchText(HierarchyExplicitEntityQueryBlock.BuildSearchToken(label));
                    });
                    seeAllButton.style.display = DisplayStyle.None;
                    seeAllButton.AddToClassList(UssClasses.QueryWithEntities.SeeAll);
                    entitiesView.Add(seeAllButton);
                }

                m_QueryEntries.Add(new QueryEntry
                {
                    QueryView = queryView,
                    EntitiesView = entitiesView,
                    Data = entitiesData,
                    SeeAllButton = seeAllButton,
                    QueryId = queryId
                });
            }

            Update();
            return root;
        }

        public override void Update()
        {
            if (!Target.IsVisible || !m_Cooldown.Update(DateTime.UtcNow))
                return;

            foreach (var entry in m_QueryEntries)
            {
                if (entry.EntitiesView == null)
                    continue;

                entry.EntitiesView.Update();

                var totalCount = entry.Data.TotalEntityCount;
                UpdateQueryHeader(entry.QueryView, entry.QueryId, totalCount, entry.Data.MetaEntityCount);

                if (entry.SeeAllButton != null)
                {
                    var capped = (totalCount + entry.Data.MetaEntityCount) > entry.Data.MaxEntityDisplayCount;
                    entry.SeeAllButton.style.display = capped ? DisplayStyle.Flex : DisplayStyle.None;
                    if (capped)
                    {
                        entry.SeeAllButton.text = entry.Data.HasChunkComponents
                            ? L10n.Tr("Search all entities + meta")
                            : L10n.Tr("See all");
                    }
                }
            }
        }

        static void UpdateQueryHeader(QueryView queryView, int queryId, int entityCount, int metaCount)
        {
            var unit = entityCount == 1 ? k_Entity : k_Entities;
            queryView.HeaderName.text = metaCount > 0
                ? $"{k_Query} #{queryId} ({entityCount} {unit} + {metaCount} {k_Meta})"
                : $"{k_Query} #{queryId} ({entityCount} {unit})";
        }

        // Builds the user-visible label shown inside the QuickSearch chip while an explicit
        // filter is active (the value half of HierarchyExplicitEntityQueryBlock)
        static string BuildExplicitFilterLabel(SystemProxy systemProxy, int queryId)
        {
            var name = systemProxy.Valid && !string.IsNullOrEmpty(systemProxy.NicifiedDisplayName)
                ? systemProxy.NicifiedDisplayName
                : L10n.Tr("Unknown System");
            return $"Query #{queryId} ({name})";
        }
    }
}
