using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace Ceres.Editor.Graph
{
    public class NodeSettingsElement : VisualElement
    {
        private readonly VisualElement _contentContainer;
        
        public Label Label { get; }
        
        public NodeSettingsElement()
        {
            AddToClassList(nameof(NodeSettingsElement));
            pickingMode = PickingMode.Ignore;
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/NodeSettings"));
            var visualTreeAsset = CeresGraphView.GetOrLoadVisualTreeAsset("Ceres/UXML/NodeSettings");
            visualTreeAsset.CloneTree(this);
            var settings = new VisualElement();
            settings.Add(Label = new Label("Node Settings")
            {
                name = "header"
            });
            _contentContainer = this.Q("contentContainer");
            _contentContainer.Add(settings);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private static void OnMouseUp(MouseUpEvent evt)
        {
            evt.StopPropagation();
        }

        private static void OnMouseDown(MouseDownEvent evt)
        {
            evt.StopPropagation();
        }

        // ReSharper disable once ConvertToAutoProperty
        public override VisualElement contentContainer => _contentContainer;
    }

    public class NodeSettingsView
    {
        public ICeresNodeView NodeView { get; private set; }
        
        public NodeSettingsElement SettingsElement { get; }

        private Button _settingButton;
        
        private bool _settingsExpanded;
        
        public NodeSettingsView()
        {
            CreateSettingButton();
            SettingsElement = new NodeSettingsElement
            {
                style =
                {
                    display = DisplayStyle.None
                }
            };
            OnGeometryChanged(null);
        }

        internal void Attach(ICeresNodeView nodeView)
        {
            NodeView = nodeView;
            nodeView.NodeElement.titleContainer.Add(_settingButton);
            NodeView.NodeElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            OnGeometryChanged(null);
            OnAttach();
        }
        
        protected virtual void OnAttach() { }
        
        private void CreateSettingButton()
        {
            _settingButton = new Button(ToggleSettings) { name = "settings-button" };
            _settingButton.styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/NodeSettings"));
            var image = new Image { name = "icon" };
            _settingButton.Add(image);
        }
        
        private void ToggleSettings()
        {
            _settingsExpanded = !_settingsExpanded;
            if (_settingsExpanded)
                OpenSettings();
            else
                CloseSettings();
        }

        private void OpenSettings()
        {
            _settingButton.AddToClassList("clicked");
            NodeView.NodeElement.parent.Add(SettingsElement);
            SettingsElement.style.display = DisplayStyle.Flex;
            SettingsElement.Focus();
            _settingsExpanded = true;
            OnGeometryChanged(null);
        }

        private void CloseSettings()
        {
            _settingButton.RemoveFromClassList("clicked");
            SettingsElement.style.display = DisplayStyle.None;
            _settingsExpanded = false;
        }
        
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (_settingButton == null || SettingsElement?.parent == null) return;
            bool isAttached = SettingsElement.GetFirstAncestorOfType<StackNode>() != null;
            var settingsButtonLayout = _settingButton.ChangeCoordinatesTo(SettingsElement.parent, _settingButton.layout);
            SettingsElement.style.top = settingsButtonLayout.yMax - (isAttached ? 70f : 20f);
            SettingsElement.style.left = settingsButtonLayout.xMin - NodeView.NodeElement.layout.width + (isAttached ? 10f : 20f);
        }

        public void DisableSettings()
        {
            CloseSettings();
            _settingButton.visible = false;
        }
    }
}