using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    // Non-editable QuickSearch chip used to represent the hierarchy's "explicit query filter"
    // mode in the search bar. The hierarchy window's underlying matching uses the EntityQuery
    // stashed on ExplicitFilterSystem (see EntityComponentFilter.SetQuery overload that takes
    // an explicit query) — this block exists only for the chip's appearance. Registering it via
    // [QueryListBlock] makes QuickSearch route the FilterId through QueryListBlock instead of
    // QueryFilterBlock, which avoids the editable TextField that QueryFilterBlock's Text format produces
    // This is a temporary solution while we add support for Entity Queries in the Search
    [QueryListBlock("Hierarchy", "", FilterId)]
    sealed class HierarchyExplicitEntityQueryBlock : QueryListBlock
    {
        internal const string FilterId = "__entities_explicit_filter__";
        // Hook for the dropdown-arrow USS rule in hierarchy.uss
        const string k_UssClass = "hierarchy-explicit-filter-block";

        public HierarchyExplicitEntityQueryBlock(IQuerySource source, string id, string value, QueryListBlockAttribute attr)
            : base(source, id, value, attr)
        {
            AddToClassList(k_UssClass);

            // QueryBlock registers PointerDown/PointerUp/ContextClick handlers in its constructor
            // that open the editor / context menu. We can't flip its internal @readonly flag from
            // out here, so swallow the events at trickle-down before its handlers see them.
            // StopImmediatePropagation (not StopPropagation) is required because the QueryBlock
            // handlers live on the same element — StopPropagation would not stop sibling handlers
            // on the same target.
            RegisterCallback<PointerDownEvent>(StopEvent, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(StopEvent, TrickleDown.TrickleDown);
            RegisterCallback<ClickEvent>(StopEvent, TrickleDown.TrickleDown);
            RegisterCallback<ContextClickEvent>(StopEvent, TrickleDown.TrickleDown);
        }

        public override IEnumerable<SearchProposition> GetPropositions(SearchPropositionFlags flags)
        {
            yield break;
        }

        static void StopEvent<T>(T evt) where T : EventBase => evt.StopImmediatePropagation();

        // Build the search-bar token that triggers this block. The value becomes the chip's
        // user-visible label (e.g. "Query #1 of MySystem"). Quoted so QuickSearch keeps multi-word
        // values together as a single filter value.
        public static string BuildSearchToken(string label) => $"{FilterId}:\"{label}\"";
    }
}
