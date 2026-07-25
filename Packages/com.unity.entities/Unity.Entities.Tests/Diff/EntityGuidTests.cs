using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace Unity.Entities.Tests
{
    class EntityGuidTests
    {
        EntityId CreateTestEntityId(ulong ulongValue) => EntityId.FromULong(ulongValue);
        [Test]
        public void Ctor_StoresValuesPacked()
        {
            var g0 = new EntityGuid(CreateTestEntityId(1), EntityId.None, 2, 3);
            var g1 = new EntityGuid(CreateTestEntityId(unchecked((uint)-1)), EntityId.None, 0xF0, 0x89ABCDEF);

            Assert.That(g0.OriginatingEntityId, Is.EqualTo(CreateTestEntityId(1)));
            Assert.That(g0.FullNamespaceId, Is.EqualTo(2));
            Assert.That(g0.Serial, Is.EqualTo((uint)3));

            Assert.That(g1.OriginatingEntityId, Is.EqualTo(CreateTestEntityId(unchecked((uint)-1))));
            Assert.That(g1.FullNamespaceId, Is.EqualTo(0xF0));
            Assert.That(g1.Serial, Is.EqualTo(0x89ABCDEF));
        }

        [Test]
        public void ToString_ExtractsPackedValues()
        {
            var g0 = new EntityGuid(CreateTestEntityId(1), EntityId.None, 2, 3);
            var g1 = new EntityGuid(CreateTestEntityId(unchecked((uint)-1)), EntityId.None, 0xF0, 0x89ABCDEF);

            Assert.That(g0.ToString(), Is.EqualTo("1:0:00000002:00000003"));
            Assert.That(g1.ToString(), Is.EqualTo($"{(ulong)unchecked((uint)-1)}:0:000000f0:89abcdef"));
        }

        [Test]
        public void Comparisons()
        {
            var guids = new[]
            {
                new EntityGuid(CreateTestEntityId(1), EntityId.None, 2, 3),
                new EntityGuid(CreateTestEntityId(1), EntityId.None, 2, 3),
                new EntityGuid(CreateTestEntityId(1), EntityId.None, 2, 2),
                new EntityGuid(CreateTestEntityId(1), EntityId.None, 1, 2),
                new EntityGuid(CreateTestEntityId(2), EntityId.None, 1, 2),
                new EntityGuid(CreateTestEntityId(1), EntityId.None, 2, 3),
            };

            var range = Enumerable.Range(0, guids.Length - 1).Select(i => (a: guids[i], b: guids[i + 1])).ToList();

            var equalsOp       = range.Select(v => v.a == v.b);
            var notEqualsOp    = range.Select(v => v.a != v.b);
            var equals         = range.Select(v => v.a.Equals(v.b));
            var hashCodeEquals = range.Select(v => v.a.GetHashCode() == v.b.GetHashCode());
            var compareTo      = range.Select(v => v.a.CompareTo(v.b));

            Assert.That(equalsOp,       Is.EqualTo(new[] { true, false, false, false, false }));
            Assert.That(notEqualsOp,    Is.EqualTo(new[] { false, true, true, true, true }));
            Assert.That(equals,         Is.EqualTo(new[] { true, false, false, false, false }));
            Assert.That(hashCodeEquals, Is.EqualTo(new[] { true, false, false, false, false }));
            Assert.That(compareTo,      Is.EqualTo(new[] { 0, 1, 1, -1, 1 }));
        }

        internal static ulong CreateEntityRawId(int index, int version)
        {
            return ((ulong)version << 32) | (uint)index;
        }

        [TestCase(":2", "0:0")]
        [TestCase("12:", "0:0")]
        [TestCase("12:15:19", "0:0")]
        [TestCase("", "0:0")]
        [TestCase("bla", "0:0")]
        [TestCase("1:2")]
        [TestCase("5:9")]
        public void EntityId_Parse_StringWithColon(string entityIdStr, string expectedStr = null)
        {
            var entityId = EntityId.Parse(entityIdStr);
            expectedStr = expectedStr ?? entityIdStr;
            var generatedEntityIdStr = entityId.ToString();
            Assert.AreEqual(expectedStr, generatedEntityIdStr);
        }

        [TestCase(0, 0)]
        [TestCase(3, 7)]
        [TestCase(-3, -7)]
        public void EntityId_Parse_StringWithULong(int index, int version, string expectedStr = null)
        {
            var entityRawId = CreateEntityRawId(index, version);
            var entityId = EntityId.Parse(entityRawId.ToString());
            expectedStr = expectedStr ?? $"{index}:{version}";
            var generatedEntityIdStr = entityId.ToString();
            Assert.AreEqual(expectedStr, generatedEntityIdStr);
        }
    }
}
