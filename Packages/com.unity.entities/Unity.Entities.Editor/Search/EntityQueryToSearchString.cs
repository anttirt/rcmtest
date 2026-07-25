using System.Collections.Generic;
using System.Text;

namespace Unity.Entities.Editor
{
    // Translates an EntityQuery into a QuickSearch filter string for use by a
    // SearchProvider. Coverage is partial — only what current callers need —
    // and we lean on out-of-band systems (e.g. MetaChunkSearchFilterSystem,
    // ExplicitFilterSystem) for the parts the string can't faithfully express.
    internal static class EntityQueryToSearchString
    {
        public static bool HasChunkComponents(EntityQuery query)
        {
            EntityQueryDesc[] descs;
            try
            {
                descs = query.GetEntityQueryDescs();
            }
            catch
            {
                // Disposed/invalid queries throw — treat as "no chunk components".
                return false;
            }

            if (descs == null)
                return false;

            foreach (var desc in descs)
            {
                if (ContainsChunkComponent(desc.All)) return true;
                if (ContainsChunkComponent(desc.Any)) return true;
                if (ContainsChunkComponent(desc.None)) return true;
            }
            return false;
        }

        public static string Build(EntityQuery query, World world)
        {
            var sb = new StringBuilder();

            if (world is { IsCreated: true } && !string.IsNullOrEmpty(world.Name))
            {
                sb.Append("world:\"");
                sb.Append(world.Name);
                sb.Append('"');
            }

            EntityQueryDesc[] descs;
            try
            {
                descs = query.GetEntityQueryDescs();
            }
            catch
            {
                // Disposed/invalid queries throw — return whatever world prefix we already built.
                return sb.ToString();
            }

            if (descs == null || descs.Length == 0)
                return sb.ToString();

            if (descs.Length == 1)
            {
                AppendDescTokens(sb, descs[0]);
                return sb.ToString();
            }

            // Multiple descs are OR-combined in ECS. Build each desc's tokens in
            // isolation, then wrap and OR-join so QuickSearch matches the same
            // semantics. Descs that contribute no chunk tokens are skipped.
            var perDesc = new List<string>();
            foreach (var desc in descs)
            {
                var descSb = new StringBuilder();
                AppendDescTokens(descSb, desc);
                if (descSb.Length > 0)
                    perDesc.Add(descSb.ToString());
            }
            if (perDesc.Count == 0)
                return sb.ToString();

            if (sb.Length > 0)
                sb.Append(' ');
            if (perDesc.Count == 1)
            {
                sb.Append(perDesc[0]);
            }
            else
            {
                for (var i = 0; i < perDesc.Count; i++)
                {
                    if (i > 0)
                        sb.Append(" or ");
                    sb.Append('(');
                    sb.Append(perDesc[i]);
                    sb.Append(')');
                }
            }

            return sb.ToString();
        }

        static void AppendDescTokens(StringBuilder sb, EntityQueryDesc desc)
        {
            var seenAll = new HashSet<string>();
            var seenAny = new HashSet<string>();
            var seenNone = new HashSet<string>();

            AppendChunkComponentTokens(sb, desc.All, prefix: "", seen: seenAll);
            AppendAnyChunkComponentTokens(sb, desc.Any, seenAny);
            AppendChunkComponentTokens(sb, desc.None, prefix: "-", seen: seenNone);
        }

        static bool ContainsChunkComponent(ComponentType[] types)
        {
            if (types == null)
                return false;
            for (var i = 0; i < types.Length; i++)
            {
                if (types[i].IsChunkComponent)
                    return true;
            }
            return false;
        }

        static void AppendChunkComponentTokens(StringBuilder sb, ComponentType[] types, string prefix, HashSet<string> seen)
        {
            if (types == null)
                return;
            for (var i = 0; i < types.Length; i++)
            {
                var ct = types[i];
                if (!ct.IsChunkComponent)
                    continue;
                var managed = ct.GetManagedType();
                if (managed == null)
                    continue;
                var name = managed.Name;
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(prefix);
                sb.Append("chunk:");
                sb.Append(name);
            }
        }

        // Any-list components combine as OR in ECS. Emit them as `(chunk:A or chunk:B)`
        // — parens are required so neighbouring AND tokens don't accidentally bind into
        // the OR group (QuickSearch's `or` has lower precedence than implicit AND).
        static void AppendAnyChunkComponentTokens(StringBuilder sb, ComponentType[] types, HashSet<string> seen)
        {
            if (types == null)
                return;
            var names = new List<string>();
            for (var i = 0; i < types.Length; i++)
            {
                var ct = types[i];
                if (!ct.IsChunkComponent)
                    continue;
                var managed = ct.GetManagedType();
                if (managed == null)
                    continue;
                var name = managed.Name;
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;
                names.Add(name);
            }
            if (names.Count == 0)
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            if (names.Count == 1)
            {
                sb.Append("chunk:");
                sb.Append(names[0]);
                return;
            }
            sb.Append('(');
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                    sb.Append(" or ");
                sb.Append("chunk:");
                sb.Append(names[i]);
            }
            sb.Append(')');
        }
    }
}
