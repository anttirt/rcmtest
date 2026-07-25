#if ENABLE_PROFILER && UNITY_EDITOR
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

namespace Unity.Entities.Tests
{
    // Additional test components for large archetype tests
    struct TestComponent1 : IComponentData { public int value; }
    struct TestComponent2 : IComponentData { public int value; }
    struct TestComponent3 : IComponentData { public int value; }
    struct TestComponent4 : IComponentData { public int value; }
    struct TestComponent5 : IComponentData { public int value; }
    struct TestComponent6 : IComponentData { public int value; }
    struct TestComponent7 : IComponentData { public int value; }
    struct TestComponent8 : IComponentData { public int value; }
    struct TestComponent9 : IComponentData { public int value; }
    struct TestComponent10 : IComponentData { public int value; }
    struct TestComponent11 : IComponentData { public int value; }
    struct TestComponent12 : IComponentData { public int value; }
    struct TestComponent13 : IComponentData { public int value; }
    struct TestComponent14 : IComponentData { public int value; }
    struct TestComponent15 : IComponentData { public int value; }
    struct TestComponent16 : IComponentData { public int value; }
    struct TestComponent17 : IComponentData { public int value; }
    struct TestComponent18 : IComponentData { public int value; }
    struct TestComponent19 : IComponentData { public int value; }
    struct TestComponent20 : IComponentData { public int value; }
    struct TestComponent21 : IComponentData { public int value; }
    struct TestComponent22 : IComponentData { public int value; }
    struct TestComponent23 : IComponentData { public int value; }
    struct TestComponent24 : IComponentData { public int value; }
    struct TestComponent25 : IComponentData { public int value; }
    struct TestComponent26 : IComponentData { public int value; }
    struct TestComponent27 : IComponentData { public int value; }
    struct TestComponent28 : IComponentData { public int value; }
    struct TestComponent29 : IComponentData { public int value; }
    struct TestComponent30 : IComponentData { public int value; }
    struct TestComponent31 : IComponentData { public int value; }
    struct TestComponent32 : IComponentData { public int value; }
    struct TestComponent33 : IComponentData { public int value; }
    struct TestComponent34 : IComponentData { public int value; }
    struct TestComponent35 : IComponentData { public int value; }
    struct TestComponent36 : IComponentData { public int value; }
    struct TestComponent37 : IComponentData { public int value; }
    struct TestComponent38 : IComponentData { public int value; }
    struct TestComponent39 : IComponentData { public int value; }
    struct TestComponent40 : IComponentData { public int value; }
    struct TestComponent41 : IComponentData { public int value; }
    struct TestComponent42 : IComponentData { public int value; }
    struct TestComponent43 : IComponentData { public int value; }
    struct TestComponent44 : IComponentData { public int value; }
    struct TestComponent45 : IComponentData { public int value; }
    struct TestComponent46 : IComponentData { public int value; }
    struct TestComponent47 : IComponentData { public int value; }
    struct TestComponent48 : IComponentData { public int value; }
    struct TestComponent49 : IComponentData { public int value; }
    struct TestComponent50 : IComponentData { public int value; }
    struct TestComponent51 : IComponentData { public int value; }
    struct TestComponent52 : IComponentData { public int value; }
    struct TestComponent53 : IComponentData { public int value; }
    struct TestComponent54 : IComponentData { public int value; }
    struct TestComponent55 : IComponentData { public int value; }
    struct TestComponent56 : IComponentData { public int value; }
    struct TestComponent57 : IComponentData { public int value; }
    struct TestComponent58 : IComponentData { public int value; }
    struct TestComponent59 : IComponentData { public int value; }
    struct TestComponent60 : IComponentData { public int value; }
    struct TestComponent61 : IComponentData { public int value; }
    struct TestComponent62 : IComponentData { public int value; }
    struct TestComponent63 : IComponentData { public int value; }
    struct TestComponent64 : IComponentData { public int value; }
    struct TestComponent65 : IComponentData { public int value; }
    struct TestComponent66 : IComponentData { public int value; }
    struct TestComponent67 : IComponentData { public int value; }
    struct TestComponent68 : IComponentData { public int value; }
    struct TestComponent69 : IComponentData { public int value; }
    struct TestComponent70 : IComponentData { public int value; }
    struct TestComponent71 : IComponentData { public int value; }
    struct TestComponent72 : IComponentData { public int value; }
    struct TestComponent73 : IComponentData { public int value; }
    struct TestComponent74 : IComponentData { public int value; }
    struct TestComponent75 : IComponentData { public int value; }
    struct TestComponent76 : IComponentData { public int value; }
    struct TestComponent77 : IComponentData { public int value; }
    struct TestComponent78 : IComponentData { public int value; }
    struct TestComponent79 : IComponentData { public int value; }
    struct TestComponent80 : IComponentData { public int value; }
    struct TestComponent81 : IComponentData { public int value; }
    struct TestComponent82 : IComponentData { public int value; }
    struct TestComponent83 : IComponentData { public int value; }
    struct TestComponent84 : IComponentData { public int value; }
    struct TestComponent85 : IComponentData { public int value; }
    struct TestComponent86 : IComponentData { public int value; }
    struct TestComponent87 : IComponentData { public int value; }
    struct TestComponent88 : IComponentData { public int value; }
    struct TestComponent89 : IComponentData { public int value; }
    struct TestComponent90 : IComponentData { public int value; }
    struct TestComponent91 : IComponentData { public int value; }
    struct TestComponent92 : IComponentData { public int value; }
    struct TestComponent93 : IComponentData { public int value; }
    struct TestComponent94 : IComponentData { public int value; }
    struct TestComponent95 : IComponentData { public int value; }
    struct TestComponent96 : IComponentData { public int value; }
    struct TestComponent97 : IComponentData { public int value; }
    struct TestComponent98 : IComponentData { public int value; }
    struct TestComponent99 : IComponentData { public int value; }
    struct TestComponent100 : IComponentData { public int value; }
    struct TestComponent101 : IComponentData { public int value; }
    struct TestComponent102 : IComponentData { public int value; }
    struct TestComponent103 : IComponentData { public int value; }
    struct TestComponent104 : IComponentData { public int value; }
    struct TestComponent105 : IComponentData { public int value; }
    struct TestComponent106 : IComponentData { public int value; }
    struct TestComponent107 : IComponentData { public int value; }
    struct TestComponent108 : IComponentData { public int value; }
    struct TestComponent109 : IComponentData { public int value; }
    struct TestComponent110 : IComponentData { public int value; }

    [TestFixture]
    unsafe class MemoryProfilerTests : ECSTestsFixture
    {
        static readonly string s_DataFilePath = Path.Combine(Application.temporaryCachePath, "profilerdata");
        static readonly string s_RawDataFilePath = s_DataFilePath + ".raw";
        static bool s_LastCategoryEnabled;

        class ProfilerEnableScope : IDisposable
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

        public override void Setup()
        {
            base.Setup();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));
        }

        RawFrameDataView GenerateFrameMetaData(Action onUpdate = null)
        {
            EntitiesProfiler.Shutdown();
            EntitiesProfiler.Initialize();

            using (var scope = new ProfilerEnableScope(s_DataFilePath, MemoryProfiler.Category))
            {
                EntitiesProfiler.Update();
                onUpdate?.Invoke();
                World.Update();
                EntitiesProfiler.Update();
            }

            var loaded = ProfilerDriver.LoadProfile(s_RawDataFilePath, false);
            Assert.IsTrue(loaded);
            Assert.AreNotEqual(-1, ProfilerDriver.lastFrameIndex);

            return ProfilerDriver.GetRawFrameDataView(0, 0);
        }

        static IReadOnlyDictionary<ulong, WorldData> GetWorldsData(RawFrameDataView frame) =>
            GetSessionMetaData<WorldData>(frame, EntitiesProfiler.Guid, (int)DataTag.WorldData).Distinct().ToDictionary(x => x.SequenceNumber, x => x);

        static IReadOnlyDictionary<ulong, ArchetypeData> GetArchetypesData(RawFrameDataView frame) =>
            GetSessionMetaData<ArchetypeData>(frame, EntitiesProfiler.Guid, (int)DataTag.ArchetypeData).Distinct().ToDictionary(x => x.StableHash, x => x);

        static IReadOnlyList<ArchetypeComponentData> GetArchetypeComponentsData(RawFrameDataView frame) =>
            GetSessionMetaData<ArchetypeComponentData>(frame, EntitiesProfiler.Guid, (int)DataTag.ArchetypeComponentData).ToList();

        static IEnumerable<ArchetypeMemoryData> GetArchetypeMemoryData(RawFrameDataView frame) =>
            GetFrameMetaData<ArchetypeMemoryData>(frame, MemoryProfiler.Guid, 0);

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

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            s_LastCategoryEnabled = Profiler.IsCategoryEnabled(MemoryProfiler.Category);
            Profiler.SetCategoryEnabled(MemoryProfiler.Category, true);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Profiler.SetCategoryEnabled(MemoryProfiler.Category, s_LastCategoryEnabled);
        }

        [Test]
        public void Uninitialized_DoesNotThrow()
        {
            MemoryProfiler.Shutdown();
            Assert.DoesNotThrow(() => MemoryProfiler.Update());
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4))]
#if !UNITY_WEBGL
        [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
        public void ArchetypeOnly(params Type[] types)
        {
            var archetype = default(EntityArchetype);
            var componentTypes = types.Select(t => new ComponentType(t)).ToArray();
            using (var frame = GenerateFrameMetaData(() => { archetype = m_Manager.CreateArchetype(componentTypes); }))
            {
                var worldsData = GetWorldsData(frame);
                var archetypesData = GetArchetypesData(frame);
                var archetypeComponentsData = GetArchetypeComponentsData(frame);
                var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

                Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

                Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
                Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                Assert.That(archetypeData.ChunkCapacity, Is.EqualTo(archetype.ChunkCapacity));
                Assert.That(archetypeData.InstanceSize, Is.EqualTo(archetype.Archetype->InstanceSize));

                var componentDataForArchetype = archetypeComponentsData.Where(x => x.ArchetypeStableHash == archetype.StableHash).OrderBy(x => x.IndexInArchetype).ToList();
                Assert.That(componentDataForArchetype.Count, Is.EqualTo(archetype.Archetype->TypesCount));
                Assert.That(archetypeData.ComponentTypeCount, Is.EqualTo(archetype.Archetype->TypesCount));
                for (var i = 0; i < componentDataForArchetype.Count; ++i)
                {
                    var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var componentTypeData = componentDataForArchetype[i];
                    Assert.That(componentTypeData.ComponentStableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                    Assert.AreEqual(componentTypeData.Flags.HasFlag(ComponentTypeFlags.ChunkComponent), TypeManager.IsChunkComponent(typeIndex) ? true : false);
                }

                var allocatedBytes = archetype.ChunkCount * Chunk.kChunkSize;
                var unusedEntityCount = (archetype.ChunkCount * archetype.ChunkCapacity) - archetype.Archetype->EntityCount;
                var unusedBytes = unusedEntityCount * archetype.Archetype->InstanceSize;

                Assert.That(archetypeMemoryData.CalculateAllocatedBytes(), Is.EqualTo(allocatedBytes));
                Assert.That(archetypeMemoryData.CalculateUnusedBytes(archetypeData), Is.EqualTo(unusedBytes));
                Assert.That(archetypeMemoryData.EntityCount, Is.EqualTo(archetype.Archetype->EntityCount));
                Assert.That(archetypeMemoryData.CalculateUnusedEntityCount(archetypeData), Is.EqualTo(unusedEntityCount));
                Assert.That(archetypeMemoryData.ChunkCount, Is.EqualTo(archetype.ChunkCount));
                Assert.That(archetypeMemoryData.SegmentCount, Is.EqualTo(0));
            }
        }

        [TestCase(10, typeof(EcsTestData))]
        [TestCase(100, typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(1000, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(10000, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4))]
#if !UNITY_WEBGL
        [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
        public void ArchetypeWithEntities(int entityCount, params Type[] types)
        {
            var archetype = default(EntityArchetype);
            var componentTypes = types.Select(t => new ComponentType(t)).ToArray();
            using (var frame = GenerateFrameMetaData(() =>
            {
                archetype = m_Manager.CreateArchetype(componentTypes);
                m_Manager.CreateEntity(archetype, entityCount);
            }))
            {
                var worldsData = GetWorldsData(frame);
                var archetypesData = GetArchetypesData(frame);
                var archetypeComponentsData = GetArchetypeComponentsData(frame);
                var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

                Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

                Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
                Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                Assert.That(archetypeData.ChunkCapacity, Is.EqualTo(archetype.ChunkCapacity));
                Assert.That(archetypeData.InstanceSize, Is.EqualTo(archetype.Archetype->InstanceSize));

                var componentDataForArchetype = archetypeComponentsData.Where(x => x.ArchetypeStableHash == archetype.StableHash).OrderBy(x => x.IndexInArchetype).ToList();
                Assert.That(componentDataForArchetype.Count, Is.EqualTo(archetype.Archetype->TypesCount));
                Assert.That(archetypeData.ComponentTypeCount, Is.EqualTo(archetype.Archetype->TypesCount));
                for (var i = 0; i < componentDataForArchetype.Count; ++i)
                {
                    var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var componentTypeData = componentDataForArchetype[i];
                    Assert.That(componentTypeData.ComponentStableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                    Assert.AreEqual(componentTypeData.Flags.HasFlag(ComponentTypeFlags.ChunkComponent), TypeManager.IsChunkComponent(typeIndex) ? true : false);
                }

                var allocatedBytes = archetype.ChunkCount * Chunk.kChunkSize;
                var unusedEntityCount = (archetype.ChunkCount * archetype.ChunkCapacity) - archetype.Archetype->EntityCount;
                var unusedBytes = unusedEntityCount * archetype.Archetype->InstanceSize;

                Assert.That(archetypeMemoryData.CalculateAllocatedBytes(), Is.EqualTo(allocatedBytes));
                Assert.That(archetypeMemoryData.CalculateUnusedBytes(archetypeData), Is.EqualTo(unusedBytes));
                Assert.That(archetypeMemoryData.EntityCount, Is.EqualTo(archetype.Archetype->EntityCount));
                Assert.That(archetypeMemoryData.CalculateUnusedEntityCount(archetypeData), Is.EqualTo(unusedEntityCount));
                Assert.That(archetypeMemoryData.ChunkCount, Is.EqualTo(archetype.ChunkCount));
                Assert.That(archetypeMemoryData.SegmentCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void ArchetypeWithManyComponents_ExceedsPreviousLimit()
        {
            // Previously, archetypes were limited to 111 components. This test validates
            // that we can now create archetypes with more components and properly track them.
            var extendedTypes = new List<Type>
            {
                // Existing test components
                typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4), typeof(EcsTestData5),
                typeof(EcsTestData6), typeof(EcsTestData7), typeof(EcsTestData8), typeof(EcsTestData9), typeof(EcsTestData10),
                typeof(EcsTestData11), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestDataEnableable3),
                typeof(EcsTestDataEnableable4), typeof(EcsTestDataEnableable5), typeof(EcsTestFloatData), typeof(EcsTestFloatData2),
                typeof(EcsTestFloatData3), typeof(EcsTestDataEntity), typeof(EcsTestDataEntity2), typeof(EcsTestTag),
                typeof(EcsTestTagEnableable), typeof(EcsTestTagEnableable2), typeof(EcsTestEmptyEnableable1), typeof(EcsTestEmptyEnableable2),
                typeof(EcsTestDataBlobAssetRef), typeof(EcsTestDataBlobAssetRef2), typeof(EcsIntElement), typeof(EcsIntElement2),
                typeof(EcsIntElement3), typeof(EcsIntElement4), typeof(EcsComplexEntityRefElement), typeof(EcsTestComponentWithBool),
                typeof(EcsTestContainerData), typeof(EcsTestContainerElement), typeof(EcsTestDataBlobAssetElement),
                typeof(EcsTestDataBlobAssetElement2), typeof(EcsIntElementEnableable), typeof(EcsIntElementEnableable2),
                typeof(EcsIntElementEnableable3), typeof(EcsIntElementEnableable4),
                // Additional test components to exceed 111 limit
                typeof(TestComponent1), typeof(TestComponent2), typeof(TestComponent3), typeof(TestComponent4), typeof(TestComponent5),
                typeof(TestComponent6), typeof(TestComponent7), typeof(TestComponent8), typeof(TestComponent9), typeof(TestComponent10),
                typeof(TestComponent11), typeof(TestComponent12), typeof(TestComponent13), typeof(TestComponent14), typeof(TestComponent15),
                typeof(TestComponent16), typeof(TestComponent17), typeof(TestComponent18), typeof(TestComponent19), typeof(TestComponent20),
                typeof(TestComponent21), typeof(TestComponent22), typeof(TestComponent23), typeof(TestComponent24), typeof(TestComponent25),
                typeof(TestComponent26), typeof(TestComponent27), typeof(TestComponent28), typeof(TestComponent29), typeof(TestComponent30),
                typeof(TestComponent31), typeof(TestComponent32), typeof(TestComponent33), typeof(TestComponent34), typeof(TestComponent35),
                typeof(TestComponent36), typeof(TestComponent37), typeof(TestComponent38), typeof(TestComponent39), typeof(TestComponent40),
                typeof(TestComponent41), typeof(TestComponent42), typeof(TestComponent43), typeof(TestComponent44), typeof(TestComponent45),
                typeof(TestComponent46), typeof(TestComponent47), typeof(TestComponent48), typeof(TestComponent49), typeof(TestComponent50),
                typeof(TestComponent51), typeof(TestComponent52), typeof(TestComponent53), typeof(TestComponent54), typeof(TestComponent55),
                typeof(TestComponent56), typeof(TestComponent57), typeof(TestComponent58), typeof(TestComponent59), typeof(TestComponent60),
                typeof(TestComponent61), typeof(TestComponent62), typeof(TestComponent63), typeof(TestComponent64), typeof(TestComponent65),
                typeof(TestComponent66), typeof(TestComponent67), typeof(TestComponent68), typeof(TestComponent69), typeof(TestComponent70),
                typeof(TestComponent71), typeof(TestComponent72), typeof(TestComponent73), typeof(TestComponent74), typeof(TestComponent75),
                typeof(TestComponent76), typeof(TestComponent77), typeof(TestComponent78), typeof(TestComponent79), typeof(TestComponent80),
                typeof(TestComponent81), typeof(TestComponent82), typeof(TestComponent83), typeof(TestComponent84), typeof(TestComponent85),
                typeof(TestComponent86), typeof(TestComponent87), typeof(TestComponent88), typeof(TestComponent89), typeof(TestComponent90),
                typeof(TestComponent91), typeof(TestComponent92), typeof(TestComponent93), typeof(TestComponent94), typeof(TestComponent95),
                typeof(TestComponent96), typeof(TestComponent97), typeof(TestComponent98), typeof(TestComponent99), typeof(TestComponent100),
                typeof(TestComponent101), typeof(TestComponent102), typeof(TestComponent103), typeof(TestComponent104), typeof(TestComponent105),
                typeof(TestComponent106), typeof(TestComponent107), typeof(TestComponent108), typeof(TestComponent109), typeof(TestComponent110)
            };

            var archetype = default(EntityArchetype);
            var componentTypeArray = extendedTypes.Select(t => new ComponentType(t)).ToArray();
            using var frame = GenerateFrameMetaData(() => { archetype = m_Manager.CreateArchetype(componentTypeArray); });
            
            var worldsData = GetWorldsData(frame);
            var archetypesData = GetArchetypesData(frame);
            var archetypeComponentsData = GetArchetypeComponentsData(frame);
            var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

            Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
            Assert.That(worldData.Name, Is.EqualTo(World.Name));
            Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

            Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
            Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                
            Assume.That(archetype.Archetype->TypesCount, Is.GreaterThan(111), "Test must create an archetype with more than 111 components to validate the fix");

            // The key validation: ensure all components are tracked
            var componentDataForArchetype = archetypeComponentsData.Where(x => x.ArchetypeStableHash == archetype.StableHash).OrderBy(x => x.IndexInArchetype).ToList();
            Assert.That(componentDataForArchetype.Count, Is.EqualTo(archetype.Archetype->TypesCount), $"Expected {archetype.Archetype->TypesCount} components to be tracked, but found {componentDataForArchetype.Count}");
            Assert.That(archetypeData.ComponentTypeCount, Is.EqualTo(archetype.Archetype->TypesCount));

            // Validate each component is correctly recorded
            for (var i = 0; i < componentDataForArchetype.Count; ++i)
            {
                var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                var componentTypeData = componentDataForArchetype[i];
                Assert.That(componentTypeData.ComponentStableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                Assert.That(componentTypeData.IndexInArchetype, Is.EqualTo(i));
            }
        }

        [Test]
        [Description("UUM-146419: ArchetypeComponentData equality allows deduplication of shared archetypes across worlds")]
        public void ArchetypeComponentData_Equality_IdenticalValuesAreEqual()
        {
            var a = new ArchetypeComponentData(123, 456, ComponentTypeFlags.None, 0);
            var b = new ArchetypeComponentData(123, 456, ComponentTypeFlags.None, 0);
            var c = new ArchetypeComponentData(999, 456, ComponentTypeFlags.None, 0);

            Assert.That(a.Equals(b), Is.True, "Identical ArchetypeComponentData should be equal");
            Assert.That(a.Equals(c), Is.False, "Different ArchetypeComponentData should not be equal");
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()), "Equal objects must have equal hash codes");
            Assert.That(a == b, Is.True, "Operator == should return true for equal values");
            Assert.That(a != c, Is.True, "Operator != should return true for different values");

            var set = new HashSet<ArchetypeComponentData> { a, b, c };
            Assert.That(set.Count, Is.EqualTo(2), "HashSet should deduplicate equal ArchetypeComponentData");
        }

        [Test]
        public void ArchetypeWithSharedComponentValues()
        {
            var archetype = default(EntityArchetype);
            using (var frame = GenerateFrameMetaData(() =>
            {
                archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

                var entity1 = m_Manager.CreateEntity(archetype);
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp { value = 42 });
                #pragma warning restore 0618

                var entity2 = m_Manager.CreateEntity(archetype);
                #pragma warning disable 0618 // managed API obsolete; internal/test caller still needs it.
                m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp { value = 24 });
                #pragma warning restore 0618
            }))
            {
                var worldsData = GetWorldsData(frame);
                var archetypesData = GetArchetypesData(frame);
                var archetypeComponentsData = GetArchetypeComponentsData(frame);
                var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

                Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

                Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
                Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                Assert.That(archetypeData.ChunkCapacity, Is.EqualTo(archetype.ChunkCapacity));
                Assert.That(archetypeData.InstanceSize, Is.EqualTo(archetype.Archetype->InstanceSize));

                var componentDataForArchetype = archetypeComponentsData.Where(x => x.ArchetypeStableHash == archetype.StableHash).OrderBy(x => x.IndexInArchetype).ToList();
                Assert.That(componentDataForArchetype.Count, Is.EqualTo(archetype.Archetype->TypesCount));
                Assert.That(archetypeData.ComponentTypeCount, Is.EqualTo(archetype.Archetype->TypesCount));
                for (var i = 0; i < componentDataForArchetype.Count; ++i)
                {
                    var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var componentTypeData = componentDataForArchetype[i];
                    Assert.That(componentTypeData.ComponentStableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                    Assert.AreEqual(componentTypeData.Flags.HasFlag(ComponentTypeFlags.ChunkComponent), TypeManager.IsChunkComponent(typeIndex) ? true : false);
                }

                var allocatedBytes = archetype.ChunkCount * Chunk.kChunkSize;
                var unusedEntityCount = (archetype.ChunkCount * archetype.ChunkCapacity) - archetype.Archetype->EntityCount;
                var unusedBytes = unusedEntityCount * archetype.Archetype->InstanceSize;

                Assert.That(archetypeMemoryData.CalculateAllocatedBytes(), Is.EqualTo(allocatedBytes));
                Assert.That(archetypeMemoryData.CalculateUnusedBytes(archetypeData), Is.EqualTo(unusedBytes));
                Assert.That(archetypeMemoryData.EntityCount, Is.EqualTo(archetype.Archetype->EntityCount));
                Assert.That(archetypeMemoryData.CalculateUnusedEntityCount(archetypeData), Is.EqualTo(unusedEntityCount));
                Assert.That(archetypeMemoryData.ChunkCount, Is.EqualTo(archetype.ChunkCount));

                // Disabled for now
                //Assert.That(archetypeMemoryData.SegmentCount, Is.EqualTo(2));
            }
        }
    }
}
#endif
