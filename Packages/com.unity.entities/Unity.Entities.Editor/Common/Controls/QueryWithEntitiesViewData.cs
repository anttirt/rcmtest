using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Entities.Editor
{
    class QueryWithEntitiesViewData
    {
        const int k_DefaultMaxEntityDisplayCount = 5;

        public readonly World World;
        public readonly SystemProxy SystemProxy;
        public readonly EntityQuery Query;
        public readonly int QueryOrder;
        public readonly int MaxEntityDisplayCount;
        public readonly bool HasChunkComponents;

        int m_LastVersion;

        public QueryWithEntitiesViewData(World world, EntityQuery query, SystemProxy systemProxy = default, int queryOrder = 0, int maxEntityDisplayCount = k_DefaultMaxEntityDisplayCount)
        {
            World = world;
            SystemProxy = systemProxy;
            Query = query;
            QueryOrder = queryOrder;
            MaxEntityDisplayCount = maxEntityDisplayCount;
            HasChunkComponents = EntityQueryToSearchString.HasChunkComponents(query);
        }

        public int TotalEntityCount { get; private set; }
        public int MetaEntityCount { get; private set; }
        public List<ChunkGroup> Groups { get; } = new List<ChunkGroup>();

        public class ChunkGroup
        {
            public EntityViewData? Meta;
            public readonly List<EntityViewData> RealEntities = new List<EntityViewData>();
        }

        public bool Update()
        {
            if (!World.IsCreated)
            {
                var hadGroups = Groups.Count != 0;
                TotalEntityCount = 0;
                MetaEntityCount = 0;
                Groups.Clear();
                return hadGroups;
            }

            Query.CompleteDependency();
            if (!World.EntityManager.IsQueryValid(Query))
                return false;

            var query = Query;
            // TODO(DOTS-10317): Replace this with a proper EntityQuery results hash if & when we have one
            var currentVersion = query.GetCombinedComponentOrderVersion(true);
            if (m_LastVersion == currentVersion)
                return false;

            m_LastVersion = currentVersion;
            Groups.Clear();

            var em = World.EntityManager;
            using var entities = query.ToEntityArray(Allocator.Temp);

            // Queries that put ChunkHeader in All (directly or via IncludeMetaChunks) get back meta
            // entities, not real ones — key them by the real chunk they represent so each meta
            // renders with the meta row treatment instead of being mistaken for a real entity.
            // NativeParallelMultiHashMap iterates values per key in reverse insertion order, which
            // is fine here: entity slot order within a chunk isn't semantically meaningful for the
            // "first N per chunk" display cap.
            using var chunkToEntities = new NativeParallelMultiHashMap<ArchetypeChunk, Entity>(entities.Length, Allocator.Temp);
            using var chunkToMeta = new NativeHashMap<ArchetypeChunk, Entity>(16, Allocator.Temp);
            using var orderedChunks = new NativeList<ArchetypeChunk>(16, Allocator.Temp);
            using var chunkSeen = new NativeHashSet<ArchetypeChunk>(16, Allocator.Temp);
            var realCount = 0;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (em.HasComponent<ChunkHeader>(entity))
                {
                    var realChunk = em.GetComponentData<ChunkHeader>(entity).ArchetypeChunk;
                    if (chunkSeen.Add(realChunk))
                        orderedChunks.Add(realChunk);
                    chunkToMeta.Add(realChunk, entity);
                }
                else
                {
                    var chunk = em.GetChunk(entity);
                    if (chunkSeen.Add(chunk))
                        orderedChunks.Add(chunk);
                    chunkToEntities.Add(chunk, entity);
                    realCount++;
                }
            }

            // Real-entity queries with chunk components still get their meta entities annotated
            // alongside; meta-entity queries already filled chunkToMeta above.
            if (orderedChunks.Length > 0 && chunkToMeta.IsEmpty && HasChunkComponents)
            {
                for (var i = 0; i < orderedChunks.Length; i++)
                {
                    var chunk = orderedChunks[i];
                    var metaEntity = chunk.m_Chunk.MetaChunkEntity;
                    if (metaEntity != Entity.Null)
                        chunkToMeta.Add(chunk, metaEntity);
                }
            }

            TotalEntityCount = realCount;
            MetaEntityCount = chunkToMeta.Count;

            var remaining = MaxEntityDisplayCount;
            for (var i = 0; i < orderedChunks.Length && remaining > 0; i++)
            {
                var chunk = orderedChunks[i];
                var group = new ChunkGroup();
                if (chunkToMeta.TryGetValue(chunk, out var metaEntity))
                {
                    group.Meta = new EntityViewData(World, metaEntity);
                    remaining--;
                }

                if (remaining > 0 && chunkToEntities.TryGetFirstValue(chunk, out var realEntity, out var it))
                {
                    do
                    {
                        group.RealEntities.Add(new EntityViewData(World, realEntity));
                        remaining--;
                    }
                    while (remaining > 0 && chunkToEntities.TryGetNextValue(out realEntity, ref it));
                }

                Groups.Add(group);
            }

            return true;
        }
    }
}
