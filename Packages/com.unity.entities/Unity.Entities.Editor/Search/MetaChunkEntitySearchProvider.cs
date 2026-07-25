using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using Unity.Editor.Bridge;

namespace Unity.Entities.Editor
{
    public static class MetaChunkEntitySearchProvider
    {
        public const string type = "metachunk";

        internal readonly struct EntityRowData
        {
            public readonly World World;
            public readonly Entity Entity;
            public readonly ArchetypeChunk Chunk;
            public readonly bool IsMeta;

            public EntityRowData(World world, Entity entity, ArchetypeChunk chunk, bool isMeta)
            {
                World = world;
                Entity = entity;
                Chunk = chunk;
                IsMeta = isMeta;
            }
        }

        static QueryEngine<EntityRowData> s_QueryEngine;
        static TypeIndex s_ChunkHeaderTypeIndex;
        static bool s_ChunkHeaderTypeIndexInitialized;

        static QueryEngine<EntityRowData> queryEngine
        {
            get
            {
                if (s_QueryEngine == null)
                    SetupQueryEngine();
                return s_QueryEngine;
            }
        }

        static TypeIndex ChunkHeaderTypeIndex
        {
            get
            {
                if (!s_ChunkHeaderTypeIndexInitialized)
                {
                    s_ChunkHeaderTypeIndex = TypeManager.GetTypeIndex<ChunkHeader>();
                    s_ChunkHeaderTypeIndexInitialized = true;
                }
                return s_ChunkHeaderTypeIndex;
            }
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            yield break;
        }

        [SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            var p = new SearchProvider(type, "Chunk Meta Entities")
            {
                type = type,
                filterId = "meta:",
                isExplicitProvider = true,
                active = true,
                priority = 2600,
                fetchThumbnail = SearchUtils.DefaultFetchThumbnail,
                fetchLabel = SearchUtils.DefaultFetchLabel,
                fetchDescription = SearchUtils.DefaultFetchDescription,
                fetchColumns = FetchColumns,
                fetchItems = (context, items, provider) => FetchItems(context, provider),
                fetchPropositions = (context, options) => FetchPropositions(context, options),
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Default | ShowDetailsOptions.Inspector | ShowDetailsOptions.InspectorWithoutHeader,
                toObject = (item, _) => CreateProxy(item),
                trackSelection = (item, _) => SelectItem(item),
                onDisable = ClearQueryContext,
            };
            SearchBridge.SetTableConfig(p, GetDefaultTableConfig);
            return p;
        }

        static SearchTable GetDefaultTableConfig(SearchContext context)
        {
            var columns = new List<SearchColumn> { new("Name", "label") };
            foreach (var column in FetchColumns(null, null))
                columns.Add(column);
            return new SearchTable(type, columns);
        }

        const string k_ColumnProviderName = "MetaChunkEntity";

        static IEnumerable<SearchColumn> FetchColumns(SearchContext context, IEnumerable<SearchItem> items)
        {
            yield return new SearchColumn("World", "world", k_ColumnProviderName);
            yield return new SearchColumn("Chunk (Seq Number)", "chunk", k_ColumnProviderName,
                content: null,
                options: SearchColumnFlags.Default | SearchColumnFlags.Sorted);
        }

        [SearchColumnProvider(k_ColumnProviderName)]
        internal static void MetaChunkColumnProvider(SearchColumn column)
        {
            column.getter = args =>
            {
                if (args.item.data is EntityRowData data)
                {
                    switch (column.selector)
                    {
                        case "world": return data.World?.Name;
                        case "chunk":
                            return data.Chunk.Equals(ArchetypeChunk.Null)
                                ? (object)null
                                : data.Chunk.SequenceNumber;
                    }
                }
                return null;
            };

            if (column.selector == "chunk")
            {
                // Sort by chunk SequenceNumber, then keep the meta entity above the real
                // entities that live in the same chunk so each group reads top-down.
                column.comparer = args =>
                {
                    var lhs = args.lhs.value is ulong l ? l : ulong.MaxValue;
                    var rhs = args.rhs.value is ulong r ? r : ulong.MaxValue;
                    var cmp = lhs.CompareTo(rhs);
                    if (cmp != 0)
                        return args.sortAscending ? cmp : -cmp;
                    var lhsMeta = args.lhs.item.data is EntityRowData ld && ld.IsMeta;
                    var rhsMeta = args.rhs.item.data is EntityRowData rd && rd.IsMeta;
                    if (lhsMeta == rhsMeta)
                        return 0;
                    return lhsMeta ? -1 : 1;
                };
            }
        }

        static void SetupQueryEngine()
        {
            s_QueryEngine = new QueryEngine<EntityRowData>();
            s_QueryEngine.skipUnknownFilters = true;
            s_QueryEngine.SetSearchDataCallback(data => GetChunkComponentNames(data));

            SearchBridge.SetFilter(s_QueryEngine, "world", data => data.World?.Name)
                .AddOrUpdateProposition(category: null, label: "World", replacement: "world:\"Default World\"", help: "Search by world name.");

            SearchBridge.AddFilter<string, EntityRowData>(s_QueryEngine, "chunk", OnChunkComponentFilter, new[] { ":", "=" });
        }

        static List<string> GetChunkComponentNames(EntityRowData data)
        {
            var result = new List<string>();
            if (data.Chunk.Equals(ArchetypeChunk.Null))
                return result;
            // Allocator.Temp lifetime is one frame; materialize before returning so callers can
            // consume the list later (e.g. lazily through QuickSearch) without dangling reads.
            using var types = data.Chunk.Archetype.GetComponentTypes(Allocator.Temp);
            var headerIndex = ChunkHeaderTypeIndex;
            for (var i = 0; i < types.Length; i++)
            {
                var ct = types[i];
                if (!ct.IsChunkComponent || ct.TypeIndex == headerIndex)
                    continue;
                var t = TypeManager.GetType(ct.TypeIndex);
                if (t != null)
                    result.Add(t.Name);
            }
            return result;
        }

        static bool OnChunkComponentFilter(EntityRowData data, QueryFilterOperator op, string value)
        {
            return SearchBridge.CompareWords(op, value, GetChunkComponentNames(data));
        }

        static List<EntityRowData> EnumerateAllRows()
        {
            var rows = new List<EntityRowData>();
            FindActiveFilter(out var activeWorld, out var activeQuery);

            foreach (var world in World.All)
            {
                if (world is not { IsCreated: true })
                    continue;
                var em = world.EntityManager;
                if (!em.CanBeginExclusiveEntityTransaction())
                    continue;

                AppendMetaEntities(world, em, rows);

                if (world != activeWorld || activeQuery == default)
                    continue;

                AppendRealEntitiesByChunk(world, em, activeQuery, rows);
            }
            return rows;
        }

        static void AppendMetaEntities(World world, EntityManager em, List<EntityRowData> rows)
        {
            var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ChunkHeader>() },
                Options = EntityQueryOptions.IncludeMetaChunks
            });
            try
            {
                // Allocator.Temp lifetime is one frame; materialize into the caller's list before
                // the native array is disposed so QuickSearch can consume the results lazily.
                using var entities = query.ToEntityArray(Allocator.Temp);
                for (var i = 0; i < entities.Length; i++)
                {
                    var metaEntity = entities[i];
                    var header = em.GetComponentData<ChunkHeader>(metaEntity);
                    rows.Add(new EntityRowData(world, metaEntity, header.ArchetypeChunk, isMeta: true));
                }
            }
            finally
            {
                query.Dispose();
            }
        }

        static void AppendRealEntitiesByChunk(World world, EntityManager em, EntityQuery query, List<EntityRowData> rows)
        {
            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            var handle = em.GetEntityTypeHandle();
            for (var i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                var entities = chunk.GetNativeArray(handle);
                for (var j = 0; j < entities.Length; j++)
                    rows.Add(new EntityRowData(world, entities[j], chunk, isMeta: false));
            }
        }

        static void FindActiveFilter(out World world, out EntityQuery query)
        {
            foreach (var w in World.All)
            {
                if (!w.IsCreated)
                    continue;
                var sys = w.GetExistingSystemManaged<MetaChunkSearchFilterSystem>();
                if (sys == null || !sys.HasFilter)
                    continue;
                world = w;
                query = sys.Query;
                return;
            }
            world = null;
            query = default;
        }

        // The search inspector pane creates and retains an Editor bound to whatever toObject
        // returned. Destroying a proxy when navigating away leaves that Editor pointing at a
        // dead ScriptableObject, and navigating back hands the pane a fresh proxy that the stale
        // Editor doesn't pick up — symptom is a blank pane on the return click. Keep proxies
        // alive for the window's lifetime; version comparison on the cached entry prevents
        // recycled entity slots from returning the wrong proxy.
        static readonly Dictionary<long, EntitySelectionProxy> s_InspectorProxies = new();

        static EntitySelectionProxy CreateProxy(SearchItem item)
        {
            if (item.data is not EntityRowData data
                || data.World is not { IsCreated: true }
                || !data.World.EntityManager.SafeExists(data.Entity))
                return null;

            var key = ((long)data.World.SequenceNumber << 32) | (uint)data.Entity.Index;
            if (s_InspectorProxies.TryGetValue(key, out var cached)
                && cached != null
                && cached.World == data.World
                && cached.Entity == data.Entity)
                return cached;

            if (cached != null)
                cached.Release();

            var proxy = EntitySelectionProxy.CreateInstance(data.World, data.Entity);
            proxy.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            proxy.Retain();
            s_InspectorProxies[key] = proxy;
            return proxy;
        }

        static void ReleaseInspectorProxies()
        {
            foreach (var proxy in s_InspectorProxies.Values)
            {
                if (proxy != null)
                    proxy.Release();
            }
            s_InspectorProxies.Clear();
        }

        static void SelectItem(SearchItem item)
        {
            if (item.data is EntityRowData data && data.World is { IsCreated: true })
                EntitySelectionProxy.SelectEntity(data.World, data.Entity);
        }

        static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
        {
            ParsedQuery<EntityRowData> parsed = null;
            if (!string.IsNullOrEmpty(context.searchQuery))
            {
                parsed = queryEngine.ParseQuery(context.searchQuery);
                if (!parsed.valid)
                {
                    var sb = new StringBuilder();
                    foreach (var error in parsed.errors)
                    {
                        if (sb.Length > 0)
                            sb.Append(' ');
                        sb.Append(error.reason);
                    }
                    SearchUtils.AddError(sb.ToString(), context, provider);
                    yield break;
                }
            }

            IEnumerable<EntityRowData> results = EnumerateAllRows();
            if (parsed != null)
                results = parsed.Apply(results);

            foreach (var data in results)
            {
                var worldName = data.World?.Name;
                var id = $"{worldName}/{(data.IsMeta ? "meta" : "ent")}/{data.Entity.Index}-{data.Entity.Version}";
                var label = data.IsMeta
                    ? $"Meta Entity {data.Entity.Index}:{data.Entity.Version}"
                    : GetRealEntityLabel(data);
                // Only meta entities benefit from a description — real entities show their
                // index/version through the EntityEditor below.
                var description = data.IsMeta
                    ? $"{label} — {data.Chunk.Count} entit{(data.Chunk.Count == 1 ? "y" : "ies")} in chunk"
                    : null;
                yield return provider.CreateItem(context, id, worldName?.GetHashCode() ?? 0, label, description, null, data);
            }
        }

        static string GetRealEntityLabel(EntityRowData data)
        {
            if (data.World is { IsCreated: true })
            {
                var name = data.World.EntityManager.GetName(data.Entity);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return $"Entity {data.Entity.Index}:{data.Entity.Version}";
        }

        static IEnumerable<SearchProposition> FetchPropositions(SearchContext context, SearchPropositionOptions options)
        {
            foreach (var p in SearchBridge.GetPropositions(queryEngine))
                yield return p;

            foreach (var l in SearchBridge.GetPropositionsFromListBlockType(typeof(QueryWorldBlock)))
                yield return l;
        }

        internal static void OpenProviderForQuery(string filter, World world, EntityQueryDesc[] descs)
        {
            SetQueryContext(world, descs);
            var view = SearchBridge.OpenContextualTable(type, filter ?? "", GetDefaultTableConfig(null));
            view?.Focus();
        }

        internal static void SetQueryContext(World world, EntityQueryDesc[] descs)
        {
            if (world is { IsCreated: true } && descs is { Length: > 0 })
            {
                var system = world.GetOrCreateSystemManaged<MetaChunkSearchFilterSystem>();
                // Wire the system into the world's tick. Idempotent — duplicate adds are no-ops.
                world.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(system);
                system.SetFilterQuery(descs);
                EnsureSubscribed();
            }
            else
            {
                ClearQueryContext();
            }
        }

        internal static void ClearQueryContext()
        {
            foreach (var w in World.All)
            {
                if (!w.IsCreated)
                    continue;
                var sys = w.GetExistingSystemManaged<MetaChunkSearchFilterSystem>();
                if (sys == null)
                    continue;
                var group = w.GetExistingSystemManaged<SimulationSystemGroup>();
                group?.RemoveSystemFromUpdateList(sys);
                w.DestroySystem(sys.SystemHandle);
            }
            UnsubscribeIfNeeded();
            ReleaseInspectorProxies();
        }

        static bool s_Subscribed;

        static void EnsureSubscribed()
        {
            if (s_Subscribed)
                return;
            MetaChunkSearchFilterSystem.OnFilterChanged += OnFilterChanged;
            s_Subscribed = true;
        }

        static void UnsubscribeIfNeeded()
        {
            if (!s_Subscribed)
                return;
            MetaChunkSearchFilterSystem.OnFilterChanged -= OnFilterChanged;
            s_Subscribed = false;
        }

        static void OnFilterChanged(World world)
        {
            SearchBridge.RefreshWindowsWithProvider(type);
        }
    }
}
