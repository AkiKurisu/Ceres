using UnityEngine.UIElements;

namespace Ceres.UIElements
{
    #if UNITY_6000_0_OR_NEWER
    [UxmlElement]
    #endif
    public partial class UIToolkitToggle : Toggle
    {
#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<UIToolkitToggle, Toggle.UxmlTraits> { }
#endif

        public UIToolkitToggle()
        {
            AddToClassList("ceres-toggle");
            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("ceres-toggle__mark");
            this.Q(className: checkmarkUssClassName).Add(mark);
        }
    }
}
