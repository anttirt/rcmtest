#if UNITY_INCLUDE_INSTRUMENTATION && !DISABLE_ENTITIES_JOURNALING
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Unity.Entities.Serialization;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Export journaling data as CSV.
        /// </summary>
        /// <returns>An enumerable of strings, one per CSV data row.</returns>
        public static IEnumerable<string> ExportToCSV()
        {
            if (!s_Initialized)
                yield break;

            yield return $"{nameof(RecordView.Index)},{nameof(RecordView.RecordType)},{nameof(RecordView.FrameIndex)},{nameof(RecordView.World)},{nameof(RecordView.ExecutingSystem)},{nameof(RecordView.OriginSystem)},{nameof(RecordView.Entities)},{nameof(RecordView.ComponentTypes)},{nameof(RecordView.Data)}";

            var records = GetRecords(Ordering.Ascending);
            var sb = new StringBuilder();
            foreach (var record in records)
            {
                var world = record.World.Name.ToCSV();
                var executingSystem = record.ExecutingSystem.Name.ToCSV();
                var originSystem = record.OriginSystem.Name.ToCSV();
                var sortedEntities = record.Entities.Select(e => e.Name).OrderBy(e => e);
                var entities = string.Join(";", sortedEntities).ToCSV();
                var sortedComponentTypes = record.ComponentTypes.Select(t => t.Name).OrderBy(c => c);
                var componentTypes = string.Join(";", sortedComponentTypes).ToCSV();
                var data = string.Empty;
                switch (record.RecordType)
                {
                    case RecordType.SystemAdded:
                    case RecordType.SystemRemoved:
                        if (TryGetRecordDataAsSystemView(record, out var systemView))
                        {
                            data = systemView.Name.ToCSV();
                        }
                        break;

                    case RecordType.SetComponentData:
                    case RecordType.SetSharedComponentData:
                    case RecordType.GetComponentDataRW:
                        if (TryGetRecordDataAsComponentDataArrayBoxed(record, out var componentDataArray))
                        {
                            sb.Clear();
                            var node = JsonSerializer.SerializeToNode(componentDataArray, componentDataArray.GetType(), EntitiesJsonOptions.Default);
                            SimplifiedJsonWriter.Write(node, sb);
                            data = sb.ToString().ToCSV();
                        }
                        break;
                }
                yield return string.Join(",", record.Index, record.RecordType, record.FrameIndex, world, executingSystem, originSystem, entities, componentTypes, data);
            }
        }

        static string ToCSV(this string value)
        {
            var result = value.Replace('\"', '\'');
            return result.Contains(' ') || result.Contains(',') ? result.DoubleQuote() : result;
        }

        static string DoubleQuote(this string value)
        {
            return "\"" + value + "\"";
        }

        // Reproduces the byte-for-byte shape that Unity.Serialization.Json's `Simplified=true, Minified=true`
        // produced, which the journaling CSV fixtures depend on:
        //   - object members written as `key=value` (key unquoted unless it has special chars)
        //   - object/array members separated by a single space (not comma)
        //   - no surrounding whitespace
        static class SimplifiedJsonWriter
        {
            public static void Write(JsonNode node, StringBuilder sb)
            {
                if (node == null)
                {
                    sb.Append("null");
                    return;
                }

                switch (node)
                {
                    case JsonObject obj:
                        sb.Append('{');
                        var firstO = true;
                        foreach (var kvp in obj)
                        {
                            if (!firstO) sb.Append(' ');
                            firstO = false;
                            AppendKey(sb, kvp.Key);
                            sb.Append('=');
                            Write(kvp.Value, sb);
                        }
                        sb.Append('}');
                        break;

                    case JsonArray arr:
                        sb.Append('[');
                        var firstA = true;
                        foreach (var item in arr)
                        {
                            if (!firstA) sb.Append(' ');
                            firstA = false;
                            Write(item, sb);
                        }
                        sb.Append(']');
                        break;

                    default:
                        // JsonValue: numbers/bools/null inline; strings come out double-quoted and the
                        // ToCSV() post-processor converts those quotes to single quotes (matching the old
                        // FixedString JsonAdapter behaviour).
                        sb.Append(node.ToJsonString());
                        break;
                }
            }

            static void AppendKey(StringBuilder sb, string key)
            {
                if (IsSafeIdentifier(key))
                    sb.Append(key);
                else
                    sb.Append('"').Append(key).Append('"');
            }

            static bool IsSafeIdentifier(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return false;
                if (!char.IsLetter(s[0]) && s[0] != '_')
                    return false;
                for (var i = 1; i < s.Length; i++)
                {
                    var c = s[i];
                    if (!char.IsLetterOrDigit(c) && c != '_')
                        return false;
                }
                return true;
            }
        }
    }
}
#endif
