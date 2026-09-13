using UnityEngine.UIElements;

namespace Ceres.UIElements
{
    public enum UIToolkitButtonVariant { Secondary, Primary, Ghost, Outline, Danger }
    public enum UIToolkitButtonSize { Standard, Compact, Prominent, Icon, IconCompact }

    #if UNITY_6000_0_OR_NEWER
    [UxmlElement]
    #endif
    public partial class UIToolkitButton : Button
    {
#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<UIToolkitButton, UxmlTraits> { }

        public new class UxmlTraits : Button.UxmlTraits
        {
            private readonly UxmlEnumAttributeDescription<UIToolkitButtonVariant> _variant = new()
                { name = "variant", defaultValue = UIToolkitButtonVariant.Secondary };
            private readonly UxmlEnumAttributeDescription<UIToolkitButtonSize> _size = new()
                { name = "size", defaultValue = UIToolkitButtonSize.Standard };

            public override void Init(VisualElement element, IUxmlAttributes attributes, CreationContext context)
            {
                base.Init(element, attributes, context);
                var button = (UIToolkitButton)element;
                button.Variant = _variant.GetValueFromBag(attributes, context);
                button.Size = _size.GetValueFromBag(attributes, context);
            }
        }
#endif

        private readonly Label _surface;
        private UIToolkitButtonVariant _variant;
        private UIToolkitButtonSize _size;

        public override VisualElement contentContainer => _surface ?? base.contentContainer;

        #if UNITY_6000_0_OR_NEWER
        [UxmlAttribute]
        #endif
        public UIToolkitButtonVariant Variant
        {
            get => _variant;
            set
            {
                RemoveFromClassList("ceres-action--" + _variant.ToString().ToLowerInvariant());
                _variant = value;
                AddToClassList("ceres-action--" + value.ToString().ToLowerInvariant());
            }
        }

        #if UNITY_6000_0_OR_NEWER
        [UxmlAttribute]
        #endif
        public UIToolkitButtonSize Size
        {
            get => _size;
            set
            {
                RemoveFromClassList("ceres-action--" + _size.ToString().ToLowerInvariant());
                _size = value;
                AddToClassList("ceres-action--" + value.ToString().ToLowerInvariant());
            }
        }

        public override string text
        {
            get => base.text;
            set
            {
                base.text = value;
                if (_surface != null) _surface.text = value;
            }
        }

        public UIToolkitButton()
        {
            AddToClassList("ceres-action");
            Variant = UIToolkitButtonVariant.Secondary;
            Size = UIToolkitButtonSize.Standard;
            // The native caption stays transparent so the child surface can be smaller than the hit target.
            _surface = new Label(base.text) { pickingMode = PickingMode.Ignore };
            _surface.AddToClassList("ceres-action__surface");
            hierarchy.Add(_surface);
        }
    }
}
