#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Unity.Entities.Tests.Fuzzer
{
    [TestFixture]
    class EntityDifferPatcherFuzzTests
    {
        static IEnumerable<uint> FuzzTestingSeeds()
        {
            for (uint i = 0; i < 1; i++)
                yield return i;
        }

#if false // set to true to add fuzz testing
        [Test, Explicit]
        public void EntityDifferPatcherFuzzer([ValueSource(nameof(FuzzTestingSeeds))]
            uint seed)
        {
            using (var fuzzer = new DifferPatcherFuzzer())
            {
                var commands = fuzzer.GenerateCommands(DifferPatcherFuzzer.WeightedGenerators, seed, 200, 20);
                fuzzer.Run(commands, str => Console.WriteLine(str.ToCSharpString()));
            }
        }

        [Test, Explicit]
        public void ReduceTestCase()
        {
            int lastIndex = TestCases.Length - 1;
            var commands = Fuzzer.ParseLog(DifferPatcherFuzzer.Generators, TestCases[lastIndex].Item2.Split('\n')).ToList();
            var simplified = Fuzzer.Reduce(() => new DifferPatcherFuzzer(), commands);

            Debug.Log($"Simplified from {commands.Count} to {simplified.Count} commands:\n" + string.Join("\n", simplified.Select(c => c.ToCSharpString())));
            using (var fuzzer = new DifferPatcherFuzzer())
            {
                fuzzer.Run(simplified, str => Console.WriteLine(str.ToCSharpString()));
            }
        }
#endif

        [Test, TestCaseSource(typeof(EntityDifferPatcherFuzzTests), nameof(FuzzTestCases))]
        public void EntityDifferPatcherTests(IEnumerable<Fuzzer.CommandData<DifferPatcherFuzzer>> commands)
        {
            using (var fuzzer = new DifferPatcherFuzzer())
            {
                fuzzer.Run(commands, str => Console.WriteLine(str.ToCSharpString()));
            }
        }

        public static IEnumerable FuzzTestCases
        {
            get
            {
                foreach (var entry in TestCases)
                {
                    var commands = Fuzzer.ParseLog(DifferPatcherFuzzer.Generators, entry.Data.Split('\n'));
                    yield return new TestCaseData(commands).SetName(entry.Name);
                }
            }
        }

        private static readonly (string Name, string Data)[] TestCases =
        {
            ("ShuffleLinkedEntityGroups", @"
CreateEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 3}}}
CreateLinkedEntityGroup_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 3}}}
CreateEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 7}}}
RemoveLinkedEntityGroup_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 3}}}
CreateLinkedEntityGroup_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 3}}}
AddToLinkedEntityGroup_|_{""ToAdd"":{""_OriginatingEntityId"":{""m_rawData"": 7}},""AddTo"":{""_OriginatingEntityId"":{""m_rawData"": 3}}}
Validate_|_
RemoveFromLinkedEntityGroup_|_{""ToRemove"":{""_OriginatingEntityId"":{""m_rawData"": 7}},""RemoveFrom"":{""_OriginatingEntityId"":{""m_rawData"": 3}}}
Validate_|_"),
            ("AddAndRemoveFromLinkedEntityGroup_ThenDestroy", @"
CreateEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 1}}}
DestroyEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 1}}}
CreateEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 27}}}
CreateEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 28}}}
CreateEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
CreateLinkedEntityGroup_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
AddToLinkedEntityGroup_|_{""ToAdd"":{""_OriginatingEntityId"":{""m_rawData"": 27}},""AddTo"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
AddToLinkedEntityGroup_|_{""ToAdd"":{""_OriginatingEntityId"":{""m_rawData"": 28}},""AddTo"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
RemoveFromLinkedEntityGroup_|_{""ToRemove"":{""_OriginatingEntityId"":{""m_rawData"": 27}},""RemoveFrom"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
RemoveLinkedEntityGroup_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
DestroyEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 30}}}
DestroyEntity_|_{""Guid"":{""_OriginatingEntityId"":{""m_rawData"": 28}}}")
        };
    }
}
#endif
