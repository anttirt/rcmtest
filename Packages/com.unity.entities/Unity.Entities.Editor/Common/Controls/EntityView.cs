using System;
using Unity.Hierarchy;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class EntityView : VisualElement
    {
        const string k_IconId = "Icon";
        const string k_NameId = "Name";

        readonly Label m_EntityName;
        readonly VisualElement m_EntityIcon;
        EntityViewData m_Data;
        bool m_IsPrefab;

        public EntityView(EntityViewData data)
        {
            m_IsPrefab = false;

            Resources.Templates.EntityView.Clone(this);

            m_EntityName = this.Q<Label>(k_NameId);
            m_EntityIcon = this.Q<VisualElement>(k_IconId);
            if (data.Entity != default)
                Update(data);
            this.Q<VisualElement>(className: UssClasses.EntityView.GoTo)
                .RegisterCallback<MouseDownEvent, EntityView>((_, @this) =>
                {
                    Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.RelationshipGoTo, Analytics.GoToEntityDestination);
                    EntitySelectionProxy.SelectEntity(@this.m_Data.World, @this.m_Data.Entity);
                    FrameInHierarchy(@this.m_Data.Entity);
                }, this);
        }

        static void FrameInHierarchy(Entity entity)
        {
            var window = EditorWindow.GetWindow<Unity.Hierarchy.Editor.HierarchyWindow>();
            window.View.Update();
            var handler = window.View.Source.GetNodeTypeHandlerBase<HierarchyEntityHandler>();
            var node = handler != null ? handler.GetNode(entity) : Unity.Hierarchy.HierarchyNode.Null;
            if (node == Unity.Hierarchy.HierarchyNode.Null)
                return;

            var viewModel = window.View.ViewModel;
            viewModel.ClearFlags(Unity.Hierarchy.HierarchyNodeFlags.Selected);
            viewModel.SetFlags(in node, Unity.Hierarchy.HierarchyNodeFlags.Selected);
            window.View.Frame(in node);
        }

        public void Update(EntityViewData data)
        {
            m_Data = data;
            m_EntityName.text = data.EntityName;

            var entity = data.Entity;
            var isPrefab = data.World.EntityManager.HasComponent<Prefab>(entity);

            if (m_IsPrefab != isPrefab)
            {
                if (isPrefab)
                {
                    m_EntityIcon.RemoveFromClassList(UssClasses.EntityView.EntityIconEntity);
                    m_EntityName.RemoveFromClassList(UssClasses.EntityView.EntityNameEntity);
                    m_EntityIcon.AddToClassList(UssClasses.EntityView.EntityIconPrefab);
                    m_EntityName.AddToClassList(UssClasses.EntityView.EntityNamePrefab);
                }
                else
                {
                    m_EntityIcon.RemoveFromClassList(UssClasses.EntityView.EntityIconPrefab);
                    m_EntityName.RemoveFromClassList(UssClasses.EntityView.EntityNamePrefab);
                    m_EntityIcon.AddToClassList(UssClasses.EntityView.EntityIconEntity);
                    m_EntityName.AddToClassList(UssClasses.EntityView.EntityNameEntity);
                }

                m_IsPrefab = isPrefab;
            }
        }
    }

    class MetaChunkEntityRowView : VisualElement
    {
        const string k_IconId = "Icon";
        const string k_NameId = "Name";

        public MetaChunkEntityRowView(EntityViewData data)
        {
            Resources.Templates.EntityView.Clone(this);
            tooltip = L10n.Tr("Meta entity: holds the chunk components for this chunk.");

            this.Q<VisualElement>(k_IconId)?.AddToClassList(UssClasses.EntityView.EntityIconEntity);

            var nameLabel = this.Q<Label>(k_NameId);
            if (nameLabel != null)
            {
                nameLabel.AddToClassList(UssClasses.EntityView.EntityNameEntity);
                nameLabel.text = $"Meta Entity {data.Entity.Index}:{data.Entity.Version}";
            }

            this.Q<VisualElement>(className: UssClasses.EntityView.GoTo)
                ?.RegisterCallback<MouseDownEvent, EntityViewData>(
                    (_, d) => EntitySelectionProxy.SelectEntity(d.World, d.Entity),
                    data);
        }
    }
}
