using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Properties;
using UnityEngine;

namespace Unity.Entities.Editor.Tests.Serialization
{
    [TestFixture]
    class EntitiesJsonTests
    {
        [Test]
        public void DefaultOptions_RoundTripsPrimitivesAndCamelCaseInsensitive()
        {
            var original = new SimplePoco
            {
                Name = "world",
                Count = 3,
                Ratio = 1.5f,
            };

            var json = EntitiesJson.Serialize(original);
            StringAssert.Contains("name", json);
            StringAssert.DoesNotContain("Name", json);

            var restored = EntitiesJson.Deserialize<PascalNamedPoco>(json);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Name, Is.EqualTo("world"));
            Assert.That(restored.Count, Is.EqualTo(3));
            Assert.That(restored.Ratio, Is.EqualTo(1.5f));
        }

        [Test]
        public void DefaultOptions_RoundTripsNestedPocoAndCollections()
        {
            var original = new HierarchyStateLike
            {
                Nodes = new Dictionary<string, HierarchyNodesStateLike>
                {
                    ["default"] = new HierarchyNodesStateLike { ExpandedKeys = new List<int> { 1, 2, 3 } },
                },
            };

            var json = EntitiesJson.Serialize(original);
            var restored = EntitiesJson.Deserialize<HierarchyStateLike>(json);

            Assert.That(restored.Nodes, Is.Not.Null);
            Assert.That(restored.Nodes.Count, Is.EqualTo(1));
            Assert.That(restored.Nodes["default"].ExpandedKeys, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void DefaultOptions_RoundTripsUiPersistentStateShape()
        {
            var original = new UiPersistentStateLike
            {
                FoldoutState = new Dictionary<int, bool> { { 42, true }, { 7, false } },
                PaginationState = new Dictionary<int, PaginationDataLike>
                {
                    [10] = new PaginationDataLike { PaginationSize = 25, CurrentPage = 2 },
                },
            };

            var json = EntitiesJson.Serialize(original);
            var restored = EntitiesJson.Deserialize<UiPersistentStateLike>(json);

            Assert.That(restored.FoldoutState[42], Is.True);
            Assert.That(restored.FoldoutState[7], Is.False);
            Assert.That(restored.PaginationState[10].PaginationSize, Is.EqualTo(25));
            Assert.That(restored.PaginationState[10].CurrentPage, Is.EqualTo(2));
        }

        [Test]
        public void Converters_RoundTripFixedStringVariants()
        {
            var original = new FixedStringPoco
            {
                S32 = new FixedString32Bytes("thirty-two"),
                S64 = new FixedString64Bytes("sixty-four"),
                S128 = new FixedString128Bytes("one-twenty-eight"),
                S512 = new FixedString512Bytes("five-twelve"),
                S4096 = new FixedString4096Bytes("fourk"),
            };

            var restored = EntitiesJson.Deserialize<FixedStringPoco>(EntitiesJson.Serialize(original));

            Assert.That(restored.S32.ToString(), Is.EqualTo("thirty-two"));
            Assert.That(restored.S64.ToString(), Is.EqualTo("sixty-four"));
            Assert.That(restored.S128.ToString(), Is.EqualTo("one-twenty-eight"));
            Assert.That(restored.S512.ToString(), Is.EqualTo("five-twelve"));
            Assert.That(restored.S4096.ToString(), Is.EqualTo("fourk"));
        }

        [Test]
        public void Converters_RoundTripHash128_AsLowerHexString()
        {
            var original = new Hash128(0x01020304, 0x05060708, 0x090a0b0c, 0x0d0e0f10);
            var restored = EntitiesJson.Deserialize<Hash128>(EntitiesJson.Serialize(original));
            Assert.That(restored, Is.EqualTo(original));
            StringAssert.IsMatch("^[0-9a-f]{32}$", original.ToString());
        }

        [Test]
        public void Converters_RoundTripEntityGuid_MatchesToStringContract()
        {
            var originating = EntityId.FromULong(0x07E251050000000cUL);
            var sub = EntityId.FromULong(0x07E251050000000dUL);
            var original = new EntityGuid(originating, sub, 0xaabbccdd, 0x000000ff);

            var json = EntitiesJson.Serialize(original);
            var restored = EntitiesJson.Deserialize<EntityGuid>(json);

            Assert.That(restored, Is.EqualTo(original));
            Assert.That(json, Does.Contain(original.ToString()));
        }

        [Test]
        public void Default_IncludesPublicFields()
        {
            Assert.That(EntitiesJsonOptions.Default.IncludeFields, Is.True);
        }

        [Test]
        public void Default_RoundTripsPublicFields()
        {
            var original = new FieldOnlyClass { Counter = 42 };
            var json = EntitiesJson.Serialize(original);
            var restored = EntitiesJson.Deserialize<FieldOnlyClass>(json);
            Assert.That(restored.Counter, Is.EqualTo(42));
        }

        [Test]
        public void Serialize_WithRuntimeType_UsesRegisteredConverters()
        {
            object boxed = new Hash128(1, 2, 3, 4);
            var json = EntitiesJson.Serialize(boxed, typeof(Hash128));
            var restored = (Hash128)EntitiesJson.Deserialize(json, typeof(Hash128));
            Assert.That(restored, Is.EqualTo((Hash128)boxed));
        }

        class SimplePoco
        {
            [CreateProperty] public string Name { get; set; }
            [CreateProperty] public int Count { get; set; }
            [CreateProperty] public float Ratio { get; set; }
        }

        class PascalNamedPoco
        {
            [CreateProperty] public string Name { get; set; }
            [CreateProperty] public int Count { get; set; }
            [CreateProperty] public float Ratio { get; set; }
        }

        class HierarchyStateLike
        {
            [CreateProperty] public Dictionary<string, HierarchyNodesStateLike> Nodes { get; set; }
        }

        class HierarchyNodesStateLike
        {
            [CreateProperty] public List<int> ExpandedKeys { get; set; }
        }

        class UiPersistentStateLike
        {
            [CreateProperty] public Dictionary<int, bool> FoldoutState { get; set; }
            [CreateProperty] public Dictionary<int, PaginationDataLike> PaginationState { get; set; }
        }

        struct PaginationDataLike
        {
            [CreateProperty] public int PaginationSize { get; set; }
            [CreateProperty] public int CurrentPage { get; set; }
        }

        class FixedStringPoco
        {
            [CreateProperty] public FixedString32Bytes S32 { get; set; }
            [CreateProperty] public FixedString64Bytes S64 { get; set; }
            [CreateProperty] public FixedString128Bytes S128 { get; set; }
            [CreateProperty] public FixedString512Bytes S512 { get; set; }
            [CreateProperty] public FixedString4096Bytes S4096 { get; set; }
        }

        class FieldOnlyClass
        {
            public int Counter;
        }

        class DontSerializeOnPublic
        {
            public int Kept = 1;
            [DontSerialize] public int Skipped = 2;
            [CreateProperty, DontSerialize] public int SkippedProp { get; set; } = 3;
        }

        class CreatePropertyOnPrivate
        {
            [Unity.Properties.CreateProperty] int m_Backing = 7;
            public int Read => m_Backing;
            public void Set(int v) => m_Backing = v;
        }

        [Test]
        public void Default_RemovesDontSerializeMembers()
        {
            var original = new DontSerializeOnPublic { Kept = 11, Skipped = 22, SkippedProp = 33 };
            var json = EntitiesJson.Serialize(original);

            StringAssert.Contains("kept", json);
            StringAssert.DoesNotContain("skipped", json);
            StringAssert.DoesNotContain("\"22\"", json);
            StringAssert.DoesNotContain("\"33\"", json);

            var restored = EntitiesJson.Deserialize<DontSerializeOnPublic>(json);
            Assert.That(restored.Kept, Is.EqualTo(11));
            Assert.That(restored.Skipped, Is.EqualTo(2));
            Assert.That(restored.SkippedProp, Is.EqualTo(3));
        }

        [Test]
        public void Default_IncludesPrivateCreatePropertyMembers()
        {
            var original = new CreatePropertyOnPrivate();
            original.Set(42);

            var json = EntitiesJson.Serialize(original);
            var restored = EntitiesJson.Deserialize<CreatePropertyOnPrivate>(json);

            Assert.That(restored.Read, Is.EqualTo(42));
        }

        class UntaggedPropertyClass
        {
            // System.Text.Json would auto-include these public properties; Unity.Properties' walker would not.
            // The contract here mirrors Unity.Properties, so untagged public properties are dropped — which is
            // what prevents ContentProvider subclasses (e.g. ComponentContentProvider.ComponentType) from
            // tripping over System.Type/UnityEngine.Object during serialization.
            public System.Type SomeType { get; set; }
            public int Counter { get; set; }
        }

        [Test]
        public void Default_DropsUntaggedPublicProperties()
        {
            var original = new UntaggedPropertyClass { SomeType = typeof(int), Counter = 5 };

            var json = EntitiesJson.Serialize(original);
            StringAssert.DoesNotContain("someType", json);
            StringAssert.DoesNotContain("counter", json);
        }

        class ObjectFieldHolder
        {
            public UnityEngine.Object Reference;
        }

        [Test]
        public void Converters_UnityObjectField_SerializesAsGlobalObjectIdString()
        {
            // ScriptableObject is the cheapest concrete UnityEngine.Object we can newup in a test.
            var instance = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                var json = EntitiesJson.Serialize(new ObjectFieldHolder { Reference = instance });
                // GlobalObjectId.ToString() always starts with "GlobalObjectId_".
                StringAssert.Contains("GlobalObjectId_", json);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Converters_UnityObjectNull_RoundTripsAsNull()
        {
            var original = new ObjectFieldHolder { Reference = null };
            var restored = EntitiesJson.Deserialize<ObjectFieldHolder>(EntitiesJson.Serialize(original));
            Assert.That(restored.Reference, Is.Null);
        }

        [Test]
        public void Converters_UnityObjectField_DestroyedReferenceSerializesAsNull()
        {
            var instance = ScriptableObject.CreateInstance<ScriptableObject>();
            UnityEngine.Object.DestroyImmediate(instance);
            // After DestroyImmediate, `instance` is "fake null" — reference != null but the implicit bool is false.
            var json = EntitiesJson.Serialize(new ObjectFieldHolder { Reference = instance });
            StringAssert.Contains("null", json);
        }
    }
}
