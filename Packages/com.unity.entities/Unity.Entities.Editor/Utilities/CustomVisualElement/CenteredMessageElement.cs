using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    [UxmlElement]
    partial class CenteredMessageElement : VisualElement
    {
        internal readonly Label m_Title;
        internal readonly Label m_Message;
        string m_TitleContent;
        string m_MessageContent;

        public CenteredMessageElement()
        {
            Resources.Templates.CenteredMessageElement.Clone(this);
            style.flexGrow = 1;

            m_Title = this.Q<Label>(className: UssClasses.DotsEditorCommon.CenteredMessageElementTitle);
            m_Message = this.Q<Label>(className: UssClasses.DotsEditorCommon.CenteredMessageElementMessage);
        }

        [UxmlAttribute]
        public string Title
        {
            get => m_TitleContent;
            set
            {
                if (m_TitleContent == value)
                    return;

                m_TitleContent = value;
                m_Title.SetVisibility(!string.IsNullOrWhiteSpace(m_TitleContent));
                m_Title.text = m_TitleContent;
            }
        }

        [UxmlAttribute]
        public string Message
        {
            get => m_MessageContent;
            set
            {
                if (m_MessageContent == value)
                    return;

                m_MessageContent = value;
                m_Message.SetVisibility(!string.IsNullOrWhiteSpace(m_MessageContent));
                m_Message.text = m_MessageContent;
            }
        }
    }
}
