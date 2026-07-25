using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemScheduleWindowIntegrationTests
    {
        [UnityTest]
        public IEnumerator SystemScheduleWindow_SchedulingColumn_SelectSystem1_ShowsUpdateBeforeDependencies()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            // Verify the WorldProxy has the dependency data populated.
            var worldProxy = m_SystemScheduleWindow.WorldProxyManager.GetWorldProxyForGivenWorld(m_DefaultWorld);
            Assert.That(worldProxy, Is.Not.Null);

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestSystem1), out var system1Item), Is.True);

            // System1 has [UpdateBefore(System2)]
            var updateBeforeSystems = system1Item.GetUpdateBeforeSystemNames();
            Assert.That(updateBeforeSystems.Count, Is.GreaterThan(0), "System1 should have UpdateBefore dependencies");
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SchedulingColumn_SelectSystem2_ShowsUpdateAfterDependencies()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestSystem2), out var system2Item), Is.True);

            // System2 has [UpdateAfter(System1)]
            var updateAfterSystems = system2Item.GetUpdateAfterSystemNames();
            Assert.That(updateAfterSystems.Count, Is.GreaterThan(0), "System2 should have UpdateAfter dependencies");
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SchedulingColumn_ClearSelection_HidesAllPills()
        {
            m_SystemScheduleWindow.ForceUpdate();

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestSystem1), out var system1Item), Is.True);

            // Select System1 first to populate pills.
            systemTreeView.MultiColumnTreeViewElement.SetSelectionById(system1Item.id);
            yield return null;

            // Clear selection.
            systemTreeView.MultiColumnTreeViewElement.ClearSelection();
            yield return null;

            // After clearing, no scheduling pills should be visible.
            var allPills = systemTreeView.Query<Label>(className: "scheduling-pill").ToList();
            foreach (var pill in allPills)
            {
                Assert.That(pill.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    $"Pill '{pill.name}' should be hidden after clearing selection, but text is '{pill.text}'");
            }
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SchedulingColumn_NoDependencies_NoPills()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var groupItem), Is.True);

            var updateBefore = groupItem.GetUpdateBeforeSystemNames();
            var updateAfter = groupItem.GetUpdateAfterSystemNames();
            Assert.That(updateBefore.Count, Is.EqualTo(0), "Group system should have no UpdateBefore dependencies");
            Assert.That(updateAfter.Count, Is.EqualTo(0), "Group system should have no UpdateAfter dependencies");
        }
    }
}
