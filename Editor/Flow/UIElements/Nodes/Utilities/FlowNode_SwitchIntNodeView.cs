using System;
using Ceres.Graph;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.Utilities
{
    /// <summary>
    /// Node view for <see cref="FlowNode_SwitchInt"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_SwitchInt))]
    public class FlowNode_SwitchIntNodeView : ExecutablePortArrayNodeView
    {
        private readonly IFieldResolver _settingsResolver;

        public FlowNode_SwitchIntNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            var settingsView = this.CreateSettingsView<NodeSettingsView>();
            var settingsField = typeof(FlowNode_SwitchInt).GetField(nameof(FlowNode_SwitchInt.settings));
            using (FieldResolverFactory.InlineIMGUIAuto(true))
            {
                _settingsResolver = FieldResolverFactory.Get().Create(settingsField);
                settingsView.SettingsElement.Add(_settingsResolver.EditorField);
                FieldResolverInfos.Add(new FieldResolverInfo(_settingsResolver, settingsField));
                _settingsResolver.RegisterValueChangeCallback(OnSettingsChange);
            }
        }

        private void OnSettingsChange(object newValue)
        {
            var settings = (FlowNode_SwitchInt.Settings)newValue;
            var length = settings.conditions?.Length ?? 0;
            while (PortLength > length)
            {
                RemovePort(PortLength - 1);
            }
            while (PortLength < length)
            {
                AddPort(PortLength);
            }
            ReorderDynamicPorts();

            var defaultPort = FindPortView(nameof(FlowNode_SwitchInt.defaultOutput));
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
            OnSettingsChange(((FlowNode_SwitchInt)ceresNode).settings);
        }

        protected override string GetPortArrayElementDisplayName(int index)
        {
            var settings = (FlowNode_SwitchInt.Settings)_settingsResolver.Value;
            return settings.conditions == null ? string.Empty : settings.conditions[index].ToString();
        }
    }
}
