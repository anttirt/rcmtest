using NUnit.Framework;

namespace Unity.Entities.Tests
{
    public class WorldUnmanagedTests : ECSTestsCommonBase
    {
        [Test]
        public void WorldUnmanaged_SameWorld_Equals_True()
        {
            using var world = new World("TestWorld");
            var unmanagedOne = world.Unmanaged;
            var unmanagedTwo = unmanagedOne;
            
            Assert.That(unmanagedOne.Equals(unmanagedTwo), Is.True);
            Assert.That(unmanagedOne == unmanagedTwo, Is.True);
            Assert.That(unmanagedOne != unmanagedTwo, Is.False);
            
            // Boxed testing
            Assert.That(unmanagedOne.Equals((object)unmanagedTwo), Is.True);
        }
        
        [Test]
        public void WorldUnmanaged_DifferentWorld_Equals_False()
        {
            using var worldOne = new World("TestWorld_1");
            using var worldTwo = new World("TestWorld_2");
            
            var unmanagedOne = worldOne.Unmanaged;
            var unmanagedTwo = worldTwo.Unmanaged;
            
            Assert.That(unmanagedOne.Equals(unmanagedTwo), Is.False);
            Assert.That(unmanagedOne == unmanagedTwo, Is.False);
            Assert.That(unmanagedOne != unmanagedTwo, Is.True);
            
            // Boxed testing
            Assert.That(unmanagedOne.Equals((object)unmanagedTwo), Is.False);
        }

        [Test]
        public void WorldUnmanaged_SameWorld_SameHashCode()
        {
            using var world = new World("TestWorld");
            var unmanagedOne = world.Unmanaged;
            var unmanagedTwo = unmanagedOne;
            
            Assert.That(unmanagedOne.GetHashCode(), Is.EqualTo(unmanagedTwo.GetHashCode()));
        }
        
        [Test]
        public void WorldUnmanaged_DifferentWorld_DifferentHashCode()
        {
            using var worldOne = new World("TestWorld_1");
            using var worldTwo = new World("TestWorld_2");
            
            var unmanagedOne = worldOne.Unmanaged;
            var unmanagedTwo = worldTwo.Unmanaged;
            
            Assert.That(unmanagedOne.GetHashCode(), Is.Not.EqualTo(unmanagedTwo.GetHashCode()));
        }        
    }
}
