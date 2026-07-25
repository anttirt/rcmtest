using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemInformationVisualElement : BindableElement, IBinding
    {
        internal static readonly ObjectPool<SystemInformationVisualElement> Pool = new ObjectPool<SystemInformationVisualElement>(() => new SystemInformationVisualElement());

        SystemTreeViewItemData m_Target;
        public SystemTreeView TreeView { get; set; }
        const float k_SystemNameLabelWidth = 200f;
        const float k_SingleIndentWidth = 15f;
        const string k_UnityTreeViewItemIndentsName = "unity-tree-view__item-indents";

        public SystemTreeViewItemData Target
        {
            get => m_Target;
            set
            {
                if (m_Target == value)
                    return;
                m_Target = value;
                Update();
            }
        }

        public int IndexInTreeView { get; set; }

        readonly VisualElement m_SystemEnableToggleContainer;
        readonly Toggle m_SystemEnableToggle;
        readonly VisualElement m_Icon;
        readonly Label m_SystemNameLabel;

        SystemInformationVisualElement()
        {
            Resources.Templates.SystemScheduleItem.Clone(this);
            Resources.Templates.SystemScheduleItem.AddStyles(this);
            Resources.Templates.SystemSchedule.AddStyles(this);

            binding = this;

            AddToClassList(UssClasses.DotsEditorCommon.CommonResources);

            m_SystemEnableToggleContainer = new VisualElement();
            m_SystemEnableToggleContainer.AddToClassList(UssClasses.SystemScheduleWindow.Items.EnabledContainer);
            m_SystemEnableToggle = new Toggle();
            m_SystemEnableToggle.AddToClassList(UssClasses.SystemScheduleWindow.Items.StateToggle);
            m_SystemEnableToggleContainer.Add(m_SystemEnableToggle);
            m_SystemEnableToggle.RegisterValueChangedCallback(OnSystemTogglePress);

            m_Icon = this.Q(className: UssClasses.SystemScheduleWindow.Items.Icon);

            m_SystemNameLabel = this.Q<Label>(className: UssClasses.SystemScheduleWindow.Items.SystemName);
        }

        public static SystemInformationVisualElement Acquire(SystemTreeView treeView)
        {
            var item = Pool.Get();

            item.TreeView = treeView;
            return item;
        }

        public void Release()
        {
            Target = null;
            TreeView = null;
            Pool.Release(this);
        }

        static void SetText(Label label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        public void Update()
        {
            if (null == Target)
                return;

            if (Target.SystemProxy.Valid && Target.SystemProxy.World == null)
                return;

            // Insert system toggle above the system information visual element
            var itemRoot = parent?.parent;
            itemRoot.Insert(0, m_SystemEnableToggleContainer);

            m_Icon.style.display = string.Empty == GetSystemClass(Target.SystemProxy) ? DisplayStyle.None : DisplayStyle.Flex;
            SetText(m_SystemNameLabel, Target.GetSystemName());
            SetSystemNameLabelWidth(m_SystemNameLabel);

            SetSystemClass(m_Icon, Target.SystemProxy);
            SetGroupNodeLabelBold(m_SystemNameLabel, Target.SystemProxy);

            if (!Target.SystemProxy.Valid) // player loop system without children
            {
                SetEnabled(Target.HasChildren);

                m_SystemEnableToggle.style.display = DisplayStyle.Flex;
                m_SystemEnableToggle.SetEnabled(false);
                SetSystemToggleState();

                m_SystemNameLabel.SetEnabled(true);
            }
            else
            {
                SetEnabled(true);
                m_SystemEnableToggle.style.display = DisplayStyle.Flex;

                var systemState = Target.SystemProxy.Enabled;
                m_SystemEnableToggle.value = systemState;
                SetSystemToggleState();

                var groupState = systemState && Target.GetParentState();
                m_SystemEnableToggle.SetEnabled(true);
                m_SystemNameLabel.SetEnabled(groupState);
            }
        }

        void SetSystemToggleState()
        {
            switch (Target.GetSystemToggleState())
            {
                case SystemTreeViewItemData.SystemToggleState.Disabled:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, false);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, false);
                    break;
                case SystemTreeViewItemData.SystemToggleState.Mixed:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, false);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, true);
                    break;
                case SystemTreeViewItemData.SystemToggleState.AllEnabled:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, true);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, false);
                    break;
                default:
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleEnabled, true);
                    m_SystemEnableToggle.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemToggleMixed, false);
                    break;
            }
        }

        void SetSystemNameLabelWidth(VisualElement label)
        {
            var treeViewItemVisualElement = parent?.parent;
            var itemIndentsContainerName = treeViewItemVisualElement?.Q(k_UnityTreeViewItemIndentsName);
            if (itemIndentsContainerName == null)
            {
                label.style.width = k_SystemNameLabelWidth;
            }
            else
            {
                var indentWidth = itemIndentsContainerName.childCount * k_SingleIndentWidth;
                label.style.width = k_SystemNameLabelWidth - indentWidth;
                itemIndentsContainerName.style.width = indentWidth;
            }
        }

        static void SetSystemClass(VisualElement element, SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;

            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.BeginCommandBufferIcon,
                (flags & SystemCategory.ECBSystemBegin) != 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.EndCommandBufferIcon,
                (flags & SystemCategory.ECBSystemEnd) != 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.UnmanagedSystemIcon,
                (flags & SystemCategory.Unmanaged) != 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.SystemIcon,
                (flags & SystemCategory.SystemBase) != 0 && (flags & SystemCategory.EntityCommandBufferSystem) == 0);
            element.EnableInClassList(
                UssClasses.SystemScheduleWindow.Items.SystemGroupIcon,
                (flags & SystemCategory.SystemGroup) != 0);
        }

        static void SetGroupNodeLabelBold(VisualElement element, SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;

            var isBold = flags == 0 || (flags & SystemCategory.SystemGroup) != 0;
            element.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemNameBold, isBold);
            element.EnableInClassList(UssClasses.SystemScheduleWindow.Items.SystemNameNormal, !isBold);
        }

        static string GetSystemClass(SystemProxy systemProxy)
        {
            var flags = systemProxy.Valid ? systemProxy.Category : 0;

            if ((flags & SystemCategory.ECBSystemBegin) != 0)
                return UssClasses.SystemScheduleWindow.Items.BeginCommandBufferIcon;
            if ((flags & SystemCategory.ECBSystemEnd) != 0)
                return UssClasses.SystemScheduleWindow.Items.EndCommandBufferIcon;
            if ((flags & SystemCategory.EntityCommandBufferSystem) != 0)
                return string.Empty;
            if ((flags & SystemCategory.Unmanaged) != 0)
                return UssClasses.SystemScheduleWindow.Items.UnmanagedSystemIcon;
            if ((flags & SystemCategory.SystemGroup) != 0)
                return UssClasses.SystemScheduleWindow.Items.SystemGroupIcon;
            if ((flags & SystemCategory.SystemBase) != 0)
                return UssClasses.SystemScheduleWindow.Items.SystemIcon;

            return string.Empty;
        }

        void OnSystemTogglePress(ChangeEvent<bool> evt)
        {
            if (!Target.SystemProxy.Valid)
                return;

            Target.SetSystemEnabled(evt.newValue);
            // Update to reflect the toggle state right away in the UI.
            Update();

            // Refresh all items currently being displayed, so we can update children of a group being disabled for example
            TreeView.MultiColumnTreeViewElement.RefreshItems();

            evt.StopPropagation();
        }

        void IBinding.PreUpdate() { }

        void IBinding.Release() { }
    }
}
