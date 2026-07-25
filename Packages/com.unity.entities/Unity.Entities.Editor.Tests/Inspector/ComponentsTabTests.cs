using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.UI;
using Unity.Properties;
using Unity.Transforms;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class ComponentsTabTests
    {
        World m_World;
        VisualElement m_Root;
        Entity m_InspectedEntity;
        ComponentsTab m_Tab;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("ComponentsTabTestsWorld");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            m_Root = new VisualElement();
            var propertyElement = new PropertyElement();
            var inspectorCtx = new EntityInspectorContext();
            m_InspectedEntity = m_World.EntityManager.CreateEntity();
            inspectorCtx.SetContext(EntitySelectionProxy.CreateInstance(m_World, m_InspectedEntity), true);
            m_Tab = new ComponentsTab(inspectorCtx);
            m_Tab.OnTabVisibilityChanged(true);
            propertyElement.SetTarget(m_Tab);
            m_Root.Add(propertyElement);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_World.EntityManager.Exists(m_InspectedEntity))
            {
                m_World.EntityManager.DestroyEntity(m_InspectedEntity);
            }
        }

        [Test]
        public void ComponentsTab_Comparer()
        {
            var componentTypes = new TestProperty[]
            {
                new TestProperty(nameof(XComponent)),
                new TestProperty(nameof(BComponent)),
                new TestProperty(nameof(YComponent)),
                new TestProperty(nameof(LocalTransform)),
                new TestProperty(nameof(DComponent)),
                new TestProperty(nameof(LocalToWorld)),
            };

            Array.Sort(componentTypes, EntityInspectorComponentsComparer.Instance);

            Assert.That(componentTypes.Select(p => p.DisplayName).ToArray(), Is.EquivalentTo(new[]
            {
                nameof(LocalTransform),
                nameof(LocalToWorld),
                nameof(BComponent),
                nameof(DComponent),
                nameof(XComponent),
                nameof(YComponent)
            }));
        }

        class TestProperty : IComponentProperty
        {
            public TestProperty(string displayName)
                => DisplayName = displayName;
            public string DisplayName { get; set; }

            public TypeIndex TypeIndex => throw new NotImplementedException();
            public ComponentPropertyType Type => throw new NotImplementedException();
            public string Name => throw new NotImplementedException();
            public bool IsReadOnly => throw new NotImplementedException();
            public void Accept(IPropertyVisitor visitor, ref EntityContainer container) => throw new NotImplementedException();
            public Type DeclaredValueType() => throw new NotImplementedException();
            public TAttribute GetAttribute<TAttribute>() where TAttribute : Attribute => throw new NotImplementedException();
            public IEnumerable<TAttribute> GetAttributes<TAttribute>() where TAttribute : Attribute => throw new NotImplementedException();
            public IEnumerable<Attribute> GetAttributes() => throw new NotImplementedException();
            public object GetValue(ref EntityContainer container) => throw new NotImplementedException();
            public bool HasAttribute<TAttribute>() where TAttribute : Attribute => throw new NotImplementedException();
            public void SetValue(ref EntityContainer container, object value) => throw new NotImplementedException();

        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddNewComponentsBeginning()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(BComponent), typeof(AComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddNewComponentsEnd()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
            }));

            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddNewComponentsMiddle()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(BComponent), typeof(CComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_RemoveComponentsBeginning()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent), typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<AComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<BComponent>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_RemoveComponentsEnd()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent), typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<CComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<DComponent>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_RemoveComponentsMiddle()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent), typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<BComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<CComponent>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddAndRemoveMultipleComponentsBeginning()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent), typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<AComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<BComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent<LocalTransform>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<LocalTransform>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddAndRemoveMultipleComponentsEnd()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent), typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<CComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<DComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent<XComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent<YComponent>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<XComponent>(),
                GetComponentPropertyName<YComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddAndRemoveMultipleComponentsMiddle()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new Unity.Entities.ComponentTypeSet(typeof(LocalTransform), typeof(CComponent), typeof(DComponent), typeof(YComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<LocalTransform>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
                GetComponentPropertyName<YComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<CComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent<AComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent<EComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent<XComponent>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<LocalTransform>(),
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<DComponent>(),
                GetComponentPropertyName<EComponent>(),
                GetComponentPropertyName<XComponent>(),
                GetComponentPropertyName<YComponent>(),
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_RemoveAll()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(AComponent), typeof(BComponent), typeof(CComponent), typeof(DComponent)));
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<DComponent>(),
            }));

            m_World.EntityManager.RemoveComponent<AComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<BComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<CComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<DComponent>(m_InspectedEntity);
            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.Empty);
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddAndRemoveRandomOrder1()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new Unity.Entities.ComponentTypeSet(new ComponentType[] { typeof(BComponent), typeof(EComponent), typeof(CComponent), typeof(LocalTransform) }));
            m_Root.ForceUpdateBindings();

            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<LocalTransform>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<CComponent>(),
                GetComponentPropertyName<EComponent>()
            }));

            m_World.EntityManager.RemoveComponent<CComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent(m_InspectedEntity, new Unity.Entities.ComponentTypeSet(typeof(AComponent), typeof(DComponent)));

            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<LocalTransform>(),
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<DComponent>(),
                GetComponentPropertyName<EComponent>()
            }));
        }

        [Test]
        public void ComponentsTab_UpdateComponentOrder_AddAndRemoveRandomOrder2()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(new ComponentType[] { typeof(AComponent), typeof(BComponent), typeof(DComponent), typeof(EComponent), typeof(XComponent), typeof(YComponent) }));
            m_Root.ForceUpdateBindings();

            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<AComponent>(),
                GetComponentPropertyName<BComponent>(),
                GetComponentPropertyName<DComponent>(),
                GetComponentPropertyName<EComponent>(),
                GetComponentPropertyName<XComponent>(),
                GetComponentPropertyName<YComponent>()
            }));

            m_World.EntityManager.RemoveComponent<AComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<BComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<DComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<EComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<XComponent>(m_InspectedEntity);
            m_World.EntityManager.RemoveComponent<YComponent>(m_InspectedEntity);
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(typeof(EComponent), typeof(XComponent)));

            m_Root.ForceUpdateBindings();
            Assert.That(GetComponentsOrderFromUI(), Is.EquivalentTo(new[]
            {
                GetComponentPropertyName<EComponent>(),
                GetComponentPropertyName<XComponent>()
            }));
        }

        [Test]
        public void ComponentsTab_Update_DoesNotChangeEntityVersion()
        {
            uint GetChangeVersion(EntityQuery entityQuery, ref ComponentTypeHandle<VersionChangeTestComponent> handle)
            {
                using var chunks = entityQuery.ToArchetypeChunkArray(Allocator.Temp);
                Assert.That(chunks.Length, Is.EqualTo(1), $"Should not have multiple chunks of entities with {nameof(VersionChangeTestComponent)}");
                return chunks[0].GetChangeVersion(ref handle);
            }

            var entityManager = m_World.EntityManager;
            entityManager.Debug.IncrementGlobalSystemVersion();
            var query = entityManager.CreateEntityQuery(typeof(VersionChangeTestComponent));

            var componentTypeHandle = entityManager.GetComponentTypeHandle<VersionChangeTestComponent>(false);

            var entity = m_InspectedEntity;

            entityManager.Debug.IncrementGlobalSystemVersion();
            entityManager.AddComponent<VersionChangeTestComponent>(entity);
            var initialVersion = GetChangeVersion(query, ref componentTypeHandle);

            entityManager.Debug.IncrementGlobalSystemVersion();
            m_Root.ForceUpdateBindings();
            var currentVersion = GetChangeVersion(query, ref componentTypeHandle);
            Assert.That(currentVersion, Is.EqualTo(initialVersion), "Updating the Components Tab changed the entity");

            entityManager.Debug.IncrementGlobalSystemVersion();
            var comp = entityManager.GetComponentData<VersionChangeTestComponent>(entity);
            comp.Field += 1;
            entityManager.SetComponentData(entity, comp);
            currentVersion = GetChangeVersion(query, ref componentTypeHandle);
            Assume.That(currentVersion, Is.Not.EqualTo(initialVersion), "This test no longer properly detects changes");
        }

        [Test]
        public void ComponentsTab_Search_NoFilter_ShowsAllComponents()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(
                typeof(AComponent),
                typeof(BComponent),
                typeof(XComponent)));
            m_Root.ForceUpdateBindings();

            m_Tab.ClearSearch();

            var visibleComponents = GetVisibleComponents();
            Assert.That(visibleComponents.Count, Is.EqualTo(3), "All components should be visible with no search");
        }

        [Test]
        public void ComponentsTab_Search_FilterByComponentName_ShowsMatchingComponents()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(
                typeof(AComponent),
                typeof(BComponent),
                typeof(XComponent),
                typeof(YComponent)));
            m_Root.ForceUpdateBindings();

            m_Tab.ApplySearch("x component");

            var visibleComponents = GetVisibleComponents();
            Assert.That(visibleComponents.Count, Is.EqualTo(1), "Only XComponent should be visible");
            Assert.That(visibleComponents[0].Path, Does.Contain("XComponent"));
        }

        [Test]
        public void ComponentsTab_Search_FilterByPartialName_ShowsMatchingComponents()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(
                typeof(AComponent),
                typeof(BComponent),
                typeof(LocalTransform)));
            m_Root.ForceUpdateBindings();

            m_Tab.ApplySearch("transform");

            var visibleComponents = GetVisibleComponents();
            Assert.That(visibleComponents.Count, Is.EqualTo(1), "Only LocalTransform should be visible");
            Assert.That(visibleComponents[0].Path, Does.Contain("LocalTransform"));
        }

        [Test]
        public void ComponentsTab_Search_NoMatches_HidesAllComponents()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(
                typeof(AComponent),
                typeof(BComponent)));
            m_Root.ForceUpdateBindings();

            m_Tab.ApplySearch("NonExistentComponent");

            var visibleComponents = GetVisibleComponents();
            Assert.That(visibleComponents.Count, Is.EqualTo(0), "No components should be visible");
        }

        [Test]
        public void ComponentsTab_Search_ClearSearch_ShowsAllComponents()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet(
                typeof(AComponent),
                typeof(BComponent),
                typeof(XComponent)));
            m_Root.ForceUpdateBindings();

            m_Tab.ApplySearch("a component");

            var visibleAfterSearch = GetVisibleComponents();
            Assert.That(visibleAfterSearch.Count, Is.EqualTo(1), "Only AComponent should be visible");

            m_Tab.ClearSearch();

            var visibleAfterClear = GetVisibleComponents();
            Assert.That(visibleAfterClear.Count, Is.EqualTo(3), "All components should be visible after clearing search");
        }

        [Test]
        public void ComponentsTab_Search_CaseInsensitive()
        {
            m_World.EntityManager.AddComponent(m_InspectedEntity, new ComponentTypeSet( typeof(LocalTransform)));
            m_Root.ForceUpdateBindings();

            m_Tab.ApplySearch("TRANSFORM");
            Assert.That(GetVisibleComponents().Count, Is.EqualTo(1), "Should match uppercase");

            m_Tab.ApplySearch("transform");
            Assert.That(GetVisibleComponents().Count, Is.EqualTo(1), "Should match lowercase");

            m_Tab.ApplySearch("Transform");
            Assert.That(GetVisibleComponents().Count, Is.EqualTo(1), "Should match mixed case");
        }

        List<ComponentElementBase> GetVisibleComponents()
        {
            var allComponents = new List<ComponentElementBase>();
            m_Root.Query<ComponentElementBase>().ToList(allComponents);

            var visibleComponents = new List<ComponentElementBase>();
            for (var i = 0; i < allComponents.Count; i++)
            {
                var component = allComponents[i];
                // Skip tags because they're displayed in a separate TagComponentContainer
                // and have different visibility logic than regular components
                if (component.Type == ComponentPropertyType.Tag)
                    continue;

                // Component is visible if: no explicit style (Null) or style is not None
                if (component.style.display.keyword == StyleKeyword.Null ||
                    component.style.display.value != DisplayStyle.None)
                {
                    visibleComponents.Add(component);
                }
            }

            return visibleComponents;
        }

        string[] GetComponentsOrderFromUI()
        {
            var allComponents = new List<ComponentElementBase>();
            m_Root.Query<ComponentElementBase>().ToList(allComponents);

            var paths = new List<string>();
            for (var i = 0; i < allComponents.Count; i++)
            {
                var component = allComponents[i];
                // Skip tags because they're grouped separately in TagComponentContainer
                // and we only want to test the order of regular components
                if (component.Type != ComponentPropertyType.Tag)
                {
                    paths.Add(component.Path);
                }
            }

            return paths.ToArray();
        }

        struct AComponent : IComponentData { public int Field; }
        struct BComponent : IComponentData { public int Field; }
        struct CComponent : IComponentData { public int Field; }
        struct DComponent : IComponentData { public int Field; }
        struct EComponent : IComponentData { public int Field; }
        struct XComponent : IComponentData { public int Field; }
        struct YComponent : IComponentData { public int Field; }

        // Reserved for: ComponentsTab_Update_DoesNotChangeEntityVersion
        struct VersionChangeTestComponent : IComponentData { [UsedImplicitly] public int Field; }

        static string GetComponentPropertyName<T>()
            => $"{typeof(T).Namespace}.{TypeUtility.GetTypeDisplayName(typeof(T))}".Replace(".", "_");
    }
}
