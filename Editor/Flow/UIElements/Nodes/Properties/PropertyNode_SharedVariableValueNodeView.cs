using System;
using Ceres.Graph;
using Ceres.Graph.Flow.Properties;

namespace Ceres.Editor.Graph.Flow.Properties
{
    [CustomNodeView(typeof(PropertyNode_SharedVariableValue), true)]
    public class PropertyNode_SharedVariableValueNodeView : PropertyNodeView
    {
        public PropertyNode_SharedVariableValueNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            GraphView.Blackboard.RegisterCallback<VariableChangeEvent>(OnVariableChange);
        }
        
        private SharedVariable _boundVariable;
        
        public override void SetPropertyName(string propertyName)
        {
            base.SetPropertyName(propertyName);
            _boundVariable =  GraphView.Blackboard.GetSharedVariable(propertyName);
        }

        private void OnVariableChange(VariableChangeEvent evt)
        {
            if (NodeElement.panel == null) return;
            if (evt.Variable != _boundVariable) return;
            if (evt.ChangeType == VariableChangeType.Name)
            {
                SetPropertyName(evt.Variable.Name);
            }
            else if(evt.ChangeType == VariableChangeType.Type)
            {
                CeresLogger.LogWarning($"The variable type of {evt.Variable.Name} has changed, which may cause an error in the referenced PropertyNode during runtime. Please recreate the corresponding node.");
                GraphView.ClearSelection();
                GraphView.schedule.Execute(FrameNode).ExecuteLater(200);
            }
            else if(evt.ChangeType == VariableChangeType.Remove)
            {
                CeresLogger.LogWarning($"The variable {evt.Variable.Name} was deleted, which may cause an error in the referenced PropertyNode during runtime. Please remove the corresponding node.");
                GraphView.ClearSelection();
                GraphView.schedule.Execute(FrameNode).ExecuteLater(200);
            }
        }

        private void FrameNode()
        {
            GraphView.AddToSelection(NodeElement);
            GraphView.FrameSelection();
        }
    }
}