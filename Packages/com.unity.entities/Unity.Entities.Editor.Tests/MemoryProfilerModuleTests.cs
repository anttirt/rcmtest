#if ENABLE_PROFILER
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    unsafe class MemoryProfilerModuleTests
    {
        static readonly string s_DataFilePath = Path.Combine(Application.temporaryCachePath, "profilerdata_memoryprofilertest");
        static readonly string s_RawDataFilePath = s_DataFilePath + ".raw";

        [Test]
        [Description("UUM-146419: Multiple worlds sharing an archetype should not cause duplicate components in the profiler view")]
        public void GetTreeViewData_MultipleWorldsSameArchetype_NoDuplicateComponents()
        {
            var worlds = new List<World>();
            const int worldCount = 4;

            try
            {
                int expectedComponentCount = 0;
                ulong testArchetypeHash = 0;
                for (int i = 0; i < worldCount; i++)
                {
                    var world = new World($"TestWorld{i}");
                    worlds.Add(world);
                    var archetype = world.EntityManager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
                    world.EntityManager.CreateEntity(archetype);
                    expectedComponentCount = archetype.Archetype->TypesCount;
                    testArchetypeHash = archetype.StableHash;
                }

                EntitiesProfiler.Shutdown();
                EntitiesProfiler.Initialize();

                using (new ProfilerEnableScope(s_DataFilePath, MemoryProfiler.Category))
                {
                    EntitiesProfiler.Update();
                    foreach (var world in worlds)
                        world.Update();
                    MemoryProfiler.Update();
                    EntitiesProfiler.Update();
                }

                var loaded = ProfilerDriver.LoadProfile(s_RawDataFilePath, false);
                Assert.IsTrue(loaded, "Failed to load profiler data");

                using (var frame = ProfilerDriver.GetRawFrameDataView(0, 0))
                {
                    Assert.IsTrue(frame.valid, "Frame data is not valid");

                    var treeViewData = MemoryProfilerModule.GetTreeViewData(frame).ToList();
                    var testArchetypeItems = treeViewData.Where(d => d.StableHash == testArchetypeHash).ToList();

                    Assert.That(testArchetypeItems.Count, Is.GreaterThan(0),
                        "Expected at least one entry for test archetype");

                    foreach (var item in testArchetypeItems)
                    {
                        // Without the fix, this would be expectedComponentCount * worldCount
                        Assert.That(item.ComponentTypes.Length, Is.EqualTo(expectedComponentCount),
                            $"Archetype {item.StableHash:X} in world '{item.WorldName}' has {item.ComponentTypes.Length} components, expected {expectedComponentCount}. " +
                            "Duplicate components from multiple worlds were not properly deduplicated.");
                    }
                }
            }
            finally
            {
                foreach (var world in worlds)
                    world.Dispose();

                EntitiesProfiler.Shutdown();
            }
        }

        class ProfilerEnableScope : System.IDisposable
        {
            readonly bool m_Enabled;
            readonly bool m_EnableAllocationCallstacks;
            readonly bool m_EnableBinaryLog;
            readonly string m_LogFile;
            readonly ProfilerCategory m_Category;
            readonly bool m_CategoryEnabled;

            public ProfilerEnableScope(string dataFilePath, ProfilerCategory category)
            {
                m_Enabled = Profiler.enabled;
                m_EnableAllocationCallstacks = Profiler.enableAllocationCallstacks;
                m_EnableBinaryLog = Profiler.enableBinaryLog;
                m_LogFile = Profiler.logFile;
                m_Category = category;
                m_CategoryEnabled = Profiler.IsCategoryEnabled(category);

                Profiler.logFile = dataFilePath;
                Profiler.enableBinaryLog = true;
                Profiler.enableAllocationCallstacks = false;
                Profiler.enabled = true;
                Profiler.SetCategoryEnabled(category, true);
            }

            public void Dispose()
            {
                Profiler.enabled = m_Enabled;
                Profiler.enableAllocationCallstacks = m_EnableAllocationCallstacks;
                Profiler.enableBinaryLog = m_EnableBinaryLog;
                Profiler.logFile = m_LogFile;
                Profiler.SetCategoryEnabled(m_Category, m_CategoryEnabled);
            }
        }
    }
}
#endif
