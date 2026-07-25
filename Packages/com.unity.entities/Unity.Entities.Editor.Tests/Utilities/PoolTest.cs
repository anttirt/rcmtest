using System;
using NUnit.Framework;
using System.Collections.Generic;

namespace Unity.Entities.Editor.Tests
{
    class PoolTest
    {
        [Test]
        public void PoolReset()
        {
            var item = Pool<PoolableItem>.GetPooled();

            var itemResetCount = item.ResetCount;
            var itemReturnedToPoolCount = item.ReturnedToPoolCount;

            item.ReturnToPool();

            Assert.That(item.ResetCount, Is.EqualTo(itemResetCount + 1));
            Assert.That(item.ReturnedToPoolCount, Is.EqualTo(itemReturnedToPoolCount + 1));
        }

        class PoolableItem : IPoolable
        {
            public int ResetCount { get; private set; }
            public int ReturnedToPoolCount{ get; private set; }


            public void Reset() => ResetCount++;

            public void ReturnToPool()
            {
                ReturnedToPoolCount++;
                Pool<PoolableItem>.Release(this);
            }
        }
    }
}
