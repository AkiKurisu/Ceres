using UnityEngine.UIElements;

namespace Ceres.UIElements
{
    #if UNITY_6000_0_OR_NEWER
    [UxmlElement]
    #endif
    public partial class UIToolkitSlider : Slider
    {
#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<UIToolkitSlider, Slider.UxmlTraits> { }
#endif

        public UIToolkitSlider()
        {
            AddToClassList("ceres-slider");
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var number = this.Q<TextField>(className: "unity-base-slider__text-field");
                if (number != null) number.pickingMode = PickingMode.Position;
            });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                var number = this.Q<TextField>(className: "unity-base-slider__text-field");
                if (number == null || evt.target != number) return;
                number.Focus();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
        }
    }
}
