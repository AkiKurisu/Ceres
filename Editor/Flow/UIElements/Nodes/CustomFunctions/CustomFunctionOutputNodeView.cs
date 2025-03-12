using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    public class CustomFunctionOutputSettingsView: NodeSettingsView
    {
        public CustomFunctionOutputParameter Parameter { get; private set; }

        private readonly CustomFunctionOutputParameterView _outputParameterView;
        
        public Action OnSettingsChange;
        
        public CustomFunctionOutputSettingsView()
        {
            SettingsElement.AddToClassList(nameof(CustomFunctionOutputSettingsView));
            SettingsElement.styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/CustomFunctionSettingsView"));
            SettingsElement.Add(_outputParameterView = new CustomFunctionOutputParameterView());
            SettingsElement.Label.text = "Output Settings";
            _outputParameterView.RegisterCallback<ChangeEvent<CustomFunctionParameter>>(_ =>
            {
                OnSettingsChange?.Invoke();
            });
        }

        public void SetParameter(CustomFunctionOutputParameter parameter)
        {
            Parameter = parameter;
            _outputParameterView.BindParameter(parameter);
            _outputParameterView.UpdateView();
        }
    }
    
    [CustomNodeView(typeof(CustomFunctionOutput))]
    public class CustomFunctionOutputNodeView : ExecutableNodeView
    {
        private readonly CustomFunctionOutputSettingsView _settingsView;

        private readonly CeresPortView _returnPortView;
        
        public CustomFunctionOutputNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            _settingsView = this.CreateSettingsView<CustomFunctionOutputSettingsView>();
            _settingsView.OnSettingsChange = OnSettingsChange;
            NodeElement.capabilities &= ~Capabilities.Copiable;
            NodeElement.capabilities &= ~Capabilities.Deletable;
            _returnPortView = FindPortView(nameof(CustomFunctionOutput.returnValue));
        }

        private void OnSettingsChange()
        {
            if (_settingsView.Parameter.hasReturn)
            {
                _returnPortView.ShowPort();
                _returnPortView.SetDisplayType(_settingsView.Parameter.GetParameterType());
            }
            else
            {
                _returnPortView.HidePort();
            }
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var output = (CustomFunctionOutput)ceresNode;
            _settingsView.SetParameter(output.parameter);
            OnSettingsChange();
            base.SetNodeInstance(output);
        }

        public override ExecutableNode CompileNode()
        {
            var output = (CustomFunctionOutput)base.CompileNode();
            output.parameter = _settingsView.Parameter;
            return output;
        }
    }
}