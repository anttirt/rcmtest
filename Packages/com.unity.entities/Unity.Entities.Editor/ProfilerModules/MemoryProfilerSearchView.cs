using System;
using UnityEditor.Search;

namespace Unity.Entities.Editor
{
    class MemoryProfilerSearchView : SearchViewModel
    {
        readonly MemoryProfilerModule.MemoryProfilerModuleView m_View;

        public MemoryProfilerSearchView(MemoryProfilerModule.MemoryProfilerModuleView view)
            : base(new SearchViewState(SearchService.CreateContext(new[] { ArchetypeSearchProvider.CreateProvider() }, "")).LoadDefaults())
        {
            m_View = view;
            context.searchView = this;
        }

        public override void Dispose()
        {
            context?.Dispose();
            base.Dispose();
        }

        public override void SetSearchText(string searchText, TextCursorPlacement moveCursor = TextCursorPlacement.Default)
        {
            ((ISearchView)this).SetSearchText(searchText, moveCursor, 0);
        }

        public override void SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition)
        {
            context.searchText = searchText ?? "";
            m_View.OnSearchTextChanged(searchText);
        }
    }
}
