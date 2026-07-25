using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scenes;
using UnityEngine.Pool;

namespace Unity.Entities.Editor
{
    using TreeViewItemData = UnityEngine.UIElements.TreeViewItemData<SystemTreeViewItemData>;
    class SystemTreeViewItemData
    {
        internal static readonly ObjectPool<SystemTreeViewItemData> Pool = new ObjectPool<SystemTreeViewItemData>(() => new SystemTreeViewItemData());

        public IPlayerLoopNode Node;
        public WorldProxy WorldProxy;
        public bool ShowUnityNamespaceSystems = true;
        public bool ShowPlayerLoop = true;
        SystemGraph Graph;
        readonly List<TreeViewItemData> m_CachedChildren = new List<TreeViewItemData>();
        public enum SystemToggleState
        {
            AllEnabled,
            Mixed,
            Disabled
        }

        SystemTreeViewItemData() { }

        public static SystemTreeViewItemData Acquire(SystemGraph graph, IPlayerLoopNode node, WorldProxy worldProxy, bool showUnityNamespaceSystems = true, bool showPlayerLoop = true)
        {
            var item = Pool.Get();

            item.WorldProxy = worldProxy;
            item.Graph = graph;
            item.Node = node;
            item.ShowUnityNamespaceSystems = showUnityNamespaceSystems;
            item.ShowPlayerLoop = showPlayerLoop;

            return item;
        }

        public SystemProxy SystemProxy
        {
            get
            {
                if (Node is ISystemHandleNode systemHandleNode)
                    return systemHandleNode.SystemProxy;

                return default;
            }
        }

        public bool HasChildren => Node?.Children.Count > 0;

        public string GetSystemName() => Node?.Name;

        public string GetWorldName()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.World.Name;

            return string.Empty;
        }

        public IReadOnlyList<SystemProxy> GetUpdateBeforeSystemNames()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.UpdateBeforeSet;
            return Array.Empty<SystemProxy>();
        }

        public IReadOnlyList<SystemProxy> GetUpdateAfterSystemNames()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.UpdateAfterSet;
            return Array.Empty<SystemProxy>();
        }

        public IReadOnlyList<SystemProxy> GetUpdateBeforeReverseSystemNames()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.UpdateBeforeReverseSet;
            return Array.Empty<SystemProxy>();
        }

        public IReadOnlyList<SystemProxy> GetUpdateAfterReverseSystemNames()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.UpdateAfterReverseSet;
            return Array.Empty<SystemProxy>();
        }

        public string GetNamespace()
        {
            if (Node is ISystemHandleNode _)
                return SystemProxy.Namespace;

            return string.Empty;
        }

        public bool GetParentState()
        {
            return Node.EnabledInHierarchy;
        }

        public void SetSystemEnabled(bool value)
        {
            if (Node.Enabled == value)
                return;

            Node.Enabled = value;
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        public SystemToggleState GetSystemToggleState()
        {
            // Root group nodes, ini, update, prelate update.
            if (!SystemProxy.Valid)
                return HasMixedState() ? SystemToggleState.Mixed : SystemToggleState.AllEnabled;

            // No children
            if (!Node.Children.Any())
                return SystemProxy.Enabled ? SystemToggleState.AllEnabled : SystemToggleState.Disabled;

            // With children
            // current node is disabled.
            if (!SystemProxy.Enabled)
                return SystemToggleState.Disabled;

            // any children is in mixed state.
            if (HasMixedState())
               return SystemToggleState.Mixed;

            return SystemToggleState.AllEnabled;
        }

        bool HasMixedState()
        {
            return children.Select(child => child.data).Any(childItem =>
            {
                var toggleState = childItem?.GetSystemToggleState();
                return toggleState == SystemToggleState.Mixed || toggleState == SystemToggleState.Disabled;
            });
        }

        public string GetEntityMatches()
        {
            if (HasChildren) // Group system do not need entity matches.
                return string.Empty;

            if (!SystemProxy.Valid)
                return string.Empty;

            if (!Node.Enabled || !NodeParentsAllEnabled(Node))
            {
                return Constants.SystemSchedule.k_Dash;
            }

            return SystemProxy.TotalEntityMatches.ToString();
        }

        float GetAverageRunningTime(SystemProxy systemProxy)
        {
            return Graph.GetAverageRunningTime(systemProxy);
        }

        public string GetRunningTime(bool morePrecision)
        {
            if (Node == null)
                return Constants.SystemSchedule.k_Dash;

            if (Node is IPlayerLoopSystemData)
                return string.Empty;

            // if the node is disabled, not running, or if its parents are all disabled
            // TODO this isn't really great; we should read the raw profile data, if something is taking up time we should know it regardless of enabled etc state
            if (!Node.IsRunning || !Node.Enabled || !NodeParentsAllEnabled(Node))
                return Constants.SystemSchedule.k_Dash;

            // if the node is a group node, it's just its own time (it has its own profiler marker)
            if (Node is ComponentGroupNode groupNode)
            {
                return AdjustPrecision(GetAverageRunningTime(groupNode.SystemProxy));
            }

            // if it has any children, it's the sum of all of its children that are SystemHandleNodes
            if (children.Any())
            {
                var sum = Node.Children
                    .OfType<ISystemHandleNode>()
                    .Sum(child => GetAverageRunningTime(child.SystemProxy));

                return AdjustPrecision(sum);
            }

            // if it's not a system handle at this point, we can't show anything useful
            if (Node is ISystemHandleNode sysNode)
            {
                return AdjustPrecision(sysNode.SystemProxy.RunTimeMillisecondsForDisplay);
            }

            return Constants.SystemSchedule.k_Dash;

            string AdjustPrecision(float sum)
            {
                return morePrecision ? sum.ToString("f4") : sum.ToString("f2");
            }
        }

        bool NodeParentsAllEnabled(IPlayerLoopNode node)
        {
            if (node.Parent != null)
            {
                if (!node.Parent.Enabled) return false;
                if (!NodeParentsAllEnabled(node.Parent)) return false;
            }

            return true;
        }

        public int id => Node.Hash;
        public IEnumerable<TreeViewItemData> children => m_CachedChildren;

        public void PopulateChildren()
        {
            m_CachedChildren.Clear();

            foreach (var child in Node.Children)
            {
                if (SystemTreeView.ShouldShowNode(child, WorldProxy, ShowUnityNamespaceSystems, ShowPlayerLoop))
                {
                    var item = Acquire(Graph, child, WorldProxy, ShowUnityNamespaceSystems, ShowPlayerLoop);
                    m_CachedChildren.Add(new TreeViewItemData(item.id, item));
                }
            }
        }

        public void Release()
        {
            WorldProxy = null;
            Graph = null;
            Node = null;
            ShowUnityNamespaceSystems = true;
            ShowPlayerLoop = true;
            foreach (var child in m_CachedChildren.OfType<TreeViewItemData>())
            {
                child.data.Release();
            }

            m_CachedChildren.Clear();

            Pool.Release(this);
        }
    }
}
