using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    static class SystemScheduleTreeViewExtension
    {
        public static bool CheckIfTreeViewContainsGivenSystemType(this SystemTreeView @this, Type systemType, out SystemTreeViewItemData item)
        {
            if (@this.TreeViewRootItems == null || @this.TreeViewRootItems.Count == 0)
            {
                item = null;
                return false;
            }

            var systemName = systemType.Name;
            foreach (var rootItem in @this.TreeViewRootItems)
            {
                if (!(rootItem.data is SystemTreeViewItemData systemTreeViewItem))
                {
                    item = null;
                    return false;
                }

                if (CheckIfTreeViewItemContainsSystem(systemTreeViewItem, systemName, out var outItem))
                {
                    item = outItem;
                    return true;
                }
            }

            item = null;
            return false;
        }

        static bool CheckIfTreeViewItemContainsSystem(SystemTreeViewItemData item, string systemName, out SystemTreeViewItemData outItem)
        {
            var itemName = item.GetSystemName();
            itemName = Regex.Replace(itemName, @"[(].*", string.Empty);
            itemName = Regex.Replace(itemName, @"\s+", string.Empty, RegexOptions.IgnoreCase).Trim();

            if (itemName.IndexOf(systemName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                outItem = item;
                return true;
            }

            foreach (var childItem in item.children)
            {
                if (CheckIfTreeViewItemContainsSystem(childItem.data, systemName, out outItem))
                    return true;
            }

            outItem = null;
            return false;
        }
    }

    static class SystemScheduleTestUtilities
    {
        public static SystemScheduleWindow CreateSystemsWindow()
        {
            var window = ScriptableObject.CreateInstance<SystemScheduleWindow>();
            window.Show();
            window.Update();
            return window;
        }

        public static void DestroySystemsWindow(SystemScheduleWindow window)
        {
            window.Close();
            Object.DestroyImmediate(window);
        }

        public static void CollectExpandedGroupNodeNames(SystemTreeView treeView, SystemTreeViewItemData item, List<string> resultList)
        {
            if (!item.children.Any())
                return;

            var systemTreeView = treeView.Q<MultiColumnTreeView>();
            var systemTreeViewItem = item as SystemTreeViewItemData;
            var itemName = systemTreeViewItem?.GetSystemName();

            if (systemTreeView.IsExpanded(item.id))
                resultList.Add(itemName);

            foreach (var child in item.children)
            {
                CollectExpandedGroupNodeNames(treeView, child.data, resultList);
            }
        }

        public static void ExpandAllGroupNodes(SystemTreeView treeView, SystemTreeViewItemData item)
        {
            if (!item.children.Any())
                return;

            var systemTreeView = treeView.Q<MultiColumnTreeView>();
            if (!systemTreeView.IsExpanded(item.id))
                systemTreeView.ExpandItem(item.id);

            foreach (var child in item.children)
            {
                ExpandAllGroupNodes(treeView, child.data);
            }
        }

        public class UpdateSystemGraph : IEditModeTestYieldInstruction
        {
            readonly SystemScheduleWindow m_SystemScheduleWindow;
            readonly Type m_GivenSystemType;

            public UpdateSystemGraph(Type systemType)
            {
                m_SystemScheduleWindow = EditorWindow.GetWindow<SystemScheduleWindow>();
                m_GivenSystemType = systemType;
            }

            public IEnumerator Perform()
            {
                if (m_GivenSystemType == null)
                    throw new ArgumentNullException(nameof(m_GivenSystemType), $"{nameof(m_GivenSystemType)} is null.");
                
                // Force immediate synchronous update
                m_SystemScheduleWindow.ForceUpdate();

                // Wait one frame for UIElements to render
                yield return null;

                var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
                if (!systemTreeView.CheckIfTreeViewContainsGivenSystemType(m_GivenSystemType, out _))
                    throw new TimeoutException($"Expected system of type {m_GivenSystemType.Name} is not detected in system tree view after forced update.");
            }

            public bool ExpectDomainReload { get; }
            public bool ExpectedPlaymodeState { get; }
        }
    }
}
