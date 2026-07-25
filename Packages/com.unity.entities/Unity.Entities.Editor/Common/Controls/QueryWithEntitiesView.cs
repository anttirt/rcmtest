using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class QueryWithEntitiesView : FoldoutWithoutActionButton
    {
        readonly QueryWithEntitiesViewData m_Data;
        readonly VisualElement m_EntitiesContainer;
        static readonly string k_Query = L10n.Tr("Query");

        public QueryWithEntitiesView(in QueryWithEntitiesViewData data)
        {
            m_Data = data;
            Resources.Templates.QueryWithEntities.AddStyles(this);
            this.Q(className: "unity-foldout__content").AddToClassList(UssClasses.QueryWithEntities.ToggleContent);

            HeaderName.text = $"{k_Query} #{data.QueryOrder}";
            MatchingCount.text = "0";

            m_EntitiesContainer = new VisualElement();
            Add(m_EntitiesContainer);

            SetValueWithoutNotify(true);
        }

        public void Update()
        {
            if (!m_Data.Update())
                return;

            if (m_Data.MetaEntityCount == 0)
                MatchingCount.text = m_Data.TotalEntityCount.ToString();
            else if (m_Data.TotalEntityCount == 0)
                MatchingCount.text = $"{m_Data.MetaEntityCount} meta";
            else
                MatchingCount.text = $"{m_Data.TotalEntityCount} (+{m_Data.MetaEntityCount} meta)";
            m_EntitiesContainer.Clear();

            foreach (var group in m_Data.Groups)
            {
                if (group.Meta.HasValue)
                    m_EntitiesContainer.Add(new MetaChunkEntityRowView(group.Meta.Value));

                foreach (var realEntity in group.RealEntities)
                {
                    var row = new EntityView(realEntity);
                    if (group.Meta.HasValue)
                        row.AddToClassList(UssClasses.QueryWithEntities.NestedEntityRow);
                    m_EntitiesContainer.Add(row);
                }
            }
        }
    }
}
