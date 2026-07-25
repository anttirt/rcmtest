using System;
using System.Linq;
using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine.Pool;

namespace Unity.Entities.Editor
{
    class ComponentsTab : ITabContent
    {
        readonly EntityInspectorContext m_Context;
        bool m_IsVisible;
        ComponentsTabInspector m_Inspector;

        public string TabName { get; } = L10n.Tr("Components");
        public void OnTabVisibilityChanged(bool isVisible)
        {
            if (isVisible)
                Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.InspectorTabFocus, Analytics.ComponentsTabName);
            m_IsVisible = isVisible;
        }

        public ComponentsTab(EntityInspectorContext entityInspectorContext)
        {
            m_Context = entityInspectorContext;
        }
        
        internal void ClearSearch() => m_Inspector?.ClearSearch();
        internal void ApplySearch(string searchText) => m_Inspector?.ApplySearch(searchText);

        [UsedImplicitly]
        internal class ComponentsTabInspector : PropertyInspector<ComponentsTab>
        {
            EntityInspectorComponentStructure m_CurrentComponentStructure;
            EntityInspectorComponentStructure m_LastComponentStructure;
            EntityInspectorBuilderVisitor m_InspectorBuilderVisitor;
            VisualElement m_Root;
            TagComponentContainer m_TagsRoot;
            VisualElement m_ComponentsRoot;
            ToolbarSearchField m_SearchField;

            public override VisualElement Build()
            {
                Target.m_Inspector = this;

                m_Root = Resources.Templates.Inspector.ComponentsTab.Clone();

                Resources.AddCommonVariables(m_Root);
                UnityEditor.Search.SearchElement.AppendStyleSheets(m_Root);

                m_SearchField = InspectorUtility.CreateSearchField(
                    UssClasses.Inspector.ComponentsTab.SearchField,
                    ApplySearch,
                    ClearSearch);

                var searchContainer = m_Root.Q(className: "search-field-container");
                searchContainer.Add(m_SearchField);

                m_TagsRoot = new TagComponentContainer(Target.m_Context);
                m_ComponentsRoot = new VisualElement();
                m_Root.Add(m_TagsRoot);
                m_Root.Add(m_ComponentsRoot);

                m_Root.RegisterCallback<GeometryChangedEvent, VisualElement>((_, elem) =>
                {
                    StylingUtility.AlignInspectorLabelWidth(elem);
                }, m_Root);

                m_InspectorBuilderVisitor = new EntityInspectorBuilderVisitor(Target.m_Context);
                m_CurrentComponentStructure = new EntityInspectorComponentStructure();
                BuildOrUpdateUI();

                return m_Root;
            }

            public void ClearSearch()
            {
                m_SearchField.SetValueWithoutNotify(string.Empty);

                using var _ = ListPool<ComponentElementBase>.Get(out var list);
                m_Root.Query<ComponentElementBase>().ToList(list);

                foreach (var comp in list)
                    comp.Show();
            }

            public void ApplySearch(string searchText)
            {
                using var _ = ListPool<ComponentElementBase>.Get(out var list);
                m_Root.Query<ComponentElementBase>().ToList(list);

                foreach (var element in list)
                {
                    var isMatch = element.DisplayName != null &&
                                  element.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    element.SetVisibility(isMatch);
                }
            }

            public override void Update()
            {
                if (!Target.m_IsVisible || !Target.m_Context.TargetExists())
                    return;

                BuildOrUpdateUI();
            }

            void BuildOrUpdateUI()
            {
                m_CurrentComponentStructure.Reset();

                var container = Target.m_Context.EntityContainer;
                var propertyBag = PropertyBag.GetPropertyBag<EntityContainer>();
                var properties = propertyBag.GetProperties(ref container);

                foreach (var property in properties)
                {
                    if (property is IComponentProperty componentProperty)
                    {
                        if (componentProperty.Type == ComponentPropertyType.Tag)
                            m_CurrentComponentStructure.Tags.Add(componentProperty);
                        else
                            m_CurrentComponentStructure.Components.Add(componentProperty);
                    }
                }

                m_CurrentComponentStructure.Sort();

                if (m_LastComponentStructure == null)
                {
                    m_LastComponentStructure = new EntityInspectorComponentStructure();
                    foreach (var p in m_CurrentComponentStructure.Tags)
                    {
                        PropertyContainer.Accept(m_InspectorBuilderVisitor, ref container, new PropertyPath(p.Name));
                        m_TagsRoot.Add(m_InspectorBuilderVisitor.Result);
                    }

                    foreach (var p in m_CurrentComponentStructure.Components)
                    {
                        PropertyContainer.Accept(m_InspectorBuilderVisitor, ref container, new PropertyPath(p.Name));
                        m_ComponentsRoot.Add(m_InspectorBuilderVisitor.Result);
                    }
                }
                else
                {
                    UpdateUI(!m_CurrentComponentStructure.Tags.SequenceEqual(m_LastComponentStructure.Tags),
                             !m_CurrentComponentStructure.Components.SequenceEqual(m_LastComponentStructure.Components));
                }

                m_LastComponentStructure.CopyFrom(m_CurrentComponentStructure);
            }

            void UpdateUI(bool updateTags, bool updateComponents)
            {
                if (!updateTags && !updateComponents)
                    return;

                var container = Target.m_Context.EntityContainer;

                // update tags
                if (updateTags)
                {
                    InspectorUtility.Synchronize(m_LastComponentStructure.Tags,
                        m_CurrentComponentStructure.Tags,
                                EntityInspectorComponentsComparer.Instance,
                                m_TagsRoot,
                                Factory);
                }

                // update regular components
                if (updateComponents)
                {
                    InspectorUtility.Synchronize(m_LastComponentStructure.Components,
                        m_CurrentComponentStructure.Components,
                                EntityInspectorComponentsComparer.Instance,
                                m_ComponentsRoot,
                                Factory);
                }

                VisualElement Factory(IComponentProperty property)
                {
                    PropertyContainer.Accept(m_InspectorBuilderVisitor, ref container, new PropertyPath(property.Name));
                    return m_InspectorBuilderVisitor.Result;
                }
            }

        }
    }
}
