#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Properties;

namespace Unity.Entities.Tests
{
    [TestFixture]
    [IgnoreTest_IL2CPP("DOTSE-1903 - Uses Properties which is broken in non-generic sharing IL2CPP builds")]
    public class UniqueReferenceExcludeAdapterTests
    {
        sealed class TestRemapVisitor : PropertyVisitor, IVisitPropertyAdapter<Entity>
        {
            public readonly Dictionary<Entity, Entity> Remap = new();
            public readonly UniqueReferenceExcludeAdapter UniqueRefs = new();

            public TestRemapVisitor()
            {
                AddAdapter(UniqueRefs);
                AddAdapter(this);
            }

            void IVisitPropertyAdapter<Entity>.Visit<TContainer>(in VisitContext<TContainer, Entity> context, ref TContainer container, ref Entity value)
            {
                if (Remap.TryGetValue(value, out var remapped))
                    value = remapped;
            }

            public void Run(ref object root)
            {
                UniqueRefs.PrepareForNewRootVisit();
                PropertyContainer.Accept(this, ref root);
            }
        }

        sealed class Node
        {
            public Entity Entity;
            public Node Left;
            public Node Right;
        }

        sealed class DiamondRoot
        {
            public Node B;
            public Node A;
            public Node C;
        }

        [Test]
        public void Diamond_RemapPropagatesToAllSharedReferences()
        {
            // - instance "B"
            //     - instance "A"
            // - instance "A"
            // - instance "C"
            //     - instance "A"
            //     - instance "B"
            //         - instance "A"
            //
            // The adapter visits each unique reference exactly once. That is intentional: the
            // typed adapter mutates the shared instance in place, so any one path is enough to
            // propagate the remap to every reference path. This test verifies that property.

            var src = new Entity { Index = 1, Version = 1 };
            var dst = new Entity { Index = 99, Version = 1 };

            var a = new Node { Entity = src };
            var b = new Node { Entity = src, Left = a };
            var c = new Node { Entity = src, Left = a, Right = b };

            var root = new DiamondRoot { B = b, A = a, C = c };

            var visitor = new TestRemapVisitor();
            visitor.Remap[src] = dst;

            object boxed = root;
            visitor.Run(ref boxed);

            Assert.That(a.Entity, Is.EqualTo(dst), "Shared instance A's Entity should be remapped via the path the visitor took");
            Assert.That(b.Entity, Is.EqualTo(dst), "Shared instance B's Entity should be remapped");
            Assert.That(c.Entity, Is.EqualTo(dst), "Instance C's Entity should be remapped");
        }

        sealed class Cycle
        {
            public Entity Entity;
            public Cycle Self;
        }

        [Test]
        public void SelfCycle_DoesNotInfinitelyRecurseAndStillRemaps()
        {
            var src = new Entity { Index = 7, Version = 3 };
            var dst = new Entity { Index = 700, Version = 3 };

            var node = new Cycle { Entity = src };
            node.Self = node;

            var visitor = new TestRemapVisitor();
            visitor.Remap[src] = dst;

            object boxed = node;
            Assert.DoesNotThrow(() => visitor.Run(ref boxed));
            Assert.That(node.Entity, Is.EqualTo(dst));
        }

        sealed class CollidingEquals
        {
            public Entity Entity;
            public override bool Equals(object obj) => obj is CollidingEquals;
            public override int GetHashCode() => 0;
        }

        sealed class CollidingRoot
        {
            public CollidingEquals X;
            public CollidingEquals Y;
        }

        [Test]
        public void DistinctInstancesWithCollidingEquals_AreBothVisited()
        {
            var srcX = new Entity { Index = 1, Version = 1 };
            var srcY = new Entity { Index = 2, Version = 1 };
            var dstX = new Entity { Index = 10, Version = 1 };
            var dstY = new Entity { Index = 20, Version = 1 };

            var root = new CollidingRoot
            {
                X = new CollidingEquals { Entity = srcX },
                Y = new CollidingEquals { Entity = srcY },
            };

            var visitor = new TestRemapVisitor();
            visitor.Remap[srcX] = dstX;
            visitor.Remap[srcY] = dstY;

            object boxed = root;
            visitor.Run(ref boxed);

            Assert.That(root.X.Entity, Is.EqualTo(dstX));
            Assert.That(root.Y.Entity, Is.EqualTo(dstY));
        }
    }
}
#endif
