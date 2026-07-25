using System;
using Unity.Collections;
using NUnit.Framework;
using System.Collections.Generic;

namespace Unity.Entities.Tests
{

    public struct Issue476Data : IComponentData
    {
        public int a;
        public int b;
    }

    class Bug476 : ECSTestsFixture
    {
        [Test]
        public void EntityArchetypeQueryMembersHaveSensibleDefaults()
        {
            ComponentType[] types = {typeof(Issue476Data)};
            var group = m_Manager.CreateEntityQuery(types);
            var temp = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();
        }
    }

    class Bug148 : ECSTestsFixture
    {
        [Test]
        public void Test1()
        {
            using World w = new World("TestWorld");
            World.DefaultGameObjectInjectionWorld = w;
            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
            List<Entity> remember = new List<Entity>();
            for (int i = 0; i < 5; i++)
            {
                remember.Add(em.CreateEntity());
            }

            var allEnt = em.GetAllEntities(Allocator.Temp);
            allEnt.Dispose();
            foreach (Entity e in remember)
            {
                Assert.IsTrue(em.Exists(e));
            }

            foreach (Entity e in remember)
            {
                em.DestroyEntity(e);
            }
        }

        [Test]
        public void Test2()
        {
            World w = new World("TestWorld");
            World.DefaultGameObjectInjectionWorld = w;
            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

            List<Entity> remember = new List<Entity>();
            for (int i = 0; i < 5; i++)
            {
                remember.Add(em.CreateEntity());
            }

            w.Dispose();
            w = null;

            w = new World("TestWorld2");
            World.DefaultGameObjectInjectionWorld = w;
            em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var allEnt = em.GetAllEntities(Allocator.Temp);
            Assert.AreEqual(0, allEnt.Length);
            allEnt.Dispose();

            foreach (Entity e in remember)
            {
                bool exists = em.Exists(e);
                Assert.IsFalse(exists);
            }

            foreach (Entity e in remember)
            {
                if (em.Exists(e))
                {
                    em.DestroyEntity(e);
                }
            }

            w.Dispose();
        }

        [Test]
        public void Entity_EqualsNullObject_ReturnsFalse()
        {
            Entity e = m_Manager.CreateEntity();
            Assert.IsFalse(e.Equals(null));
        }
    }
}
