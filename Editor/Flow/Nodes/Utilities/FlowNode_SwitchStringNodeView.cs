using System;
using Ceres.Graph;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.Utilities
{
    /// <summary>
    /// Node view for <see cref="FlowNode_SwitchString"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_SwitchString))]
    public class FlowNode_SwitchStringNodeView: ExecutablePortArrayNodeView
    {
        private readonly IFieldResolver _settingsResolver;
        
        public FlowNode_SwitchStringNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            var settingsView = this.CreateSettingsView<NodeSettingsView>();
            var settingsField = typeof(FlowNode_SwitchString).GetField(nameof(FlowNode_SwitchString.settings));
            using (FieldResolverFactory.InlineIMGUIAuto(true))
            {
                _settingsResolver = FieldResolverFactory.Get().Create(settingsField);
                settingsView.SettingsElement.Add(_settingsResolver.EditorField);
                FieldResolvers.Add(_settingsResolver);
                _settingsResolver.RegisterValueChangeCallback(OnSettingsChange);
            }
        }

        private void OnSettingsChange(object newValue)
        {
            var settings = (FlowNode_SwitchString.Settings)newValue;
            while (PortLength > settings.conditions.Length)
            {
                RemovePort(PortLength - 1);
            }
            while (PortLength < settings.conditions.Length)
            {
                AddPort(PortLength);
            }
            ReorderDynamicPorts();

            var defaultPort = FindPortView(nameof(FlowNode_SwitchString.defaultOutput));
            if (settings.hasDefault)
            {
                defaultPort.ShowPort();
            }
            else
            {
                defaultPort.HidePort();
            }
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            base.SetNodeInstance(ceresNode);
            OnSettingsChange(((FlowNode_SwitchString)ceresNode).settings);
        }

        protected override string GetPortArrayElementDisplayName(int index)
        {
            var settings = (FlowNode_SwitchString.Settings)_settingsResolver.Value;
            return settings.conditions == null ? string.Empty : settings.conditions[index];
        }
    }
}