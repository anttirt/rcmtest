using System;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;

namespace Unity.Entities.Editor
{
    internal class SystemSearchView : SearchViewModel
    {
        readonly SystemScheduleWindow m_SearchWindow;

        public SystemSearchView(SystemScheduleWindow window)
            : base(new SearchViewState(SearchService.CreateContext(new[] { SystemSearchProvider.CreateProvider() }, "")).LoadDefaults())
        {
            m_SearchWindow = window;
            SetWorld(window.SelectedWorld);
            context.searchView = this;
        }

        public void SetWorld(World world) => SystemSearchProvider.SetWorld(world);

        public override void SetSearchText(string searchText, TextCursorPlacement moveCursor = TextCursorPlacement.Default)
        {
            ((ISearchView)this).SetSearchText(searchText, moveCursor, 0);
        }

        public override void SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition)
        {
            if (string.Equals(context.searchText.Trim(), searchText.Trim(), StringComparison.Ordinal))
                return;
            
            context.searchText = searchText;
            
            if (string.IsNullOrEmpty(searchText))
                m_SearchWindow.StopSearch();
            else
                SearchService.Request(state.context, RefreshDone);
        }

        void RefreshDone(SearchContext c, IList<SearchItem> items) => m_SearchWindow.SetResults(items);
    }
}
