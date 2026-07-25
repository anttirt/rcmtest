using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.UIElements;
using Unity.Editor.Bridge;
using Unity.Collections;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

using Unity.Profiling.Editor;

namespace Unity.Entities.Editor
{
    [ProfilerModuleMetadata("Entities Memory", IconPath = "Profiler.Memory")]
    [Serializable]
    partial class MemoryProfilerModule : ProfilerModule
    {
        class MemoryProfilerViewController : ProfilerModuleViewController
        {
            readonly MemoryProfilerModuleView m_View;
            long m_FrameIndex = -1;

            public bool IsRecording => ProfilerWindowBridge.IsRecording(ProfilerWindow);

            public MemoryProfilerViewController(ProfilerWindow profilerWindow) :
                base(profilerWindow)
            {
                m_View = new MemoryProfilerModuleView();
                m_View.SearchFinished = () => Update();
                ProfilerWindow.SelectedFrameIndexChanged += OnSelectedFrameIndexChanged;
            }

            protected override VisualElement CreateView()
            {
                Analytics.SendEditorEvent(Analytics.Window.Profiler, Analytics.EventType.ProfilerModuleCreate, Analytics.MemoryProfilerModuleName);
                return m_View.Create();
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing)
                    return;

                ProfilerWindow.SelectedFrameIndexChanged -= OnSelectedFrameIndexChanged;
                m_View.Dispose();
                base.Dispose(disposing);
            }

            void OnSelectedFrameIndexChanged(long index)
            {
                m_FrameIndex = index;
                if (IsRecording)
                    return;

                var archetypes = new List<MemoryProfilerTreeViewItemData>();
                var allocatorData = new Dictionary<ulong, MemoryProfiler.WorldAllocatorData>();
                foreach (var frame in GetFrames(index))
                {
                    foreach (var data in GetTreeViewData(frame))
                        archetypes.Add(data);
                    foreach (var data in GetWorldAllocatorData(frame))
                        allocatorData[data.WorldSequenceNumber] = data;
                }
                m_View.ArchetypesDataSource = archetypes.ToArray();
                m_View.WorldAllocatorDataSource = allocatorData;

                m_View.Search();
            }

            public void Update()
            {
                if (m_FrameIndex == -1 || IsRecording || !m_View.HasArchetypesDataSource)
                    m_View.Clear(IsRecording ? s_DisplayingFrameDataDisabled : s_NoFrameDataAvailable);
                else
                    m_View.Rebuild();
            }
        }

        static ProfilerCounterDescriptor[] ProfilerCounters = new[]
        {
            new ProfilerCounterDescriptor(k_AllocatedMemoryCounterName, k_CategoryName),
            new ProfilerCounterDescriptor(k_UnusedMemoryCounterName, k_CategoryName)
        };

        public MemoryProfilerModule() :
            base(ProfilerCounters, ProfilerModuleChartType.Line, new[] { k_CategoryName })
        {
        }

        public override ProfilerModuleViewController CreateDetailsViewController() => new MemoryProfilerViewController(ProfilerWindow);
    }

    partial class MemoryProfilerModule
    {
        static readonly string s_NoFrameDataAvailable = L10n.Tr("No frame data available. Select a frame from the charts above to see its details here.");
        static readonly string s_DisplayingFrameDataDisabled = L10n.Tr("Displaying of frame data disabled while recording. To see the data, pause recording.");

        internal static IEnumerable<MemoryProfilerTreeViewItemData> GetTreeViewData(RawFrameDataView frame)
        {
            var worldsData = GetDistinctSessionMetaDataAsDictionary<WorldData, ulong>(frame, EntitiesProfiler.Guid, (int)DataTag.WorldData, x => x.SequenceNumber);
            var archetypesData = GetDistinctSessionMetaDataAsDictionary<ArchetypeData, ulong>(frame, EntitiesProfiler.Guid, (int)DataTag.ArchetypeData, x => x.StableHash);

            var componentsSet = new HashSet<ArchetypeComponentData>();
            GetDistinctSessionMetaData(frame, EntitiesProfiler.Guid, (int)DataTag.ArchetypeComponentData, componentsSet);
            var archetypeComponentsData = new NativeArray<ArchetypeComponentData>(componentsSet.Count, Allocator.Temp);
            var componentIndex = 0;
            foreach (var component in componentsSet)
                archetypeComponentsData[componentIndex++] = component;

            foreach (var archetypeMemoryData in GetFrameMetaData<ArchetypeMemoryData>(frame, MemoryProfiler.Guid, 0))
            {
                if (worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData) &&
                    archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData))
                {
                    yield return new MemoryProfilerTreeViewItemData(worldData.Name, archetypeMemoryData.WorldSequenceNumber, archetypeData, archetypeMemoryData, archetypeComponentsData);
                }
            }

            archetypeComponentsData.Dispose();
        }

        static IEnumerable<MemoryProfiler.WorldAllocatorData> GetWorldAllocatorData(RawFrameDataView frame)
        {
            return GetFrameMetaData<MemoryProfiler.WorldAllocatorData>(frame, MemoryProfiler.Guid, 1);
        }

        static IEnumerable<T> GetSessionMetaData<T>(RawFrameDataView frame, Guid guid, int tag) where T : unmanaged
        {
            var metaDataCount = frame.GetSessionMetaDataCount(guid, tag);
            for (var metaDataIter = 0; metaDataIter < metaDataCount; ++metaDataIter)
            {
                var metaDataArray = frame.GetSessionMetaData<T>(guid, tag, metaDataIter);
                for (var i = 0; i < metaDataArray.Length; ++i)
                    yield return metaDataArray[i];
            }
        }

        static IEnumerable<T> GetFrameMetaData<T>(RawFrameDataView frame, Guid guid, int tag) where T : unmanaged
        {
            var metaDataCount = frame.GetFrameMetaDataCount(guid, tag);
            for (var metaDataIter = 0; metaDataIter < metaDataCount; ++metaDataIter)
            {
                var metaDataArray = frame.GetFrameMetaData<T>(guid, tag, metaDataIter);
                for (var i = 0; i < metaDataArray.Length; ++i)
                    yield return metaDataArray[i];
            }
        }

        static Dictionary<TKey, T> GetDistinctSessionMetaDataAsDictionary<T, TKey>(RawFrameDataView frame, Guid guid, int tag, Func<T, TKey> keySelector) where T : unmanaged
        {
            var result = new Dictionary<TKey, T>();
            foreach (var item in GetSessionMetaData<T>(frame, guid, tag))
            {
                var key = keySelector(item);
                if (!result.ContainsKey(key))
                    result[key] = item;
            }
            return result;
        }

        static void GetDistinctSessionMetaData<T>(RawFrameDataView frame, Guid guid, int tag, HashSet<T> result) where T : unmanaged
        {
            result.Clear();
            foreach (var item in GetSessionMetaData<T>(frame, guid, tag))
                result.Add(item);
        }

        static IEnumerable<RawFrameDataView> GetFrames(long index)
        {
            for (var threadIndex = 0; ; ++threadIndex)
            {
                var frame = ProfilerDriver.GetRawFrameDataView((int)index, threadIndex);
                if (!frame.valid)
                    yield break;

                if (frame.GetFrameMetaDataCount(MemoryProfiler.Guid, 0) > 0)
                    yield return frame;
            }
        }
    }
}
