using System;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
namespace Ceres.Editor.Graph.Flow
{
    [CustomNodeView(typeof(PropertyNode), true)]
    public class PropertyNodeView: ExecutableNodeView
    {
        protected string PropertyName { get; private set; }
        
        public PropertyNodeView(Type type, CeresGraphView graphView)
        {
            Initialize(type, graphView);
            SetupNodeElement(new ExecutableNodeElement(this));
            FillDefaultNodeProperties();
            FillDefaultNodePorts();
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var propertyNode =(PropertyNode)ceresNode;
            base.SetNodeInstance(ceresNode);
            SetPropertyName(propertyNode.GetPropertyName());
        }

        public virtual void SetPropertyName(string propertyName)
        {
            PropertyName = propertyName;
            var label = CeresLabel.GetLabel(NodeType);
            NodeElement.title = string.Format(label, propertyName);
        }

        public override ExecutableNode CompileNode()
        {
            var instance = (PropertyNode)base.CompileNode();
            instance.SetPropertyName(PropertyName);
            return instance;
        }
    }

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
            if(evt.Variable != _boundVariable) return;
            if(evt.ChangeType == VariableChangeType.Name)
            {
                SetPropertyName(evt.Variable.Name);
            }
            else if(evt.ChangeType == VariableChangeType.Type)
            {
                CeresGraph.LogWarning($"The variable type of {evt.Variable.Name} has changed, which will cause an error in the referenced PropertyNode during runtime. Please recreate the corresponding node.");
                GraphView.ClearSelection();
                GraphView.schedule.Execute(FrameNode).ExecuteLater(200);
            }
            else if(evt.ChangeType == VariableChangeType.Delete)
            {
                CeresGraph.LogWarning($"The variable {evt.Variable.Name} was deleted, which will cause an error in the referenced PropertyNode during runtime. Please remove the corresponding node.");
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