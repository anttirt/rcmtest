using System.Collections.Generic;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace Unity.Entities.Editor
{
    internal static class SystemSearchProvider
    {
        /// <summary>
        /// Search Provider type id. 
        /// </summary>
        const string k_Type = "system";

        static Dictionary<string, string[]> s_SystemDependencyMap = new ();
        static World s_CurrentWorld;
        static WorldProxyManager s_WorldProxyManager;
        static WorldProxy s_WorldProxy;
        
        static QueryEngine<SystemDescriptor> s_QueryEngine;
        static QueryEngine<SystemDescriptor> queryEngine
        {
            get
            {
                if (s_QueryEngine == null)
                    SetupQueryEngine();
                return s_QueryEngine;
            }
        }

        static List<SystemDescriptor> s_Systems;
        internal static IEnumerable<SystemDescriptor> systems
        {
            get
            {
                if (s_Systems != null) 
                    return s_Systems;
                
                if (SetupSystemDescriptors())
                    GetAllSystems();
                return s_Systems;
            }
        }

        internal static void SetWorld(World world)
        {
            Cleanup();
            s_CurrentWorld = world;
        }

        static void SelectItem(SearchItem item, SearchContext ctx)
        {
            var data = (SystemDescriptor)item.data;
            SelectionUtility.ShowInInspector(new SystemContentProvider
            {
                World = data.Proxy.World,
                SystemProxy = data.Proxy
            }, new InspectorContentParameters
            {
                UseDefaultMargins = false,
                ApplyInspectorStyling = false
            });
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            return new []
            {
                new SearchAction(k_Type, "select", null, "Select System")
                {
                    execute = (items) =>
                    {
                        SelectItem(items[0], null);
                    },
                    closeWindowAfterExecution = false
                }
            };
        }

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            var p = new SearchProvider(k_Type, "Systems")
            {
                type = k_Type,
                filterId = "sys:",
                onEnable = OnEnable,
                onDisable = OnDisable,
                fetchColumns = FetchColumns,
                isExplicitProvider = true,
                active = true,
                priority = 2500,
                fetchThumbnail = SearchUtils.DefaultFetchThumbnail,
                fetchLabel = SearchUtils.DefaultFetchLabel,
                fetchDescription = SearchUtils.DefaultFetchDescription,
                fetchItems = (context, _, provider) => FetchItems(context, provider),
                fetchPropositions = FetchPropositions,
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Default | ShowDetailsOptions.Inspector,
                trackSelection = SelectItem,
                toObject = (item, _) =>
                {
                    var wrapper = ScriptableObject.CreateInstance<SystemInfo>();
                    wrapper.name = item.label;
                    wrapper.item = item;
                    wrapper.objItem = (SystemDescriptor)item.data;
                    return wrapper;
                }
            };
            SearchBridge.SetTableConfig(p, GetDefaultTableConfig);
            return p;
        }

        static IEnumerable<string> GetWords(SystemDescriptor desc)
        {
            yield return desc.Name;
        }

        static void SetupQueryEngine()
        {
            s_QueryEngine = new();
            s_QueryEngine.SetSearchDataCallback(GetWords);

            SearchBridge.AddFilter<string, SystemDescriptor>(s_QueryEngine, "c", OnComponentTypeFilter, new[] { ":", "=" });
            SearchBridge.AddFilter<string, SystemDescriptor>(s_QueryEngine, "ns", OnNamespaceFilter, new[] { ":", "=" });
            SearchBridge.AddFilter<string, SystemDescriptor>(s_QueryEngine, "sd", OnSystemDependencyFilter, new[] { ":", "=" });
            SearchBridge.AddFilter<string, SystemDescriptor>(s_QueryEngine, "parent", OnParentFilter, new[] { ":", "=" });

            // Note: Skip filter for world filter that shouldn't be tested against the dataset
            s_QueryEngine.skipUnknownFilters = true;

            SearchBridge.SetFilter(s_QueryEngine, "entitycount", data => data.Proxy.TotalEntityMatches)
                .AddOrUpdateProposition(category: null, label: "Entity Count", replacement: "entitycount>10", help: "Search Systems by Entity Count");

            SearchBridge.SetFilter(s_QueryEngine, "componentcount", data => data.ComponentNamesInQueryCache.Length)
                .AddOrUpdateProposition(category: null, label: "Component Count", replacement: "componentcount>5", help: "Search Systems by Component Count");

            SearchBridge.SetFilter(s_QueryEngine, "dependencycount", data => data.SystemDependencyCache.Length)
                .AddOrUpdateProposition(category: null, label: "Dependency Count", replacement: "dependencycount>0", help: "Search Systems by Dependency Count");
            SearchBridge.SetFilter(s_QueryEngine, "time", data => data.Proxy.RunTimeMillisecondsForDisplay)
                .AddOrUpdateProposition(category: null, label: "Time", replacement: "time>100", help: "Search Systems by time");

            SearchBridge.SetFilter(s_QueryEngine, "enabled", data => data.Proxy.Enabled)
                .AddOrUpdateProposition(category: null, label: "Enabled", replacement: "enabled=true", help: "Search Enabled systems");

            SearchBridge.SetFilter(s_QueryEngine, "isrunning", data => data.Proxy.IsRunning)
                .AddOrUpdateProposition(category: null, label: "Is Running", replacement: "isrunning=true", help: "Search Running systems");

            SearchBridge.SetFilter(s_QueryEngine, "childcount", data => data.Proxy.ChildCount)
                .AddOrUpdateProposition(category: null, label: "childcount", replacement: "childcount>5", help: "Search systems by child count");
        }

        static bool SetupSystemDescriptors()
        {
            s_WorldProxyManager = new WorldProxyManager();
            s_SystemDependencyMap = new();            
            if (s_CurrentWorld == null)
            {
                Debug.LogWarning("System Search provider: cannot find a valid World");
                return false;
            }
            s_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            s_WorldProxy = s_WorldProxyManager.GetWorldProxyForGivenWorld(s_CurrentWorld);
            s_WorldProxyManager.SetSelectedWorldProxy(s_WorldProxy);
            return true;
        }

        static void OnEnable()
        {
            s_CurrentWorld = World.DefaultGameObjectInjectionWorld;
            
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Cleanup();
        }

        static void Cleanup()
        {
            s_CurrentWorld = null;
            s_WorldProxyManager?.Dispose();
            s_WorldProxyManager = null;
            s_Systems?.Clear();
            s_Systems = null;
            s_SystemDependencyMap?.Clear();
            s_SystemDependencyMap = null;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode || stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                Cleanup();
            }
            else if (stateChange == PlayModeStateChange.EnteredPlayMode || stateChange == PlayModeStateChange.EnteredEditMode)
            {
                SearchBridge.RefreshWindowsWithProvider(k_Type);
            }
        }

        static bool OnComponentTypeFilter(SystemDescriptor desc, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, desc.ComponentNamesInQueryCache);
        }
        
        static bool OnNamespaceFilter(SystemDescriptor desc, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, desc.Proxy.Namespace);
        }
        
        static bool OnParentFilter(SystemDescriptor desc, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, desc.Proxy.Parent.TypeName);
        }        

        static bool OnSystemDependencyFilter(SystemDescriptor desc, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, desc.SystemDependencyCache);
        }

        static SearchTable GetDefaultTableConfig(SearchContext context)
        {
            return new SearchTable(k_Type, FetchColumns(null, null));
        }

        static IEnumerable<SystemDescriptor> GetAllSystems()
        {
            if (s_SystemDependencyMap == null)
                s_SystemDependencyMap = new();
            if (s_Systems == null)
                s_Systems = new();

            s_SystemDependencyMap.Clear();
            s_Systems.Clear();

            foreach (var world in World.All)
            {
                if (!s_WorldProxyManager.TryGetWorldProxy(world, out var proxy))
                    continue;

                var systemsSnapshot = new List<SystemProxy>(proxy.AllSystems);
                foreach (var system in systemsSnapshot)
                {
                    s_Systems.Add(new SystemDescriptor(system));
                    SystemProxy.BuildSystemDependencyMap(system, s_SystemDependencyMap);
                }
            }

            FillSystemDependencyCache(s_Systems, s_SystemDependencyMap);

            return s_Systems;
        }

        static class Styles
        {
            public static Texture2D systemIcon = PackageResources.LoadIcon("System/System.png");
            public static Texture2D systemGroupIcon = PackageResources.LoadIcon("Group/Group.png");
            public static Texture2D beginCommandBufferIcon = PackageResources.LoadIcon("BeginCommandBuffer/BeginCommandBuffer.png");
            public static Texture2D endCommandBufferIcon = PackageResources.LoadIcon("EndCommandBuffer/EndCommandBuffer.png");
            public static Texture2D unmanagedSystemIcon = PackageResources.LoadIcon("UnmanagedSystem/UnmanagedSystem.png");
        }

        static Texture2D GetSystemIcon(SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;
            if ((flags & SystemCategory.ECBSystemBegin) != 0)
                return Styles.beginCommandBufferIcon;
            if ((flags & SystemCategory.ECBSystemEnd) != 0)
                return Styles.endCommandBufferIcon;
            if ((flags & SystemCategory.EntityCommandBufferSystem) != 0)
                return null;
            if ((flags & SystemCategory.Unmanaged) != 0)
                return Styles.unmanagedSystemIcon;
            if ((flags & SystemCategory.SystemGroup) != 0)
                return Styles.systemGroupIcon;
            if ((flags & SystemCategory.SystemBase) != 0)
                return Styles.systemIcon;

            return null;
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

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            if (s_CurrentWorld == null)
                s_CurrentWorld = World.DefaultGameObjectInjectionWorld;
            
            var searchQuery = context.searchQuery;
            ParsedQuery<SystemDescriptor> query = null;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = queryEngine.ParseQuery(context.searchQuery);
                if (!query.valid)
                {
                    foreach (var e in query.errors)
                        context.AddSearchQueryErrors(new SearchQueryError[] { new (e, context, provider) });
                    yield break;
                }

                var toggles = new List<IQueryNode>();
                var filters = new List<IFilterNode>();
                var searches = new List<ISearchNode>();
                SearchUtils.GetQueryParts(query.queryGraph.root, filters, toggles, searches);
            }

            if (s_CurrentWorld == null || systems == null)
            {
                SearchUtils.AddError("Cannot find a world to execute the query", context, provider);
                yield break;
            }
            
            var score = 0;
            var results = string.IsNullOrEmpty(searchQuery) ? systems : query.Apply(systems);
            var tmpResults = new List<SystemDescriptor>(results);
            foreach (var data in tmpResults)
            {
                yield return provider.CreateItem(context, data.Proxy.TypeFullName, score++, data.Name, data.Proxy.Namespace, GetSystemIcon(data.Proxy), data);
            }
        }

        static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            if (!options.flags.HasAny(SearchPropositionFlags.QueryBuilder))
                yield break;
            
            foreach (var p in SearchBridge.GetAndOrQueryBlockPropositions())
                yield return p;
            foreach (var p in SearchBridge.GetPropositions(queryEngine))
                yield return p;
            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryComponentTypeBlock)))
                yield return l;
            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QuerySystemDependenciesBlock)))
                yield return l;
            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryNamespaceBlock)))
                yield return l;
            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryParentBlock)))
                yield return l;
        }

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {
            yield return new SearchColumn("Systems/Enabled", "Systems/Enabled", "Systems/Enabled");
            if (context == null)
            {
                yield return new SearchColumn("Name", "label");
            }
            
            yield return new SearchColumn("Systems/Namespace", "namespace", nameof(System));
            yield return new SearchColumn("Systems/Entity Count", "entitycount", nameof(System));
            yield return new SearchColumn("Systems/Time", "time", nameof(System));
            if (context == null)
            {
                yield break;
            }

            yield return new SearchColumn("Systems/Category", "category", nameof(System));
            yield return new SearchColumn("Systems/Is Running", "isrunning", nameof(System));
            yield return new SearchColumn("Systems/Child Count", "childcount", nameof(System));
        }
    }
    
    internal class SystemDescriptor
    {
        public SystemProxy Proxy { get; }
        public IPlayerLoopNode Node { get; set; }

        public string[] ComponentNamesInQueryCache { get; }
        public string[] SystemDependencyCache { get; private set; }
        
        public string Name => Proxy.TypeName;

        public SystemDescriptor(SystemProxy proxy)
        {
            Proxy = proxy;
            ComponentNamesInQueryCache = EntityQueryUtility.CollectComponentTypesFromSystemQuery(proxy);
            SystemDependencyCache = null;
        }

        public void UpdateDependencies(string[] dependencies)
        {
            SystemDependencyCache = dependencies;
        }

        public override string ToString()
        {
            return $"{Name} Comp: {(ComponentNamesInQueryCache == null ? 0 : ComponentNamesInQueryCache.Length)} Dep: {(SystemDependencyCache == null ? 0 : SystemDependencyCache.Length)}";
        }
    }

    [ExcludeFromPreset]
    internal class SystemInfo : SearchItemWrapper<SystemDescriptor>
    {
        void OnEnable()
        {
            hideFlags &= HideFlags.NotEditable;
        }
    }    
}