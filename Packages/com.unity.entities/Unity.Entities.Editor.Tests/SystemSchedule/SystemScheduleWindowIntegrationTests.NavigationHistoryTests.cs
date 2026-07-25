using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemScheduleWindowIntegrationTests
    {
        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_InitialState_HistoryEmpty_ButtonsDisabled()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            Assert.That(m_SystemScheduleWindow.m_NavigationHistory, Is.Empty);
            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(-1));
            Assert.That(m_SystemScheduleWindow.m_BackButton.enabledSelf, Is.False);
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_SelectingSystem_PushesOntoHistory()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);

            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(1));
            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(0));
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory[0].Equals(proxy1), Is.True);
            // Back disabled when at index 0; forward disabled because nothing ahead.
            Assert.That(m_SystemScheduleWindow.m_BackButton.enabledSelf, Is.False);
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_SelectingTwoSystems_EnablesBackButton()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(2));
            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(1));
            Assert.That(m_SystemScheduleWindow.m_BackButton.enabledSelf, Is.True);
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_NavigateBack_MovesIndexAndEnablesForward()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            m_SystemScheduleWindow.NavigateHistory(-1);

            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(0));
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(2));
            Assert.That(m_SystemScheduleWindow.m_BackButton.enabledSelf, Is.False);
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_NavigateForward_RestoresIndex()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            m_SystemScheduleWindow.NavigateHistory(-1);
            m_SystemScheduleWindow.NavigateHistory(+1);

            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(1));
            Assert.That(m_SystemScheduleWindow.m_BackButton.enabledSelf, Is.True);
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_NavigateBackDoesNotRePushOnUpdateSelectedSystem()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            m_SystemScheduleWindow.NavigateHistory(-1);

            // History size unchanged; back navigation must not duplicate the entry.
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_SelectAfterBack_TruncatesForwardBranch()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            var proxyGroup = new SystemProxy(m_TestSystemGroup, m_WorldProxy);

            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            m_SystemScheduleWindow.NavigateHistory(-1); // back to proxy1
            m_SystemScheduleWindow.UpdateSelectedSystem(proxyGroup); // new branch

            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(2));
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory[0].Equals(proxy1), Is.True);
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory[1].Equals(proxyGroup), Is.True);
            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(1));
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_CapAtCapacity_DropsOldestEntry()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);

            // Push capacity+1 distinct selections by alternating between two systems.
            for (var i = 0; i < SystemScheduleWindow.k_NavigationHistoryCapacity + 1; i++)
                m_SystemScheduleWindow.UpdateSelectedSystem(i % 2 == 0 ? proxy1 : proxy2);

            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(SystemScheduleWindow.k_NavigationHistoryCapacity));
            // First push was proxy1, so after dropping one from the front the oldest should now be proxy2.
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory[0].Equals(proxy2), Is.True);
            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(SystemScheduleWindow.k_NavigationHistoryCapacity - 1));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_NavigateBackAtStart_NoOp()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);

            m_SystemScheduleWindow.NavigateHistory(-1);

            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(0));
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_NavigateForwardAtEnd_NoOp()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            m_SystemScheduleWindow.NavigateHistory(+1);

            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(1));
            Assert.That(m_SystemScheduleWindow.m_NavigationHistory.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_ClearNavigationHistory_ResetsState()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var proxy1 = new SystemProxy(m_TestSystem1, m_WorldProxy);
            var proxy2 = new SystemProxy(m_TestSystem2, m_WorldProxy);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            m_SystemScheduleWindow.ClearNavigationHistory();

            Assert.That(m_SystemScheduleWindow.m_NavigationHistory, Is.Empty);
            Assert.That(m_SystemScheduleWindow.m_NavigationIndex, Is.EqualTo(-1));
            Assert.That(m_SystemScheduleWindow.m_BackButton.enabledSelf, Is.False);
            Assert.That(m_SystemScheduleWindow.m_ForwardButton.enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_NavigationHistory_NavigateBack_FiresSelectionChangedForTargetSystem()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestSystem1), out var system1Item), Is.True);
            Assert.That(systemTreeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestSystem2), out var system2Item), Is.True);

            var proxy1 = system1Item.SystemProxy;
            var proxy2 = system2Item.SystemProxy;
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy1);
            m_SystemScheduleWindow.UpdateSelectedSystem(proxy2);

            SystemProxy lastSelected = default;
            void OnSelected(SystemProxy p) => lastSelected = p;
            systemTreeView.systemSelectionChanged += OnSelected;
            try
            {
                m_SystemScheduleWindow.NavigateHistory(-1);
            }
            finally
            {
                systemTreeView.systemSelectionChanged -= OnSelected;
            }

            // Navigation must route through the tree view's selection change so dependency arrows and
            // the inspector update — proxy1.Equals checks against the non-null side first.
            Assert.That(proxy1.Equals(lastSelected), Is.True);
        }
    }
}
